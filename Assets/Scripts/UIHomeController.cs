using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIHomeController : MonoBehaviour
{
    private enum HomeCharacterSelection
    {
        Pandora,
        Hector,
        Zhuang
    }

    [Header("Characters")]
    [SerializeField] private CharacterData pandoraCharacterData;
    [SerializeField] private CharacterData hectorCharacterData;
    [SerializeField] private CharacterData zhuangCharacterData;
    [SerializeField] private GameObject pandoraCharacterPrefab;
    [SerializeField] private GameObject hectorCharacterPrefab;
    public float themeTweenDuration = 0.5f;

    [Header("UI")]
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

    [Header("Theme Colors")]
    [SerializeField] private List<Image> element35Images = new List<Image>();
    [SerializeField] private List<Image> element41Images = new List<Image>();

    private BoardManager board;
    private UIGame uiGame;
    private SoundManager soundManager;
    private Image portraitButtonImage;
    private HomeCharacterSelection currentSelection = HomeCharacterSelection.Pandora;

    public bool IsVisible => gameObject.activeInHierarchy;

    public void Initialize(
        BoardManager boardManager,
        UIGame gameUi,
        SoundManager manager)
    {
        board = boardManager;
        uiGame = gameUi;
        soundManager = manager;
        Debug.Log($"[Pouet Startup] UIHome.Initialize called. board={(board != null ? board.name : "null")} uiGame={(uiGame != null ? uiGame.name : "null")} soundManager={(soundManager != null ? soundManager.name : "null")}", this);
        CacheReferences();
    }

    public void ShowDefault()
    {
        currentSelection = HomeCharacterSelection.Pandora;
        Debug.Log("[Pouet Startup] UIHome.ShowDefault -> Pandora", this);
        Show(true);
    }

    public void ShowForCharacter(CharacterData characterData)
    {
        SelectCharacter(characterData);
        Debug.Log($"[Pouet Startup] UIHome.ShowForCharacter -> {(characterData != null ? characterData.CharacterName : "null")}", this);
        Show(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(HandleStartClicked);
        }

        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(HandlePreviousClicked);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(HandleNextClicked);
        }
    }

    private void CacheReferences()
    {
        if (characterTitleImage == null)
        {
            characterTitleImage = FindComponentByName<Image>(transform, "CharaTitle");
        }

        if (portraitButton == null)
        {
            portraitButton = FindComponentByName<Button>(transform, "BPortrait");
        }

        if (portraitButtonImage == null && portraitButton != null)
        {
            portraitButtonImage = portraitButton.GetComponent<Image>();
        }

        if (characterNameText == null)
        {
            characterNameText = FindComponentByName<TMP_Text>(transform, "Chara-Name");
        }

        if (characterPortraitImage == null)
        {
            characterPortraitImage = FindComponentByName<Image>(transform, "iChara");
        }

        if (startButton == null)
        {
            startButton = FindComponentByName<Button>(transform, "BStart");
        }

        if (startText == null)
        {
            startText = FindComponentByName<TMP_Text>(transform, "tStart");
        }

        if (startCharacterIconImage == null)
        {
            startCharacterIconImage = FindComponentByName<Image>(transform, "iCharaIcon");
        }

        if (storyFrameImage == null)
        {
            storyFrameImage = FindComponentByName<Image>(transform, "iStoryFrame");
        }

        if (storyDescriptionText == null)
        {
            storyDescriptionText = FindComponentByName<TMP_Text>(transform, "tDescription");
        }

        if (previousButton == null)
        {
            previousButton = FindComponentByName<Button>(transform, "BPrevious");
        }

        if (nextButton == null)
        {
            nextButton = FindComponentByName<Button>(transform, "BNext");
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(HandleStartClicked);
            startButton.onClick.AddListener(HandleStartClicked);
        }

        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(HandlePreviousClicked);
            previousButton.onClick.AddListener(HandlePreviousClicked);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(HandleNextClicked);
            nextButton.onClick.AddListener(HandleNextClicked);
        }

        CacheThemeColorImages();
    }

    private void Show(bool immediate)
    {
        CacheReferences();
        if (uiGame != null)
        {
            uiGame.gameObject.SetActive(false);
        }

        gameObject.SetActive(true);
        soundManager?.PlayMainMenuMusic();
        RefreshMenu(immediate);
    }

    private void HandlePreviousClicked()
    {
        SoundManager.Instance?.PlayClick();
        currentSelection = (HomeCharacterSelection)(((int)currentSelection + 2) % 3);
        RefreshMenu(false);
    }

    private void HandleNextClicked()
    {
        SoundManager.Instance?.PlayClick();
        currentSelection = (HomeCharacterSelection)(((int)currentSelection + 1) % 3);
        RefreshMenu(false);
    }

    private void HandleStartClicked()
    {
        HomeCharacterConfig config = GetCurrentCharacterConfig();
        Debug.Log($"[Pouet Startup] UIHome.HandleStartClicked. selection={currentSelection} data={(config.Data != null ? config.Data.CharacterName : "null")} prefab={(config.Prefab != null ? config.Prefab.name : "null")} isLocked={config.IsLocked} boardAssigned={board != null}", this);
        if (config.Data == null || config.IsLocked || board == null)
        {
            return;
        }

        if (config.Prefab == null)
        {
            Debug.LogWarning($"[UIHome] Missing playable prefab for {config.Data.CharacterName}.", this);
            return;
        }

        Hide();
        if (uiGame != null)
        {
            uiGame.gameObject.SetActive(true);
        }

        board.SetPlayerCharacterSetup(config.Prefab, config.Data);
        board.ResetArenaProgression();
        board.GenerateBoard();
        Debug.Log("[Pouet Startup] UIHome.HandleStartClicked finished board setup + generate.", this);
    }

    private void SelectCharacter(CharacterData characterData)
    {
        if (characterData == hectorCharacterData)
        {
            currentSelection = HomeCharacterSelection.Hector;
        }
        else if (characterData == zhuangCharacterData)
        {
            currentSelection = HomeCharacterSelection.Zhuang;
        }
        else
        {
            currentSelection = HomeCharacterSelection.Pandora;
        }
    }

    private void RefreshMenu(bool immediate)
    {
        CacheReferences();
        HomeCharacterConfig config = GetCurrentCharacterConfig();
        CharacterData characterData = config.Data;
        if (characterData == null)
        {
            return;
        }

        if (characterNameText != null)
        {
            characterNameText.text = characterData.CharacterName;
        }

        if (characterPortraitImage != null)
        {
            characterPortraitImage.sprite = characterData.Portrait;
            characterPortraitImage.enabled = characterData.Portrait != null;
        }

        if (startCharacterIconImage != null)
        {
            startCharacterIconImage.sprite = characterData.PathIcon;
            startCharacterIconImage.enabled = characterData.PathIcon != null;
            startCharacterIconImage.color = config.IsLocked ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
        }

        if (startButton != null)
        {
            startButton.interactable = !config.IsLocked;
        }

        if (startText != null)
        {
            startText.text = config.IsLocked ? "LOCKED" : "START";
        }

        ApplyTheme(characterData, immediate);
    }

    private void ApplyTheme(CharacterData characterData, bool immediate)
    {
        Color element35Color = characterData.GetUIColor(CharacterUIColorKey.PortraitNameplateBackground, new Color(0.30588236f, 0.08235294f, 0.47058824f, 1f));
        Color element41Color = characterData.GetUIColor(CharacterUIColorKey.AbilityButtonTypeIcon, new Color(0.44313726f, 0f, 0.62352943f, 1f));
        Color descriptionTextColor = characterData.GetUIColor(CharacterUIColorKey.PowerRewardDescriptionText, new Color(0.6862745f, 0.54509807f, 0.74509805f, 1f));
        Color titleTextColor = characterData.GetUIColor(CharacterUIColorKey.PowerRewardNewSubtitleText, new Color(1f, 0.73f, 0f, 1f));
        float duration = immediate ? 0f : themeTweenDuration;

        TweenImageColor(characterTitleImage, element35Color, duration);
        TweenImageColor(portraitButtonImage, element35Color, duration);
        TweenImageColor(storyFrameImage, element35Color, duration);
        TweenImagesColor(element35Images, element35Color, duration);
        TweenImagesColor(element41Images, element41Color, duration);
        TweenTextColor(characterNameText, titleTextColor, duration);
        TweenTextColor(storyDescriptionText, descriptionTextColor, duration);
    }

    private void CacheThemeColorImages()
    {
        AutoAssignThemeColorImages(element35Images, new Color(0.30588236f, 0.08235294f, 0.47058824f, 1f));
        AutoAssignThemeColorImages(element41Images, new Color(0.44313726f, 0f, 0.62352943f, 1f));
    }

    private void AutoAssignThemeColorImages(List<Image> targetList, Color referenceColor)
    {
        if (targetList == null)
        {
            return;
        }

        RemoveMissingImages(targetList);
        if (targetList.Count > 0)
        {
            return;
        }

        Image[] allImages = GetComponentsInChildren<Image>(true);
        for (int index = 0; index < allImages.Length; index++)
        {
            Image image = allImages[index];
            if (image == null || ColorsDiffer(image.color, referenceColor))
            {
                continue;
            }

            targetList.Add(image);
        }
    }

    private static void RemoveMissingImages(List<Image> images)
    {
        if (images == null)
        {
            return;
        }

        for (int index = images.Count - 1; index >= 0; index--)
        {
            if (images[index] == null)
            {
                images.RemoveAt(index);
            }
        }
    }

    private static bool ColorsDiffer(Color left, Color right)
    {
        const float tolerance = 0.01f;
        return Mathf.Abs(left.r - right.r) > tolerance ||
               Mathf.Abs(left.g - right.g) > tolerance ||
               Mathf.Abs(left.b - right.b) > tolerance ||
               Mathf.Abs(left.a - right.a) > tolerance;
    }

    private HomeCharacterConfig GetCurrentCharacterConfig()
    {
        switch (currentSelection)
        {
            case HomeCharacterSelection.Hector:
                return new HomeCharacterConfig(hectorCharacterData, hectorCharacterPrefab, false);
            case HomeCharacterSelection.Zhuang:
                return new HomeCharacterConfig(zhuangCharacterData, null, true);
            default:
                return new HomeCharacterConfig(pandoraCharacterData, pandoraCharacterPrefab, false);
        }
    }

    private readonly struct HomeCharacterConfig
    {
        public HomeCharacterConfig(CharacterData data, GameObject prefab, bool isLocked)
        {
            Data = data;
            Prefab = prefab;
            IsLocked = isLocked;
        }

        public CharacterData Data { get; }
        public GameObject Prefab { get; }
        public bool IsLocked { get; }
    }

    private static void TweenImageColor(Image image, Color targetColor, float duration)
    {
        if (image == null)
        {
            return;
        }

        image.DOKill();
        if (duration <= 0f)
        {
            image.color = targetColor;
            return;
        }

        image.DOColor(targetColor, duration).SetUpdate(true);
    }

    private static void TweenImagesColor(List<Image> images, Color targetColor, float duration)
    {
        if (images == null)
        {
            return;
        }

        for (int index = 0; index < images.Count; index++)
        {
            TweenImageColor(images[index], targetColor, duration);
        }
    }

    private static void TweenTextColor(TMP_Text text, Color targetColor, float duration)
    {
        if (text == null)
        {
            return;
        }

        text.DOKill();
        if (duration <= 0f)
        {
            text.color = targetColor;
            return;
        }

        text.DOColor(targetColor, duration).SetUpdate(true);
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
            if (component != null && component.name == objectName)
            {
                return component;
            }
        }

        return null;
    }
}
