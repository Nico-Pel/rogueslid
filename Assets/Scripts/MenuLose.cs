using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuLose : MonoBehaviour
{
    [SerializeField] private Image characterPortraitImage;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;

    public void CacheReferences()
    {
        characterPortraitImage ??= FindComponentByName<Image>(transform, "iChara");
        characterNameText ??= FindComponentByName<TMP_Text>(transform, "Chara-Name")
                              ?? FindComponentByName<TMP_Text>(transform, "tTitle");
        retryButton ??= FindNamedButton(transform, "BRetry");
        menuButton ??= FindNamedButton(transform, "BMenu");
    }

    public void Bind(string characterName, Sprite losePortrait, Action retryCallback, Action menuCallback)
    {
        CacheReferences();

        if (characterPortraitImage != null)
        {
            characterPortraitImage.sprite = losePortrait;
            characterPortraitImage.enabled = losePortrait != null;
        }

        if (characterNameText != null)
        {
            characterNameText.text = characterName ?? string.Empty;
        }

        BindButton(retryButton, retryCallback);
        BindButton(menuButton, menuCallback);
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
