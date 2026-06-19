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
    [SerializeField] private GameObject rewardsMenu;
    [SerializeField] private Button ignoreRewardsButton;
    [SerializeField] private GameObject targetCellIndicatorPrefab;
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
    [SerializeField] private float statsMenuClickThreshold = 16f;

    private readonly List<GameObject> mobilityIcons = new List<GameObject>();
    private readonly List<GameObject> itemIcons = new List<GameObject>();
    private readonly Dictionary<ItemRewardKey, ItemIconUI> itemIconsByKey = new Dictionary<ItemRewardKey, ItemIconUI>();
    private readonly List<AbilityButtonUI> abilityButtons = new List<AbilityButtonUI>();
    private readonly List<RewardButtonUI> rewardCards = new List<RewardButtonUI>();
    private readonly List<GameObject> targetCellIndicators = new List<GameObject>();
    private Character observedCharacter;
    private Action<RewardOffer> onRewardSelected;
    private Action onRewardsIgnored;
    private RewardButtonStyle powerRewardStyle;
    private RewardButtonStyle itemRewardStyle;
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
        if (rewardsMenu != null)
        {
            rewardsMenu.SetActive(false);
        }

        HideRewardCheck();
        HideYesNoPrompt();
        HideLoseMenu();
        HideAbilityCheck();
        HideSwitchAbilityMenu();
    }

    private void OnEnable()
    {
        if (gameTurnManager != null)
        {
            gameTurnManager.TurnChanged += HandleTurnChanged;
            gameTurnManager.PendingAbilityChanged += HandlePendingAbilityChanged;
            gameTurnManager.EndTurnAvailabilityChanged += HandleEndTurnAvailabilityChanged;
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
        }

        ClearTargetCellIndicators();
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
            Sprite typeSprite = rewardOffer != null ? ResolveTypeIconSprite(rewardOffer.IconKind) : null;
            rewardCards[index].Bind(rewardOffer, style, typeSprite, HandleRewardPreviewRequested);
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

        RebuildMobilityBar();
        RefreshAbilityButtons();
        RefreshItemsList();
        RefreshFooterCharacterInfo();
        RefreshTargetCellIndicators();
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

        ClearTargetCellIndicators();
    }

    private void HandleMovementPointsChanged(Character character)
    {
        RefreshMobilityBar();
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

        if (observedCharacter == null || mobilityIconPrefab == null)
        {
            return;
        }

        for (int index = 0; index < observedCharacter.BaseMovementPoints; index++)
        {
            GameObject icon = Instantiate(mobilityIconPrefab, mobilityBar);
            icon.name = $"iMobility_{index + 1}";
            mobilityIcons.Add(icon);
        }

        RefreshMobilityBar();
    }

    private void RefreshMobilityBar()
    {
        if (observedCharacter == null)
        {
            return;
        }

        for (int index = 0; index < mobilityIcons.Count; index++)
        {
            bool isAvailable = index < observedCharacter.RemainingMovementPoints;
            SetMobilityIconColor(mobilityIcons[index], isAvailable ? mobilityAvailableColor : mobilityConsumedColor);
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
                powerRewardStyle = powerCard.CaptureStyle();
            }

            if (itemCard != null)
            {
                itemRewardStyle = itemCard.CaptureStyle();
                objectRewardIcon = itemCard.CurrentTypeSprite;
            }
        }
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

        for (int index = 0; index < abilityButtons.Count; index++)
        {
            AbilityButtonUI button = abilityButtons[index];
            if (button == null)
            {
                continue;
            }

            button.Setup(gameTurnManager, observedCharacter, index, HandleAbilityButtonPrimaryClick, HandleAbilityButtonLongPress);
        }
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

            itemIcon.Bind(itemRewardDefinition, HandleItemIconClicked);
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
        }
        else if (Input.GetMouseButtonUp(0) && isStatsPointerTracking)
        {
            isStatsPointerTracking = false;
            TryOpenEnemyStatsFromScreenPosition(Input.mousePosition, (Vector2)Input.mousePosition - statsPointerStartPosition);
        }
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
        Sprite typeSprite = ResolveTypeIconSprite(rewardOffer.IconKind);
        rewardCheckCard.Bind(rewardOffer, style, typeSprite, null);

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
        abilityCheckCard.Bind(rewardOffer, style, ResolveTypeIconSprite(rewardOffer.IconKind), null);

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
