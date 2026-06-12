using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

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

    private void HandleEndTurnClicked()
    {
        SoundManager.Instance?.PlayClick();
        gameTurnManager?.RequestEndTurn();
    }

    public void ShowRewards(IReadOnlyList<RewardOffer> rewardOffers, Action<RewardOffer> rewardSelectedCallback, Action rewardsIgnoredCallback)
    {
        CacheRewardsMenu();
        CacheRewardTypeIcons();

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

    private void HandleTurnChanged(TurnSide turnSide)
    {
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

    private void HandleRewardSelected(RewardOffer rewardOffer)
    {
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
            if (button == null || button.BoundDefinition == null || button.TypeSprite == null)
            {
                continue;
            }

            switch (button.BoundDefinition.Category)
            {
                case AbilityCategory.BasicAttack:
                    basicAttackRewardIcon = button.TypeSprite;
                    break;
                case AbilityCategory.MobilitySkill:
                    mobilityRewardIcon = button.TypeSprite;
                    break;
                case AbilityCategory.SpecialPower:
                    specialRewardIcon = button.TypeSprite;
                    break;
            }
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
}
