using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuMain : MonoBehaviour
{
    [SerializeField] private Image characterTitleImage;
    [SerializeField] private Button portraitButton;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private Image characterPortraitImage;
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text startText;
    [SerializeField] private Image startCharacterIconImage;
    [SerializeField] private Image storyFrameImage;
    [SerializeField] private TMP_Text storyDescriptionText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private GameObject difficultyRoot;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private Image difficultyIconImage;
    [SerializeField] private Button difficultyPreviousButton;
    [SerializeField] private Button difficultyNextButton;
    [SerializeField] private GameObject statsFrameRoot;
    [SerializeField] private UnitStatRowUI attackStat = new UnitStatRowUI();
    [SerializeField] private UnitStatRowUI forceStat = new UnitStatRowUI();
    [SerializeField] private UnitStatRowUI hpStat = new UnitStatRowUI();
    [SerializeField] private UnitStatRowUI resistanceStat = new UnitStatRowUI();
    [SerializeField] private UnitStatRowUI mobilityStat = new UnitStatRowUI();
    [SerializeField] private UnitStatRowUI regenStat = new UnitStatRowUI();

    public Image CharacterTitleImage => characterTitleImage;
    public Button PortraitButton => portraitButton;
    public TMP_Text CharacterNameText => characterNameText;
    public Image CharacterPortraitImage => characterPortraitImage;
    public Button StartButton => startButton;
    public TMP_Text StartText => startText;
    public Image StartCharacterIconImage => startCharacterIconImage;
    public Image StoryFrameImage => storyFrameImage;
    public TMP_Text StoryDescriptionText => storyDescriptionText;
    public Button PreviousButton => previousButton;
    public Button NextButton => nextButton;
    public GameObject DifficultyRoot => difficultyRoot;
    public TMP_Text DifficultyText => difficultyText;
    public Image DifficultyIconImage => difficultyIconImage;
    public Button DifficultyPreviousButton => difficultyPreviousButton;
    public Button DifficultyNextButton => difficultyNextButton;
    public GameObject StatsFrameRoot => statsFrameRoot;

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
        characterTitleImage ??= FindComponentByName<Image>(transform, "CharaTitle");
        portraitButton ??= FindComponentByName<Button>(transform, "BPortrait");
        characterNameText ??= FindComponentByName<TMP_Text>(transform, "Chara-Name");
        characterPortraitImage ??= FindComponentByName<Image>(transform, "iChara");
        startButton ??= FindComponentByName<Button>(transform, "BStart");
        startText ??= FindComponentByName<TMP_Text>(transform, "tStart");
        startCharacterIconImage ??= FindComponentByName<Image>(transform, "iCharaIcon");
        storyFrameImage ??= FindComponentByName<Image>(transform, "iStoryFrame");
        storyDescriptionText ??= FindComponentByName<TMP_Text>(transform, "tDescription");
        previousButton ??= FindNamedButton(transform, "BPrevious");
        nextButton ??= FindNamedButton(transform, "BNext");
        difficultyRoot ??= FindGameObjectByName(transform, "iDifficulty");
        Transform difficultyRootTransform = difficultyRoot != null ? difficultyRoot.transform : null;
        difficultyText ??= FindComponentByName<TMP_Text>(difficultyRootTransform, "tDifficulty");
        difficultyIconImage ??= FindComponentByName<Image>(difficultyRootTransform, "iDifficultyIcon");
        difficultyPreviousButton ??= FindNamedButton(difficultyRootTransform, "BPrevious");
        difficultyNextButton ??= FindNamedButton(difficultyRootTransform, "BNext");

        if (statsFrameRoot == null)
        {
            Transform statsTransform = FindTransformByName(transform, "iFrameStats") ?? FindTransformByName(transform, "iStats");
            statsFrameRoot = statsTransform != null ? statsTransform.gameObject : null;
        }

        Transform statsRootTransform = statsFrameRoot != null ? statsFrameRoot.transform : null;
        attackStat.CacheFrom(statsRootTransform, "StatAttack");
        forceStat.CacheFrom(statsRootTransform, "StatForce");
        hpStat.CacheFrom(statsRootTransform, "StatHP");
        resistanceStat.CacheFrom(statsRootTransform, "StatResistance");
        mobilityStat.CacheFrom(statsRootTransform, "StatMobility");
        regenStat.CacheFrom(statsRootTransform, "StatRegen");
    }

    public void BindPersistentStats(CharacterData characterData)
    {
        CacheReferences();
        if (characterData == null)
        {
            return;
        }

        attackStat.Bind(true, characterData.GetPersistentPreviewBasicAttackDamage().ToString());
        forceStat.Bind(true, characterData.GetPersistentPreviewBonusDamage().ToString());
        hpStat.Bind(true, characterData.GetPersistentPreviewMaxHealth().ToString());
        resistanceStat.Bind(true, characterData.GetPersistentPreviewResistance().ToString());
        mobilityStat.Bind(true, characterData.GetPersistentPreviewMovementPoints().ToString());
        regenStat.Bind(true, characterData.GetPersistentPreviewRegen().ToString());
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

    private static GameObject FindGameObjectByName(Transform root, string objectName)
    {
        Transform match = FindTransformByName(root, objectName);
        return match != null ? match.gameObject : null;
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
}
