using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum TurnSide
{
    Player,
    Enemy
}

public class GameTurnManager : MonoBehaviour
{
    [SerializeField] private BoardManager board;
    [SerializeField] private UIGame uiGame;
    [SerializeField] private SoundManager soundManager;
    [SerializeField] private KeyCode debugEndTurnKey = KeyCode.Space;
    [SerializeField] private float swipeThreshold = 60f;
    [SerializeField] private float endTurnLockDuration = 6f;
    [SerializeField] private float enemyTurnStartDelay = 0.5f;
    [SerializeField] private float rewardMenuDelay = 2f;
    [SerializeField] private float loseMenuDelay = 1.5f;
#if UNITY_EDITOR
    private const KeyCode DebugRewardMenuKey = KeyCode.K;
    private const KeyCode DebugWinArenaKey = KeyCode.Z;
    private const KeyCode DebugOpenShopKey = KeyCode.Y;
    private const KeyCode DebugGiveMoneyKey = KeyCode.M;
    private const KeyCode DebugFullHealKey = KeyCode.O;
#endif

    private bool isEnemyTurnRunning;
    private bool isPointerTracking;
    private bool hasStarted;
    private bool hasPlayerActedThisTurn;
    private bool hasPlayerMovedThisTurn;
    private bool isArenaTransitionRunning;
    private bool isRewardMenuOpen;
    private bool isLoseMenuOpen;
    private bool combatStartChoiceResolved;
    private bool combatStartChoiceAccepted;
    private bool canEndTurn = true;
    private Coroutine endTurnUnlockCoroutine;
    private Coroutine nextArenaCoroutine;
    private Coroutine combatStartSequenceCoroutine;
    private Coroutine enemyTurnCoroutine;
    private Coroutine loseMenuCoroutine;
    private Coroutine debugRewardSequenceCoroutine;
    private Vector2 pointerStartPosition;
    private int pendingCellTargetAbilityIndex = -1;
    private ItemRewardDefinition activeCombatStartPrompt;
    private Character trackedCharacter;
    private bool loadedDieRefreshUsedForCurrentRewards;
    private bool combatStartRelocationSelectionActive;
    private bool combatStartRelocationResolved;
    private bool combatStartRelocationMoved;
    private bool currentRewardMenuIsDebug;
    private List<TourmentUnlockResult> pendingLoseUnlockResults;
    private UIHomeController homeMenu;
    private int remainingDebugRewardChoices;

    public TurnSide CurrentTurn { get; private set; } = TurnSide.Player;
    public BoardManager Board => board;
    public int PendingCellTargetAbilityIndex => pendingCellTargetAbilityIndex;
    public bool CanEndTurn => canEndTurn;
    public bool IsRewardMenuOpen => isRewardMenuOpen;
    public bool IsArenaTransitionRunning => isArenaTransitionRunning;
    public bool IsLoseMenuOpen => isLoseMenuOpen;
    public bool IsCombatStartRelocationSelectionActive => combatStartRelocationSelectionActive;
    public event Action<TurnSide> TurnChanged;
    public event Action<int> PendingAbilityChanged;
    public event Action<bool> EndTurnAvailabilityChanged;
    public event Action<CharacterAbilityRuntime> NoValidTargetFeedbackRequested;

    private void Start()
    {
        Debug.Log($"[Pouet Startup] GameTurnManager.Start begin. boardAssigned={board != null} uiGameAssigned={uiGame != null} soundManagerAssigned={soundManager != null}", this);
        if (board == null)
        {
            board = GetComponent<BoardManager>();
        }

        if (uiGame == null)
        {
            uiGame = FindFirstObjectByType<UIGame>(FindObjectsInactive.Include);
        }

        if (soundManager == null)
        {
            soundManager = GetComponent<SoundManager>();
            if (soundManager == null)
            {
                soundManager = FindFirstObjectByType<SoundManager>();
            }
        }

        if (board != null)
        {
            board.AllEnemiesDefeated += HandleAllEnemiesDefeated;
        }

        hasStarted = true;
        homeMenu = FindFirstObjectByType<UIHomeController>(FindObjectsInactive.Include);
        Debug.Log($"[Pouet Startup] GameTurnManager.Start resolved references. board={(board != null ? board.name : "null")} playerExists={(board != null && board.Player != null)} homeMenu={(homeMenu != null ? homeMenu.name : "null")} generateOnStart={(board != null && board.GenerateOnStart)}", this);
        if (homeMenu != null)
        {
            Debug.Log("[Pouet Startup] GameTurnManager.Start showing home menu path.", this);
            homeMenu.Initialize(
                board,
                uiGame,
                soundManager);
            homeMenu.ShowDefault();
            return;
        }

        if (board != null && board.GenerateOnStart && board.Player == null)
        {
            Debug.Log("[Pouet Startup] GameTurnManager.Start auto-generate path.", this);
            board.ResetArenaProgression();
            board.GenerateBoard();
            return;
        }

        Debug.Log("[Pouet Startup] GameTurnManager.Start direct combat path.", this);
        soundManager?.PlayArenaMusic(board != null ? board.CurrentCombatMusic : null);
        BindToCurrentCharacter();
        StartCombatEntryFlow();
    }

    private void OnDestroy()
    {
        if (board != null)
        {
            board.AllEnemiesDefeated -= HandleAllEnemiesDefeated;
        }

        UnbindTrackedCharacter();
    }

    private void Update()
    {
#if UNITY_EDITOR
        HandleDebugKeyboardInput();
#endif

        if (board == null || board.Player == null || board.Player.ControlledCharacter == null || isEnemyTurnRunning || isArenaTransitionRunning || isLoseMenuOpen)
        {
            return;
        }

        if (homeMenu != null && homeMenu.IsVisible)
        {
            return;
        }

        if (combatStartRelocationSelectionActive)
        {
            HandleCombatStartRelocationInput();
            return;
        }

        if (isRewardMenuOpen)
        {
            return;
        }

        if (CurrentTurn != TurnSide.Player)
        {
            return;
        }

        HandleSwipeInput();
    }

    public void RequestEndTurn()
    {
        if (CurrentTurn != TurnSide.Player || isEnemyTurnRunning || isArenaTransitionRunning || !canEndTurn)
        {
            return;
        }

        Character controlledCharacter = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        ClearPendingAbility();
        board?.HandlePlayerTurnEnded(controlledCharacter);
        if (controlledCharacter == null || controlledCharacter.CurrentHealth <= 0)
        {
            return;
        }

        controlledCharacter.HandlePlayerTurnEnded();
        if (controlledCharacter.CurrentHealth <= 0)
        {
            return;
        }

        enemyTurnCoroutine = StartCoroutine(RunEnemyTurn());
    }

    public void UnlockEndTurnFromGameTouch()
    {
        if (CurrentTurn != TurnSide.Player || isEnemyTurnRunning || isArenaTransitionRunning || isRewardMenuOpen || isLoseMenuOpen)
        {
            return;
        }

        StopEndTurnUnlockTimer();
        SetCanEndTurn(true);
    }

    public bool CanUseLoadedDieReroll()
    {
        Character currentCharacter = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        return isRewardMenuOpen
            && currentCharacter != null
            && currentCharacter.HasItem(ItemRewardKey.LoadedDie)
            && !loadedDieRefreshUsedForCurrentRewards;
    }

    public void RequestLoadedDieReroll()
    {
        if (!CanUseLoadedDieReroll() || board == null)
        {
            return;
        }

        loadedDieRefreshUsedForCurrentRewards = true;
        List<RewardOffer> rewardChoices = board.GenerateRewardChoices();
        if (rewardChoices.Count == 0)
        {
            if (currentRewardMenuIsDebug)
            {
                HandleDebugRewardIgnored();
            }
            else
            {
                CompleteArenaTransition();
            }

            return;
        }

        if (currentRewardMenuIsDebug)
        {
            uiGame?.ShowRewards(rewardChoices, HandleDebugRewardSelected, HandleDebugRewardIgnored);
            soundManager?.PlayVictoryChoiceMusic();
            return;
        }

        uiGame?.ShowRewards(rewardChoices, HandleRewardSelected, HandleRewardsIgnored);
        soundManager?.PlayVictoryChoiceMusic();
    }

    public void RestartForNewBoard()
    {
        if (!hasStarted)
        {
            return;
        }

        Debug.Log($"[Pouet Startup] RestartForNewBoard called. playerExists={(board != null && board.Player != null)} currentTurn={CurrentTurn} isRewardMenuOpen={isRewardMenuOpen} isLoseMenuOpen={isLoseMenuOpen}", this);

        StopAllCoroutines();
        isEnemyTurnRunning = false;
        isPointerTracking = false;
        isArenaTransitionRunning = false;
        isRewardMenuOpen = false;
        isLoseMenuOpen = false;
        nextArenaCoroutine = null;
        combatStartSequenceCoroutine = null;
        enemyTurnCoroutine = null;
        loseMenuCoroutine = null;
        activeCombatStartPrompt = null;
        combatStartChoiceResolved = false;
        combatStartChoiceAccepted = false;
        loadedDieRefreshUsedForCurrentRewards = false;
        combatStartRelocationSelectionActive = false;
        combatStartRelocationResolved = false;
        combatStartRelocationMoved = false;
        currentRewardMenuIsDebug = false;
        ClearPendingAbility();
        uiGame?.HideRewards();
        uiGame?.HideShop();
        uiGame?.HideYesNoPrompt();
        uiGame?.HideLoseMenu();
        uiGame?.HideWinMenu();
        uiGame?.HideUnlockMenu();
        soundManager?.PlayArenaMusic(board != null ? board.CurrentCombatMusic : null);
        BindToCurrentCharacter();
        StartCombatEntryFlow();
    }

    public bool RequestAbilityUse(int abilityIndex)
    {
        Character character = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        if (CurrentTurn != TurnSide.Player || isEnemyTurnRunning || isArenaTransitionRunning || character == null)
        {
            return false;
        }

        CharacterAbilityRuntime ability = character.GetAbility(abilityIndex);
        if (ability == null || (!ability.IsActive && !ability.IsUsable(character)))
        {
            return false;
        }

        if (ability.TargetingMode == AbilityTargetingMode.FreeCell && !ability.IsActive)
        {
            if (ability.Definition != null && ability.Definition.TryGetAutomaticTargetCell(character, ability, out Vector2Int automaticTargetCell))
            {
                bool automaticTargetUsed = character.TryUseAbility(abilityIndex, automaticTargetCell);
                if (automaticTargetUsed)
                {
                    RegisterPlayerAction();
                    ClearPendingAbility();
                }

                return automaticTargetUsed;
            }

            bool canAutoUseSingleTarget = ability.Definition != null
                && (ability.Definition.Category == AbilityCategory.BasicAttack
                    || ability.Definition.ShouldAutoUseWhenOnlyOneValidTarget(character, ability));
            if (canAutoUseSingleTarget
                && TryGetSingleValidTargetCell(character, ability, out Vector2Int singleTargetCell)
                && ShouldAutoUseSingleTargetCell(character, ability, singleTargetCell))
            {
                bool singleTargetUsed = character.TryUseAbility(abilityIndex, singleTargetCell);
                if (singleTargetUsed)
                {
                    RegisterPlayerAction();
                    ClearPendingAbility();
                }

                return singleTargetUsed;
            }

            if (!HasAnyValidTargetCell(character, ability))
            {
                NoValidTargetFeedbackRequested?.Invoke(ability);
                return false;
            }

            if (pendingCellTargetAbilityIndex == abilityIndex)
            {
                ClearPendingAbility();
            }
            else
            {
                pendingCellTargetAbilityIndex = abilityIndex;
                PendingAbilityChanged?.Invoke(pendingCellTargetAbilityIndex);
            }

            return true;
        }

        bool used = character.TryUseAbility(abilityIndex);
        if (used)
        {
            RegisterPlayerAction();
            ClearPendingAbility();
        }

        return used;
    }

    private bool TryGetSingleValidTargetCell(Character character, CharacterAbilityRuntime ability, out Vector2Int targetCell)
    {
        targetCell = default;
        if (character == null || ability?.Definition == null || board == null)
        {
            return false;
        }

        bool foundOne = false;
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                Vector2Int candidateCell = new Vector2Int(x, y);
                if (!ability.Definition.CanActivateOnCell(character, ability, candidateCell))
                {
                    continue;
                }

                if (foundOne)
                {
                    targetCell = default;
                    return false;
                }

                foundOne = true;
                targetCell = candidateCell;
            }
        }

        return foundOne;
    }

    private bool ShouldAutoUseSingleTargetCell(Character character, CharacterAbilityRuntime ability, Vector2Int targetCell)
    {
        if (character == null || ability?.Definition == null || board == null)
        {
            return false;
        }

        if (ability.Definition.Category == AbilityCategory.BasicAttack
            && board.TryGetBarrel(targetCell, out BarrelObstacle barrel)
            && barrel != null)
        {
            return false;
        }

        return true;
    }

    private bool HasAnyValidTargetCell(Character character, CharacterAbilityRuntime ability)
    {
        if (character == null || ability?.Definition == null || board == null)
        {
            return false;
        }

        if (ability.Definition.TryGetAutomaticTargetCell(character, ability, out _))
        {
            return true;
        }

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (ability.Definition.CanActivateOnCell(character, ability, new Vector2Int(x, y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool HasAnyUsableTarget(Character character, CharacterAbilityRuntime ability)
    {
        if (character == null || ability?.Definition == null)
        {
            return false;
        }

        if (ability.TargetingMode == AbilityTargetingMode.FreeCell && !ability.IsActive)
        {
            if (ability.Definition.TryGetAutomaticTargetCell(character, ability, out _))
            {
                return true;
            }

            return HasAnyValidTargetCell(character, ability);
        }

        if (ability.Definition.SupportsCellSelectionWhileActive(character, ability))
        {
            if (board == null)
            {
                return false;
            }

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (ability.Definition.CanActivateFromSelectedCell(character, ability, new Vector2Int(x, y)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        return true;
    }

    private void RequestAbilityUseForSlot(int slotIndex)
    {
        Character character = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        if (character == null)
        {
            return;
        }

        int runtimeIndex = character.GetRuntimeIndexForSlot(slotIndex);
        if (runtimeIndex < 0)
        {
            return;
        }

        RequestAbilityUse(runtimeIndex);
    }

    private void HandleDebugKeyboardInput()
    {
        if (Input.GetKeyDown(debugEndTurnKey))
        {
            RequestEndTurn();
            return;
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(DebugWinArenaKey))
        {
            RequestDebugWinArena();
            return;
        }

        if (Input.GetKeyDown(DebugRewardMenuKey))
        {
            RequestDebugRewardChoice();
            return;
        }

        if (Input.GetKeyDown(DebugOpenShopKey))
        {
            RequestDebugOpenShop();
            return;
        }

        if (IsDebugGiveMoneyPressed())
        {
            RequestDebugGiveMoney();
            return;
        }

        if (Input.GetKeyDown(DebugFullHealKey))
        {
            RequestDebugFullHeal();
            return;
        }
#endif

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            RequestAbilityUseForSlot(0);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            RequestAbilityUseForSlot(1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            RequestAbilityUseForSlot(2);
            return;
        }

        if (pendingCellTargetAbilityIndex >= 0)
        {
            return;
        }

        Vector2Int direction = Vector2Int.zero;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            direction = Vector2Int.down;
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            direction = Vector2Int.right;
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            direction = Vector2Int.up;
        }
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            direction = Vector2Int.left;
        }

        if (direction != Vector2Int.zero)
        {
            bool moved = board.Player.ControlledCharacter.TrySlide(direction);
            if (moved)
            {
                RegisterPlayerAction();
            }
        }
    }

#if UNITY_EDITOR
    private static bool IsDebugGiveMoneyPressed()
    {
        if (Input.GetKeyDown(DebugGiveMoneyKey))
        {
            return true;
        }

        string typedCharacters = Input.inputString;
        return !string.IsNullOrEmpty(typedCharacters)
            && (typedCharacters.IndexOf('m') >= 0 || typedCharacters.IndexOf('M') >= 0);
    }

    private void RequestDebugFullHeal()
    {
        Character controlledCharacter = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        if (controlledCharacter == null || controlledCharacter.CurrentHealth <= 0)
        {
            return;
        }

        controlledCharacter.Heal(controlledCharacter.MaxHealth, null, true);
    }
#endif

    private void HandleSwipeInput()
    {
        Character character = board.Player.ControlledCharacter;
        if (character == null || character.IsBusy)
        {
            return;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (!IsTargetingModeActive(character) && IsPointerOverUI(touch.position, touch.fingerId))
                    {
                        return;
                    }

                    isPointerTracking = true;
                    pointerStartPosition = touch.position;
                    if (pendingCellTargetAbilityIndex >= 0)
                    {
                        uiGame?.UpdateTargetedAbilityPreview(touch.position);
                    }
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (isPointerTracking && pendingCellTargetAbilityIndex >= 0)
                    {
                        uiGame?.UpdateTargetedAbilityPreview(touch.position);
                    }
                    break;
                case TouchPhase.Ended:
                    if (!isPointerTracking)
                    {
                        return;
                    }

                    isPointerTracking = false;
                    TryHandlePointerRelease(character, touch.position);
                    break;
                case TouchPhase.Canceled:
                    isPointerTracking = false;
                    break;
            }

            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (!IsTargetingModeActive(character) && IsPointerOverUI(Input.mousePosition, -1))
            {
                return;
            }

            isPointerTracking = true;
            pointerStartPosition = Input.mousePosition;
            if (pendingCellTargetAbilityIndex >= 0)
            {
                uiGame?.UpdateTargetedAbilityPreview(Input.mousePosition);
            }
        }
        else if (Input.GetMouseButton(0) && isPointerTracking && pendingCellTargetAbilityIndex >= 0)
        {
            uiGame?.UpdateTargetedAbilityPreview(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0) && isPointerTracking)
        {
            isPointerTracking = false;
            TryHandlePointerRelease(character, Input.mousePosition);
        }
    }

    private void TryHandlePointerRelease(Character character, Vector2 pointerEndPosition)
    {
        Vector2 delta = pointerEndPosition - pointerStartPosition;
        if (pendingCellTargetAbilityIndex >= 0)
        {
            TryUseTargetedAbility(character, pointerEndPosition);
            return;
        }

        if (delta.magnitude < swipeThreshold && TryUseActiveCellSelectableAbility(character, pointerEndPosition))
        {
            return;
        }

        TrySwipe(character, delta);
    }

    private void HandleCombatStartRelocationInput()
    {
        Character character = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        if (character == null || character.IsBusy)
        {
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

                    isPointerTracking = true;
                    pointerStartPosition = touch.position;
                    break;
                case TouchPhase.Ended:
                    if (!isPointerTracking)
                    {
                        return;
                    }

                    isPointerTracking = false;
                    TryResolveCombatStartRelocation(touch.position);
                    break;
                case TouchPhase.Canceled:
                    isPointerTracking = false;
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

            isPointerTracking = true;
            pointerStartPosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0) && isPointerTracking)
        {
            isPointerTracking = false;
            TryResolveCombatStartRelocation(Input.mousePosition);
        }
    }

    private void TryResolveCombatStartRelocation(Vector2 screenPosition)
    {
        Character character = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        if (character == null || !TryGetTargetCellFromScreen(screenPosition, out Vector2Int targetCell))
        {
            return;
        }

        if (!CanSelectCombatStartRelocationCell(targetCell))
        {
            return;
        }

        combatStartRelocationSelectionActive = false;
        combatStartRelocationResolved = true;
        combatStartRelocationMoved = targetCell != character.GridPosition && character.TryTeleportToImmediate(targetCell);
        uiGame?.RefreshTargetingIndicators();
    }

    private void TrySwipe(Character character, Vector2 delta)
    {
        if (delta.magnitude < swipeThreshold)
        {
            return;
        }

        Vector2Int direction = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
            ? (delta.x > 0f ? Vector2Int.right : Vector2Int.left)
            : (delta.y > 0f ? Vector2Int.down : Vector2Int.up);

        bool moved = character.TrySlide(direction);
        if (moved)
        {
            RegisterPlayerAction();
        }
    }

    private void TryUseTargetedAbility(Character character, Vector2 screenPosition)
    {
        if (character == null || board == null)
        {
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        Plane boardPlane = new Plane(board.transform.up, board.transform.position);
        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        if (!boardPlane.Raycast(ray, out float distance))
        {
            return;
        }

        Vector3 hitPoint = ray.GetPoint(distance);
        if (!board.TryWorldToGridPosition(hitPoint, out Vector2Int targetCell))
        {
            return;
        }

        CharacterAbilityRuntime runtime = character.GetAbility(pendingCellTargetAbilityIndex);
        if (character.TryUseAbility(pendingCellTargetAbilityIndex, targetCell))
        {
            uiGame?.HandleTargetedAbilityCommitted(runtime, targetCell);
            RegisterPlayerAction();
            ClearPendingAbility();
        }
    }

    private bool TryUseActiveCellSelectableAbility(Character character, Vector2 screenPosition)
    {
        if (character == null || board == null)
        {
            return false;
        }

        int abilityIndex = GetActiveCellSelectableAbilityIndex(character);
        if (abilityIndex < 0)
        {
            return false;
        }

        CharacterAbilityRuntime runtime = character.GetAbility(abilityIndex);
        if (runtime?.Definition == null)
        {
            return false;
        }

        if (!TryGetTargetCellFromScreen(screenPosition, out Vector2Int targetCell))
        {
            return false;
        }

        if (!runtime.Definition.TryActivateFromSelectedCell(character, runtime, targetCell))
        {
            return false;
        }

        runtime.ConsumePreparedActivation();
        if (runtime.Definition.DeactivateAfterSelectedCellActivation)
        {
            runtime.Deactivate(character);
        }

        character.RefreshAbilityState();
        RegisterPlayerAction();
        return true;
    }

    private int GetActiveCellSelectableAbilityIndex(Character character)
    {
        if (character == null)
        {
            return -1;
        }

        for (int index = 0; index < character.Abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = character.GetAbility(index);
            if (runtime?.Definition == null || !runtime.Definition.SupportsCellSelectionWhileActive(character, runtime))
            {
                continue;
            }

            return index;
        }

        return -1;
    }

    private bool IsTargetingModeActive(Character character)
    {
        return combatStartRelocationSelectionActive || pendingCellTargetAbilityIndex >= 0 || GetActiveCellSelectableAbilityIndex(character) >= 0;
    }

    public bool CanSelectCombatStartRelocationCell(Vector2Int targetCell)
    {
        Character character = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        return combatStartRelocationSelectionActive
            && board != null
            && character != null
            && board.IsValidSpringCoilDestination(character, targetCell, 2);
    }

    private bool TryGetTargetCellFromScreen(Vector2 screenPosition, out Vector2Int targetCell)
    {
        targetCell = default;
        if (board == null)
        {
            return false;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return false;
        }

        Plane boardPlane = new Plane(board.transform.up, board.transform.position);
        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        if (!boardPlane.Raycast(ray, out float distance))
        {
            return false;
        }

        Vector3 hitPoint = ray.GetPoint(distance);
        return board.TryWorldToGridPosition(hitPoint, out targetCell);
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

    private void BeginPlayerTurn()
    {
        CurrentTurn = TurnSide.Player;
        if (board != null && board.Player != null)
        {
            board.Player.ResetTurn();
            board.HandlePlayerTurnStarted(board.Player.ControlledCharacter);
        }

        hasPlayerActedThisTurn = false;
        hasPlayerMovedThisTurn = false;
        SetCanEndTurn(false);
        StartEndTurnUnlockTimer();
        TurnChanged?.Invoke(CurrentTurn);
    }

    private void StartCombatEntryFlow()
    {
        BindToCurrentCharacter();
        if (board == null)
        {
            BeginPlayerTurn();
            return;
        }

        Character currentCharacter = board.Player != null ? board.Player.ControlledCharacter : null;
        currentCharacter?.HandleCombatStarted();
        if (currentCharacter != null && currentCharacter.CurrentHealth <= 0)
        {
            return;
        }

        if (combatStartSequenceCoroutine != null)
        {
            StopCoroutine(combatStartSequenceCoroutine);
        }

        List<ItemRewardDefinition> promptDefinitions = board.GetCombatStartYesNoItemDefinitions();
        if (promptDefinitions == null || promptDefinitions.Count == 0 || uiGame == null)
        {
            isRewardMenuOpen = false;
            uiGame?.HideYesNoPrompt();
            combatStartSequenceCoroutine = StartCoroutine(BeginPlayerTurnAfterCombatStartActions());
            return;
        }

        isRewardMenuOpen = true;
        SetCanEndTurn(false);
        combatStartSequenceCoroutine = StartCoroutine(RunCombatStartSequence(promptDefinitions));
    }

    private IEnumerator RunCombatStartSequence(IReadOnlyList<ItemRewardDefinition> promptDefinitions)
    {
        for (int index = 0; index < promptDefinitions.Count; index++)
        {
            ItemRewardDefinition promptDefinition = promptDefinitions[index];
            if (promptDefinition == null)
            {
                continue;
            }

            activeCombatStartPrompt = promptDefinition;
            combatStartChoiceResolved = false;
            combatStartChoiceAccepted = false;
            uiGame.ShowYesNoPrompt(promptDefinition, HandleCombatStartPromptAccepted, HandleCombatStartPromptDeclined);

            while (!combatStartChoiceResolved)
            {
                yield return null;
            }

            uiGame.HideYesNoPrompt();

            if (!combatStartChoiceAccepted)
            {
                continue;
            }

            if (promptDefinition.ActivationSound != null)
            {
                soundManager?.Play2DSound(promptDefinition.ActivationSound, promptDefinition.ActivationSoundVolume);
            }

            switch (promptDefinition.YesNoEffect)
            {
                case ItemYesNoEffect.SpawnExtraEnemy:
                    if (board.ExtraEnemySpawnDelay > 0f)
                    {
                        yield return new WaitForSeconds(board.ExtraEnemySpawnDelay);
                    }

                    if (board.TrySpawnExtraEnemyWithDefaultFx(out _))
                    {
                        board.MarkBonusRewardForCurrentArena();
                    }

                    break;
                case ItemYesNoEffect.VoodooCharm:
                    Character voodooCharacter = board.Player != null ? board.Player.ControlledCharacter : null;
                    if (voodooCharacter != null && voodooCharacter.TrySpendHealth(3))
                    {
                        board.AddGold(4);
                    }

                    break;
                case ItemYesNoEffect.SpringCoil:
                    combatStartRelocationSelectionActive = true;
                    combatStartRelocationResolved = false;
                    combatStartRelocationMoved = false;
                    uiGame?.RefreshTargetingIndicators();
                    while (!combatStartRelocationResolved)
                    {
                        yield return null;
                    }

                    break;
                case ItemYesNoEffect.BlastStaff:
                    board.DamageAllEnemies(5, promptDefinition);
                    break;
            }

            if (promptDefinition.ConsumeOnYes)
            {
                board.RunRewardState?.RemoveItem(promptDefinition.ItemKey);
                board.Player?.ControlledCharacter?.ApplyRunRewardState(board.RunRewardState);
            }
        }

        activeCombatStartPrompt = null;
        isRewardMenuOpen = false;
        uiGame?.HideYesNoPrompt();
        uiGame?.RefreshTargetingIndicators();
        yield return BeginPlayerTurnAfterCombatStartActions();
        combatStartSequenceCoroutine = null;
    }

    private IEnumerator BeginPlayerTurnAfterCombatStartActions()
    {
        Character currentCharacter = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        if (currentCharacter != null && currentCharacter.HasItem(ItemRewardKey.Scarecrow))
        {
            board?.ApplyFearToAllEnemies(1);
        }

        if (board != null && currentCharacter != null && board.HasCombatStartEnemyActionsReady())
        {
            isEnemyTurnRunning = true;
            SetCanEndTurn(false);
            yield return board.ResolveCombatStartEnemyActions(currentCharacter);
            isEnemyTurnRunning = false;
        }

        BeginPlayerTurn();
        combatStartSequenceCoroutine = null;
    }

    private void HandleCombatStartPromptAccepted()
    {
        if (activeCombatStartPrompt == null)
        {
            return;
        }

        combatStartChoiceAccepted = true;
        combatStartChoiceResolved = true;
    }

    private void HandleCombatStartPromptDeclined()
    {
        if (activeCombatStartPrompt == null)
        {
            return;
        }

        combatStartChoiceAccepted = false;
        combatStartChoiceResolved = true;
    }

    private IEnumerator RunEnemyTurn()
    {
        isEnemyTurnRunning = true;
        CurrentTurn = TurnSide.Enemy;
        board?.Player?.ControlledCharacter?.BeginEnemyTurn();
        board?.HandleEnemyTurnStarted();
        StopEndTurnUnlockTimer();
        SetCanEndTurn(false);
        TurnChanged?.Invoke(CurrentTurn);

        if (enemyTurnStartDelay > 0f)
        {
            yield return new WaitForSeconds(enemyTurnStartDelay);
        }

        if (board == null)
        {
            isEnemyTurnRunning = false;
            enemyTurnCoroutine = null;
            yield break;
        }

        List<Enemy> enemiesToPlay = new List<Enemy>(board.SpawnedEnemies);
        for (int index = 0; index < enemiesToPlay.Count; index++)
        {
            Enemy enemy = enemiesToPlay[index];
            if (enemy == null)
            {
                continue;
            }

            yield return enemy.ExecuteTurn();
            yield return new WaitForSeconds(0.08f);
        }

        Character controlledCharacter = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        if (controlledCharacter != null)
        {
            controlledCharacter.HandleEnemyTurnEnded();
        }
        board?.HandleEnemyTurnEnded();
        isEnemyTurnRunning = false;
        enemyTurnCoroutine = null;

        if (isArenaTransitionRunning || isLoseMenuOpen)
        {
            yield break;
        }

        BeginPlayerTurn();
    }

    private void BindToCurrentCharacter()
    {
        Character currentCharacter = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        if (trackedCharacter == currentCharacter)
        {
            return;
        }

        UnbindTrackedCharacter();
        trackedCharacter = currentCharacter;
        if (trackedCharacter != null)
        {
            trackedCharacter.Died += HandleTrackedCharacterDied;
            trackedCharacter.Moved += HandleTrackedCharacterMoved;
        }
    }

    private void UnbindTrackedCharacter()
    {
        if (trackedCharacter != null)
        {
            trackedCharacter.Died -= HandleTrackedCharacterDied;
            trackedCharacter.Moved -= HandleTrackedCharacterMoved;
            trackedCharacter = null;
        }
    }

    private void HandleTrackedCharacterDied(Character character)
    {
        if (character == null || loseMenuCoroutine != null || isLoseMenuOpen)
        {
            return;
        }

        if (enemyTurnCoroutine != null)
        {
            StopCoroutine(enemyTurnCoroutine);
            enemyTurnCoroutine = null;
        }

        if (combatStartSequenceCoroutine != null)
        {
            StopCoroutine(combatStartSequenceCoroutine);
            combatStartSequenceCoroutine = null;
        }

        if (nextArenaCoroutine != null)
        {
            StopCoroutine(nextArenaCoroutine);
            nextArenaCoroutine = null;
        }

        StopEndTurnUnlockTimer();
        isEnemyTurnRunning = false;
        isArenaTransitionRunning = false;
        isRewardMenuOpen = false;
        SetCanEndTurn(false);
        ClearPendingAbility();
        uiGame?.HideRewards();
        uiGame?.HideYesNoPrompt();

        string characterName = character.CharacterName;
        Sprite losePortrait = character.CharacterLosePortrait;
        loseMenuCoroutine = StartCoroutine(ShowLoseMenuAfterDelay(characterName, losePortrait));
    }

    private void HandleTrackedCharacterMoved(Character character, Vector2Int previousCell, Vector2Int currentCell)
    {
        if (character == null || CurrentTurn != TurnSide.Player || isEnemyTurnRunning || isArenaTransitionRunning || isRewardMenuOpen || isLoseMenuOpen)
        {
            return;
        }

        hasPlayerMovedThisTurn = true;
        StopEndTurnUnlockTimer();
        SetCanEndTurn(true);
    }

    private IEnumerator ShowLoseMenuAfterDelay(string characterName, Sprite losePortrait)
    {
        if (loseMenuDelay > 0f)
        {
            yield return new WaitForSeconds(loseMenuDelay);
        }

        isLoseMenuOpen = true;
        loseMenuCoroutine = null;
        soundManager?.PlayLoseJingle();
        CharacterData currentCharacterData = trackedCharacter != null
            ? trackedCharacter.Data
            : board != null && board.Player != null && board.Player.ControlledCharacter != null
                ? board.Player.ControlledCharacter.Data
                : null;
        pendingLoseUnlockResults = board != null ? board.EvaluateAndApplyRunUnlocks(currentCharacterData, false) : null;
        bool hasUnlocks = pendingLoseUnlockResults != null && pendingLoseUnlockResults.Count > 0;
        uiGame?.ShowLoseMenu(
            characterName,
            losePortrait,
            hasUnlocks ? null : HandleRetryRequested,
            hasUnlocks ? HandleAdvanceFromLoseMenuToUnlocks : HandleReturnToHomeRequested,
            !hasUnlocks,
            hasUnlocks ? "Next" : "Menu");
    }

    private void HandleRetryRequested()
    {
        isLoseMenuOpen = false;
        pendingLoseUnlockResults = null;
        uiGame?.HideLoseMenu();
        UnbindTrackedCharacter();
        board?.ResetArenaProgression();
        board?.GenerateBoard();
    }

    private void HandleReturnToHomeRequested()
    {
        StopAllCoroutines();
        isEnemyTurnRunning = false;
        isPointerTracking = false;
        isArenaTransitionRunning = false;
        isRewardMenuOpen = false;
        isLoseMenuOpen = false;
        nextArenaCoroutine = null;
        combatStartSequenceCoroutine = null;
        enemyTurnCoroutine = null;
        loseMenuCoroutine = null;
        activeCombatStartPrompt = null;
        combatStartChoiceResolved = false;
        combatStartChoiceAccepted = false;
        loadedDieRefreshUsedForCurrentRewards = false;
        combatStartRelocationSelectionActive = false;
        combatStartRelocationResolved = false;
        combatStartRelocationMoved = false;
        currentRewardMenuIsDebug = false;
        pendingLoseUnlockResults = null;
        ClearPendingAbility();
        soundManager?.StopArenaMusic();
        uiGame?.HideRewards();
        uiGame?.HideShop();
        uiGame?.HideYesNoPrompt();
        uiGame?.HideLoseMenu();
        uiGame?.HideWinMenu();
        uiGame?.HideUnlockMenu();
        if (homeMenu != null)
        {
            CharacterData currentCharacterData = trackedCharacter != null ? trackedCharacter.Data : null;
            if (currentCharacterData == null && board != null && board.Player != null && board.Player.ControlledCharacter != null)
            {
                currentCharacterData = board.Player.ControlledCharacter.Data;
            }

            homeMenu.ShowForCharacter(currentCharacterData);
        }
    }

    private void HandleAdvanceFromLoseMenuToUnlocks()
    {
        isLoseMenuOpen = false;
        uiGame?.HideLoseMenu();

        CharacterData currentCharacterData = trackedCharacter != null ? trackedCharacter.Data : null;
        if (pendingLoseUnlockResults != null && pendingLoseUnlockResults.Count > 0)
        {
            List<TourmentUnlockResult> unlockResults = pendingLoseUnlockResults;
            pendingLoseUnlockResults = null;
            uiGame?.ShowUnlockSequence(unlockResults, currentCharacterData, HandleReturnToHomeRequested);
            return;
        }

        HandleReturnToHomeRequested();
    }

    private void ClearPendingAbility()
    {
        if (pendingCellTargetAbilityIndex < 0)
        {
            return;
        }

        pendingCellTargetAbilityIndex = -1;
        PendingAbilityChanged?.Invoke(pendingCellTargetAbilityIndex);
    }

    private void RegisterPlayerAction()
    {
        if (CurrentTurn != TurnSide.Player || hasPlayerActedThisTurn)
        {
            return;
        }

        hasPlayerActedThisTurn = true;
    }

    private void StartEndTurnUnlockTimer()
    {
        StopEndTurnUnlockTimer();
        if (endTurnLockDuration <= 0f)
        {
            return;
        }

        endTurnUnlockCoroutine = StartCoroutine(UnlockEndTurnAfterDelay());
    }

    private void StopEndTurnUnlockTimer()
    {
        if (endTurnUnlockCoroutine == null)
        {
            return;
        }

        StopCoroutine(endTurnUnlockCoroutine);
        endTurnUnlockCoroutine = null;
    }

    private IEnumerator UnlockEndTurnAfterDelay()
    {
        yield return new WaitForSeconds(endTurnLockDuration);
        endTurnUnlockCoroutine = null;
        SetCanEndTurn(CurrentTurn == TurnSide.Player);
    }

    private void SetCanEndTurn(bool value)
    {
        if (canEndTurn == value)
        {
            return;
        }

        canEndTurn = value;
        EndTurnAvailabilityChanged?.Invoke(canEndTurn);
    }

    private void HandleAllEnemiesDefeated()
    {
        if (!hasStarted || isArenaTransitionRunning || board == null)
        {
            return;
        }

        AwardArenaCompletionOrbs();

        isArenaTransitionRunning = true;
        isRewardMenuOpen = false;
        isPointerTracking = false;
        StopEndTurnUnlockTimer();
        ClearPendingAbility();
        SetCanEndTurn(false);
        board.Player?.ControlledCharacter?.CommitCurrentTurnStateForNextTurn();
        soundManager?.PlayVictoryJingle();

        if (nextArenaCoroutine != null)
        {
            StopCoroutine(nextArenaCoroutine);
        }

        if (board.IsCurrentArenaFinalBossBattle())
        {
            nextArenaCoroutine = StartCoroutine(ShowFinalVictorySequenceAfterDelay());
            return;
        }

        nextArenaCoroutine = StartCoroutine(LoadNextArenaAfterDelay());
    }

    private void AwardArenaCompletionOrbs()
    {
        Character character = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        CharacterData characterData = character != null ? character.Data : null;
        if (characterData == null || string.IsNullOrWhiteSpace(characterData.CharacterId))
        {
            return;
        }

        int tourmentLevel = board != null ? Mathf.Max(1, board.CurrentTourmentLevel) : 1;
        int orbReward = board.IsCurrentArenaBossBattle() ? tourmentLevel * 10 : tourmentLevel;
        CharacterProgressionSaveManager.AddOrbs(characterData.CharacterId, orbReward);
        uiGame?.RefreshOrbDisplay();
    }

    public void RequestDebugRewardChoices(int rewardChoiceCount)
    {
        if (board == null || uiGame == null || isEnemyTurnRunning || isArenaTransitionRunning || isLoseMenuOpen)
        {
            return;
        }

        int clampedCount = Mathf.Max(1, rewardChoiceCount);
        if (debugRewardSequenceCoroutine != null)
        {
            StopCoroutine(debugRewardSequenceCoroutine);
            debugRewardSequenceCoroutine = null;
        }

        remainingDebugRewardChoices = clampedCount;
        debugRewardSequenceCoroutine = StartCoroutine(RunDebugRewardChoiceSequence());
    }

    private void RequestDebugWinArena()
    {
        if (board == null || isEnemyTurnRunning || isArenaTransitionRunning || isRewardMenuOpen)
        {
            return;
        }

        HandleAllEnemiesDefeated();
    }

    private void RequestDebugRewardChoice()
    {
        RequestDebugRewardChoices(1);
    }

    private void RequestDebugOpenShop()
    {
        if (board == null || uiGame == null || isEnemyTurnRunning || isArenaTransitionRunning || isRewardMenuOpen || isLoseMenuOpen)
        {
            return;
        }

        isPointerTracking = false;
        ClearPendingAbility();
        isRewardMenuOpen = true;
        SetCanEndTurn(false);
        uiGame.ShowShop(HandleDebugShopClosed);
        soundManager?.PlayArenaMusic(soundManager != null ? soundManager.ShopMusic : null);
    }

    public void DebugGiveMoney()
    {
        if (board == null)
        {
            return;
        }

        board.AwardGold(50);
    }

    private void RequestDebugGiveMoney()
    {
        DebugGiveMoney();
    }

    private void HandleDebugRewardSelected(RewardOffer rewardOffer)
    {
        if (board != null && rewardOffer != null)
        {
            board.ApplyReward(rewardOffer);
        }

        FinishSingleDebugRewardChoice();
    }

    private void HandleDebugRewardIgnored()
    {
        FinishSingleDebugRewardChoice();
    }

    private IEnumerator RunDebugRewardChoiceSequence()
    {
        isPointerTracking = false;
        ClearPendingAbility();
        SetCanEndTurn(false);

        while (remainingDebugRewardChoices > 0)
        {
            if (board == null || uiGame == null)
            {
                break;
            }

            List<RewardOffer> rewardChoices = board.GenerateRewardChoices();
            if (rewardChoices == null || rewardChoices.Count == 0)
            {
                break;
            }

            isRewardMenuOpen = true;
            currentRewardMenuIsDebug = true;
            uiGame.ShowRewards(rewardChoices, HandleDebugRewardSelected, HandleDebugRewardIgnored);
            soundManager?.PlayVictoryChoiceMusic();
            yield return new WaitUntil(() => !isRewardMenuOpen);
        }

        bool shouldAllowEndTurn = CurrentTurn == TurnSide.Player;
        SetCanEndTurn(shouldAllowEndTurn);
        soundManager?.PlayArenaMusic(board != null ? board.CurrentCombatMusic : null);
        debugRewardSequenceCoroutine = null;
    }

    private void FinishSingleDebugRewardChoice()
    {
        remainingDebugRewardChoices = Mathf.Max(0, remainingDebugRewardChoices - 1);
        isRewardMenuOpen = false;
        currentRewardMenuIsDebug = false;
        uiGame?.HideRewards();
    }

    private void HandleDebugShopClosed()
    {
        isRewardMenuOpen = false;
        uiGame?.HideShop();
        soundManager?.PlayArenaMusic(board != null ? board.CurrentCombatMusic : null);
        SetCanEndTurn(CurrentTurn == TurnSide.Player);
    }

    private IEnumerator LoadNextArenaAfterDelay()
    {
        yield return new WaitForSeconds(rewardMenuDelay);
        nextArenaCoroutine = null;

        if (board == null)
        {
            yield break;
        }

        Character currentCharacter = board.Player != null ? board.Player.ControlledCharacter : null;
        if (currentCharacter != null)
        {
            currentCharacter.ApplyRunRewardState(board.RunRewardState);
        }

        if (board.ShouldOpenShopAfterCurrentArena())
        {
            board.MarkShopShownForCurrentBiome();
            isRewardMenuOpen = true;
            uiGame?.ShowShop(HandleShopClosed);
            soundManager?.PlayArenaMusic(soundManager != null ? soundManager.ShopMusic : null);
            yield break;
        }

        loadedDieRefreshUsedForCurrentRewards = false;
        currentRewardMenuIsDebug = false;
        List<RewardOffer> rewardChoices = board.GenerateRewardChoices();
        if (rewardChoices.Count == 0)
        {
            CompleteArenaTransition();
            yield break;
        }

        isRewardMenuOpen = true;
        uiGame?.ShowRewards(rewardChoices, HandleRewardSelected, HandleRewardsIgnored);
        soundManager?.PlayVictoryChoiceMusic();
    }

    private void HandleRewardSelected(RewardOffer rewardOffer)
    {
        if (board != null && rewardOffer != null)
        {
            board.ApplyReward(rewardOffer);
            if (rewardOffer.Kind == RewardOfferKind.Item
                && rewardOffer.ItemKey == ItemRewardKey.ShopBell
                && board.ShouldOpenShopAfterCurrentArena())
            {
                board.MarkShopShownForCurrentBiome();
                isRewardMenuOpen = true;
                uiGame?.ShowShop(HandleShopClosed);
                soundManager?.PlayArenaMusic(soundManager != null ? soundManager.ShopMusic : null);
                return;
            }
        }

        CompleteArenaTransition();
    }

    private void HandleRewardsIgnored()
    {
        Character currentCharacter = board != null && board.Player != null ? board.Player.ControlledCharacter : null;
        if (board != null
            && currentCharacter != null
            && currentCharacter.HasItem(ItemRewardKey.LoadedDie)
            && !loadedDieRefreshUsedForCurrentRewards)
        {
            loadedDieRefreshUsedForCurrentRewards = true;
            List<RewardOffer> rewardChoices = board.GenerateRewardChoices();
            if (rewardChoices.Count > 0)
            {
                isRewardMenuOpen = true;
                uiGame?.ShowRewards(rewardChoices, HandleRewardSelected, HandleRewardsIgnored);
                soundManager?.PlayVictoryChoiceMusic();
                return;
            }
        }

        CompleteArenaTransition();
    }

    private void HandleShopClosed()
    {
        CompleteArenaTransition();
    }

    private void CompleteArenaTransition()
    {
        isRewardMenuOpen = false;
        currentRewardMenuIsDebug = false;
        uiGame?.HideRewards();
        uiGame?.HideShop();
        if (board != null)
        {
            board.GenerateNextArena();
        }
    }

    private IEnumerator ShowFinalVictorySequenceAfterDelay()
    {
        yield return new WaitForSeconds(rewardMenuDelay);
        nextArenaCoroutine = null;

        if (board == null || uiGame == null)
        {
            yield break;
        }

        Character currentCharacter = board.Player != null ? board.Player.ControlledCharacter : null;
        CharacterData characterData = currentCharacter != null ? currentCharacter.Data : null;
        TourmentData currentTourment = board.GetTourmentData(board.CurrentTourmentLevel);
        List<TourmentUnlockResult> unlockResults = board.EvaluateAndApplyFinalVictoryUnlocks(characterData);

        uiGame.ShowWinMenu(characterData, currentTourment, () => HandleVictoryMenuAdvanced(characterData, unlockResults));
    }

    private void HandleVictoryMenuAdvanced(CharacterData characterData, List<TourmentUnlockResult> unlockResults)
    {
        if (unlockResults != null && unlockResults.Count > 0)
        {
            uiGame?.ShowUnlockSequence(unlockResults, characterData, ReturnToHomeAfterVictory);
            return;
        }

        ReturnToHomeAfterVictory();
    }

    private void ReturnToHomeAfterVictory()
    {
        StopAllCoroutines();
        isEnemyTurnRunning = false;
        isPointerTracking = false;
        isArenaTransitionRunning = false;
        isRewardMenuOpen = false;
        isLoseMenuOpen = false;
        nextArenaCoroutine = null;
        combatStartSequenceCoroutine = null;
        enemyTurnCoroutine = null;
        loseMenuCoroutine = null;
        activeCombatStartPrompt = null;
        combatStartChoiceResolved = false;
        combatStartChoiceAccepted = false;
        loadedDieRefreshUsedForCurrentRewards = false;
        combatStartRelocationSelectionActive = false;
        combatStartRelocationResolved = false;
        combatStartRelocationMoved = false;
        currentRewardMenuIsDebug = false;
        ClearPendingAbility();
        soundManager?.StopArenaMusic();
        uiGame?.HideRewards();
        uiGame?.HideShop();
        uiGame?.HideYesNoPrompt();
        uiGame?.HideLoseMenu();
        uiGame?.HideWinMenu();
        uiGame?.HideUnlockMenu();
        if (homeMenu != null)
        {
            homeMenu.ShowForCharacter(board != null && board.Player != null && board.Player.ControlledCharacter != null
                ? board.Player.ControlledCharacter.Data
                : null);
        }
    }
}
