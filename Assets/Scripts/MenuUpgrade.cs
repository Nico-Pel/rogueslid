using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuUpgrade : MonoBehaviour
{
    [SerializeField] private Transform upgradeTreeRoot;
    [SerializeField] private Transform upgradeArrowsRoot;
    [SerializeField] private Button backgroundButton;
    [SerializeField] private GameObject frameRoot;
    [SerializeField] private Image frameIconImage;
    [SerializeField] private TMP_Text frameTitleText;
    [SerializeField] private TMP_Text frameDescriptionText;
    [SerializeField] private Button unlockButton;
    [SerializeField] private TMP_Text unlockPriceText;
    [SerializeField] private Image unlockOrbImage;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Image characterTitleImage;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private Image characterIconImage;
    [SerializeField] private List<UpgradeButtons> upgradeButtons = new List<UpgradeButtons>();

    public Transform UpgradeTreeRoot => upgradeTreeRoot;
    public Transform UpgradeArrowsRoot => upgradeArrowsRoot;
    public Button BackgroundButton => backgroundButton;
    public GameObject FrameRoot => frameRoot;
    public Image FrameIconImage => frameIconImage;
    public TMP_Text FrameTitleText => frameTitleText;
    public TMP_Text FrameDescriptionText => frameDescriptionText;
    public Button UnlockButton => unlockButton;
    public TMP_Text UnlockPriceText => unlockPriceText;
    public Image UnlockOrbImage => unlockOrbImage;
    public Button PreviousButton => previousButton;
    public Button NextButton => nextButton;
    public Image CharacterTitleImage => characterTitleImage;
    public TMP_Text CharacterNameText => characterNameText;
    public Image CharacterIconImage => characterIconImage;
    public List<UpgradeButtons> UpgradeButtons => upgradeButtons;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheReferences();
        }
    }

    public void CacheReferences()
    {
        upgradeTreeRoot ??= FindTransformByName(transform, "UpgradesTree");
        upgradeArrowsRoot ??= FindTransformByName(upgradeTreeRoot, "Arrows");
        backgroundButton ??= FindNamedButton(transform, "iBackground");

        if (frameRoot == null)
        {
            Transform frameTransform = FindTransformByName(transform, "iFrame");
            frameRoot = frameTransform != null ? frameTransform.gameObject : null;
        }

        Transform frameTransformRoot = frameRoot != null ? frameRoot.transform : transform;
        frameIconImage ??= FindComponentByName<Image>(frameTransformRoot, "iIcon");
        frameTitleText ??= FindComponentByName<TMP_Text>(frameTransformRoot, "tTitle");
        frameDescriptionText ??= FindComponentByName<TMP_Text>(frameTransformRoot, "tDescription");
        unlockButton ??= FindNamedButton(frameTransformRoot, "BUnlock") ?? FindNamedButton(frameTransformRoot, "bUnlock");
        unlockPriceText ??= FindComponentByName<TMP_Text>(unlockButton != null ? unlockButton.transform : frameTransformRoot, "tPrice");
        unlockOrbImage ??= FindComponentByName<Image>(unlockButton != null ? unlockButton.transform : frameTransformRoot, "iOrb");
        previousButton ??= FindNamedButton(transform, "BPrevious");
        nextButton ??= FindNamedButton(transform, "BNext");
        characterTitleImage ??= FindComponentByName<Image>(transform, "CharaTitle");
        characterNameText ??= FindComponentByName<TMP_Text>(transform, "Chara-Name");
        characterIconImage ??= FindComponentByName<Image>(transform, "iCharaIcon");

        if (upgradeButtons.Count == 0 && upgradeTreeRoot != null)
        {
            UpgradeButtons[] foundButtons = upgradeTreeRoot.GetComponentsInChildren<UpgradeButtons>(true);
            Array.Sort(foundButtons, CompareUpgradeButtons);
            for (int index = 0; index < foundButtons.Length; index++)
            {
                foundButtons[index]?.CacheReferences();
            }

            upgradeButtons.AddRange(foundButtons);
        }
        else
        {
            for (int index = 0; index < upgradeButtons.Count; index++)
            {
                upgradeButtons[index]?.CacheReferences();
            }
        }
    }

    private static int CompareUpgradeButtons(UpgradeButtons left, UpgradeButtons right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        RectTransform leftRect = left.RectTransform;
        RectTransform rightRect = right.RectTransform;
        if (leftRect != null && rightRect != null)
        {
            Vector2 leftPosition = leftRect.anchoredPosition;
            Vector2 rightPosition = rightRect.anchoredPosition;
            int yCompare = leftPosition.y.CompareTo(rightPosition.y);
            if (yCompare != 0)
            {
                return yCompare;
            }

            int xCompare = leftPosition.x.CompareTo(rightPosition.x);
            if (xCompare != 0)
            {
                return xCompare;
            }
        }

        return string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
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

    private static Transform FindTransformByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        if (string.Equals(root.name, objectName, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform match = FindTransformByName(root.GetChild(index), objectName);
            if (match != null)
            {
                return match;
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
