using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIHomeController : MonoBehaviour
{
    private enum HomeCharacterSelection
    {
        Pandora,
        Hector,
        Zhuang
    }

    private enum HomeMenuSection
    {
        Main,
        Upgrade
    }

    [Header("Characters")]
    [SerializeField] private CharacterData pandoraCharacterData;
    [SerializeField] private CharacterData hectorCharacterData;
    [SerializeField] private CharacterData zhuangCharacterData;
    [SerializeField] private GameObject pandoraCharacterPrefab;
    [SerializeField] private GameObject hectorCharacterPrefab;
    public float themeTweenDuration = 0.5f;

    [Header("Menus")]
    [SerializeField] private MenuMain menuMain;
    [SerializeField] private MenuUpgrade menuUpgrade;

    [Header("Tabs")]
    [SerializeField] private Button upgradeTabButton;
    [SerializeField] private Image upgradeTabImage;
    [SerializeField] private Button mainTabButton;
    [SerializeField] private Image mainTabImage;
    [SerializeField] private Button thirdTabButton;
    [SerializeField] private Image thirdTabImage;

    [Header("Orbs")]
    [SerializeField] private GameObject orbRoot;
    [SerializeField] private Image orbIconImage;
    [SerializeField] private TMP_Text orbCountText;

    [Header("Theme Colors")]
    [SerializeField] private List<Image> element35Images = new List<Image>();
    [SerializeField] private List<Image> element41Images = new List<Image>();

#if UNITY_EDITOR
    private const KeyCode DebugGiveOrbsKey = KeyCode.J;
#endif

    private static readonly Color LockedUpgradeColor = new Color32(0xA4, 0xA4, 0xA4, 0x76);
    private static readonly Color PriceNormalColor = Color.white;
    private static readonly Color PriceInsufficientColor = new Color32(0xFF, 0x63, 0x63, 0xFF);
    private static readonly int[][] UpgradePrerequisiteIndices =
    {
        Array.Empty<int>(),
        new[] { 1 },
        new[] { 1 },
        new[] { 2 },
        new[] { 2 },
        new[] { 3 },
        new[] { 3 },
        new[] { 5, 6 },
        new[] { 5, 6 },
        new[] { 8, 9 },
        new[] { 8, 9 },
        new[] { 8, 9 }
    };

    private BoardManager board;
    private UIGame uiGame;
    private SoundManager soundManager;
    private Image portraitButtonImage;
    private HomeCharacterSelection currentSelection = HomeCharacterSelection.Pandora;
    private HomeMenuSection currentMenuSection = HomeMenuSection.Main;
    private int selectedUpgradeIndex = -1;

    private GameObject menuMainRoot => menuMain != null ? menuMain.gameObject : null;
    private GameObject menuUpgradeRoot => menuUpgrade != null ? menuUpgrade.gameObject : null;
    private Image characterTitleImage => menuMain != null ? menuMain.CharacterTitleImage : null;
    private Button portraitButton => menuMain != null ? menuMain.PortraitButton : null;
    private TMP_Text characterNameText => menuMain != null ? menuMain.CharacterNameText : null;
    private Image characterPortraitImage => menuMain != null ? menuMain.CharacterPortraitImage : null;
    private Button startButton => menuMain != null ? menuMain.StartButton : null;
    private TMP_Text startText => menuMain != null ? menuMain.StartText : null;
    private Image startCharacterIconImage => menuMain != null ? menuMain.StartCharacterIconImage : null;
    private Image storyFrameImage => menuMain != null ? menuMain.StoryFrameImage : null;
    private TMP_Text storyDescriptionText => menuMain != null ? menuMain.StoryDescriptionText : null;
    private Button previousButton => menuMain != null ? menuMain.PreviousButton : null;
    private Button nextButton => menuMain != null ? menuMain.NextButton : null;
    private GameObject difficultyRoot => menuMain != null ? menuMain.DifficultyRoot : null;
    private TMP_Text difficultyText => menuMain != null ? menuMain.DifficultyText : null;
    private Image difficultyIconImage => menuMain != null ? menuMain.DifficultyIconImage : null;
    private Button difficultyPreviousButton => menuMain != null ? menuMain.DifficultyPreviousButton : null;
    private Button difficultyNextButton => menuMain != null ? menuMain.DifficultyNextButton : null;
    private Transform upgradeTreeRoot => menuUpgrade != null ? menuUpgrade.UpgradeTreeRoot : null;
    private Transform upgradeArrowsRoot => menuUpgrade != null ? menuUpgrade.UpgradeArrowsRoot : null;
    private Button upgradeBackgroundButton => menuUpgrade != null ? menuUpgrade.BackgroundButton : null;
    private GameObject upgradeFrameRoot => menuUpgrade != null ? menuUpgrade.FrameRoot : null;
    private Image upgradeFrameIconImage => menuUpgrade != null ? menuUpgrade.FrameIconImage : null;
    private TMP_Text upgradeFrameTitleText => menuUpgrade != null ? menuUpgrade.FrameTitleText : null;
    private TMP_Text upgradeFrameDescriptionText => menuUpgrade != null ? menuUpgrade.FrameDescriptionText : null;
    private Button upgradeUnlockButton => menuUpgrade != null ? menuUpgrade.UnlockButton : null;
    private TMP_Text upgradeUnlockPriceText => menuUpgrade != null ? menuUpgrade.UnlockPriceText : null;
    private Image upgradeUnlockOrbImage => menuUpgrade != null ? menuUpgrade.UnlockOrbImage : null;
    private Button upgradePreviousButton => menuUpgrade != null ? menuUpgrade.PreviousButton : null;
    private Button upgradeNextButton => menuUpgrade != null ? menuUpgrade.NextButton : null;
    private Image upgradeCharacterTitleImage => menuUpgrade != null ? menuUpgrade.CharacterTitleImage : null;
    private TMP_Text upgradeCharacterNameText => menuUpgrade != null ? menuUpgrade.CharacterNameText : null;
    private Image upgradeCharacterIconImage => menuUpgrade != null ? menuUpgrade.CharacterIconImage : null;
    private List<UpgradeButtons> upgradeButtons => menuUpgrade != null ? menuUpgrade.UpgradeButtons : null;

    public bool IsVisible => gameObject.activeInHierarchy;

    public void Initialize(
        BoardManager boardManager,
        UIGame gameUi,
        SoundManager manager)
    {
        board = boardManager;
        uiGame = gameUi;
        soundManager = manager;
        CacheReferences();
    }

    public void ShowDefault()
    {
        currentSelection = HomeCharacterSelection.Pandora;
        currentMenuSection = HomeMenuSection.Main;
        selectedUpgradeIndex = -1;
        Show(true);
    }

    public void ShowForCharacter(CharacterData characterData)
    {
        SelectCharacter(characterData);
        currentMenuSection = HomeMenuSection.Main;
        selectedUpgradeIndex = -1;
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

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheReferences();
        }
    }

    private void OnDestroy()
    {
        BindButton(startButton, HandleStartClicked, false);
        BindButton(previousButton, HandlePreviousClicked, false);
        BindButton(nextButton, HandleNextClicked, false);
        BindButton(difficultyPreviousButton, HandleDifficultyPreviousClicked, false);
        BindButton(difficultyNextButton, HandleDifficultyNextClicked, false);
        BindButton(upgradePreviousButton, HandleUpgradePreviousClicked, false);
        BindButton(upgradeNextButton, HandleUpgradeNextClicked, false);
        BindButton(upgradeBackgroundButton, HandleUpgradeBackgroundClicked, false);
        BindButton(upgradeUnlockButton, HandleUpgradeUnlockClicked, false);
        BindButton(upgradeTabButton, HandleUpgradeTabClicked, false);
        BindButton(mainTabButton, HandleMainTabClicked, false);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (gameObject.activeInHierarchy && Input.GetKeyDown(DebugGiveOrbsKey))
        {
            CharacterData selectedCharacter = GetCurrentCharacterData();
            if (selectedCharacter != null)
            {
                CharacterProgressionSaveManager.AddOrbs(selectedCharacter.CharacterId, 50);
                RefreshMenu(false);
            }
        }
#endif
    }

    private void CacheReferences()
    {
        if (menuMain == null)
        {
            Transform menuMainTransform = FindTransformByName(transform, "MenuMain");
            menuMain = menuMainTransform != null ? menuMainTransform.GetComponent<MenuMain>() : null;
        }

        if (menuUpgrade == null)
        {
            Transform menuUpgradeTransform = FindTransformByName(transform, "MenuUpgrade");
            menuUpgrade = menuUpgradeTransform != null ? menuUpgradeTransform.GetComponent<MenuUpgrade>() : null;
        }

        menuMain?.CacheReferences();
        menuUpgrade?.CacheReferences();
        orbRoot ??= FindGameObjectByName(transform, "UIOrb");

        upgradeTabButton ??= FindButtonInParent("BTab1");
        mainTabButton ??= FindButtonInParent("BTab2");
        thirdTabButton ??= FindButtonInParent("BTab3");
        upgradeTabImage ??= upgradeTabButton != null ? upgradeTabButton.GetComponent<Image>() : null;
        mainTabImage ??= mainTabButton != null ? mainTabButton.GetComponent<Image>() : null;
        thirdTabImage ??= thirdTabButton != null ? thirdTabButton.GetComponent<Image>() : null;

        CacheOrbReferences();
        CacheThemeColorImages();

        portraitButtonImage ??= portraitButton != null ? portraitButton.GetComponent<Image>() : null;

        BindButton(startButton, HandleStartClicked);
        BindButton(previousButton, HandlePreviousClicked);
        BindButton(nextButton, HandleNextClicked);
        BindButton(difficultyPreviousButton, HandleDifficultyPreviousClicked);
        BindButton(difficultyNextButton, HandleDifficultyNextClicked);
        BindButton(upgradePreviousButton, HandleUpgradePreviousClicked);
        BindButton(upgradeNextButton, HandleUpgradeNextClicked);
        BindButton(upgradeBackgroundButton, HandleUpgradeBackgroundClicked);
        BindButton(upgradeUnlockButton, HandleUpgradeUnlockClicked);
        BindButton(upgradeTabButton, HandleUpgradeTabClicked);
        BindButton(mainTabButton, HandleMainTabClicked);

        CacheUpgradeButtons();
    }

    private void CacheOrbReferences()
    {
        Transform root = orbRoot != null ? orbRoot.transform : transform;
        orbIconImage ??= FindComponentByName<Image>(root, "iOrb");
        orbCountText ??= FindComponentByName<TMP_Text>(root, "tOrb");
    }

    private void CacheUpgradeButtons()
    {
        if (menuUpgrade == null || upgradeTreeRoot == null || upgradeButtons == null)
        {
            return;
        }

        List<UpgradeButtons> foundButtons = new List<UpgradeButtons>();
        UpgradeButtons[] buttons = upgradeTreeRoot.GetComponentsInChildren<UpgradeButtons>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            UpgradeButtons candidate = buttons[index];
            if (candidate == null)
            {
                continue;
            }

            candidate.CacheReferences();
            foundButtons.Add(candidate);
        }

        foundButtons.Sort(CompareUpgradeButtons);
        if (upgradeButtons.Count != foundButtons.Count)
        {
            upgradeButtons.Clear();
            for (int index = 0; index < foundButtons.Count; index++)
            {
                upgradeButtons.Add(foundButtons[index]);
            }
        }
        else
        {
            for (int index = 0; index < foundButtons.Count; index++)
            {
                upgradeButtons[index] = foundButtons[index];
            }
        }

        List<Image> sortedArrows = GetSortedUpgradeArrows();
        for (int index = 0; index < upgradeButtons.Count; index++)
        {
            UpgradeButtons binding = upgradeButtons[index];
            if (binding == null)
            {
                continue;
            }

            binding.ArrowImages.Clear();
            foreach (int arrowIndex in GetArrowIndicesForUpgrade(index + 1))
            {
                if (arrowIndex >= 0 && arrowIndex < sortedArrows.Count && sortedArrows[arrowIndex] != null)
                {
                    binding.ArrowImages.Add(sortedArrows[arrowIndex]);
                }
            }
        }

        for (int index = 0; index < upgradeButtons.Count; index++)
        {
            int buttonIndex = index;
            Button button = upgradeButtons[index]?.Button;
            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleUpgradeButtonClicked(buttonIndex));
        }
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
        CycleCharacter(-1);
    }

    private void HandleNextClicked()
    {
        SoundManager.Instance?.PlayClick();
        CycleCharacter(1);
    }

    private void HandleUpgradePreviousClicked()
    {
        SoundManager.Instance?.PlayClick();
        CloseUpgradeFrame();
        CycleCharacter(-1);
    }

    private void HandleUpgradeNextClicked()
    {
        SoundManager.Instance?.PlayClick();
        CloseUpgradeFrame();
        CycleCharacter(1);
    }

    private void HandleUpgradeBackgroundClicked()
    {
        CloseUpgradeFrame();
    }

    private void HandleDifficultyPreviousClicked()
    {
        SoundManager.Instance?.PlayClick();
        OffsetSelectedTourment(-1);
    }

    private void HandleDifficultyNextClicked()
    {
        SoundManager.Instance?.PlayClick();
        OffsetSelectedTourment(1);
    }

    private void HandleUpgradeTabClicked()
    {
        SoundManager.Instance?.PlayClick();
        currentMenuSection = HomeMenuSection.Upgrade;
        CloseUpgradeFrame();
        RefreshMenu(false);
    }

    private void HandleMainTabClicked()
    {
        SoundManager.Instance?.PlayClick();
        currentMenuSection = HomeMenuSection.Main;
        CloseUpgradeFrame();
        RefreshMenu(false);
    }

    private void HandleUpgradeButtonClicked(int upgradeIndex)
    {
        if (upgradeIndex < 0 || upgradeIndex >= upgradeButtons.Count)
        {
            return;
        }

        SoundManager.Instance?.PlayClick();
        selectedUpgradeIndex = upgradeIndex;
        RefreshUpgradeButtons();
        RefreshUpgradeFrame();
    }

    private void HandleUpgradeUnlockClicked()
    {
        CharacterData characterData = GetCurrentCharacterData();
        CharacterUpgradeData upgradeData = GetSelectedUpgradeData();
        if (characterData == null || upgradeData == null)
        {
            return;
        }

        if (!CanPurchaseUpgrade(selectedUpgradeIndex, characterData, upgradeData))
        {
            return;
        }

        if (CharacterProgressionSaveManager.TryPurchaseUpgrade(characterData.CharacterId, upgradeData))
        {
            if (soundManager != null)
            {
                soundManager.PlayUiSound(soundManager.MoneySound, 1f, 1f);
            }

            RefreshMenu(false);
        }
    }

    private void HandleStartClicked()
    {
        HomeCharacterConfig config = GetCurrentCharacterConfig();
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

        board.SetPlayerCharacterSetup(config.Prefab, config.Data, GetSelectedTourmentLevel(config.Data));
        board.ResetArenaProgression();
        board.GenerateBoard();
    }

    private void CycleCharacter(int delta)
    {
        int selectionCount = Enum.GetValues(typeof(HomeCharacterSelection)).Length;
        currentSelection = (HomeCharacterSelection)(((int)currentSelection + delta + selectionCount) % selectionCount);
        RefreshMenu(false);
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

    private int GetSelectedTourmentLevel(CharacterData characterData)
    {
        if (characterData == null || string.IsNullOrWhiteSpace(characterData.CharacterId))
        {
            return 1;
        }

        return CharacterProgressionSaveManager.GetSelectedTourment(characterData.CharacterId);
    }

    private void OffsetSelectedTourment(int delta)
    {
        CharacterData characterData = GetCurrentCharacterData();
        if (characterData == null || string.IsNullOrWhiteSpace(characterData.CharacterId) || delta == 0)
        {
            return;
        }

        int currentTourment = GetSelectedTourmentLevel(characterData);
        int maxUnlockedTourment = CharacterProgressionSaveManager.GetMaxUnlockedTourment(characterData.CharacterId);
        int targetTourment = Mathf.Clamp(currentTourment + delta, 1, maxUnlockedTourment);
        if (targetTourment == currentTourment)
        {
            return;
        }

        CharacterProgressionSaveManager.SetSelectedTourment(characterData.CharacterId, targetTourment);
        RefreshMenu(false);
    }

    private void RefreshMenu(bool immediate)
    {
        CacheReferences();
        RefreshMenuVisibility();

        CharacterData characterData = GetCurrentCharacterData();
        HomeCharacterConfig config = GetCurrentCharacterConfig();
        if (characterData == null)
        {
            return;
        }

        RefreshMainMenu(characterData, config);
        RefreshUpgradeMenu(characterData, immediate);
        UpdateOrbUi(characterData);
        ApplyTheme(characterData, immediate);
        RefreshTabs(characterData, immediate);
    }

    private void RefreshMenuVisibility()
    {
        if (menuMainRoot != null)
        {
            menuMainRoot.SetActive(currentMenuSection == HomeMenuSection.Main);
        }

        if (menuUpgradeRoot != null)
        {
            menuUpgradeRoot.SetActive(currentMenuSection == HomeMenuSection.Upgrade);
        }
    }

    private void RefreshMainMenu(CharacterData characterData, HomeCharacterConfig config)
    {
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

        if (storyDescriptionText != null)
        {
            storyDescriptionText.text = characterData.Description ?? string.Empty;
        }

        RefreshDifficultyUi(characterData);
        menuMain?.BindPersistentStats(characterData);
    }

    private void RefreshDifficultyUi(CharacterData characterData)
    {
        if (difficultyRoot != null)
        {
            difficultyRoot.SetActive(characterData != null);
        }

        if (characterData == null)
        {
            return;
        }

        int selectedTourmentLevel = GetSelectedTourmentLevel(characterData);
        int maxUnlockedTourmentLevel = CharacterProgressionSaveManager.GetMaxUnlockedTourment(characterData.CharacterId);
        TourmentData selectedTourment = board != null ? board.GetTourmentData(selectedTourmentLevel) : null;

        if (difficultyText != null)
        {
            difficultyText.text = selectedTourment != null
                ? selectedTourment.DisplayName
                : $"TORMENT {selectedTourmentLevel}";
        }

        if (difficultyIconImage != null)
        {
            difficultyIconImage.sprite = selectedTourment != null ? selectedTourment.Icon : null;
            difficultyIconImage.enabled = difficultyIconImage.sprite != null;
        }

        if (difficultyPreviousButton != null)
        {
            bool canGoPrevious = selectedTourmentLevel > 1;
            difficultyPreviousButton.gameObject.SetActive(canGoPrevious);
            difficultyPreviousButton.interactable = canGoPrevious;
        }

        if (difficultyNextButton != null)
        {
            bool canShowNext = selectedTourmentLevel < 5;
            difficultyNextButton.gameObject.SetActive(canShowNext);
            difficultyNextButton.interactable = canShowNext && selectedTourmentLevel < maxUnlockedTourmentLevel;
        }
    }

    private void RefreshUpgradeMenu(CharacterData characterData, bool immediate)
    {
        if (upgradeCharacterNameText != null)
        {
            upgradeCharacterNameText.text = characterData.CharacterName;
        }

        if (upgradeCharacterIconImage != null)
        {
            upgradeCharacterIconImage.sprite = characterData.PathIcon;
            upgradeCharacterIconImage.enabled = characterData.PathIcon != null;
        }

        if (currentMenuSection == HomeMenuSection.Upgrade
            && (selectedUpgradeIndex < 0 || selectedUpgradeIndex >= characterData.PersistentUpgrades.Count))
        {
            selectedUpgradeIndex = FindFirstNonMaxedUpgradeIndex(characterData);
        }

        RefreshUpgradeButtons();
        RefreshUpgradeFrame();
    }

    private void RefreshUpgradeButtons()
    {
        CharacterData characterData = GetCurrentCharacterData();
        if (characterData == null)
        {
            return;
        }

        IReadOnlyList<CharacterUpgradeData> upgrades = characterData.PersistentUpgrades;
        Dictionary<Image, bool> arrowActivationStates = new Dictionary<Image, bool>();
        for (int index = 0; index < upgradeButtons.Count; index++)
        {
            UpgradeButtons binding = upgradeButtons[index];
            CharacterUpgradeData upgradeData = index < upgrades.Count ? upgrades[index] : null;
            int unlockCount = upgradeData != null
                ? CharacterProgressionSaveManager.GetUpgradeUnlockCount(characterData.CharacterId, upgradeData.UpgradeId)
                : 0;
            bool isUnlockable = IsUpgradeUnlockedForPurchase(index, characterData, upgradeData);
            bool isSelected = selectedUpgradeIndex == index;

            if (binding?.IconImage != null)
            {
                if (upgradeData != null && upgradeData.Icon != null)
                {
                    binding.IconImage.sprite = upgradeData.Icon;
                }

                binding.IconImage.enabled = binding.IconImage.sprite != null;
                binding.IconImage.color = isUnlockable ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            }

            if (binding?.CountText != null)
            {
                int maxCount = upgradeData != null ? upgradeData.MaxUnlockCount : 0;
                binding.CountText.text = upgradeData != null ? $"{unlockCount}/{maxCount}" : string.Empty;
            }

            if (binding?.BackgroundImage != null)
            {
                binding.BackgroundImage.color = upgradeData == null
                    ? LockedUpgradeColor
                    : (isUnlockable ? upgradeData.Color : LockedUpgradeColor);
            }

            if (binding?.SelectorImage != null)
            {
                binding.SelectorImage.gameObject.SetActive(isSelected);
            }

            if (binding?.Button != null)
            {
                binding.Button.interactable = upgradeData != null;
            }

            if (binding?.ArrowImages != null)
            {
                for (int arrowIndex = 0; arrowIndex < binding.ArrowImages.Count; arrowIndex++)
                {
                    Image arrowImage = binding.ArrowImages[arrowIndex];
                    if (arrowImage == null)
                    {
                        continue;
                    }

                    if (arrowActivationStates.TryGetValue(arrowImage, out bool wasActive))
                    {
                        arrowActivationStates[arrowImage] = wasActive || isUnlockable;
                    }
                    else
                    {
                        arrowActivationStates.Add(arrowImage, isUnlockable);
                    }
                }
            }
        }

        foreach (KeyValuePair<Image, bool> pair in arrowActivationStates)
        {
            if (pair.Key == null)
            {
                continue;
            }

            Color arrowColor = pair.Key.color;
            arrowColor.a = pair.Value ? 1f : 0.075f;
            pair.Key.color = arrowColor;
        }
    }

    private void RefreshUpgradeFrame()
    {
        CharacterData characterData = GetCurrentCharacterData();
        CharacterUpgradeData upgradeData = GetSelectedUpgradeData();
        if (upgradeFrameRoot == null)
        {
            return;
        }

        if (selectedUpgradeIndex < 0 || characterData == null || upgradeData == null)
        {
            upgradeFrameRoot.SetActive(false);
            return;
        }

        upgradeFrameRoot.SetActive(true);

        if (upgradeFrameIconImage != null)
        {
            Sprite targetIcon = upgradeData.Icon;
            if (targetIcon == null && selectedUpgradeIndex >= 0 && selectedUpgradeIndex < upgradeButtons.Count)
            {
                targetIcon = upgradeButtons[selectedUpgradeIndex]?.IconImage != null
                    ? upgradeButtons[selectedUpgradeIndex].IconImage.sprite
                    : null;
            }

            upgradeFrameIconImage.sprite = targetIcon;
            upgradeFrameIconImage.enabled = targetIcon != null;
            upgradeFrameIconImage.color = upgradeData.Color;
        }

        if (upgradeFrameTitleText != null)
        {
            upgradeFrameTitleText.text = upgradeData.UpgradeName;
        }

        if (upgradeFrameDescriptionText != null)
        {
            upgradeFrameDescriptionText.text = upgradeData.Description ?? string.Empty;
        }

        if (upgradeUnlockOrbImage != null)
        {
            upgradeUnlockOrbImage.sprite = characterData.OrbIcon;
            upgradeUnlockOrbImage.enabled = characterData.OrbIcon != null;
        }

        bool hasEnoughOrbs = CharacterProgressionSaveManager.GetOrbCount(characterData.CharacterId) >= upgradeData.OrbPrice;
        bool canPurchase = CanPurchaseUpgrade(selectedUpgradeIndex, characterData, upgradeData);
        bool shouldShowUnlock = CanShowUnlockButton(selectedUpgradeIndex, characterData, upgradeData);

        if (upgradeUnlockPriceText != null)
        {
            upgradeUnlockPriceText.text = upgradeData.OrbPrice.ToString();
            upgradeUnlockPriceText.color = hasEnoughOrbs ? PriceNormalColor : PriceInsufficientColor;
        }

        if (upgradeUnlockButton != null)
        {
            upgradeUnlockButton.gameObject.SetActive(shouldShowUnlock);
            upgradeUnlockButton.interactable = canPurchase;
        }
    }

    private void CloseUpgradeFrame()
    {
        selectedUpgradeIndex = -1;
        if (upgradeFrameRoot != null)
        {
            upgradeFrameRoot.SetActive(false);
        }

        RefreshUpgradeButtons();
    }

    private void UpdateOrbUi(CharacterData characterData)
    {
        if (characterData == null)
        {
            return;
        }

        if (orbIconImage != null)
        {
            orbIconImage.sprite = characterData.OrbIcon;
            orbIconImage.enabled = characterData.OrbIcon != null;
        }

        if (orbCountText != null)
        {
            orbCountText.text = CharacterProgressionSaveManager.GetOrbCount(characterData.CharacterId).ToString();
        }
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
        TweenImageColor(upgradeCharacterTitleImage, element35Color, duration);
        TweenImagesColor(element35Images, element35Color, duration);
        TweenImagesColor(element41Images, element41Color, duration);
        TweenTextColor(characterNameText, titleTextColor, duration);
        TweenTextColor(upgradeCharacterNameText, titleTextColor, duration);
        TweenTextColor(storyDescriptionText, descriptionTextColor, duration);
        TweenTextColor(upgradeFrameDescriptionText, descriptionTextColor, duration);
        TweenTextColor(upgradeFrameTitleText, titleTextColor, duration);
    }

    private void RefreshTabs(CharacterData characterData, bool immediate)
    {
        Color inactiveColor = characterData.GetUIColor(CharacterUIColorKey.PortraitNameplateBackground, new Color(0.30588236f, 0.08235294f, 0.47058824f, 1f));
        Color activeColor = characterData.GetUIColor(CharacterUIColorKey.AbilityButtonTypeIcon, new Color(0.44313726f, 0f, 0.62352943f, 1f));
        float duration = immediate ? 0f : themeTweenDuration;

        TweenImageColor(upgradeTabImage, currentMenuSection == HomeMenuSection.Upgrade ? activeColor : inactiveColor, duration);
        TweenImageColor(mainTabImage, currentMenuSection == HomeMenuSection.Main ? activeColor : inactiveColor, duration);
        TweenImageColor(thirdTabImage, inactiveColor, duration);
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

    private HomeCharacterConfig GetCurrentCharacterConfig()
    {
        return currentSelection switch
        {
            HomeCharacterSelection.Hector => new HomeCharacterConfig(hectorCharacterData, hectorCharacterPrefab, false),
            HomeCharacterSelection.Zhuang => new HomeCharacterConfig(zhuangCharacterData, null, true),
            _ => new HomeCharacterConfig(pandoraCharacterData, pandoraCharacterPrefab, false)
        };
    }

    private CharacterData GetCurrentCharacterData()
    {
        return GetCurrentCharacterConfig().Data;
    }

    private CharacterUpgradeData GetSelectedUpgradeData()
    {
        CharacterData characterData = GetCurrentCharacterData();
        if (characterData == null || selectedUpgradeIndex < 0 || selectedUpgradeIndex >= characterData.PersistentUpgrades.Count)
        {
            return null;
        }

        return characterData.PersistentUpgrades[selectedUpgradeIndex];
    }

    private bool CanShowUnlockButton(int upgradeIndex, CharacterData characterData, CharacterUpgradeData upgradeData)
    {
        return IsUpgradeUnlockedForPurchase(upgradeIndex, characterData, upgradeData)
            && !IsUpgradeMaxed(characterData, upgradeData);
    }

    private bool CanPurchaseUpgrade(int upgradeIndex, CharacterData characterData, CharacterUpgradeData upgradeData)
    {
        return CanShowUnlockButton(upgradeIndex, characterData, upgradeData)
            && CharacterProgressionSaveManager.GetOrbCount(characterData.CharacterId) >= upgradeData.OrbPrice;
    }

    private bool IsUpgradeUnlockedForPurchase(int upgradeIndex, CharacterData characterData, CharacterUpgradeData upgradeData)
    {
        if (characterData == null || upgradeData == null || upgradeIndex < 0 || upgradeIndex >= UpgradePrerequisiteIndices.Length)
        {
            return false;
        }

        int[] prerequisiteIndices = UpgradePrerequisiteIndices[upgradeIndex];
        if (prerequisiteIndices == null || prerequisiteIndices.Length == 0)
        {
            return true;
        }

        for (int index = 0; index < prerequisiteIndices.Length; index++)
        {
            int prerequisiteUpgradeIndex = prerequisiteIndices[index] - 1;
            if (prerequisiteUpgradeIndex < 0 || prerequisiteUpgradeIndex >= characterData.PersistentUpgrades.Count)
            {
                continue;
            }

            CharacterUpgradeData prerequisiteUpgrade = characterData.PersistentUpgrades[prerequisiteUpgradeIndex];
            if (prerequisiteUpgrade == null)
            {
                continue;
            }

            int unlockCount = CharacterProgressionSaveManager.GetUpgradeUnlockCount(characterData.CharacterId, prerequisiteUpgrade.UpgradeId);
            if (unlockCount >= prerequisiteUpgrade.MaxUnlockCount)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUpgradeMaxed(CharacterData characterData, CharacterUpgradeData upgradeData)
    {
        if (characterData == null || upgradeData == null)
        {
            return true;
        }

        return CharacterProgressionSaveManager.GetUpgradeUnlockCount(characterData.CharacterId, upgradeData.UpgradeId) >= upgradeData.MaxUnlockCount;
    }

    private static int FindFirstNonMaxedUpgradeIndex(CharacterData characterData)
    {
        if (characterData == null)
        {
            return -1;
        }

        for (int index = 0; index < characterData.PersistentUpgrades.Count; index++)
        {
            CharacterUpgradeData upgradeData = characterData.PersistentUpgrades[index];
            if (!IsUpgradeMaxed(characterData, upgradeData))
            {
                return index;
            }
        }

        return -1;
    }

    private static int CompareUpgradeButtons(UpgradeButtons left, UpgradeButtons right)
    {
        RectTransform leftRect = left != null ? left.RectTransform : null;
        RectTransform rightRect = right != null ? right.RectTransform : null;
        if (leftRect == null || rightRect == null)
        {
            return 0;
        }

        const float rowTolerance = 8f;
        float yDifference = leftRect.anchoredPosition.y - rightRect.anchoredPosition.y;
        if (Mathf.Abs(yDifference) > rowTolerance)
        {
            return yDifference < 0f ? -1 : 1;
        }

        return leftRect.anchoredPosition.x.CompareTo(rightRect.anchoredPosition.x);
    }

    private List<Image> GetSortedUpgradeArrows()
    {
        List<Image> arrows = new List<Image>();
        if (upgradeArrowsRoot == null)
        {
            return arrows;
        }

        Image[] allArrows = upgradeArrowsRoot.GetComponentsInChildren<Image>(true);
        for (int index = 0; index < allArrows.Length; index++)
        {
            Image image = allArrows[index];
            if (image != null && image.name.StartsWith("iArrow", StringComparison.Ordinal))
            {
                arrows.Add(image);
            }
        }

        arrows.Sort((left, right) =>
        {
            RectTransform leftRect = left != null ? left.transform as RectTransform : null;
            RectTransform rightRect = right != null ? right.transform as RectTransform : null;
            if (leftRect == null || rightRect == null)
            {
                return 0;
            }

            const float rowTolerance = 8f;
            float yDifference = leftRect.anchoredPosition.y - rightRect.anchoredPosition.y;
            if (Mathf.Abs(yDifference) > rowTolerance)
            {
                return yDifference < 0f ? -1 : 1;
            }

            return leftRect.anchoredPosition.x.CompareTo(rightRect.anchoredPosition.x);
        });

        return arrows;
    }

    private static int[] GetArrowIndicesForUpgrade(int upgradeIndex)
    {
        return upgradeIndex switch
        {
            2 => new[] { 0 },
            3 => new[] { 1 },
            4 => new[] { 2 },
            5 => new[] { 3 },
            6 => new[] { 4 },
            7 => new[] { 5 },
            8 => new[] { 6 },
            9 => new[] { 6 },
            10 => new[] { 7, 8, 9 },
            11 => new[] { 7, 8, 9 },
            12 => new[] { 7, 8, 9 },
            _ => Array.Empty<int>()
        };
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

    private static void BindButton(Button button, Action callback, bool shouldBind = true)
    {
        if (button == null || callback == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        if (shouldBind)
        {
            button.onClick.AddListener(() => callback());
        }
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

    private Button FindButtonInParent(string objectName)
    {
        if (!string.IsNullOrWhiteSpace(objectName))
        {
            Button button = FindComponentByName<Button>(transform, objectName);
            if (button != null)
            {
                return button;
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

        if (root.name == objectName)
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

    private static T FindDescendantByName<T>(Transform root, string objectName) where T : Component
    {
        return FindComponentByName<T>(root, objectName);
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
