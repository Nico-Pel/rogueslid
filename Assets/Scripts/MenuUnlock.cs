using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuUnlock : MonoBehaviour
{
    [SerializeField] private TMP_Text unlockTitleText;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private Image difficultyIconImage;
    [SerializeField] private Button nextButton;
    [SerializeField] private RewardButtonUI rewardCard;
    [SerializeField] private Image characterUnlockImage;
    [SerializeField] private RectTransform glowTransform;
    [SerializeField] private ContinuousRotate glowRotate;
    [SerializeField] private AudioClip unlockCharacterSound;

    private Tween characterRevealDelayTween;
    private Tween glowScaleTween;
    private Tween glowSpeedResetTween;
    private Vector3 glowBaseScale = Vector3.one;
    private float glowBaseRotateSpeed = 45f;

    public void CacheReferences()
    {
        unlockTitleText ??= FindComponentByName<TMP_Text>(transform, "tUnlock");
        difficultyText ??= FindComponentByName<TMP_Text>(transform, "tDifficulty");
        difficultyIconImage ??= FindComponentByName<Image>(transform, "iDifficultyIcon");
        nextButton ??= FindNamedButton(transform, "BNext");
        rewardCard ??= GetComponentInChildren<RewardButtonUI>(true);
        characterUnlockImage ??= FindComponentByName<Image>(transform, "iChara");
        glowTransform ??= FindComponentByName<RectTransform>(transform, "iGlow");
        glowRotate ??= glowTransform != null ? glowTransform.GetComponent<ContinuousRotate>() : null;
        if (glowTransform != null)
        {
            glowBaseScale = glowTransform.localScale;
        }
        if (glowRotate != null)
        {
            glowBaseRotateSpeed = glowRotate.rotateSpeed;
        }
    }

    public void Bind(
        TourmentUnlockResult unlockResult,
        CharacterData fallbackCharacterData,
        TourmentData tourmentData,
        RewardButtonStyle powerRewardStyle,
        RewardButtonTheme powerRewardTheme,
        RewardButtonStyle itemRewardStyle,
        RewardButtonTheme itemRewardTheme,
        Func<RewardPresentationIconKind, Sprite> resolveTypeIcon,
        Action nextCallback)
    {
        CacheReferences();

        CharacterData resolvedCharacterData = unlockResult != null && unlockResult.CharacterData != null
            ? unlockResult.CharacterData
            : fallbackCharacterData;
        CharacterData unlockedCharacterData = unlockResult != null ? unlockResult.UnlockedCharacterData : null;
        bool isCharacterUnlock = unlockResult != null && unlockResult.Kind == TourmentUnlockResultKind.Character;

        ResetCharacterUnlockPresentation();

        if (unlockTitleText != null)
        {
            unlockTitleText.text = unlockResult != null ? unlockResult.Kind switch
            {
                TourmentUnlockResultKind.Tourment => $"You unlocked a new Tourment with {(resolvedCharacterData != null ? resolvedCharacterData.CharacterName : "this character")}!",
                TourmentUnlockResultKind.Character => $"You unlocked a new Character: {(unlockedCharacterData != null ? unlockedCharacterData.CharacterName : "Unknown")}!",
                TourmentUnlockResultKind.Item => "You unlocked a new Object!",
                _ => "You unlocked a new Ability!"
            } : string.Empty;
        }

        if (difficultyText != null)
        {
            int level = unlockResult != null ? Mathf.Max(1, unlockResult.TourmentLevel) : 1;
            difficultyText.text = tourmentData != null ? tourmentData.DisplayName : $"TORMENT {level}";
        }

        if (difficultyIconImage != null)
        {
            difficultyIconImage.sprite = tourmentData != null ? tourmentData.Icon : null;
            difficultyIconImage.enabled = difficultyIconImage.sprite != null;
        }

        if (rewardCard != null)
        {
            RewardDefinition rewardDefinition = unlockResult != null ? unlockResult.RewardDefinition : null;
            if (rewardDefinition == null || isCharacterUnlock)
            {
                rewardCard.gameObject.SetActive(false);
            }
            else
            {
                rewardCard.gameObject.SetActive(true);
                RewardOffer rewardOffer = rewardDefinition.CreateOffer();
                bool isItem = rewardOffer.Kind == RewardOfferKind.Item;
                RewardButtonStyle style = isItem ? itemRewardStyle : powerRewardStyle;
                RewardButtonTheme theme = isItem ? itemRewardTheme : powerRewardTheme;
                Sprite typeIcon = resolveTypeIcon != null ? resolveTypeIcon(rewardOffer.IconKind) : null;
                rewardCard.Bind(rewardOffer, style, theme, typeIcon, null);
                rewardCard.SetShopPurchaseState(false, 0, false, false, null);
                if (rewardCard.Button != null)
                {
                    rewardCard.Button.interactable = false;
                }
            }
        }

        if (characterUnlockImage != null)
        {
            characterUnlockImage.gameObject.SetActive(isCharacterUnlock && unlockedCharacterData != null);
            if (isCharacterUnlock && unlockedCharacterData != null)
            {
                characterUnlockImage.sprite = unlockedCharacterData.Portrait != null
                    ? unlockedCharacterData.Portrait
                    : unlockedCharacterData.PortraitWin;
                characterUnlockImage.enabled = characterUnlockImage.sprite != null;
                PlayCharacterUnlockPresentation();
            }
        }

        BindButton(nextButton, nextCallback);
    }

    private void ResetCharacterUnlockPresentation()
    {
        characterRevealDelayTween?.Kill();
        glowScaleTween?.Kill();
        glowSpeedResetTween?.Kill();

        if (characterUnlockImage != null)
        {
            characterUnlockImage.DOKill();
            characterUnlockImage.color = Color.white;
            characterUnlockImage.gameObject.SetActive(false);
        }

        if (glowTransform != null)
        {
            glowTransform.DOKill();
            glowTransform.localScale = glowBaseScale;
            glowTransform.gameObject.SetActive(false);
        }
        if (glowRotate != null)
        {
            glowRotate.SetRotateSpeed(glowBaseRotateSpeed);
        }
    }

    private void PlayCharacterUnlockPresentation()
    {
        if (characterUnlockImage == null)
        {
            return;
        }

        characterUnlockImage.color = Color.black;
        if (glowTransform != null)
        {
            glowTransform.gameObject.SetActive(true);
            glowTransform.localScale = glowBaseScale;
        }

        characterRevealDelayTween = DOVirtual.DelayedCall(2f, () =>
        {
            if (characterUnlockImage == null)
            {
                return;
            }

            characterUnlockImage.DOColor(Color.white, 0.25f);
            if (unlockCharacterSound != null)
            {
                SoundManager.Instance?.Play2DSound(unlockCharacterSound, 1f, 1f);
            }

            if (glowTransform != null)
            {
                glowScaleTween = glowTransform.DOScale(glowBaseScale * 2f, 0.5f)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.OutQuad);
            }

            if (glowRotate != null)
            {
                float baseSpeed = Mathf.Max(0.01f, glowBaseRotateSpeed);
                glowRotate.SetRotateSpeed(baseSpeed * 4f);
                glowSpeedResetTween = DOVirtual.DelayedCall(1f, () =>
                {
                    if (glowRotate != null)
                    {
                        glowRotate.SetRotateSpeed(baseSpeed);
                    }
                });
            }
        });
    }

    private static void BindButton(Button button, Action callback)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        if (callback != null)
        {
            button.onClick.AddListener(() => callback());
        }
    }

    private static Button FindNamedButton(Transform root, string objectName)
    {
        Button[] buttons = root != null ? root.GetComponentsInChildren<Button>(true) : Array.Empty<Button>();
        for (int index = 0; index < buttons.Length; index++)
        {
            if (buttons[index] != null && string.Equals(buttons[index].name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return buttons[index];
            }
        }

        return null;
    }

    private static T FindComponentByName<T>(Transform root, string objectName) where T : Component
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        T[] components = root.GetComponentsInChildren<T>(true);
        for (int index = 0; index < components.Length; index++)
        {
            T component = components[index];
            if (component != null && string.Equals(component.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return component;
            }
        }

        return null;
    }
}
