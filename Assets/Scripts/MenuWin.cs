using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuWin : MonoBehaviour
{
    [SerializeField] private Image characterPortraitImage;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private Image difficultyIconImage;
    [SerializeField] private Button nextButton;

    public void CacheReferences()
    {
        characterPortraitImage ??= FindComponentByName<Image>(transform, "PortraitWin")
                                  ?? FindComponentByName<Image>(transform, "iPortraits")
                                  ?? FindComponentByName<Image>(transform, "Portrait");
        characterNameText ??= FindComponentByName<TMP_Text>(transform, "tCharaName");
        difficultyText ??= FindComponentByName<TMP_Text>(transform, "tDifficulty");
        difficultyIconImage ??= FindComponentByName<Image>(transform, "iDifficultyIcon");
        nextButton ??= FindNamedButton(transform, "BNext");
    }

    public void Bind(CharacterData characterData, TourmentData tourmentData, Action nextCallback)
    {
        CacheReferences();

        if (characterPortraitImage != null)
        {
            Sprite portrait = characterData != null ? characterData.PortraitWin : null;
            characterPortraitImage.sprite = portrait;
            characterPortraitImage.enabled = portrait != null;
        }

        if (characterNameText != null)
        {
            characterNameText.text = characterData != null ? characterData.CharacterName : string.Empty;
        }

        if (difficultyText != null)
        {
            int level = tourmentData != null ? tourmentData.Level : 1;
            difficultyText.text = tourmentData != null ? tourmentData.DisplayName : $"TORMENT {level}";
        }

        if (difficultyIconImage != null)
        {
            difficultyIconImage.sprite = tourmentData != null ? tourmentData.Icon : null;
            difficultyIconImage.enabled = difficultyIconImage.sprite != null;
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
