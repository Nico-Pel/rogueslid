using System;
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

    public void CacheReferences()
    {
        unlockTitleText ??= FindComponentByName<TMP_Text>(transform, "tUnlock");
        difficultyText ??= FindComponentByName<TMP_Text>(transform, "tDifficulty");
        difficultyIconImage ??= FindComponentByName<Image>(transform, "iDifficultyIcon");
        nextButton ??= FindNamedButton(transform, "BNext");
        rewardCard ??= GetComponentInChildren<RewardButtonUI>(true);
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

        if (unlockTitleText != null)
        {
            unlockTitleText.text = unlockResult != null ? unlockResult.Kind switch
            {
                TourmentUnlockResultKind.Tourment => $"You unlocked a new Tourment with {(resolvedCharacterData != null ? resolvedCharacterData.CharacterName : "this character")}!",
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
            if (rewardDefinition == null)
            {
                rewardCard.gameObject.SetActive(false);
            }
            else
            {
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

        BindButton(nextButton, nextCallback);
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
