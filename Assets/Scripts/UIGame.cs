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
    [SerializeField] private float statsMenuClickThreshold = 16f;

    private readonly List<GameObject> mobilityIcons = new List<GameObject>();
    private readonly List<GameObject> itemIcons = new List<GameObject>();
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
        if (rewardsMenu != null)
        {
            rewardsMenu.SetActive(false);
        }
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
            rewardCards[index].Bind(rewardOffer, style, typeSprite, HandleRewardSelected);
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
        UpdateEndTurnButtonVisibility();
        RefreshTargetCellIndicators();
    }

    public void HideStatsMenus()
    {
        enemyStatsMenu?.Hide();
        characterStatsMenu?.Hide();
    }

    private void HandleTurnChanged(TurnSide turnSide)
    {
        HideStatsMenus();

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

        if (observedCharacter != null)
        {
            observedCharacter.MovementPointsChanged += HandleMovementPointsChanged;
            observedCharacter.AbilitiesChanged += HandleAbilitiesChanged;
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
        SoundManager.Instance?.PlayClick();
        if (observedCharacter == null)
        {
            return;
        }

        enemyStatsMenu?.Hide();
        characterStatsMenu?.ShowCharacter(observedCharacter);
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
        SoundManager.Instance?.PlayClick();
        Action<RewardOffer> selectedCallback = onRewardSelected;
        HideRewards();
        selectedCallback?.Invoke(rewardOffer);
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
            && !gameTurnManager.IsArenaTransitionRunning;
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

            button.Setup(gameTurnManager, observedCharacter, index);
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

            itemIcon.Bind(itemRewardDefinition.Artwork);
            itemIcons.Add(itemIconObject);
        }
    }

    private void RefreshFooterCharacterInfo()
    {
        footerUI?.RefreshCharacter(observedCharacter);
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

        if (!gameTurnManager.Board.TryGetEnemy(gridPosition, out Enemy enemy) || enemy == null)
        {
            return;
        }

        characterStatsMenu?.Hide();
        enemyStatsMenu?.ShowEnemy(enemy);
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
