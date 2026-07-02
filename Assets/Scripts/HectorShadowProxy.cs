using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class HectorShadowProxy : MonoBehaviour
{
    [SerializeField] private Animator shadowAnimator;
    [SerializeField] private Transform shadowBody;
    [SerializeField] private string attackTriggerParameter = "Attack";
    [SerializeField] private string attackPlaceholderClipName = "Attack_Spiral";
    [SerializeField] private List<CharacterBasicAttackVisualConfig> basicAttackVisuals = new List<CharacterBasicAttackVisualConfig>();
    [SerializeField] private float rotateDuration = 0.05f;

    private Character observedCharacter;
    private AnimatorOverrideController animatorOverrideController;
    private AnimationClip attackPlaceholderClip;
    private Tween bodyRotationTween;
    private readonly Quaternion defaultBodyLocalRotation = Quaternion.identity;

    public Transform LaunchAnchor => shadowBody != null ? shadowBody : transform;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDestroy()
    {
        BindCharacter(null);
    }

    public void BindCharacter(Character character)
    {
        if (observedCharacter == character)
        {
            RefreshBasicAttackVisuals();
            return;
        }

        if (observedCharacter != null)
        {
            observedCharacter.AbilitiesChanged -= HandleCharacterAbilitiesChanged;
        }

        observedCharacter = character;
        if (observedCharacter != null)
        {
            observedCharacter.AbilitiesChanged += HandleCharacterAbilitiesChanged;
        }

        RefreshBasicAttackVisuals();
    }

    public void FaceTargetCell(Vector2Int originCell, Vector2Int targetCell)
    {
        Vector2Int direction = targetCell - originCell;
        if (direction == Vector2Int.zero)
        {
            return;
        }

        Transform target = shadowBody != null ? shadowBody : transform;
        if (target == null)
        {
            return;
        }

        Vector3 worldDirection = new Vector3(
            Mathf.Clamp(direction.x, -1, 1),
            0f,
            -Mathf.Clamp(direction.y, -1, 1));

        if (observedCharacter != null && observedCharacter.Board != null)
        {
            worldDirection = observedCharacter.Board.transform.TransformDirection(worldDirection);
        }

        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        bodyRotationTween?.Kill();
        bodyRotationTween = target.DORotateQuaternion(Quaternion.LookRotation((-worldDirection).normalized, Vector3.up), rotateDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => bodyRotationTween = null);
    }

    public void PlayAttackAnimation(AnimationClip attackAnimationClip)
    {
        if (attackAnimationClip == null)
        {
            return;
        }

        CacheReferences();
        if (shadowAnimator == null)
        {
            return;
        }

        EnsureAnimatorOverrideController();
        if (animatorOverrideController == null || attackPlaceholderClip == null)
        {
            return;
        }

        animatorOverrideController[attackPlaceholderClip] = attackAnimationClip;
        shadowAnimator.ResetTrigger(attackTriggerParameter);
        shadowAnimator.SetTrigger(attackTriggerParameter);
    }

    private void HandleCharacterAbilitiesChanged(Character character)
    {
        RefreshBasicAttackVisuals();
    }

    private void RefreshBasicAttackVisuals()
    {
        AbilityDefinition equippedBasicAttack = observedCharacter != null
            ? observedCharacter.GetPresentedBasicAttackDefinition()
            : null;

        for (int index = 0; index < basicAttackVisuals.Count; index++)
        {
            CharacterBasicAttackVisualConfig config = basicAttackVisuals[index];
            if (config == null || config.VisualRoots == null)
            {
                continue;
            }

            bool shouldBeVisible = equippedBasicAttack != null
                && config.BasicAttackAbility != null
                && config.BasicAttackAbility == equippedBasicAttack;
            for (int visualIndex = 0; visualIndex < config.VisualRoots.Count; visualIndex++)
            {
                GameObject visualRoot = config.VisualRoots[visualIndex];
                if (visualRoot != null)
                {
                    visualRoot.SetActive(shouldBeVisible);
                }
            }
        }
    }

    private void CacheReferences()
    {
        if (shadowAnimator == null)
        {
            shadowAnimator = GetComponentInChildren<Animator>(true);
        }

        if (shadowBody == null)
        {
            shadowBody = shadowAnimator != null ? shadowAnimator.transform : transform;
        }
    }

    private void EnsureAnimatorOverrideController()
    {
        if (shadowAnimator == null)
        {
            return;
        }

        if (attackPlaceholderClip == null)
        {
            RuntimeAnimatorController currentController = shadowAnimator.runtimeAnimatorController;
            if (currentController != null && !string.IsNullOrWhiteSpace(attackPlaceholderClipName))
            {
                AnimationClip[] clips = currentController.animationClips;
                for (int index = 0; index < clips.Length; index++)
                {
                    AnimationClip clip = clips[index];
                    if (clip != null && clip.name == attackPlaceholderClipName)
                    {
                        attackPlaceholderClip = clip;
                        break;
                    }
                }
            }
        }

        if (animatorOverrideController != null)
        {
            return;
        }

        RuntimeAnimatorController runtimeController = shadowAnimator.runtimeAnimatorController;
        if (runtimeController == null)
        {
            return;
        }

        if (runtimeController is AnimatorOverrideController existingOverrideController)
        {
            animatorOverrideController = existingOverrideController;
        }
        else
        {
            animatorOverrideController = new AnimatorOverrideController(runtimeController);
            shadowAnimator.runtimeAnimatorController = animatorOverrideController;
        }
    }
}
