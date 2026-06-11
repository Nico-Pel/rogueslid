using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Character : MonoBehaviour
{
    [Header("Core Stats")]
    [SerializeField] private int maxHealth = 10;
    [SerializeField] [ReadOnly] private int currentHealth = 10;
    [SerializeField] private int bonusDamage;
    [SerializeField] private int resistance;
    [SerializeField] private int movementPointsPerTurn = 2;
    [SerializeField] private List<AbilityDefinition> startingAbilities = new List<AbilityDefinition>();

    [Header("Board")]
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private float moveDuration = 0.18f;
    [SerializeField] private float spawnHeight = 0.08f;
    [SerializeField] private Image hpFillBar;
    [SerializeField] private Transform characterBody;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private TrailRenderer characterTrail;
    [SerializeField] private string dashingBoolParameter = "Dashing";
    [SerializeField] private string attackTriggerParameter = "Attack";
    [SerializeField] private string attackPlaceholderClipName = "Attack_Spiral";
    [SerializeField] private bool orientTowardsDashDirection = true;
    [SerializeField] private float dashBodyRotateDuration = 0.1f;
    [SerializeField] private float dashBodyResetDuration = 0.2f;

    private Renderer[] renderers;
    private Color[] baseColors;
    private Tween moveTween;
    private Tween bodyRotationTween;
    private int remainingMovementPoints;
    private readonly List<CharacterAbilityRuntime> abilities = new List<CharacterAbilityRuntime>();
    private readonly List<Enemy> traversedEnemiesBuffer = new List<Enemy>();
    private RendererBlinkFeedback blinkFeedback;
    private AnimatorOverrideController animatorOverrideController;
    private AnimationClip attackPlaceholderClip;
    private Material runtimeTrailMaterial;
    private Color defaultTrailMaterialColor = Color.white;
    private GameObject defaultTrailObject;
    private GameObject activeReplacementTrailInstance;
    private AbilityDefinition activeTrailColorOwner;
    private AbilityDefinition activeTrailReplacementOwner;
    private Coroutine temporaryTrailColorCoroutine;
    private Coroutine temporaryTrailReplacementCoroutine;
    private readonly Quaternion defaultBodyLocalRotation = Quaternion.identity;

    public Vector2Int GridPosition => gridPosition;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public int BonusDamage => bonusDamage;
    public int Resistance => resistance;
    public int BaseMovementPoints => movementPointsPerTurn;
    public int RemainingMovementPoints => remainingMovementPoints;
    public bool CanAct => remainingMovementPoints > 0 && !IsMoving;
    public bool IsMoving { get; private set; }
    public Player Owner { get; private set; }
    public BoardManager Board { get; private set; }
    public event Action<Character> MovementPointsChanged;
    public event Action<Character> AbilitiesChanged;
    public IReadOnlyList<CharacterAbilityRuntime> Abilities => abilities;

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
    }

    public void Assign(Player owner, Vector2Int spawnGridPosition, BoardManager board)
    {
        Owner = owner;
        Board = board;
        gridPosition = spawnGridPosition;
        currentHealth = maxHealth;
        CacheRenderers();
        CacheBody();
        CacheAnimator();
        CacheTrail();
        blinkFeedback = GetComponent<RendererBlinkFeedback>();
        CacheHpBar();
        InitializeAbilities();
        SnapToGrid();
        ResetTurn();
        SetDashingAnimation(false);
        RefreshHpBar();
        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
    }

    public void ResetTurn()
    {
        remainingMovementPoints = movementPointsPerTurn;
        for (int index = 0; index < abilities.Count; index++)
        {
            abilities[index].BeginTurn();
        }

        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
    }

    public bool TrySlide(Vector2Int direction)
    {
        if (Board == null || remainingMovementPoints <= 0 || IsMoving || direction == Vector2Int.zero)
        {
            return false;
        }

        traversedEnemiesBuffer.Clear();
        Vector2Int destination = Board.GetSlideDestination(
            gridPosition,
            direction,
            CanTraverseUnits(),
            traversedEnemiesBuffer);

        if (destination == gridPosition)
        {
            return false;
        }

        if (!Board.MoveOccupant(gridPosition, destination, BoardOccupantKind.PlayerCharacter))
        {
            return false;
        }

        if (orientTowardsDashDirection)
        {
            FaceDashDirection(direction);
        }

        gridPosition = destination;
        remainingMovementPoints--;
        NotifyMovementPointsChanged();
        ApplyTraversalEffects();
        AnimateToGrid();
        return true;
    }

    public CharacterAbilityRuntime GetAbility(int abilityIndex)
    {
        return abilityIndex >= 0 && abilityIndex < abilities.Count ? abilities[abilityIndex] : null;
    }

    public bool TryUseAbility(int abilityIndex, Vector2Int? targetCell = null)
    {
        CharacterAbilityRuntime runtime = GetAbility(abilityIndex);
        if (runtime == null || IsMoving)
        {
            return false;
        }

        bool success = runtime.TryUse(this, targetCell);
        if (!success)
        {
            return false;
        }

        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
        return true;
    }

    public bool TryTeleportTo(Vector2Int targetCell)
    {
        if (Board == null || targetCell == gridPosition)
        {
            return false;
        }

        if (!Board.IsCellWalkable(targetCell))
        {
            return false;
        }

        if (!Board.MoveOccupant(gridPosition, targetCell, BoardOccupantKind.PlayerCharacter))
        {
            return false;
        }

        gridPosition = targetCell;
        AnimateToGrid();
        return true;
    }

    public void ConsumeMovementPoint()
    {
        if (remainingMovementPoints <= 0)
        {
            return;
        }

        remainingMovementPoints--;
        NotifyMovementPointsChanged();
    }

    public int DealDamageToEnemy(Enemy enemy, int baseDamage, bool addBonusDamage)
    {
        if (enemy == null)
        {
            return 0;
        }

        int totalDamage = Mathf.Max(1, baseDamage + (addBonusDamage ? bonusDamage : 0));
        return enemy.TakeDamage(totalDamage);
    }

    public int TakeDamage(int incomingDamage)
    {
        int finalDamage = Mathf.Max(1, incomingDamage - resistance);
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);

        blinkFeedback?.Blink(Color.red, 0.5f, 0.12f);
        cam.Instance?.CamShake(finalDamage);
        RefreshHpBar();

        return finalDamage;
    }

    public void PlayAttackAnimation(AnimationClip attackAnimationClip)
    {
        if (attackAnimationClip == null)
        {
            return;
        }

        CacheAnimator();
        if (characterAnimator == null)
        {
            return;
        }

        EnsureAnimatorOverrideController();
        if (animatorOverrideController == null || attackPlaceholderClip == null)
        {
            return;
        }

        animatorOverrideController[attackPlaceholderClip] = attackAnimationClip;
        characterAnimator.ResetTrigger(attackTriggerParameter);
        characterAnimator.SetTrigger(attackTriggerParameter);
    }

    public void PlayAbilityFx(IReadOnlyList<AbilityFxSpawnConfig> fxConfigs, IEnumerable<Enemy> hitTargets = null)
    {
        if (fxConfigs == null || fxConfigs.Count == 0)
        {
            return;
        }

        List<Enemy> uniqueTargets = null;
        if (hitTargets != null)
        {
            uniqueTargets = new List<Enemy>();
            HashSet<Enemy> seenTargets = new HashSet<Enemy>();
            foreach (Enemy target in hitTargets)
            {
                if (target == null || !seenTargets.Add(target))
                {
                    continue;
                }

                uniqueTargets.Add(target);
            }
        }

        for (int index = 0; index < fxConfigs.Count; index++)
        {
            AbilityFxSpawnConfig fxConfig = fxConfigs[index];
            if (fxConfig == null || fxConfig.FxPrefab == null)
            {
                continue;
            }

            if (fxConfig.SpawnAnchor == AbilityFxSpawnAnchor.Caster)
            {
                StartCoroutine(SpawnAbilityFxRoutine(fxConfig, transform, null));
                continue;
            }

            if (uniqueTargets == null)
            {
                continue;
            }

            for (int targetIndex = 0; targetIndex < uniqueTargets.Count; targetIndex++)
            {
                Enemy target = uniqueTargets[targetIndex];
                if (target == null)
                {
                    continue;
                }

                StartCoroutine(SpawnAbilityFxRoutine(fxConfig, target.transform, target));
            }
        }
    }

    public void PlayTemporaryTrailColor(Color trailColor, float duration)
    {
        CacheTrail();
        if (runtimeTrailMaterial == null)
        {
            return;
        }

        if (temporaryTrailColorCoroutine != null)
        {
            StopCoroutine(temporaryTrailColorCoroutine);
        }

        runtimeTrailMaterial.color = trailColor;
        if (duration > 0f)
        {
            temporaryTrailColorCoroutine = StartCoroutine(ResetTemporaryTrailColorAfterDelay(duration));
        }
    }

    public void SetTrailColorOverride(AbilityDefinition owner, Color trailColor)
    {
        CacheTrail();
        if (runtimeTrailMaterial == null)
        {
            return;
        }

        activeTrailColorOwner = owner;
        if (temporaryTrailColorCoroutine != null)
        {
            StopCoroutine(temporaryTrailColorCoroutine);
            temporaryTrailColorCoroutine = null;
        }

        runtimeTrailMaterial.color = trailColor;
    }

    public void ClearTrailColorOverride(AbilityDefinition owner)
    {
        CacheTrail();
        if (runtimeTrailMaterial == null || activeTrailColorOwner != owner)
        {
            return;
        }

        activeTrailColorOwner = null;
        runtimeTrailMaterial.color = defaultTrailMaterialColor;
    }

    public void PlayTemporaryTrailReplacement(GameObject replacementTrailPrefab, float duration)
    {
        CacheTrail();
        if (defaultTrailObject == null || replacementTrailPrefab == null)
        {
            return;
        }

        if (temporaryTrailReplacementCoroutine != null)
        {
            StopCoroutine(temporaryTrailReplacementCoroutine);
        }

        ClearActiveReplacementTrailInstance();
        CreateReplacementTrailInstance(replacementTrailPrefab);
        defaultTrailObject.SetActive(false);

        if (duration > 0f)
        {
            temporaryTrailReplacementCoroutine = StartCoroutine(ResetTemporaryTrailReplacementAfterDelay(duration));
        }
    }

    public void SetTrailReplacementOverride(AbilityDefinition owner, GameObject replacementTrailPrefab)
    {
        CacheTrail();
        if (defaultTrailObject == null || replacementTrailPrefab == null)
        {
            return;
        }

        activeTrailReplacementOwner = owner;
        if (temporaryTrailReplacementCoroutine != null)
        {
            StopCoroutine(temporaryTrailReplacementCoroutine);
            temporaryTrailReplacementCoroutine = null;
        }

        ClearActiveReplacementTrailInstance();
        CreateReplacementTrailInstance(replacementTrailPrefab);
        defaultTrailObject.SetActive(false);
    }

    public void ClearTrailReplacementOverride(AbilityDefinition owner)
    {
        CacheTrail();
        if (defaultTrailObject == null || activeTrailReplacementOwner != owner)
        {
            return;
        }

        activeTrailReplacementOwner = null;
        ClearActiveReplacementTrailInstance();
        defaultTrailObject.SetActive(true);
    }

    public void SetSelected(bool isSelected)
    {
        CacheRenderers();
        if (renderers == null)
        {
            return;
        }

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer targetRenderer = renderers[index];
            if (targetRenderer == null || targetRenderer.material == null)
            {
                continue;
            }

            targetRenderer.material.color = isSelected
                ? Color.Lerp(baseColors[index], Color.white, 0.35f)
                : baseColors[index];
        }
    }

    private void SnapToGrid()
    {
        if (Board == null)
        {
            return;
        }

        transform.position = Board.GridToWorldPosition(gridPosition) + Vector3.up * spawnHeight;
    }

    private void AnimateToGrid()
    {
        if (Board == null)
        {
            return;
        }

        moveTween?.Kill();
        IsMoving = true;
        SetDashingAnimation(true);
        Vector3 targetPosition = Board.GridToWorldPosition(gridPosition) + Vector3.up * spawnHeight;
        moveTween = transform.DOMove(targetPosition, moveDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                IsMoving = false;
                moveTween = null;
                ResetBodyRotation();
                SetDashingAnimation(false);
            });
    }

    private void CacheRenderers()
    {
        if (renderers != null)
        {
            return;
        }

        renderers = GetComponentsInChildren<Renderer>(true);
        baseColors = new Color[renderers.Length];
        for (int index = 0; index < renderers.Length; index++)
        {
            baseColors[index] = renderers[index] != null && renderers[index].material != null
                ? renderers[index].material.color
                : Color.white;
        }
    }

    private void OnDisable()
    {
        moveTween?.Kill();
        bodyRotationTween?.Kill();
        IsMoving = false;
        SnapBodyRotationToDefault();
        SetDashingAnimation(false);
        if (temporaryTrailColorCoroutine != null)
        {
            StopCoroutine(temporaryTrailColorCoroutine);
            temporaryTrailColorCoroutine = null;
        }

        if (temporaryTrailReplacementCoroutine != null)
        {
            StopCoroutine(temporaryTrailReplacementCoroutine);
            temporaryTrailReplacementCoroutine = null;
        }

        if (runtimeTrailMaterial != null)
        {
            runtimeTrailMaterial.color = defaultTrailMaterialColor;
        }

        activeTrailColorOwner = null;
        activeTrailReplacementOwner = null;
        ClearActiveReplacementTrailInstance();
        if (defaultTrailObject != null)
        {
            defaultTrailObject.SetActive(true);
        }
    }

    private void CacheHpBar()
    {
        if (hpFillBar != null)
        {
            return;
        }

        Transform hpFillTransform = transform.Find("Canvas/hpBar/hpFillBar");
        if (hpFillTransform != null)
        {
            hpFillBar = hpFillTransform.GetComponent<Image>();
        }
    }

    private void RefreshHpBar()
    {
        if (hpFillBar == null)
        {
            return;
        }

        float fillRatio = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        hpFillBar.fillAmount = Mathf.Clamp01(fillRatio);
    }

    private void InitializeAbilities()
    {
        abilities.Clear();

        for (int index = 0; index < startingAbilities.Count; index++)
        {
            AbilityDefinition definition = startingAbilities[index];
            if (definition == null)
            {
                continue;
            }

            CharacterAbilityRuntime runtime = new CharacterAbilityRuntime(definition);
            runtime.ResetCombat();
            abilities.Add(runtime);
        }
    }

    private bool CanTraverseUnits()
    {
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            if (runtime.Definition != null && runtime.Definition.AllowsUnitTraversal(this, runtime))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyTraversalEffects()
    {
        if (traversedEnemiesBuffer.Count == 0)
        {
            return;
        }

        int traversalDamage = 0;
        List<CharacterAbilityRuntime> traversalAbilities = new List<CharacterAbilityRuntime>();
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            if (runtime.Definition == null)
            {
                continue;
            }

            int runtimeTraversalDamage = runtime.Definition.GetTraversalDamage(this, runtime);
            if (runtimeTraversalDamage <= 0)
            {
                continue;
            }

            traversalDamage = Mathf.Max(traversalDamage, runtimeTraversalDamage);
            traversalAbilities.Add(runtime);
        }

        if (traversalDamage <= 0)
        {
            return;
        }

        HashSet<Enemy> damagedEnemies = new HashSet<Enemy>();
        for (int index = 0; index < traversedEnemiesBuffer.Count; index++)
        {
            Enemy enemy = traversedEnemiesBuffer[index];
            if (enemy == null || !damagedEnemies.Add(enemy))
            {
                continue;
            }

            enemy.TakeDamage(traversalDamage);
        }

        if (damagedEnemies.Count == 0)
        {
            return;
        }

        for (int index = 0; index < traversalAbilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = traversalAbilities[index];
            runtime.Definition?.PlayTraversalFx(this, damagedEnemies);
        }
    }

    private IEnumerator SpawnAbilityFxRoutine(AbilityFxSpawnConfig fxConfig, Transform anchor, Enemy target)
    {
        if (fxConfig == null || fxConfig.FxPrefab == null || anchor == null)
        {
            yield break;
        }

        if (fxConfig.SpawnDelay > 0f)
        {
            yield return new WaitForSeconds(fxConfig.SpawnDelay);
        }

        if (anchor == null)
        {
            yield break;
        }

        Quaternion referenceRotation = GetAbilityFxReferenceRotation(fxConfig, target);
        Vector3 spawnPosition = anchor.position + referenceRotation * fxConfig.PositionOffset;
        Quaternion defaultFxRotation = fxConfig.FxPrefab.transform.rotation;
        Vector3 defaultFxScale = fxConfig.FxPrefab.transform.localScale;
        GameObject spawnedFx = Instantiate(fxConfig.FxPrefab, spawnPosition, defaultFxRotation);

        if (fxConfig.ParentToAnchor && anchor != null)
        {
            spawnedFx.transform.SetParent(anchor, true);
        }

        spawnedFx.transform.rotation = defaultFxRotation;
        spawnedFx.transform.localScale = defaultFxScale;

        if (fxConfig.DestroyAfterSeconds > 0f)
        {
            Destroy(spawnedFx, fxConfig.DestroyAfterSeconds);
        }
    }

    private Quaternion GetAbilityFxReferenceRotation(AbilityFxSpawnConfig fxConfig, Enemy target)
    {
        switch (fxConfig.OffsetReference)
        {
            case AbilityFxOffsetReference.CasterRotation:
                return transform.rotation;
            case AbilityFxOffsetReference.TargetRotation:
                return target != null ? target.transform.rotation : transform.rotation;
            default:
                return Quaternion.identity;
        }
    }

    private void NotifyMovementPointsChanged()
    {
        MovementPointsChanged?.Invoke(this);
    }

    private void NotifyAbilitiesChanged()
    {
        AbilitiesChanged?.Invoke(this);
    }

    private void CacheAnimator()
    {
        if (characterAnimator == null)
        {
            characterAnimator = GetComponentInChildren<Animator>(true);
        }
    }

    private void CacheBody()
    {
        if (characterBody == null)
        {
            characterBody = transform.Find("Pandora");
        }
    }

    private void CacheTrail()
    {
        if (characterTrail == null)
        {
            characterTrail = GetComponentInChildren<TrailRenderer>(true);
        }

        if (characterTrail == null)
        {
            return;
        }

        defaultTrailObject = characterTrail.gameObject;
        if (runtimeTrailMaterial == null)
        {
            runtimeTrailMaterial = characterTrail.material;
            if (runtimeTrailMaterial != null)
            {
                defaultTrailMaterialColor = runtimeTrailMaterial.color;
            }
        }
    }

    private void EnsureAnimatorOverrideController()
    {
        if (characterAnimator == null)
        {
            return;
        }

        if (animatorOverrideController == null)
        {
            RuntimeAnimatorController currentController = characterAnimator.runtimeAnimatorController;
            if (currentController == null)
            {
                return;
            }

            if (currentController is AnimatorOverrideController existingOverrideController)
            {
                animatorOverrideController = existingOverrideController;
            }
            else
            {
                animatorOverrideController = new AnimatorOverrideController(currentController);
                characterAnimator.runtimeAnimatorController = animatorOverrideController;
            }
        }

        if (attackPlaceholderClip != null)
        {
            return;
        }

        AnimationClip[] animationClips = animatorOverrideController.animationClips;
        for (int index = 0; index < animationClips.Length; index++)
        {
            AnimationClip clip = animationClips[index];
            if (clip == null)
            {
                continue;
            }

            if (clip.name == attackPlaceholderClipName)
            {
                attackPlaceholderClip = clip;
                return;
            }
        }

        for (int index = 0; index < animationClips.Length; index++)
        {
            AnimationClip clip = animationClips[index];
            if (clip != null && clip.name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                attackPlaceholderClip = clip;
                return;
            }
        }
    }

    private void SetDashingAnimation(bool isDashing)
    {
        CacheAnimator();
        if (characterAnimator == null || string.IsNullOrWhiteSpace(dashingBoolParameter))
        {
            return;
        }

        characterAnimator.SetBool(dashingBoolParameter, isDashing);
    }

    private void FaceDashDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
        {
            return;
        }

        Vector3 worldDirection = new Vector3(direction.x, 0f, -direction.y);
        worldDirection = Board != null ? Board.transform.TransformDirection(worldDirection) : worldDirection;
        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        CacheBody();
        Transform targetBody = characterBody != null ? characterBody : transform;
        Quaternion targetRotation = Quaternion.LookRotation((-worldDirection).normalized, Vector3.up);

        bodyRotationTween?.Kill();
        bodyRotationTween = targetBody.DORotateQuaternion(targetRotation, dashBodyRotateDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => bodyRotationTween = null);
    }

    private void ResetBodyRotation()
    {
        CacheBody();
        Transform targetBody = characterBody != null ? characterBody : transform;
        if (targetBody == null)
        {
            return;
        }

        bodyRotationTween?.Kill();
        bodyRotationTween = targetBody.DOLocalRotateQuaternion(defaultBodyLocalRotation, dashBodyResetDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => bodyRotationTween = null);
    }

    private void SnapBodyRotationToDefault()
    {
        CacheBody();
        Transform targetBody = characterBody != null ? characterBody : transform;
        if (targetBody == null)
        {
            return;
        }

        targetBody.localRotation = defaultBodyLocalRotation;
    }

    private IEnumerator ResetTemporaryTrailColorAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        temporaryTrailColorCoroutine = null;

        if (runtimeTrailMaterial != null && activeTrailColorOwner == null)
        {
            runtimeTrailMaterial.color = defaultTrailMaterialColor;
        }
    }

    private IEnumerator ResetTemporaryTrailReplacementAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        temporaryTrailReplacementCoroutine = null;

        if (activeTrailReplacementOwner != null)
        {
            yield break;
        }

        ClearActiveReplacementTrailInstance();
        if (defaultTrailObject != null)
        {
            defaultTrailObject.SetActive(true);
        }
    }

    private void CreateReplacementTrailInstance(GameObject replacementTrailPrefab)
    {
        if (defaultTrailObject == null || replacementTrailPrefab == null)
        {
            return;
        }

        Transform defaultTrailTransform = defaultTrailObject.transform;
        Transform parent = defaultTrailTransform.parent;
        activeReplacementTrailInstance = Instantiate(replacementTrailPrefab, parent);
        activeReplacementTrailInstance.transform.localPosition = defaultTrailTransform.localPosition;
        activeReplacementTrailInstance.transform.localRotation = defaultTrailTransform.localRotation;
    }

    private void ClearActiveReplacementTrailInstance()
    {
        if (activeReplacementTrailInstance == null)
        {
            return;
        }

        Destroy(activeReplacementTrailInstance);
        activeReplacementTrailInstance = null;
    }
}
