using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using UnityEngine.EventSystems;

public class UIGame : MonoBehaviour
{
    [SerializeField] private GameTurnManager gameTurnManager;
    [SerializeField] private FooterUI footerUI;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TMP_Text turnLabel;
    [SerializeField] private RectTransform mobilityBar;
    [SerializeField] private GameObject mobilityIconPrefab;
    [SerializeField] private RectTransform abilitiesBar;
    [SerializeField] private RectTransform itemsList;
    [SerializeField] private GameObject itemIconPrefab;
    [SerializeField] private Color mobilityAvailableColor = Color.white;
    [SerializeField] private Color mobilityConsumedColor = Color.black;
    [SerializeField] private Sprite wolfMobilitySprite;
    [SerializeField] private Color wolfMobilityColor = new Color(1f, 0.7294118f, 0.003921569f, 1f);
    [SerializeField] private GameObject rewardsMenu;
    [SerializeField] private Button ignoreRewardsButton;
    [SerializeField] private GameObject targetCellIndicatorPrefab;
    [SerializeField] private GameObject targetableOnlyCellIndicatorPrefab;
    [SerializeField] private UnitStatsMenuUI enemyStatsMenu;
    [SerializeField] private UnitStatsMenuUI characterStatsMenu;
    [SerializeField] private GameObject rewardCheckMenu;
    [SerializeField] private Button rewardCheckBackgroundButton;
    [SerializeField] private Button rewardCheckChooseButton;
    [SerializeField] private RewardButtonUI rewardCheckCard;
    [SerializeField] private GameObject yesNoMenu;
    [SerializeField] private Image yesNoArtworkImage;
    [SerializeField] private TMP_Text yesNoTitleText;
    [SerializeField] private TMP_Text yesNoDescriptionText;
    [SerializeField] private TMP_Text yesNoQuestionText;
    [SerializeField] private Button yesNoButton;
    [SerializeField] private Button yesNoYesButton;
    [SerializeField] private Button yesNoNoButton;
    [SerializeField] private GameObject loseMenu;
    [SerializeField] private Image loseCharacterPortraitImage;
    [SerializeField] private TMP_Text loseCharacterNameText;
    [SerializeField] private Button retryButton;
    [SerializeField] private GameObject abilityCheckMenu;
    [SerializeField] private Button abilityCheckBackgroundButton;
    [SerializeField] private RewardButtonUI abilityCheckCard;
    [SerializeField] private List<Button> abilityCheckOptionButtons = new List<Button>();
    [SerializeField] private List<GameObject> abilityCheckOptionSelectors = new List<GameObject>();
    [SerializeField] private GameObject switchAbilityMenu;
    [SerializeField] private TMP_Text switchAbilityInfoText;
    [SerializeField] private Image switchAbilityNewIcon;
    [SerializeField] private Image switchAbilityOldIcon;
    [SerializeField] private Button switchAbilityConfirmButton;
    [SerializeField] private Button switchAbilityCancelButton;
    [SerializeField] private Button toolsButton;
    [SerializeField] private GameObject toolsMenu;
    [SerializeField] private Button toolsMenuBackdropButton;
    [SerializeField] private TMP_InputField toolsBuildNameInput;
    [SerializeField] private TMP_Text toolsSelectedBuildLabel;
    [SerializeField] private TMP_Text toolsBuildCountLabel;
    [SerializeField] private TMP_Text toolsBuildDetailsLabel;
    [SerializeField] private TMP_Text toolsStatusLabel;
    [SerializeField] private Button toolsPreviousBuildButton;
    [SerializeField] private Button toolsNextBuildButton;
    [SerializeField] private Button toolsSaveBuildButton;
    [SerializeField] private Button toolsLoadBuildButton;
    [SerializeField] private Button toolsDeleteBuildButton;
    [SerializeField] private Button toolsCloseButton;
    [SerializeField] private float statsMenuClickThreshold = 16f;
    [SerializeField] private bool disableAbilityButtonsWithoutValidTargets = false;

    private readonly List<GameObject> mobilityIcons = new List<GameObject>();
    private readonly List<bool> wolfMobilityIcons = new List<bool>();
    private readonly List<GameObject> itemIcons = new List<GameObject>();
    private readonly Dictionary<ItemRewardKey, ItemIconUI> itemIconsByKey = new Dictionary<ItemRewardKey, ItemIconUI>();
    private readonly List<AbilityButtonUI> abilityButtons = new List<AbilityButtonUI>();
    private readonly List<RewardButtonUI> rewardCards = new List<RewardButtonUI>();
    private readonly List<GameObject> targetCellIndicators = new List<GameObject>();
    private readonly List<GameObject> targetableOnlyCellIndicators = new List<GameObject>();
    private Character observedCharacter;
    private Action<RewardOffer> onRewardSelected;
    private Action onRewardsIgnored;
    private RewardButtonStyle basePowerRewardStyle;
    private RewardButtonStyle baseItemRewardStyle;
    private RewardButtonStyle powerRewardStyle;
    private RewardButtonStyle itemRewardStyle;
    private RewardButtonTheme powerRewardTheme = RewardButtonUI.DefaultPowerTheme;
    private RewardButtonTheme itemRewardTheme = RewardButtonUI.DefaultItemTheme;
    private ItemIconTheme itemIconTheme = ItemIconUI.DefaultTheme;
    private Sprite basicAttackRewardIcon;
    private Sprite mobilityRewardIcon;
    private Sprite specialRewardIcon;
    private Sprite objectRewardIcon;
    private bool isStatsPointerTracking;
    private Vector2 statsPointerStartPosition;
    private RewardOffer pendingRewardConfirmation;
    private ItemRewardDefinition currentPreviewedItemDefinition;
    private AbilityDefinition pendingAbilityReplacementOldAbility;
    private AbilityButtonUI currentAbilityCheckSourceButton;
    private readonly List<RewardOffer> currentAbilityCheckOffers = new List<RewardOffer>();
    private Action onYesNoAccepted;
    private Action onYesNoDeclined;
    private Action onRetryRequested;
    private float targetableOnlyCellIndicatorDuration = 0.75f;
    private Coroutine clearTargetableOnlyIndicatorsCoroutine;
    private readonly List<EquipmentBuildData> availableEquipmentBuilds = new List<EquipmentBuildData>();
    private int selectedEquipmentBuildIndex = -1;
    private float targetableOnlyCellIndicatorObstacleHeightOffset = 0.7f;

    private void Awake()
    {
        if (gameTurnManager == null)
        {
            gameTurnManager = FindFirstObjectByType<GameTurnManager>();
        }

        if (endTurnButton == null)
        {
            Transform buttonTransform = transform.Find("BEndTurn");
            if (buttonTransform != null)
            {
                endTurnButton = buttonTransform.GetComponent<Button>();
            }
        }

        if (turnLabel == null && endTurnButton != null)
        {
            turnLabel = endTurnButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (mobilityBar == null)
        {
            Transform mobilityBarTransform = transform.Find("MobilityBar");
            if (mobilityBarTransform != null)
            {
                mobilityBar = mobilityBarTransform as RectTransform;
            }
        }

        if (footerUI == null)
        {
            Transform footerTransform = transform.Find("Footer");
            if (footerTransform != null)
            {
                footerUI = footerTransform.GetComponent<FooterUI>();
                if (footerUI == null)
                {
                    footerUI = footerTransform.gameObject.AddComponent<FooterUI>();
                }
            }
        }

        if (abilitiesBar == null)
        {
            if (footerUI != null && footerUI.AbilitiesBar != null)
            {
                abilitiesBar = footerUI.AbilitiesBar;
            }
            else
            {
                Transform abilitiesTransform = transform.Find("Footer/Abilities");
                if (abilitiesTransform == null)
                {
                    abilitiesTransform = transform.Find("Abilities");
                }

                if (abilitiesTransform != null)
                {
                    abilitiesBar = abilitiesTransform as RectTransform;
                }
            }
        }

        if (itemsList == null)
        {
            if (footerUI != null && footerUI.ItemsList != null)
            {
                itemsList = footerUI.ItemsList;
            }
            else
            {
                Transform itemsListTransform = transform.Find("Footer/ItemsList");
                if (itemsListTransform == null)
                {
                    itemsListTransform = transform.Find("ItemsList");
                }

                if (itemsListTransform != null)
                {
                    itemsList = itemsListTransform as RectTransform;
                }
            }
        }

        if (itemIconPrefab == null && itemsList != null)
        {
            Transform itemIconTransform = itemsList.Find("ItemIcon");
            if (itemIconTransform != null)
            {
                itemIconPrefab = itemIconTransform.gameObject;
                itemIconPrefab.SetActive(false);
            }
        }

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(HandleEndTurnClicked);
            endTurnButton.onClick.AddListener(HandleEndTurnClicked);
        }

        CacheAbilityButtons();
        CacheRewardsMenu();
        CacheStatsMenus();
        CacheRewardCheckMenu();
        CacheYesNoMenu();
        CacheLoseMenu();
        CacheAbilityCheckMenu();
        CacheSwitchAbilityMenu();
        EnsureToolsUI();
        if (rewardsMenu != null)
        {
            rewardsMenu.SetActive(false);
        }

        HideRewardCheck();
        HideYesNoPrompt();
        HideLoseMenu();
        HideAbilityCheck();
        HideSwitchAbilityMenu();
        HideToolsMenu();
    }

    private void OnEnable()
    {
        if (gameTurnManager != null)
        {
            gameTurnManager.TurnChanged += HandleTurnChanged;
            gameTurnManager.PendingAbilityChanged += HandlePendingAbilityChanged;
            gameTurnManager.EndTurnAvailabilityChanged += HandleEndTurnAvailabilityChanged;
            gameTurnManager.NoValidTargetFeedbackRequested += HandleNoValidTargetFeedbackRequested;
            HandleTurnChanged(gameTurnManager.CurrentTurn);
        }

        BindToCurrentCharacter();
    }

    private void OnDisable()
    {
        if (gameTurnManager != null)
        {
            gameTurnManager.TurnChanged -= HandleTurnChanged;
            gameTurnManager.PendingAbilityChanged -= HandlePendingAbilityChanged;
            gameTurnManager.EndTurnAvailabilityChanged -= HandleEndTurnAvailabilityChanged;
            gameTurnManager.NoValidTargetFeedbackRequested -= HandleNoValidTargetFeedbackRequested;
        }

        ClearTargetCellIndicators();
        ClearTargetableOnlyCellIndicators();
        HideToolsMenu();
        UnbindCharacter();
    }

    private void Update()
    {
        HandleStatsMenuInput();
    }

    private void HandleEndTurnClicked()
    {
        SoundManager.Instance?.PlayClick();
        gameTurnManager?.RequestEndTurn();
    }

    public void ShowRewards(IReadOnlyList<RewardOffer> rewardOffers, Action<RewardOffer> rewardSelectedCallback, Action rewardsIgnoredCallback)
    {
        CacheRewardsMenu();
        CacheRewardTypeIcons();
        HideStatsMenus();
        HideRewardCheck();
        HideAbilityCheck();
        HideSwitchAbilityMenu();

        onRewardSelected = rewardSelectedCallback;
        onRewardsIgnored = rewardsIgnoredCallback;

        if (rewardsMenu == null)
        {
            rewardSelectedCallback?.Invoke(null);
            return;
        }

        rewardsMenu.SetActive(true);
        ClearTargetCellIndicators();
        UpdateEndTurnButtonVisibility();
        for (int index = 0; index < rewardCards.Count; index++)
        {
            RewardOffer rewardOffer = rewardOffers != null && index < rewardOffers.Count ? rewardOffers[index] : null;
            RewardButtonStyle style = rewardOffer != null && rewardOffer.Kind == RewardOfferKind.Item ? itemRewardStyle : powerRewardStyle;
            RewardButtonTheme theme = rewardOffer != null && rewardOffer.Kind == RewardOfferKind.Item ? itemRewardTheme : powerRewardTheme;
            Sprite typeSprite = rewardOffer != null ? ResolveTypeIconSprite(rewardOffer.IconKind) : null;
            rewardCards[index].Bind(rewardOffer, style, theme, typeSprite, HandleRewardPreviewRequested);
        }
    }

    public void HideRewards()
    {
        if (rewardsMenu != null)
        {
            rewardsMenu.SetActive(false);
        }

        onRewardSelected = null;
        onRewardsIgnored = null;
        pendingRewardConfirmation = null;
        HideRewardCheck();
        HideYesNoPrompt();
        HideAbilityCheck();
        HideSwitchAbilityMenu();
        UpdateEndTurnButtonVisibility();
        RefreshTargetCellIndicators();
    }

    public void HideStatsMenus()
    {
        enemyStatsMenu?.Hide();
        characterStatsMenu?.Hide();
    }

    public void ShowLoseMenu(string characterName, Sprite losePortrait, Action retryCallback)
    {
        CacheLoseMenu();
        HideStatsMenus();
        HideRewardCheck();
        HideYesNoPrompt();
        HideAbilityCheck();
        HideSwitchAbilityMenu();

        if (loseMenu == null)
        {
            return;
        }

        onRetryRequested = retryCallback;
        if (loseCharacterPortraitImage != null)
        {
            loseCharacterPortraitImage.sprite = losePortrait;
            loseCharacterPortraitImage.enabled = losePortrait != null;
        }

        if (loseCharacterNameText != null)
        {
            loseCharacterNameText.text = characterName ?? string.Empty;
        }

        loseMenu.SetActive(true);
        UpdateEndTurnButtonVisibility();
    }

    public void HideLoseMenu()
    {
        onRetryRequested = null;
        if (loseMenu != null)
        {
            loseMenu.SetActive(false);
        }

        UpdateEndTurnButtonVisibility();
    }

    public void ShowYesNoPrompt(ItemRewardDefinition itemRewardDefinition, Action acceptedCallback, Action declinedCallback)
    {
        CacheYesNoMenu();
        HideStatsMenus();
        HideRewardCheck();
        HideAbilityCheck();
        HideSwitchAbilityMenu();

        if (yesNoMenu == null || itemRewardDefinition == null)
        {
            declinedCallback?.Invoke();
            return;
        }

        onYesNoAccepted = acceptedCallback;
        onYesNoDeclined = declinedCallback;

        if (yesNoArtworkImage != null)
        {
            yesNoArtworkImage.sprite = itemRewardDefinition.Artwork;
            yesNoArtworkImage.enabled = itemRewardDefinition.Artwork != null;
        }

        if (yesNoTitleText != null)
        {
            yesNoTitleText.text = itemRewardDefinition.RewardTitle;
        }

        if (yesNoDescriptionText != null)
        {
            yesNoDescriptionText.text = itemRewardDefinition.RewardDescription;
        }

        if (yesNoQuestionText != null)
        {
            yesNoQuestionText.text = itemRewardDefinition.GetActivationQuestion();
        }

        yesNoMenu.SetActive(true);
        UpdateEndTurnButtonVisibility();
    }

    public void HideYesNoPrompt()
    {
        onYesNoAccepted = null;
        onYesNoDeclined = null;
        if (yesNoMenu != null)
        {
            yesNoMenu.SetActive(false);
        }

        UpdateEndTurnButtonVisibility();
    }

    private void HandleTurnChanged(TurnSide turnSide)
    {
        HideStatsMenus();
        HideAbilityCheck();

        if (endTurnButton != null)
        {
            endTurnButton.interactable = turnSide == TurnSide.Player && gameTurnManager != null && gameTurnManager.CanEndTurn;
            UpdateEndTurnButtonVisibility();
        }

        if (turnLabel != null)
        {
            turnLabel.text = turnSide == TurnSide.Player ? "END TURN" : "ENEMY TURN";
        }

        BindToCurrentCharacter();
        RefreshMobilityBar();
        RefreshAbilityButtons();
        RefreshItemsList();
        RefreshFooterCharacterInfo();
        RefreshTargetCellIndicators();
    }

    private void BindToCurrentCharacter()
    {
        Character currentCharacter = null;
        if (gameTurnManager != null)
        {
            BoardManager board = gameTurnManager.Board;
            if (board != null && board.Player != null)
            {
                currentCharacter = board.Player.ControlledCharacter;
            }
        }

        if (observedCharacter == currentCharacter)
        {
            return;
        }

        UnbindCharacter();
        observedCharacter = currentCharacter;
        HideAbilityCheck();

        if (observedCharacter != null)
        {
            observedCharacter.MovementPointsChanged += HandleMovementPointsChanged;
            observedCharacter.AbilitiesChanged += HandleAbilitiesChanged;
            observedCharacter.ItemActivationChanged += HandleItemActivationChanged;
        }

        ApplyCurrentCharacterTheme();
        RebuildMobilityBar();
        RefreshAbilityButtons();
        RefreshItemsList();
        RefreshFooterCharacterInfo();
        RefreshTargetCellIndicators();
        RefreshAvailableEquipmentBuilds();
    }

    private void UnbindCharacter()
    {
        if (observedCharacter != null)
        {
            observedCharacter.MovementPointsChanged -= HandleMovementPointsChanged;
            observedCharacter.AbilitiesChanged -= HandleAbilitiesChanged;
            observedCharacter.ItemActivationChanged -= HandleItemActivationChanged;
            observedCharacter = null;
        }

        ApplyCurrentCharacterTheme();
        ClearTargetCellIndicators();
    }

    private void HandleMovementPointsChanged(Character character)
    {
        RefreshMobilityBar();
        RefreshAbilityButtons();
        RefreshTargetCellIndicators();
    }

    private void HandleAbilitiesChanged(Character character)
    {
        HideAbilityCheck();
        RefreshAbilityButtons();
        RefreshItemsList();
        RefreshFooterCharacterInfo();
        RefreshTargetCellIndicators();
    }

    private void HandlePendingAbilityChanged(int abilityIndex)
    {
        RefreshAbilityButtons();
        RefreshTargetCellIndicators();
        if (abilityIndex >= 0)
        {
            ClearTargetableOnlyCellIndicators();
        }
    }

    private void HandleEndTurnAvailabilityChanged(bool isAvailable)
    {
        if (endTurnButton != null && gameTurnManager != null)
        {
            endTurnButton.interactable = gameTurnManager.CurrentTurn == TurnSide.Player && isAvailable;
            UpdateEndTurnButtonVisibility();
        }
    }

    private void HandleIgnoreRewardsClicked()
    {
        SoundManager.Instance?.PlayClick();
        Action ignoredCallback = onRewardsIgnored;
        HideRewards();
        ignoredCallback?.Invoke();
    }

    private void HandleCharacterPortraitClicked()
    {
        ToggleCharacterStatsMenu(true);
    }

    private void HandleCloseEnemyStatsClicked()
    {
        SoundManager.Instance?.PlayClick();
        enemyStatsMenu?.Hide();
    }

    private void HandleCloseCharacterStatsClicked()
    {
        SoundManager.Instance?.PlayClick();
        characterStatsMenu?.Hide();
    }

    private void HandleRewardSelected(RewardOffer rewardOffer)
    {
        Action<RewardOffer> selectedCallback = onRewardSelected;
        HideRewards();
        selectedCallback?.Invoke(rewardOffer);
    }

    private void HandleRewardPreviewRequested(RewardOffer rewardOffer)
    {
        if (rewardOffer == null)
        {
            return;
        }

        currentPreviewedItemDefinition = null;
        pendingRewardConfirmation = rewardOffer;
        ShowRewardCheck(rewardOffer, true);
    }

    private void HandleRewardCheckChooseClicked()
    {
        SoundManager.Instance?.PlayClick();
        if (pendingRewardConfirmation == null)
        {
            HideRewardCheck();
            return;
        }

        if (TryPrepareAbilityReplacementConfirmation(pendingRewardConfirmation))
        {
            HideRewardCheck();
            ShowSwitchAbilityMenu(pendingRewardConfirmation, pendingAbilityReplacementOldAbility);
            return;
        }

        ConfirmPendingRewardSelection();
    }

    private void HandleRewardCheckBackgroundClicked()
    {
        SoundManager.Instance?.PlayClick();
        pendingRewardConfirmation = null;
        HideRewardCheck();
    }

    private void HandleSwitchAbilityConfirmClicked()
    {
        SoundManager.Instance?.PlayClick();
        ConfirmPendingRewardSelection();
    }

    private void HandleSwitchAbilityCancelClicked()
    {
        SoundManager.Instance?.PlayClick();
        pendingRewardConfirmation = null;
        HideSwitchAbilityMenu();
    }

    private void HandleYesNoAcceptedClicked()
    {
        SoundManager.Instance?.PlayClick();
        Action acceptedCallback = onYesNoAccepted;
        HideYesNoPrompt();
        acceptedCallback?.Invoke();
    }

    private void HandleYesNoDeclinedClicked()
    {
        SoundManager.Instance?.PlayClick();
        Action declinedCallback = onYesNoDeclined;
        HideYesNoPrompt();
        declinedCallback?.Invoke();
    }

    private void HandleRetryClicked()
    {
        SoundManager.Instance?.PlayClick();
        Action retryCallback = onRetryRequested;
        HideLoseMenu();
        retryCallback?.Invoke();
    }

    private bool HandleAbilityButtonPrimaryClick(AbilityButtonUI button)
    {
        if (button == null)
        {
            return false;
        }

        ClearTargetableOnlyCellIndicators();

        if (abilityCheckMenu != null && abilityCheckMenu.activeSelf)
        {
            SoundManager.Instance?.PlayClick();
            if (currentAbilityCheckSourceButton == button)
            {
                HideAbilityCheck();
            }
            else
            {
                ShowAbilityCheck(button, 0);
            }

            return true;
        }

        return false;
    }

    private void HandleAbilityButtonLongPress(AbilityButtonUI button)
    {
        if (button == null)
        {
            return;
        }

        SoundManager.Instance?.PlayClick();
        if (abilityCheckMenu != null && abilityCheckMenu.activeSelf && currentAbilityCheckSourceButton == button)
        {
            HideAbilityCheck();
            return;
        }

        ShowAbilityCheck(button, 0);
    }

    private void UpdateEndTurnButtonVisibility()
    {
        if (endTurnButton == null || gameTurnManager == null)
        {
            return;
        }

        bool shouldShow = gameTurnManager.CurrentTurn == TurnSide.Player
            && gameTurnManager.CanEndTurn
            && !gameTurnManager.IsRewardMenuOpen
            && !gameTurnManager.IsArenaTransitionRunning
            && !gameTurnManager.IsLoseMenuOpen;
        if (endTurnButton.gameObject.activeSelf != shouldShow)
        {
            endTurnButton.gameObject.SetActive(shouldShow);
        }
    }

    private void RebuildMobilityBar()
    {
        if (mobilityBar == null)
        {
            return;
        }

        for (int index = mobilityBar.childCount - 1; index >= 0; index--)
        {
            Destroy(mobilityBar.GetChild(index).gameObject);
        }

        mobilityIcons.Clear();
        wolfMobilityIcons.Clear();

        if (observedCharacter == null || mobilityIconPrefab == null)
        {
            return;
        }

        int normalIconCount = observedCharacter.BaseMovementPoints + observedCharacter.ExtraMovementPointsBeyondBase;
        for (int index = 0; index < normalIconCount; index++)
        {
            GameObject icon = Instantiate(mobilityIconPrefab, mobilityBar);
            icon.name = $"iMobility_{index + 1}";
            mobilityIcons.Add(icon);
            wolfMobilityIcons.Add(false);
        }

        for (int index = 0; index < observedCharacter.WolfMovementPoints; index++)
        {
            GameObject icon = Instantiate(mobilityIconPrefab, mobilityBar);
            icon.name = $"iWolfMobility_{index + 1}";
            ConfigureMobilityIcon(icon, true);
            mobilityIcons.Add(icon);
            wolfMobilityIcons.Add(true);
        }

        RefreshMobilityBar();
    }

    private void RefreshMobilityBar()
    {
        if (observedCharacter == null)
        {
            return;
        }

        int desiredIconCount = observedCharacter.BaseMovementPoints
            + observedCharacter.ExtraMovementPointsBeyondBase
            + observedCharacter.WolfMovementPoints;
        if (mobilityIcons.Count != desiredIconCount)
        {
            RebuildMobilityBar();
            return;
        }

        int availableNormalIcons = Mathf.Clamp(
            observedCharacter.RemainingMovementPoints - observedCharacter.WolfMovementPoints,
            0,
            observedCharacter.BaseMovementPoints + observedCharacter.ExtraMovementPointsBeyondBase);
        int availableWolfIcons = Mathf.Max(0, observedCharacter.WolfMovementPoints);
        int normalIndex = 0;
        int wolfIndex = 0;

        for (int index = 0; index < mobilityIcons.Count; index++)
        {
            bool isWolfIcon = index < wolfMobilityIcons.Count && wolfMobilityIcons[index];
            if (isWolfIcon)
            {
                bool isAvailable = wolfIndex < availableWolfIcons;
                SetMobilityIconColor(mobilityIcons[index], isAvailable ? wolfMobilityColor : mobilityConsumedColor);
                wolfIndex++;
            }
            else
            {
                bool isAvailable = normalIndex < availableNormalIcons;
                SetMobilityIconColor(mobilityIcons[index], isAvailable ? mobilityAvailableColor : mobilityConsumedColor);
                normalIndex++;
            }
        }
    }

    private void ConfigureMobilityIcon(GameObject icon, bool isWolfIcon)
    {
        if (icon == null || !isWolfIcon || wolfMobilitySprite == null)
        {
            return;
        }

        Image image = icon.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = wolfMobilitySprite;
            return;
        }

        SpriteRenderer spriteRenderer = icon.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = wolfMobilitySprite;
        }
    }

    private void RefreshTargetCellIndicators()
    {
        ClearTargetCellIndicators();

        if (targetCellIndicatorPrefab == null
            || observedCharacter == null
            || gameTurnManager == null
            || gameTurnManager.Board == null
            || gameTurnManager.CurrentTurn != TurnSide.Player
            || gameTurnManager.IsRewardMenuOpen
            || gameTurnManager.IsArenaTransitionRunning)
        {
            return;
        }

        CharacterAbilityRuntime runtime = GetCurrentTargetIndicatorRuntime();
        if (runtime?.Definition == null)
        {
            return;
        }

        List<Vector2Int> validCells = GetValidTargetCells(runtime);
        for (int index = 0; index < validCells.Count; index++)
        {
            Vector3 spawnPosition = gameTurnManager.Board.GridToWorldPosition(validCells[index]);
            GameObject indicator = Instantiate(targetCellIndicatorPrefab, spawnPosition, targetCellIndicatorPrefab.transform.rotation, gameTurnManager.Board.transform);
            indicator.name = $"{targetCellIndicatorPrefab.name}_{validCells[index].x}_{validCells[index].y}";
            indicator.transform.localScale = targetCellIndicatorPrefab.transform.localScale;
            targetCellIndicators.Add(indicator);
        }
    }

    private CharacterAbilityRuntime GetCurrentTargetIndicatorRuntime()
    {
        int pendingAbilityIndex = gameTurnManager.PendingCellTargetAbilityIndex;
        if (pendingAbilityIndex >= 0)
        {
            return observedCharacter.GetAbility(pendingAbilityIndex);
        }

        for (int index = 0; index < observedCharacter.Abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = observedCharacter.GetAbility(index);
            if (runtime?.Definition != null && runtime.Definition.SupportsCellSelectionWhileActive(observedCharacter, runtime))
            {
                return runtime;
            }
        }

        return null;
    }

    private List<Vector2Int> GetValidTargetCells(CharacterAbilityRuntime runtime)
    {
        List<Vector2Int> validCells = new List<Vector2Int>();
        if (runtime?.Definition == null || gameTurnManager?.Board == null)
        {
            return validCells;
        }

        BoardManager board = gameTurnManager.Board;
        bool isPendingTargetedAbility = gameTurnManager.PendingCellTargetAbilityIndex >= 0;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                bool isValid = isPendingTargetedAbility
                    ? runtime.Definition.CanActivateOnCell(observedCharacter, runtime, cell)
                    : runtime.Definition.CanActivateFromSelectedCell(observedCharacter, runtime, cell);

                if (isValid)
                {
                    validCells.Add(cell);
                }
            }
        }

        return validCells;
    }

    private void ClearTargetCellIndicators()
    {
        for (int index = 0; index < targetCellIndicators.Count; index++)
        {
            if (targetCellIndicators[index] != null)
            {
                Destroy(targetCellIndicators[index]);
            }
        }

        targetCellIndicators.Clear();
    }

    private void ClearTargetableOnlyCellIndicators()
    {
        if (clearTargetableOnlyIndicatorsCoroutine != null)
        {
            StopCoroutine(clearTargetableOnlyIndicatorsCoroutine);
            clearTargetableOnlyIndicatorsCoroutine = null;
        }

        for (int index = 0; index < targetableOnlyCellIndicators.Count; index++)
        {
            if (targetableOnlyCellIndicators[index] != null)
            {
                Destroy(targetableOnlyCellIndicators[index]);
            }
        }

        targetableOnlyCellIndicators.Clear();
    }

    private void CacheAbilityButtons()
    {
        abilityButtons.Clear();
        if (footerUI != null)
        {
            if (footerUI.AbilityButton1 != null)
            {
                abilityButtons.Add(footerUI.AbilityButton1);
            }

            if (footerUI.AbilityButton2 != null)
            {
                abilityButtons.Add(footerUI.AbilityButton2);
            }

            if (footerUI.AbilityButton3 != null)
            {
                abilityButtons.Add(footerUI.AbilityButton3);
            }
        }

        if (abilityButtons.Count == 0 && abilitiesBar != null)
        {
            Button[] buttons = abilitiesBar.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                if (!buttons[index].name.StartsWith("BAbility", StringComparison.Ordinal))
                {
                    continue;
                }

                AbilityButtonUI abilityButton = buttons[index].GetComponent<AbilityButtonUI>();
                if (abilityButton == null)
                {
                    abilityButton = buttons[index].gameObject.AddComponent<AbilityButtonUI>();
                }

                abilityButtons.Add(abilityButton);
            }
        }

        abilityButtons.RemoveAll(button => button == null);
        abilityButtons.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
    }

    private void CacheRewardsMenu()
    {
        if (rewardsMenu == null)
        {
            Transform rewardsMenuTransform = transform.Find("RewardsMenu");
            if (rewardsMenuTransform != null)
            {
                rewardsMenu = rewardsMenuTransform.gameObject;
            }
        }

        if (rewardsMenu == null)
        {
            return;
        }

        if (ignoreRewardsButton == null)
        {
            ignoreRewardsButton = rewardsMenu.transform.Find("BIgnore")?.GetComponent<Button>();
            if (ignoreRewardsButton == null)
            {
                ignoreRewardsButton = rewardsMenu.GetComponentInChildren<Button>(true);
            }
        }

        if (ignoreRewardsButton != null)
        {
            ignoreRewardsButton.onClick.RemoveListener(HandleIgnoreRewardsClicked);
            ignoreRewardsButton.onClick.AddListener(HandleIgnoreRewardsClicked);
        }

        if (rewardCards.Count > 0)
        {
            return;
        }

        Transform rewardsChoices = rewardsMenu.transform.Find("RewardsChoices");
        if (rewardsChoices == null)
        {
            rewardsChoices = rewardsMenu.transform;
        }

        Button[] buttons = rewardsChoices.GetComponentsInChildren<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            if (!buttons[index].name.StartsWith("BReward", StringComparison.Ordinal))
            {
                continue;
            }

            RewardButtonUI rewardButton = buttons[index].GetComponent<RewardButtonUI>();
            if (rewardButton == null)
            {
                rewardButton = buttons[index].gameObject.AddComponent<RewardButtonUI>();
            }

            rewardCards.Add(rewardButton);
        }

        if (rewardCards.Count >= 3)
        {
            RewardButtonUI itemCard = rewardCards.Find(card => card.Button != null && card.Button.name.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0);
            RewardButtonUI powerCard = rewardCards.Find(card => card != itemCard);
            if (powerCard != null)
            {
                basePowerRewardStyle = powerCard.CaptureStyle();
            }

            if (itemCard != null)
            {
                baseItemRewardStyle = itemCard.CaptureStyle();
                objectRewardIcon = itemCard.CurrentTypeSprite;
            }
        }

        ApplyCurrentCharacterTheme();
    }

    private void CacheRewardCheckMenu()
    {
        if (rewardCheckMenu == null)
        {
            Transform rewardCheckTransform = transform.Find("MenuRewardCheck");
            if (rewardCheckTransform != null)
            {
                rewardCheckMenu = rewardCheckTransform.gameObject;
            }
        }

        if (rewardCheckMenu == null)
        {
            return;
        }

        if (rewardCheckBackgroundButton == null)
        {
            rewardCheckBackgroundButton = rewardCheckMenu.GetComponent<Button>();
        }

        if (rewardCheckChooseButton == null)
        {
            rewardCheckChooseButton = rewardCheckMenu.transform.Find("BChoose")?.GetComponent<Button>();
        }

        if (rewardCheckCard == null)
        {
            rewardCheckCard = rewardCheckMenu.GetComponentInChildren<RewardButtonUI>(true);
        }

        if (rewardCheckBackgroundButton != null)
        {
            rewardCheckBackgroundButton.onClick.RemoveListener(HandleRewardCheckBackgroundClicked);
            rewardCheckBackgroundButton.onClick.AddListener(HandleRewardCheckBackgroundClicked);
        }

        if (rewardCheckChooseButton != null)
        {
            rewardCheckChooseButton.onClick.RemoveListener(HandleRewardCheckChooseClicked);
            rewardCheckChooseButton.onClick.AddListener(HandleRewardCheckChooseClicked);
        }
    }

    private void CacheYesNoMenu()
    {
        if (yesNoMenu == null)
        {
            Transform yesNoTransform = transform.Find("MenuYesNo");
            if (yesNoTransform != null)
            {
                yesNoMenu = yesNoTransform.gameObject;
            }
        }

        if (yesNoMenu == null)
        {
            return;
        }

        if (yesNoArtworkImage == null)
        {
            yesNoArtworkImage = FindComponentByName<Image>(yesNoMenu.transform, "iAbility");
        }

        if (yesNoTitleText == null)
        {
            yesNoTitleText = FindComponentByName<TMP_Text>(yesNoMenu.transform, "Chara-Name");
            if (yesNoTitleText == null)
            {
                yesNoTitleText = FindComponentByName<TMP_Text>(yesNoMenu.transform, "tTitle");
            }
        }

        if (yesNoDescriptionText == null)
        {
            yesNoDescriptionText = FindComponentByName<TMP_Text>(yesNoMenu.transform, "tDescription");
        }

        if (yesNoQuestionText == null)
        {
            yesNoQuestionText = FindComponentByName<TMP_Text>(yesNoMenu.transform, "tQuestion");
        }

        if (yesNoButton == null)
        {
            yesNoButton = yesNoMenu.GetComponent<Button>();
        }

        if (yesNoYesButton == null)
        {
            yesNoYesButton = FindComponentByName<Button>(yesNoMenu.transform, "BYes");
        }

        if (yesNoNoButton == null)
        {
            yesNoNoButton = FindComponentByName<Button>(yesNoMenu.transform, "BNo");
        }

        if (yesNoYesButton != null)
        {
            yesNoYesButton.onClick.RemoveListener(HandleYesNoAcceptedClicked);
            yesNoYesButton.onClick.AddListener(HandleYesNoAcceptedClicked);
        }

        if (yesNoNoButton != null)
        {
            yesNoNoButton.onClick.RemoveListener(HandleYesNoDeclinedClicked);
            yesNoNoButton.onClick.AddListener(HandleYesNoDeclinedClicked);
        }
    }

    private void CacheLoseMenu()
    {
        if (loseMenu == null)
        {
            Transform loseTransform = transform.Find("MenuLose");
            if (loseTransform != null)
            {
                loseMenu = loseTransform.gameObject;
            }
        }

        if (loseMenu == null)
        {
            return;
        }

        if (loseCharacterPortraitImage == null)
        {
            loseCharacterPortraitImage = FindComponentByName<Image>(loseMenu.transform, "iChara");
        }

        if (loseCharacterNameText == null)
        {
            loseCharacterNameText = FindComponentByName<TMP_Text>(loseMenu.transform, "Chara-Name");
            if (loseCharacterNameText == null)
            {
                loseCharacterNameText = FindComponentByName<TMP_Text>(loseMenu.transform, "tTitle");
            }
        }

        if (retryButton == null)
        {
            retryButton = FindComponentByName<Button>(loseMenu.transform, "BRetry");
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(HandleRetryClicked);
            retryButton.onClick.AddListener(HandleRetryClicked);
        }
    }

    private void CacheAbilityCheckMenu()
    {
        if (abilityCheckMenu == null)
        {
            Transform abilityCheckTransform = transform.Find("MenuAbilityCheck");
            if (abilityCheckTransform != null)
            {
                abilityCheckMenu = abilityCheckTransform.gameObject;
            }
        }

        if (abilityCheckMenu == null)
        {
            return;
        }

        if (abilityCheckBackgroundButton == null)
        {
            abilityCheckBackgroundButton = abilityCheckMenu.GetComponent<Button>();
        }

        if (abilityCheckCard == null)
        {
            abilityCheckCard = abilityCheckMenu.GetComponentInChildren<RewardButtonUI>(true);
        }

        if (abilityCheckBackgroundButton != null)
        {
            abilityCheckBackgroundButton.onClick.RemoveListener(HandleAbilityCheckBackgroundClicked);
            abilityCheckBackgroundButton.onClick.AddListener(HandleAbilityCheckBackgroundClicked);
        }

        if (abilityCheckOptionButtons.Count == 0)
        {
            Transform upgradesTransform = abilityCheckMenu.transform.Find("Upgrades");
            if (upgradesTransform != null)
            {
                for (int index = 0; index < upgradesTransform.childCount; index++)
                {
                    Transform child = upgradesTransform.GetChild(index);
                    Button optionButton = child.GetComponent<Button>();
                    if (optionButton == null)
                    {
                        continue;
                    }

                    abilityCheckOptionButtons.Add(optionButton);
                    abilityCheckOptionSelectors.Add(child.Find("iSelector")?.gameObject);
                }
            }
        }

        for (int index = 0; index < abilityCheckOptionButtons.Count; index++)
        {
            int capturedIndex = index;
            abilityCheckOptionButtons[index].onClick.RemoveAllListeners();
            abilityCheckOptionButtons[index].onClick.AddListener(() => HandleAbilityCheckOptionClicked(capturedIndex));
        }
    }

    private void CacheSwitchAbilityMenu()
    {
        if (switchAbilityMenu == null)
        {
            Transform switchMenuTransform = transform.Find("MenuSwitchAbility");
            if (switchMenuTransform != null)
            {
                switchAbilityMenu = switchMenuTransform.gameObject;
            }
        }

        if (switchAbilityMenu == null)
        {
            return;
        }

        if (switchAbilityInfoText == null)
        {
            switchAbilityInfoText = switchAbilityMenu.transform.Find("Info/tInfo")?.GetComponent<TMP_Text>();
            if (switchAbilityInfoText == null)
            {
                switchAbilityInfoText = switchAbilityMenu.transform.Find("Title/tTitle")?.GetComponent<TMP_Text>();
            }
        }

        if (switchAbilityNewIcon == null)
        {
            switchAbilityNewIcon = switchAbilityMenu.transform.Find("Background/AbilityNew/Mask/iAbility")?.GetComponent<Image>();
        }

        if (switchAbilityOldIcon == null)
        {
            switchAbilityOldIcon = switchAbilityMenu.transform.Find("Background/AbilityToReplace/Mask/iAbility")?.GetComponent<Image>();
        }

        if (switchAbilityConfirmButton == null)
        {
            switchAbilityConfirmButton = switchAbilityMenu.transform.Find("Background/BConfirm")?.GetComponent<Button>();
        }

        if (switchAbilityCancelButton == null)
        {
            switchAbilityCancelButton = switchAbilityMenu.transform.Find("Background/BCancel")?.GetComponent<Button>();
        }

        if (switchAbilityConfirmButton != null)
        {
            switchAbilityConfirmButton.onClick.RemoveAllListeners();
            switchAbilityConfirmButton.onClick.AddListener(HandleSwitchAbilityConfirmClicked);
        }

        if (switchAbilityCancelButton != null)
        {
            switchAbilityCancelButton.onClick.RemoveAllListeners();
            switchAbilityCancelButton.onClick.AddListener(HandleSwitchAbilityCancelClicked);
        }
    }

    private void CacheRewardTypeIcons()
    {
        if (basicAttackRewardIcon != null && mobilityRewardIcon != null && specialRewardIcon != null)
        {
            return;
        }

        for (int index = 0; index < abilityButtons.Count; index++)
        {
            AbilityButtonUI button = abilityButtons[index];
            if (button == null)
            {
                continue;
            }

            if (basicAttackRewardIcon == null)
            {
                basicAttackRewardIcon = button.GetTypeSpriteForCategory(AbilityCategory.BasicAttack);
            }

            if (mobilityRewardIcon == null)
            {
                mobilityRewardIcon = button.GetTypeSpriteForCategory(AbilityCategory.MobilitySkill);
            }

            if (specialRewardIcon == null)
            {
                specialRewardIcon = button.GetTypeSpriteForCategory(AbilityCategory.SpecialPower);
            }

            if (basicAttackRewardIcon != null && mobilityRewardIcon != null && specialRewardIcon != null)
            {
                break;
            }
        }
    }

    private void CacheStatsMenus()
    {
        if (enemyStatsMenu == null)
        {
            Transform enemyMenuTransform = transform.Find("MenuStatsEnemy");
            if (enemyMenuTransform != null)
            {
                enemyStatsMenu = enemyMenuTransform.GetComponent<UnitStatsMenuUI>();
                if (enemyStatsMenu == null)
                {
                    enemyStatsMenu = enemyMenuTransform.gameObject.AddComponent<UnitStatsMenuUI>();
                }
            }
        }

        if (characterStatsMenu == null)
        {
            Transform characterMenuTransform = transform.Find("MenuStatsCharacter");
            if (characterMenuTransform != null)
            {
                characterStatsMenu = characterMenuTransform.GetComponent<UnitStatsMenuUI>();
                if (characterStatsMenu == null)
                {
                    characterStatsMenu = characterMenuTransform.gameObject.AddComponent<UnitStatsMenuUI>();
                }
            }
        }

        if (enemyStatsMenu != null && enemyStatsMenu.CloseButton != null)
        {
            enemyStatsMenu.CloseButton.onClick.RemoveListener(HandleCloseEnemyStatsClicked);
            enemyStatsMenu.CloseButton.onClick.AddListener(HandleCloseEnemyStatsClicked);
        }

        if (characterStatsMenu != null && characterStatsMenu.CloseButton != null)
        {
            characterStatsMenu.CloseButton.onClick.RemoveListener(HandleCloseCharacterStatsClicked);
            characterStatsMenu.CloseButton.onClick.AddListener(HandleCloseCharacterStatsClicked);
        }

        if (footerUI != null && footerUI.PortraitButton != null)
        {
            footerUI.PortraitButton.onClick.RemoveListener(HandleCharacterPortraitClicked);
            footerUI.PortraitButton.onClick.AddListener(HandleCharacterPortraitClicked);
        }
    }

    private void RefreshAbilityButtons()
    {
        if (abilityButtons.Count == 0)
        {
            CacheAbilityButtons();
        }

        Color abilityButtonBackgroundColor = GetCharacterThemeColor(CharacterUIColorKey.AbilityButtonBackground, new Color(0.74272823f, 0f, 1f, 1f));
        Color abilityButtonOutlineColor = GetCharacterThemeColor(CharacterUIColorKey.AbilityButtonOutline, new Color(0.5722367f, 0f, 0.745283f, 1f));
        Color abilityButtonCountBackgroundColor = GetCharacterThemeColor(CharacterUIColorKey.AbilityButtonCountBackground, new Color(0.47843137f, 0.003921569f, 0.5921569f, 1f));
        Color abilityButtonTypeIconColor = GetCharacterThemeColor(CharacterUIColorKey.AbilityButtonTypeIcon, new Color(0.44419536f, 0f, 0.6226415f, 1f));
        Color abilityButtonTypeOutlineColor = GetCharacterThemeColor(CharacterUIColorKey.AbilityButtonTypeOutline, new Color(0.5471698f, 0.3875786f, 0f, 1f));

        for (int index = 0; index < abilityButtons.Count; index++)
        {
            AbilityButtonUI button = abilityButtons[index];
            if (button == null)
            {
                continue;
            }

            button.Setup(gameTurnManager, observedCharacter, index, HandleAbilityButtonPrimaryClick, HandleAbilityButtonLongPress);

            CharacterAbilityRuntime runtime = observedCharacter != null ? observedCharacter.GetAbilityForSlot(index) : null;
            bool hasValidTarget = !disableAbilityButtonsWithoutValidTargets
                || runtime == null
                || gameTurnManager == null
                || gameTurnManager.HasAnyUsableTarget(observedCharacter, runtime);
            button.SetHasAnyValidTarget(hasValidTarget);
            button.ApplyTheme(abilityButtonBackgroundColor, abilityButtonOutlineColor, abilityButtonCountBackgroundColor, abilityButtonTypeIconColor, abilityButtonTypeOutlineColor);
        }
    }

    private void HandleNoValidTargetFeedbackRequested(CharacterAbilityRuntime runtime)
    {
        if (runtime?.Definition == null || observedCharacter == null || gameTurnManager?.Board == null || targetableOnlyCellIndicatorPrefab == null)
        {
            return;
        }

        ClearTargetableOnlyCellIndicators();

        BoardManager board = gameTurnManager.Board;
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!runtime.Definition.CanShowPotentialTargetCell(observedCharacter, runtime, cell))
                {
                    continue;
                }

                Vector3 spawnPosition = board.GridToWorldPosition(cell);
                if (board.TryGetCell(cell, out BoardCell boardCell) && boardCell != null && boardCell.HasBlockingTerrain)
                {
                    spawnPosition += Vector3.up * targetableOnlyCellIndicatorObstacleHeightOffset;
                }

                GameObject indicator = Instantiate(targetableOnlyCellIndicatorPrefab, spawnPosition, targetableOnlyCellIndicatorPrefab.transform.rotation, board.transform);
                indicator.name = $"{targetableOnlyCellIndicatorPrefab.name}_{cell.x}_{cell.y}";
                indicator.transform.localScale = targetableOnlyCellIndicatorPrefab.transform.localScale;
                targetableOnlyCellIndicators.Add(indicator);
            }
        }

        if (targetableOnlyCellIndicators.Count > 0)
        {
            clearTargetableOnlyIndicatorsCoroutine = StartCoroutine(ClearTargetableOnlyCellIndicatorsAfterDelay());
        }
    }

    private System.Collections.IEnumerator ClearTargetableOnlyCellIndicatorsAfterDelay()
    {
        yield return new WaitForSeconds(targetableOnlyCellIndicatorDuration);
        clearTargetableOnlyIndicatorsCoroutine = null;
        ClearTargetableOnlyCellIndicators();
    }

    private void RefreshItemsList()
    {
        if (itemsList == null)
        {
            return;
        }

        for (int index = itemIcons.Count - 1; index >= 0; index--)
        {
            if (itemIcons[index] != null)
            {
                Destroy(itemIcons[index]);
            }
        }

        itemIcons.Clear();
        itemIconsByKey.Clear();

        if (itemIconPrefab == null || observedCharacter == null || observedCharacter.RunRewardState == null)
        {
            return;
        }

        BoardManager board = gameTurnManager != null ? gameTurnManager.Board : observedCharacter.Board;
        if (board == null)
        {
            return;
        }

        List<ItemRewardKey> ownedItems = observedCharacter.RunRewardState.GetOwnedItems();
        for (int index = 0; index < ownedItems.Count; index++)
        {
            ItemRewardDefinition itemRewardDefinition = board.GetItemRewardDefinition(ownedItems[index]);
            if (itemRewardDefinition == null)
            {
                continue;
            }

            GameObject itemIconObject = Instantiate(itemIconPrefab, itemsList);
            itemIconObject.name = $"{itemIconPrefab.name}_{ownedItems[index]}";
            itemIconObject.SetActive(true);
            ItemIconUI itemIcon = itemIconObject.GetComponent<ItemIconUI>();
            if (itemIcon == null)
            {
                itemIcon = itemIconObject.AddComponent<ItemIconUI>();
            }

            itemIcon.Bind(itemRewardDefinition, itemIconTheme, HandleItemIconClicked);
            itemIcon.SetActivationVisible(observedCharacter.IsItemActivationActive(itemRewardDefinition.ItemKey));
            itemIcons.Add(itemIconObject);
            itemIconsByKey[itemRewardDefinition.ItemKey] = itemIcon;
        }
    }

    private void HandleItemActivationChanged(Character character, ItemRewardKey itemKey, bool isActive, float duration)
    {
        if (observedCharacter == null || character != observedCharacter)
        {
            return;
        }

        if (!itemIconsByKey.TryGetValue(itemKey, out ItemIconUI itemIcon) || itemIcon == null)
        {
            return;
        }

        if (duration > 0f)
        {
            itemIcon.PulseActivation(duration);
            return;
        }

        itemIcon.SetActivationVisible(isActive);
    }

    private void ApplyCurrentCharacterTheme()
    {
        mobilityAvailableColor = GetCharacterThemeColor(CharacterUIColorKey.MobilityAvailable, Color.white);
        mobilityConsumedColor = GetCharacterThemeColor(CharacterUIColorKey.MobilityConsumed, Color.black);

        powerRewardStyle = BuildRewardStyle(
            basePowerRewardStyle,
            CharacterUIColorKey.PowerRewardBackground,
            CharacterUIColorKey.PowerRewardTitleBackground,
            CharacterUIColorKey.PowerRewardTitleText,
            CharacterUIColorKey.PowerRewardDescriptionBackground,
            CharacterUIColorKey.PowerRewardDescriptionText,
            CharacterUIColorKey.PowerRewardTypeContainer,
            CharacterUIColorKey.PowerRewardTypeIcon);

        itemRewardStyle = baseItemRewardStyle;

        powerRewardTheme = new RewardButtonTheme(
            GetCharacterThemeColor(CharacterUIColorKey.PowerRewardOutline, RewardButtonUI.DefaultPowerTheme.OutlineColor),
            GetCharacterThemeColor(CharacterUIColorKey.PowerRewardSubtitleBackground, RewardButtonUI.DefaultPowerTheme.SubtitleBackgroundColor),
            GetCharacterThemeColor(CharacterUIColorKey.PowerRewardSubtitleText, RewardButtonUI.DefaultPowerTheme.SubtitleTextColor),
            GetCharacterThemeColor(CharacterUIColorKey.PowerRewardNewSubtitleBackground, RewardButtonUI.DefaultPowerTheme.NewSubtitleBackgroundColor),
            GetCharacterThemeColor(CharacterUIColorKey.PowerRewardNewSubtitleText, RewardButtonUI.DefaultPowerTheme.NewSubtitleTextColor));

        itemRewardTheme = RewardButtonUI.DefaultItemTheme;

        itemIconTheme = new ItemIconTheme(
            GetCharacterThemeColor(CharacterUIColorKey.ItemIconBackground, ItemIconUI.DefaultTheme.BackgroundColor),
            GetCharacterThemeColor(CharacterUIColorKey.ItemIconActivation, ItemIconUI.DefaultTheme.ActivationColor));

        ApplyHudTheme();
        ApplyToolsTheme();
    }

    private RewardButtonStyle BuildRewardStyle(
        RewardButtonStyle baseStyle,
        CharacterUIColorKey backgroundKey,
        CharacterUIColorKey titleBackgroundKey,
        CharacterUIColorKey titleTextKey,
        CharacterUIColorKey descriptionBackgroundKey,
        CharacterUIColorKey descriptionTextKey,
        CharacterUIColorKey typeContainerKey,
        CharacterUIColorKey typeIconKey)
    {
        return new RewardButtonStyle(
            baseStyle.ArtworkSprite,
            GetCharacterThemeColor(backgroundKey, baseStyle.BackgroundColor),
            GetCharacterThemeColor(titleBackgroundKey, baseStyle.TitleBackgroundColor),
            GetCharacterThemeColor(titleTextKey, baseStyle.TitleTextColor),
            GetCharacterThemeColor(descriptionBackgroundKey, baseStyle.DescriptionBackgroundColor),
            GetCharacterThemeColor(descriptionTextKey, baseStyle.DescriptionTextColor),
            GetCharacterThemeColor(typeContainerKey, baseStyle.TypeContainerColor),
            GetCharacterThemeColor(typeIconKey, baseStyle.TypeIconColor));
    }

    private Color GetCharacterThemeColor(CharacterUIColorKey key, Color fallback)
    {
        CharacterData characterData = observedCharacter != null ? observedCharacter.Data : null;
        return characterData != null ? characterData.GetUIColor(key, fallback) : fallback;
    }

    private void ApplyHudTheme()
    {
        if (footerUI != null)
        {
            Image footerBackground = footerUI.GetComponent<Image>();
            if (footerBackground != null)
            {
                footerBackground.color = GetCharacterThemeColor(CharacterUIColorKey.FooterBackground, new Color(0.1086727f, 0.070487715f, 0.1509434f, 1f));
            }

            if (footerUI.PortraitButton != null)
            {
                Image portraitBackground = footerUI.PortraitButton.GetComponent<Image>();
                if (portraitBackground != null)
                {
                    portraitBackground.color = GetCharacterThemeColor(CharacterUIColorKey.PortraitBackground, new Color(0.39572334f, 0f, 0.49056602f, 0.2627451f));
                }
            }

            if (footerUI.CharacterNameLabel != null)
            {
                Image nameplateBackground = footerUI.CharacterNameLabel.transform.parent != null
                    ? footerUI.CharacterNameLabel.transform.parent.GetComponent<Image>()
                    : null;
                if (nameplateBackground != null)
                {
                    nameplateBackground.color = GetCharacterThemeColor(CharacterUIColorKey.PortraitNameplateBackground, new Color(0.25490198f, 0.07450981f, 0.29803923f, 1f));
                }
            }

            if (footerUI.ArenaCountLabel != null)
            {
                Image arenaCountBackground = footerUI.ArenaCountLabel.transform.parent != null
                    ? footerUI.ArenaCountLabel.transform.parent.GetComponent<Image>()
                    : null;
                if (arenaCountBackground != null)
                {
                    arenaCountBackground.color = GetCharacterThemeColor(CharacterUIColorKey.PortraitNameplateBackground, new Color(0.25490198f, 0.07450981f, 0.29803923f, 1f));
                }
            }
        }

        if (mobilityBar != null)
        {
            Image mobilityBackground = mobilityBar.GetComponent<Image>();
            if (mobilityBackground != null)
            {
                mobilityBackground.color = GetCharacterThemeColor(CharacterUIColorKey.MobilityBarBackground, new Color(0.49411765f, 0.003921569f, 0.5568628f, 0.20784314f));
            }
        }

        RefreshMobilityBar();
    }

    private void HandleItemIconClicked(ItemRewardDefinition itemRewardDefinition)
    {
        if (itemRewardDefinition == null)
        {
            return;
        }

        if (rewardCheckMenu != null
            && rewardCheckMenu.activeSelf
            && pendingRewardConfirmation == null
            && currentPreviewedItemDefinition == itemRewardDefinition)
        {
            HideRewardCheck();
            return;
        }

        pendingRewardConfirmation = null;
        currentPreviewedItemDefinition = itemRewardDefinition;
        ShowRewardCheck(itemRewardDefinition.CreateOffer(), false);
    }

    private void HandleAbilityCheckBackgroundClicked()
    {
        SoundManager.Instance?.PlayClick();
        HideAbilityCheck();
    }

    private void HandleAbilityCheckOptionClicked(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= currentAbilityCheckOffers.Count)
        {
            return;
        }

        SoundManager.Instance?.PlayClick();
        UpdateAbilityCheckSelection(optionIndex);
    }

    private void EnsureToolsUI()
    {
        CacheToolsMenu();
        if (toolsButton == null || toolsMenu == null)
        {
            CreateRuntimeToolsUI();
            CacheToolsMenu();
        }

        if (toolsButton != null)
        {
            toolsButton.gameObject.SetActive(false);
        }

        if (toolsButton != null)
        {
            toolsButton.onClick.RemoveListener(HandleToolsButtonClicked);
            toolsButton.onClick.AddListener(HandleToolsButtonClicked);
        }

        BindToolsButton(toolsMenuBackdropButton, HandleToolsCloseBuildMenuClicked);
        BindToolsButton(toolsPreviousBuildButton, HandleToolsPreviousBuildClicked);
        BindToolsButton(toolsNextBuildButton, HandleToolsNextBuildClicked);
        BindToolsButton(toolsSaveBuildButton, HandleToolsSaveBuildClicked);
        BindToolsButton(toolsLoadBuildButton, HandleToolsLoadBuildClicked);
        BindToolsButton(toolsDeleteBuildButton, HandleToolsDeleteBuildClicked);
        BindToolsButton(toolsCloseButton, HandleToolsCloseBuildMenuClicked);

        RefreshAvailableEquipmentBuilds();
    }

    private void CacheToolsMenu()
    {
        if (toolsButton == null)
        {
            toolsButton = FindComponentByName<Button>(transform, "BTools");
        }

        if (toolsMenu == null)
        {
            Transform toolsMenuTransform = transform.Find("MenuTools");
            toolsMenu = toolsMenuTransform != null ? toolsMenuTransform.gameObject : null;
        }

        if (toolsMenu == null)
        {
            return;
        }

        if (toolsMenuBackdropButton == null)
        {
            toolsMenuBackdropButton = FindComponentByName<Button>(toolsMenu.transform, "BToolsBackdrop");
        }

        if (toolsBuildNameInput == null)
        {
            toolsBuildNameInput = FindComponentByName<TMP_InputField>(toolsMenu.transform, "InputBuildName");
        }

        if (toolsSelectedBuildLabel == null)
        {
            toolsSelectedBuildLabel = FindComponentByName<TMP_Text>(toolsMenu.transform, "tSelectedBuild");
        }

        if (toolsBuildCountLabel == null)
        {
            toolsBuildCountLabel = FindComponentByName<TMP_Text>(toolsMenu.transform, "tBuildCount");
        }

        if (toolsBuildDetailsLabel == null)
        {
            toolsBuildDetailsLabel = FindComponentByName<TMP_Text>(toolsMenu.transform, "tBuildDetails");
        }

        if (toolsStatusLabel == null)
        {
            toolsStatusLabel = FindComponentByName<TMP_Text>(toolsMenu.transform, "tToolsStatus");
        }

        if (toolsPreviousBuildButton == null)
        {
            toolsPreviousBuildButton = FindComponentByName<Button>(toolsMenu.transform, "BPrevBuild");
        }

        if (toolsNextBuildButton == null)
        {
            toolsNextBuildButton = FindComponentByName<Button>(toolsMenu.transform, "BNextBuild");
        }

        if (toolsSaveBuildButton == null)
        {
            toolsSaveBuildButton = FindComponentByName<Button>(toolsMenu.transform, "BSaveBuild");
        }

        if (toolsLoadBuildButton == null)
        {
            toolsLoadBuildButton = FindComponentByName<Button>(toolsMenu.transform, "BLoadBuild");
        }

        if (toolsDeleteBuildButton == null)
        {
            toolsDeleteBuildButton = FindComponentByName<Button>(toolsMenu.transform, "BDeleteBuild");
        }

        if (toolsCloseButton == null)
        {
            toolsCloseButton = FindComponentByName<Button>(toolsMenu.transform, "BCloseTools");
        }
    }

    private void CreateRuntimeToolsUI()
    {
        RectTransform rootRect = transform as RectTransform;
        if (rootRect == null)
        {
            return;
        }

        TMP_FontAsset fontAsset = ResolveDefaultUiFont();
        if (fontAsset == null)
        {
            return;
        }

        if (toolsButton == null)
        {
            toolsButton = CreateLabeledButton(
                rootRect,
                "BTools",
                "TOOLS",
                new Vector2(150f, 58f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -24f),
                fontAsset,
                new Color(0.56f, 0.14f, 0.73f, 0.95f));
        }

        if (toolsMenu == null)
        {
            toolsMenu = new GameObject("MenuTools", typeof(RectTransform));
            toolsMenu.transform.SetParent(rootRect, false);
        }

        RectTransform menuRect = toolsMenu.GetComponent<RectTransform>();
        EnsureCanvasRenderer(toolsMenu);
        menuRect.anchorMin = Vector2.zero;
        menuRect.anchorMax = Vector2.one;
        menuRect.offsetMin = Vector2.zero;
        menuRect.offsetMax = Vector2.zero;

        toolsMenuBackdropButton = EnsureBackdropButton(toolsMenuBackdropButton, menuRect);

        Transform panelTransform = toolsMenu.transform.Find("Panel");
        if (panelTransform == null)
        {
            GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(toolsMenu.transform, false);
            panelTransform = panelObject.transform;
        }

        RectTransform panelRect = panelTransform as RectTransform;
        Image panelImage = panelTransform.GetComponent<Image>();
        if (panelImage == null)
        {
            panelImage = panelTransform.gameObject.AddComponent<Image>();
        }

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(620f, 470f);
        panelRect.anchoredPosition = Vector2.zero;
        panelImage.color = new Color(0.11f, 0.07f, 0.16f, 0.985f);

        EnsureLabelByName(panelRect, "tToolsTitle", "BUILD TOOLS", fontAsset, 32, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(480f, 46f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Color(1f, 0.73f, 0f, 1f));
        EnsureLabel(ref toolsSelectedBuildLabel, panelRect, "tSelectedBuild", "No build saved", fontAsset, 24, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(300f, 34f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), Color.white);
        EnsureLabel(ref toolsBuildCountLabel, panelRect, "tBuildCount", "0 builds saved", fontAsset, 16, FontStyles.Italic, TextAlignmentOptions.Center, new Vector2(220f, 24f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Color(0.9f, 0.78f, 0.95f, 1f));
        EnsureLabel(ref toolsBuildDetailsLabel, panelRect, "tBuildDetails", string.Empty, fontAsset, 20, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(510f, 145f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Color(0.93f, 0.83f, 0.95f, 1f));
        EnsureLabel(ref toolsStatusLabel, panelRect, "tToolsStatus", string.Empty, fontAsset, 16, FontStyles.Italic, TextAlignmentOptions.Center, new Vector2(520f, 26f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Color(0.95f, 0.73f, 0.31f, 1f));

        toolsPreviousBuildButton = EnsureLabeledButton(toolsPreviousBuildButton, panelRect, "BPrevBuild", "<", new Vector2(58f, 44f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -96f), fontAsset, new Color(0.34f, 0.16f, 0.45f, 1f));
        toolsNextBuildButton = EnsureLabeledButton(toolsNextBuildButton, panelRect, "BNextBuild", ">", new Vector2(58f, 44f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -96f), fontAsset, new Color(0.34f, 0.16f, 0.45f, 1f));

        EnsureLabelByName(panelRect, "tBuildNameLabel", "Build Name", fontAsset, 18, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(510f, 28f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 144f), new Color(0.95f, 0.83f, 0.32f, 1f));
        toolsBuildNameInput = EnsureInputField(toolsBuildNameInput, panelRect, "InputBuildName", fontAsset, new Vector2(510f, 52f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 104f));

        toolsSaveBuildButton = EnsureLabeledButton(toolsSaveBuildButton, panelRect, "BSaveBuild", "SAVE CURRENT BUILD", new Vector2(215f, 52f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 36f), fontAsset, new Color(0.56f, 0.14f, 0.73f, 1f));
        toolsLoadBuildButton = EnsureLabeledButton(toolsLoadBuildButton, panelRect, "BLoadBuild", "LOAD SELECTED", new Vector2(190f, 52f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), fontAsset, new Color(0.11f, 0.58f, 0.85f, 1f));
        toolsDeleteBuildButton = EnsureLabeledButton(toolsDeleteBuildButton, panelRect, "BDeleteBuild", "DELETE", new Vector2(130f, 52f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 36f), fontAsset, new Color(0.66f, 0.18f, 0.22f, 1f));
        toolsCloseButton = EnsureLabeledButton(toolsCloseButton, panelRect, "BCloseTools", "CLOSE", new Vector2(160f, 44f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), fontAsset, new Color(0.16f, 0.08f, 0.23f, 1f));
        toolsMenu.transform.SetAsLastSibling();
        ApplyToolsTheme();
    }

    private void ApplyToolsTheme()
    {
        if (toolsButton != null)
        {
            ApplyButtonBackgroundColor(toolsButton, GetCharacterThemeColor(CharacterUIColorKey.ToolsPrimaryButtonBackground, new Color(0.56f, 0.14f, 0.73f, 0.95f)));
        }

        Image panelImage = toolsMenu != null ? FindComponentByName<Image>(toolsMenu.transform, "Panel") : null;
        if (panelImage != null)
        {
            panelImage.color = GetCharacterThemeColor(CharacterUIColorKey.ToolsPanelBackground, new Color(0.11f, 0.07f, 0.16f, 0.985f));
        }

        if (toolsBuildCountLabel != null)
        {
            toolsBuildCountLabel.color = GetCharacterThemeColor(CharacterUIColorKey.ToolsSecondaryText, new Color(0.9f, 0.78f, 0.95f, 1f));
        }

        if (toolsBuildDetailsLabel != null)
        {
            toolsBuildDetailsLabel.color = GetCharacterThemeColor(CharacterUIColorKey.ToolsDetailText, new Color(0.93f, 0.83f, 0.95f, 1f));
        }

        Color navigationColor = GetCharacterThemeColor(CharacterUIColorKey.ToolsNavigationButtonBackground, new Color(0.34f, 0.16f, 0.45f, 1f));
        ApplyButtonBackgroundColor(toolsPreviousBuildButton, navigationColor);
        ApplyButtonBackgroundColor(toolsNextBuildButton, navigationColor);
        ApplyButtonBackgroundColor(toolsSaveBuildButton, GetCharacterThemeColor(CharacterUIColorKey.ToolsPrimaryButtonBackground, new Color(0.56f, 0.14f, 0.73f, 1f)));
        ApplyButtonBackgroundColor(toolsCloseButton, GetCharacterThemeColor(CharacterUIColorKey.ToolsCloseButtonBackground, new Color(0.16f, 0.08f, 0.23f, 1f)));

        if (toolsBuildNameInput != null)
        {
            Image inputBackground = toolsBuildNameInput.GetComponent<Image>();
            if (inputBackground != null)
            {
                inputBackground.color = GetCharacterThemeColor(CharacterUIColorKey.ToolsInputBackground, new Color(0.16f, 0.09f, 0.23f, 1f));
            }

            if (toolsBuildNameInput.placeholder is TMP_Text placeholderText)
            {
                placeholderText.color = GetCharacterThemeColor(CharacterUIColorKey.ToolsInputPlaceholder, new Color(0.8f, 0.72f, 0.84f, 0.65f));
            }
        }
    }

    private static void ApplyButtonBackgroundColor(Button button, Color color)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
        }
    }

    private void HandleToolsButtonClicked()
    {
        SoundManager.Instance?.PlayClick();
        if (toolsMenu != null && toolsMenu.activeSelf)
        {
            HideToolsMenu();
            return;
        }

        ShowToolsMenu();
    }

    private void HandleToolsPreviousBuildClicked()
    {
        if (availableEquipmentBuilds.Count == 0)
        {
            return;
        }

        SoundManager.Instance?.PlayClick();
        selectedEquipmentBuildIndex = (selectedEquipmentBuildIndex - 1 + availableEquipmentBuilds.Count) % availableEquipmentBuilds.Count;
        UpdateToolsMenuState();
    }

    private void HandleToolsNextBuildClicked()
    {
        if (availableEquipmentBuilds.Count == 0)
        {
            return;
        }

        SoundManager.Instance?.PlayClick();
        selectedEquipmentBuildIndex = (selectedEquipmentBuildIndex + 1) % availableEquipmentBuilds.Count;
        UpdateToolsMenuState();
    }

    private void HandleToolsSaveBuildClicked()
    {
        SoundManager.Instance?.PlayClick();
        BoardManager board = gameTurnManager != null ? gameTurnManager.Board : null;
        if (board == null)
        {
            UpdateToolsMenuState("No active board.");
            return;
        }

        string desiredBuildName = toolsBuildNameInput != null ? toolsBuildNameInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(desiredBuildName))
        {
            desiredBuildName = GenerateDefaultEquipmentBuildName();
        }

        EquipmentBuildData buildData = board.CreateEquipmentBuildSnapshot(desiredBuildName);
        if (buildData == null)
        {
            UpdateToolsMenuState("Unable to save this build.");
            return;
        }

        EquipmentBuildLibrary.SaveBuild(buildData);
        RefreshAvailableEquipmentBuilds(buildData.BuildName);
        UpdateToolsMenuState($"Saved \"{buildData.BuildName}\".");
    }

    private void HandleToolsLoadBuildClicked()
    {
        SoundManager.Instance?.PlayClick();
        EquipmentBuildData selectedBuild = GetSelectedEquipmentBuild();
        BoardManager board = gameTurnManager != null ? gameTurnManager.Board : null;
        if (selectedBuild == null || board == null)
        {
            UpdateToolsMenuState("No build selected.");
            return;
        }

        if (!board.ApplyEquipmentBuild(selectedBuild))
        {
            UpdateToolsMenuState("Unable to load this build.");
            return;
        }

        BindToCurrentCharacter();
        RefreshMobilityBar();
        RefreshAbilityButtons();
        RefreshItemsList();
        RefreshFooterCharacterInfo();
        UpdateToolsMenuState($"Loaded \"{selectedBuild.BuildName}\".");
    }

    private void HandleToolsDeleteBuildClicked()
    {
        SoundManager.Instance?.PlayClick();
        EquipmentBuildData selectedBuild = GetSelectedEquipmentBuild();
        string characterId = GetCurrentToolsCharacterId();
        if (selectedBuild == null || string.IsNullOrWhiteSpace(characterId))
        {
            UpdateToolsMenuState("No build selected.");
            return;
        }

        EquipmentBuildLibrary.DeleteBuild(characterId, selectedBuild.BuildName);
        RefreshAvailableEquipmentBuilds();
        UpdateToolsMenuState($"Deleted \"{selectedBuild.BuildName}\".");
    }

    private void HandleToolsCloseBuildMenuClicked()
    {
        SoundManager.Instance?.PlayClick();
        HideToolsMenu();
    }

    private void ShowToolsMenu()
    {
        RefreshAvailableEquipmentBuilds();
        if (toolsMenu != null)
        {
            toolsMenu.SetActive(true);
        }

        UpdateToolsMenuState();
    }

    private void HideToolsMenu()
    {
        if (toolsMenu != null)
        {
            toolsMenu.SetActive(false);
        }
    }

    private void RefreshAvailableEquipmentBuilds(string preferredBuildName = null)
    {
        string characterId = GetCurrentToolsCharacterId();
        string currentSelectedName = preferredBuildName;
        if (string.IsNullOrWhiteSpace(currentSelectedName))
        {
            EquipmentBuildData selectedBuild = GetSelectedEquipmentBuild();
            currentSelectedName = selectedBuild != null ? selectedBuild.BuildName : string.Empty;
        }

        availableEquipmentBuilds.Clear();
        availableEquipmentBuilds.AddRange(EquipmentBuildLibrary.GetBuilds(characterId));

        selectedEquipmentBuildIndex = -1;
        if (!string.IsNullOrWhiteSpace(currentSelectedName))
        {
            for (int index = 0; index < availableEquipmentBuilds.Count; index++)
            {
                if (string.Equals(availableEquipmentBuilds[index].BuildName, currentSelectedName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedEquipmentBuildIndex = index;
                    break;
                }
            }
        }

        if (selectedEquipmentBuildIndex < 0 && availableEquipmentBuilds.Count > 0)
        {
            selectedEquipmentBuildIndex = 0;
        }
    }

    private void UpdateToolsMenuState(string statusMessage = null)
    {
        EquipmentBuildData selectedBuild = GetSelectedEquipmentBuild();
        if (toolsSelectedBuildLabel != null)
        {
            toolsSelectedBuildLabel.text = selectedBuild != null ? selectedBuild.BuildName : "No build saved";
        }

        if (toolsBuildCountLabel != null)
        {
            int buildCount = availableEquipmentBuilds.Count;
            toolsBuildCountLabel.text = buildCount == 1 ? "1 build saved" : $"{buildCount} builds saved";
        }

        if (toolsBuildDetailsLabel != null)
        {
            toolsBuildDetailsLabel.text = BuildEquipmentDetailsText(selectedBuild);
        }

        if (toolsBuildNameInput != null)
        {
            if (selectedBuild != null)
            {
                toolsBuildNameInput.text = selectedBuild.BuildName;
            }
            else if (string.IsNullOrWhiteSpace(toolsBuildNameInput.text))
            {
                toolsBuildNameInput.text = GenerateDefaultEquipmentBuildName();
            }
        }

        if (toolsStatusLabel != null)
        {
            toolsStatusLabel.text = statusMessage ?? string.Empty;
        }

        bool hasSavedBuild = selectedBuild != null;
        if (toolsPreviousBuildButton != null)
        {
            toolsPreviousBuildButton.interactable = availableEquipmentBuilds.Count > 1;
        }

        if (toolsNextBuildButton != null)
        {
            toolsNextBuildButton.interactable = availableEquipmentBuilds.Count > 1;
        }

        if (toolsLoadBuildButton != null)
        {
            toolsLoadBuildButton.interactable = hasSavedBuild;
        }

        if (toolsDeleteBuildButton != null)
        {
            toolsDeleteBuildButton.interactable = hasSavedBuild;
        }
    }

    private EquipmentBuildData GetSelectedEquipmentBuild()
    {
        return selectedEquipmentBuildIndex >= 0 && selectedEquipmentBuildIndex < availableEquipmentBuilds.Count
            ? availableEquipmentBuilds[selectedEquipmentBuildIndex]
            : null;
    }

    private string GetCurrentToolsCharacterId()
    {
        BoardManager board = gameTurnManager != null ? gameTurnManager.Board : null;
        if (board != null)
        {
            return board.GetCurrentCharacterId();
        }

        return observedCharacter != null && observedCharacter.Data != null ? observedCharacter.Data.name : string.Empty;
    }

    private string GenerateDefaultEquipmentBuildName()
    {
        string baseName = "Build";
        int suffix = 1;
        HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < availableEquipmentBuilds.Count; index++)
        {
            EquipmentBuildData build = availableEquipmentBuilds[index];
            if (build != null && !string.IsNullOrWhiteSpace(build.BuildName))
            {
                existingNames.Add(build.BuildName.Trim());
            }
        }

        string candidate = $"{baseName} {suffix}";
        while (existingNames.Contains(candidate))
        {
            suffix++;
            candidate = $"{baseName} {suffix}";
        }

        return candidate;
    }

    private string BuildEquipmentDetailsText(EquipmentBuildData buildData)
    {
        if (buildData == null)
        {
            return "Save your current Weapon, Mobility, Power, upgrades and items here.";
        }

        int upgradeCount = 0;
        if (buildData.Upgrades != null)
        {
            for (int index = 0; index < buildData.Upgrades.Count; index++)
            {
                EquipmentBuildUpgradeEntry entry = buildData.Upgrades[index];
                if (entry != null && entry.Stacks > 0)
                {
                    upgradeCount += entry.Stacks;
                }
            }
        }

        int itemCount = buildData.OwnedItems != null ? buildData.OwnedItems.Count : 0;
        return $"Weapon: {FormatEquipmentAbilityName(buildData.BasicAttackAbilityId)}\n"
             + $"Mobility: {FormatEquipmentAbilityName(buildData.MobilityAbilityId)}\n"
             + $"Power: {FormatEquipmentAbilityName(buildData.SpecialAbilityId)}\n"
             + $"Upgrades: {upgradeCount}\n"
             + $"Items: {itemCount}";
    }

    private string FormatEquipmentAbilityName(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return "-";
        }

        CharacterData characterData = observedCharacter != null ? observedCharacter.Data : null;
        if (characterData != null)
        {
            List<AbilityDefinition> abilities = characterData.GetAllPotentialAbilities();
            for (int index = 0; index < abilities.Count; index++)
            {
                AbilityDefinition ability = abilities[index];
                if (ability != null && ability.name == abilityId)
                {
                    return ability.AbilityName;
                }
            }
        }

        return abilityId;
    }

    private static void BindToolsButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null || callback == null)
        {
            return;
        }

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }

    private TMP_FontAsset ResolveDefaultUiFont()
    {
        if (turnLabel != null && turnLabel.font != null)
        {
            return turnLabel.font;
        }

        TMP_Text fallbackText = GetComponentInChildren<TMP_Text>(true);
        return fallbackText != null ? fallbackText.font : null;
    }

    private static Button CreateLabeledButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        TMP_FontAsset fontAsset,
        Color backgroundColor)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        Image image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;

        GameObject labelObject = new GameObject("tLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.font = fontAsset;
        labelText.fontSize = 22f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        labelText.text = label;

        return buttonObject.GetComponent<Button>();
    }

    private static TMP_Text CreateLabel(
        Transform parent,
        string objectName,
        string text,
        TMP_FontAsset fontAsset,
        float fontSize,
        FontStyles fontStyles,
        TextAlignmentOptions alignment,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Color color)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        RectTransform rectTransform = labelObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        TextMeshProUGUI textComponent = labelObject.GetComponent<TextMeshProUGUI>();
        textComponent.font = fontAsset;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyles;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.text = text;
        return textComponent;
    }

    private static TMP_InputField CreateInputField(
        Transform parent,
        string objectName,
        TMP_FontAsset fontAsset,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition)
    {
        GameObject inputObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(parent, false);

        RectTransform rectTransform = inputObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        Image background = inputObject.GetComponent<Image>();
        background.color = new Color(0.16f, 0.09f, 0.23f, 1f);

        TMP_InputField inputField = inputObject.GetComponent<TMP_InputField>();

        TMP_Text textComponent = CreateLabel(inputObject.transform, "Text", string.Empty, fontAsset, 20f, FontStyles.Bold, TextAlignmentOptions.Left, Vector2.zero, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Color.white);
        RectTransform textRect = textComponent.rectTransform;
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-16f, 0f);

        TMP_Text placeholderComponent = CreateLabel(inputObject.transform, "Placeholder", "Build name", fontAsset, 20f, FontStyles.Italic, TextAlignmentOptions.Left, Vector2.zero, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.8f, 0.72f, 0.84f, 0.65f));
        RectTransform placeholderRect = placeholderComponent.rectTransform;
        placeholderRect.offsetMin = new Vector2(16f, 0f);
        placeholderRect.offsetMax = new Vector2(-16f, 0f);

        inputField.textViewport = rectTransform;
        inputField.textComponent = textComponent as TextMeshProUGUI;
        inputField.placeholder = placeholderComponent;
        inputField.characterLimit = 32;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        return inputField;
    }

    private static void EnsureCanvasRenderer(GameObject target)
    {
        if (target != null && target.GetComponent<CanvasRenderer>() == null)
        {
            target.AddComponent<CanvasRenderer>();
        }
    }

    private Button EnsureBackdropButton(Button currentButton, RectTransform parent)
    {
        Button button = currentButton;
        if (button == null)
        {
            GameObject buttonObject = new GameObject("BToolsBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            button = buttonObject.GetComponent<Button>();
        }

        RectTransform rectTransform = button.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = button.GetComponent<Image>();
        if (image == null)
        {
            image = button.gameObject.AddComponent<Image>();
        }

        image.color = new Color(0f, 0f, 0f, 0.55f);
        return button;
    }

    private Button EnsureLabeledButton(
        Button currentButton,
        Transform parent,
        string objectName,
        string label,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        TMP_FontAsset fontAsset,
        Color backgroundColor)
    {
        Button button = currentButton;
        if (button == null)
        {
            button = CreateLabeledButton(parent, objectName, label, size, anchorMin, anchorMax, pivot, anchoredPosition, fontAsset, backgroundColor);
        }
        else
        {
            EnsureCanvasRenderer(button.gameObject);
            RectTransform rectTransform = button.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;

            Image image = button.GetComponent<Image>();
            if (image == null)
            {
                image = button.gameObject.AddComponent<Image>();
            }

            image.color = backgroundColor;
            TMP_Text labelText = button.GetComponentInChildren<TMP_Text>(true);
            if (labelText == null)
            {
                labelText = CreateLabel(button.transform, "tLabel", label, fontAsset, 22f, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Color.white);
                RectTransform labelRect = labelText.rectTransform;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }

            labelText.font = fontAsset;
            labelText.text = label;
            labelText.color = Color.white;
        }

        return button;
    }

    private void EnsureLabel(
        ref TMP_Text targetLabel,
        Transform parent,
        string objectName,
        string text,
        TMP_FontAsset fontAsset,
        float fontSize,
        FontStyles fontStyles,
        TextAlignmentOptions alignment,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Color color)
    {
        targetLabel = EnsureLabelByName(parent, objectName, text, fontAsset, fontSize, fontStyles, alignment, size, anchorMin, anchorMax, pivot, anchoredPosition, color);
    }

    private TMP_Text EnsureLabelByName(
        Transform parent,
        string objectName,
        string text,
        TMP_FontAsset fontAsset,
        float fontSize,
        FontStyles fontStyles,
        TextAlignmentOptions alignment,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Color color)
    {
        TMP_Text label = FindComponentByName<TMP_Text>(parent, objectName);
        if (label == null)
        {
            label = CreateLabel(parent, objectName, text, fontAsset, fontSize, fontStyles, alignment, size, anchorMin, anchorMax, pivot, anchoredPosition, color);
        }
        else
        {
            EnsureCanvasRenderer(label.gameObject);
            RectTransform rectTransform = label.rectTransform;
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;
            label.font = fontAsset;
            label.fontSize = fontSize;
            label.fontStyle = fontStyles;
            label.alignment = alignment;
            label.color = color;
            label.text = text;
        }

        return label;
    }

    private TMP_InputField EnsureInputField(
        TMP_InputField currentInputField,
        Transform parent,
        string objectName,
        TMP_FontAsset fontAsset,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition)
    {
        TMP_InputField inputField = currentInputField;
        if (inputField == null)
        {
            inputField = CreateInputField(parent, objectName, fontAsset, size, anchorMin, anchorMax, pivot, anchoredPosition);
        }
        else
        {
            EnsureCanvasRenderer(inputField.gameObject);
            RectTransform rectTransform = inputField.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;

            Image image = inputField.GetComponent<Image>();
            if (image == null)
            {
                image = inputField.gameObject.AddComponent<Image>();
            }

            image.color = new Color(0.16f, 0.09f, 0.23f, 1f);
            TMP_Text textComponent = FindComponentByName<TMP_Text>(inputField.transform, "Text");
            TMP_Text placeholderComponent = FindComponentByName<TMP_Text>(inputField.transform, "Placeholder");
            if (textComponent == null || placeholderComponent == null)
            {
                inputField = CreateInputField(parent, objectName, fontAsset, size, anchorMin, anchorMax, pivot, anchoredPosition);
            }
            else
            {
                textComponent.font = fontAsset;
                placeholderComponent.font = fontAsset;
                inputField.textComponent = textComponent as TextMeshProUGUI;
                inputField.placeholder = placeholderComponent;
                inputField.textViewport = rectTransform;
            }
        }

        return inputField;
    }

    private void RefreshFooterCharacterInfo()
    {
        footerUI?.RefreshCharacter(observedCharacter);
        footerUI?.RefreshArenaCount(gameTurnManager != null && gameTurnManager.Board != null ? gameTurnManager.Board.ArenaCount : 1);
        CacheStatsMenus();
    }

    private void HandleStatsMenuInput()
    {
        if (gameTurnManager == null
            || gameTurnManager.Board == null
            || gameTurnManager.IsRewardMenuOpen
            || gameTurnManager.IsArenaTransitionRunning
            || IsAbilityTargetingActive())
        {
            isStatsPointerTracking = false;
            return;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (IsPointerOverUI(touch.position, touch.fingerId))
                    {
                        return;
                    }

                    isStatsPointerTracking = true;
                    statsPointerStartPosition = touch.position;
                    TryUnlockEndTurnFromScreenPosition(touch.position);
                    break;
                case TouchPhase.Ended:
                    if (!isStatsPointerTracking)
                    {
                        return;
                    }

                    isStatsPointerTracking = false;
                    TryOpenEnemyStatsFromScreenPosition(touch.position, touch.position - statsPointerStartPosition);
                    break;
                case TouchPhase.Canceled:
                    isStatsPointerTracking = false;
                    break;
            }

            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI(Input.mousePosition, -1))
            {
                return;
            }

            isStatsPointerTracking = true;
            statsPointerStartPosition = Input.mousePosition;
            TryUnlockEndTurnFromScreenPosition(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0) && isStatsPointerTracking)
        {
            isStatsPointerTracking = false;
            TryOpenEnemyStatsFromScreenPosition(Input.mousePosition, (Vector2)Input.mousePosition - statsPointerStartPosition);
        }
    }

    private void TryUnlockEndTurnFromScreenPosition(Vector2 screenPosition)
    {
        if (gameTurnManager == null || gameTurnManager.Board == null || gameTurnManager.CanEndTurn)
        {
            return;
        }

        if (!TryGetGridCellFromScreenPosition(screenPosition, out _))
        {
            return;
        }

        gameTurnManager.UnlockEndTurnFromGameTouch();
    }

    private void TryOpenEnemyStatsFromScreenPosition(Vector2 screenPosition, Vector2 pointerDelta)
    {
        if (pointerDelta.magnitude > statsMenuClickThreshold || gameTurnManager?.Board == null)
        {
            return;
        }

        if (!TryGetGridCellFromScreenPosition(screenPosition, out Vector2Int gridPosition))
        {
            return;
        }

        if (observedCharacter != null && observedCharacter.GridPosition == gridPosition)
        {
            ToggleCharacterStatsMenu(false);
            return;
        }

        if (!gameTurnManager.Board.TryGetEnemy(gridPosition, out Enemy enemy) || enemy == null)
        {
            return;
        }

        HideAbilityCheck();
        characterStatsMenu?.Hide();
        enemyStatsMenu?.ShowEnemy(enemy);
    }

    private void ToggleCharacterStatsMenu(bool playClickSound)
    {
        if (playClickSound)
        {
            SoundManager.Instance?.PlayClick();
        }

        if (characterStatsMenu != null && characterStatsMenu.gameObject.activeSelf)
        {
            characterStatsMenu.Hide();
            return;
        }

        if (observedCharacter == null)
        {
            return;
        }

        HideAbilityCheck();
        enemyStatsMenu?.Hide();
        characterStatsMenu?.ShowCharacter(observedCharacter);
    }

    private bool TryGetGridCellFromScreenPosition(Vector2 screenPosition, out Vector2Int gridPosition)
    {
        gridPosition = default;
        if (gameTurnManager?.Board == null)
        {
            return false;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return false;
        }

        Plane boardPlane = new Plane(gameTurnManager.Board.transform.up, gameTurnManager.Board.transform.position);
        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        if (!boardPlane.Raycast(ray, out float distance))
        {
            return false;
        }

        Vector3 hitPoint = ray.GetPoint(distance);
        return gameTurnManager.Board.TryWorldToGridPosition(hitPoint, out gridPosition);
    }

    private bool IsAbilityTargetingActive()
    {
        if (observedCharacter == null || gameTurnManager == null)
        {
            return false;
        }

        if (gameTurnManager.PendingCellTargetAbilityIndex >= 0)
        {
            return true;
        }

        for (int index = 0; index < observedCharacter.Abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = observedCharacter.GetAbility(index);
            if (runtime?.Definition != null && runtime.Definition.SupportsCellSelectionWhileActive(observedCharacter, runtime))
            {
                return true;
            }
        }

        return false;
    }

    private void ShowRewardCheck(RewardOffer rewardOffer, bool canChoose)
    {
        CacheRewardCheckMenu();
        CacheRewardTypeIcons();
        HideAbilityCheck();
        if (rewardCheckMenu == null || rewardCheckCard == null || rewardOffer == null)
        {
            return;
        }

        RewardButtonStyle style = rewardOffer.Kind == RewardOfferKind.Item ? itemRewardStyle : powerRewardStyle;
        RewardButtonTheme theme = rewardOffer.Kind == RewardOfferKind.Item ? itemRewardTheme : powerRewardTheme;
        Sprite typeSprite = ResolveTypeIconSprite(rewardOffer.IconKind);
        rewardCheckCard.Bind(rewardOffer, style, theme, typeSprite, null);

        if (rewardCheckChooseButton != null)
        {
            rewardCheckChooseButton.gameObject.SetActive(canChoose);
        }

        rewardCheckMenu.SetActive(true);
    }

    private void HideRewardCheck()
    {
        currentPreviewedItemDefinition = null;
        if (rewardCheckMenu != null)
        {
            rewardCheckMenu.SetActive(false);
        }
    }

    private void ShowAbilityCheck(AbilityButtonUI sourceButton, int selectedIndex)
    {
        CacheAbilityCheckMenu();
        CacheRewardTypeIcons();
        if (abilityCheckMenu == null || abilityCheckCard == null || sourceButton == null)
        {
            return;
        }

        RewardOffer baseOffer = BuildAbilityBaseOffer(sourceButton.BoundDefinition);
        if (baseOffer == null)
        {
            return;
        }

        currentAbilityCheckSourceButton = sourceButton;
        currentAbilityCheckOffers.Clear();
        currentAbilityCheckOffers.Add(baseOffer);
        CollectAcquiredUpgradeOffers(sourceButton.BoundDefinition, currentAbilityCheckOffers);

        abilityCheckMenu.SetActive(true);
        UpdateAbilityCheckSelection(Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, currentAbilityCheckOffers.Count - 1)));
    }

    private void HideAbilityCheck()
    {
        currentAbilityCheckSourceButton = null;
        currentAbilityCheckOffers.Clear();
        if (abilityCheckMenu != null)
        {
            abilityCheckMenu.SetActive(false);
        }
    }

    private void UpdateAbilityCheckSelection(int selectedIndex)
    {
        if (abilityCheckCard == null || currentAbilityCheckOffers.Count == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(selectedIndex, 0, currentAbilityCheckOffers.Count - 1);
        RewardOffer rewardOffer = currentAbilityCheckOffers[clampedIndex];
        RewardButtonStyle style = rewardOffer.Kind == RewardOfferKind.Item ? itemRewardStyle : powerRewardStyle;
        RewardButtonTheme theme = rewardOffer.Kind == RewardOfferKind.Item ? itemRewardTheme : powerRewardTheme;
        abilityCheckCard.Bind(rewardOffer, style, theme, ResolveTypeIconSprite(rewardOffer.IconKind), null);

        for (int index = 0; index < abilityCheckOptionButtons.Count; index++)
        {
            bool isAvailable = index < currentAbilityCheckOffers.Count;
            if (abilityCheckOptionButtons[index] != null)
            {
                abilityCheckOptionButtons[index].gameObject.SetActive(isAvailable);
            }

            if (index < abilityCheckOptionSelectors.Count && abilityCheckOptionSelectors[index] != null)
            {
                abilityCheckOptionSelectors[index].SetActive(isAvailable && index == clampedIndex);
            }
        }
    }

    private RewardOffer BuildAbilityBaseOffer(AbilityDefinition abilityDefinition)
    {
        if (abilityDefinition == null)
        {
            return null;
        }

        return new RewardOffer
        {
            Id = $"ability_preview_{abilityDefinition.name}",
            Title = abilityDefinition.AbilityName,
            Description = ResolveAbilityDescription(abilityDefinition),
            Artwork = abilityDefinition.Icon,
            Kind = RewardOfferKind.AbilityUnlock,
            IconKind = GetIconKindForAbilityCategory(abilityDefinition.Category),
            ShowPowerStroke = false,
            Ability = abilityDefinition
        };
    }

    private string ResolveAbilityDescription(AbilityDefinition abilityDefinition)
    {
        if (abilityDefinition == null || observedCharacter?.Data == null)
        {
            return string.Empty;
        }

        IReadOnlyList<AbilityRewardDefinition> unlockableRewards = observedCharacter.Data.UnlockableAbilityRewards;
        for (int index = 0; index < unlockableRewards.Count; index++)
        {
            AbilityRewardDefinition rewardDefinition = unlockableRewards[index];
            if (rewardDefinition != null && rewardDefinition.Ability == abilityDefinition)
            {
                return rewardDefinition.RewardDescription;
            }
        }

        return string.Empty;
    }

    private void CollectAcquiredUpgradeOffers(AbilityDefinition abilityDefinition, List<RewardOffer> offers)
    {
        if (abilityDefinition == null || offers == null || observedCharacter == null)
        {
            return;
        }

        IReadOnlyList<AbilityUpgradeRewardDefinition> linkedUpgrades = abilityDefinition.LinkedUpgradeRewards;
        for (int index = 0; index < linkedUpgrades.Count && offers.Count < 6; index++)
        {
            AbilityUpgradeRewardDefinition upgradeDefinition = linkedUpgrades[index];
            if (upgradeDefinition == null)
            {
                continue;
            }

            int stacks = observedCharacter.GetUpgradeStacks(upgradeDefinition.UpgradeKey);
            if (stacks <= 0)
            {
                continue;
            }

            RewardOffer offer = upgradeDefinition.CreateOffer();
            offer.Description = BuildUpgradeDisplayDescription(upgradeDefinition, stacks);
            offers.Add(offer);
        }
    }

    private string BuildUpgradeDisplayDescription(AbilityUpgradeRewardDefinition upgradeDefinition, int stacks)
    {
        if (upgradeDefinition == null)
        {
            return string.Empty;
        }

        if (stacks <= 1 || !upgradeDefinition.Stackable)
        {
            return upgradeDefinition.RewardDescription;
        }

        switch (upgradeDefinition.UpgradeKey)
        {
            case AbilityUpgradeKey.GhostStepsAdrenaline:
                return $"If Pandora dealt damage to at least one enemy with Ghost Steps, she has a {5 * stacks}% chance to recover 1 movement point.";
            case AbilityUpgradeKey.SpinningBladesSharpening:
                return $"Increase Spinning Blades damage by {stacks}.";
            case AbilityUpgradeKey.AssassinsRushShadowPulse:
                return $"Increase Assassin's Rush range by {stacks}.";
            case AbilityUpgradeKey.AssassinsRushTasteOfBlood:
                return $"After using Assassin's Rush, Pandora gains {stacks} bonus damage for her next attack.";
            case AbilityUpgradeKey.NighttimeMenaceShadowArea:
                return $"Increase Nighttime Menace range by {stacks}.";
            case AbilityUpgradeKey.RoyalDaggerBlessedBlade:
                return $"Increase Royal Dagger damage by {2 * stacks}.";
            case AbilityUpgradeKey.RoyalDaggerRoyalBlessing:
                return $"Pandora recovers {2 * stacks} health when she kills an enemy with Royal Dagger.";
            case AbilityUpgradeKey.SacredCrossbowLightBolt:
                return $"Sacred Crossbow gains +{stacks} damage for each target it has already pierced.";
            case AbilityUpgradeKey.RainOfBoltsIronBolts:
                return $"Increase Rain of Bolts damage by {stacks}.";
            case AbilityUpgradeKey.RainOfBoltsCloudySky:
                return $"Increase Rain of Bolts radius by {stacks}.";
            case AbilityUpgradeKey.RainOfBoltsLostBolt:
                return $"Add {2 * stacks} extra random bolts outside Rain of Bolts' initial area.";
            case AbilityUpgradeKey.WolfStepQuickSteps:
                return $"Wolf Step grants {2 + stacks} movement-free steps.";
            default:
                return upgradeDefinition.RewardDescription.Replace("(Stackable).", string.Empty).Replace("(Cumulable).", string.Empty).Trim();
        }
    }

    private void ShowSwitchAbilityMenu(RewardOffer rewardOffer, AbilityDefinition oldAbility)
    {
        CacheSwitchAbilityMenu();
        if (switchAbilityMenu == null || rewardOffer?.Ability == null || oldAbility == null)
        {
            return;
        }

        if (switchAbilityInfoText != null)
        {
            switchAbilityInfoText.text = $"REPLACE \"{oldAbility.AbilityName.ToUpperInvariant()}\" WITH \"{rewardOffer.Ability.AbilityName.ToUpperInvariant()}\"";
        }

        if (switchAbilityNewIcon != null)
        {
            switchAbilityNewIcon.sprite = rewardOffer.Ability.Icon;
            switchAbilityNewIcon.enabled = rewardOffer.Ability.Icon != null;
        }

        if (switchAbilityOldIcon != null)
        {
            switchAbilityOldIcon.sprite = oldAbility.Icon;
            switchAbilityOldIcon.enabled = oldAbility.Icon != null;
        }

        switchAbilityMenu.SetActive(true);
    }

    private void HideSwitchAbilityMenu()
    {
        pendingAbilityReplacementOldAbility = null;
        if (switchAbilityMenu != null)
        {
            switchAbilityMenu.SetActive(false);
        }
    }

    private bool TryPrepareAbilityReplacementConfirmation(RewardOffer rewardOffer)
    {
        pendingAbilityReplacementOldAbility = null;
        if (rewardOffer == null
            || rewardOffer.Kind != RewardOfferKind.AbilityUnlock
            || rewardOffer.Ability == null
            || observedCharacter == null)
        {
            return false;
        }

        AbilityDefinition currentlyEquippedAbility = GetEquippedAbilityForCategory(rewardOffer.Ability.Category);
        if (currentlyEquippedAbility == null || currentlyEquippedAbility == rewardOffer.Ability)
        {
            return false;
        }

        pendingAbilityReplacementOldAbility = currentlyEquippedAbility;
        return true;
    }

    private AbilityDefinition GetEquippedAbilityForCategory(AbilityCategory category)
    {
        if (observedCharacter?.RunRewardState != null)
        {
            AbilityDefinition equippedFromRunState = observedCharacter.RunRewardState.GetEquippedAbility(category);
            if (equippedFromRunState != null)
            {
                return equippedFromRunState;
            }
        }

        for (int index = 0; observedCharacter != null && index < observedCharacter.Abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = observedCharacter.GetAbility(index);
            if (runtime?.Definition != null && runtime.Definition.Category == category)
            {
                return runtime.Definition;
            }
        }

        return null;
    }

    private void ConfirmPendingRewardSelection()
    {
        if (pendingRewardConfirmation == null)
        {
            HideRewardCheck();
            HideSwitchAbilityMenu();
            return;
        }

        RewardOffer rewardToConfirm = pendingRewardConfirmation;
        pendingRewardConfirmation = null;
        pendingAbilityReplacementOldAbility = null;
        HideRewardCheck();
        HideSwitchAbilityMenu();
        HandleRewardSelected(rewardToConfirm);
    }

    private Sprite ResolveTypeIconSprite(RewardPresentationIconKind iconKind)
    {
        switch (iconKind)
        {
            case RewardPresentationIconKind.BasicAttack:
                return basicAttackRewardIcon;
            case RewardPresentationIconKind.MobilitySkill:
                return mobilityRewardIcon;
            case RewardPresentationIconKind.SpecialPower:
                return specialRewardIcon;
            default:
                return objectRewardIcon;
        }
    }

    private static RewardPresentationIconKind GetIconKindForAbilityCategory(AbilityCategory category)
    {
        switch (category)
        {
            case AbilityCategory.MobilitySkill:
                return RewardPresentationIconKind.MobilitySkill;
            case AbilityCategory.SpecialPower:
                return RewardPresentationIconKind.SpecialPower;
            default:
                return RewardPresentationIconKind.BasicAttack;
        }
    }

    private static void SetMobilityIconColor(GameObject icon, Color color)
    {
        if (icon == null)
        {
            return;
        }

        Graphic graphic = icon.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.color = color;
            return;
        }

        SpriteRenderer spriteRenderer = icon.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
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
            if (components[index] != null && components[index].name == objectName)
            {
                return components[index];
            }
        }

        return null;
    }

    private static bool IsPointerOverUI(Vector2 screenPosition, int fingerId)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (fingerId >= 0)
        {
            return EventSystem.current.IsPointerOverGameObject(fingerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }
}
