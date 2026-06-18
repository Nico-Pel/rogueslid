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
#endif

    private bool isEnemyTurnRunning;
    private bool isPointerTracking;
    private bool hasStarted;
    private bool hasPlayerActedThisTurn;
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
    private Vector2 pointerStartPosition;
    private int pendingCellTargetAbilityIndex = -1;
    private ItemRewardDefinition activeCombatStartPrompt;
    private Character trackedCharacter;

    public TurnSide CurrentTurn { get; private set; } = TurnSide.Player;
    public BoardManager Board => board;
    public int PendingCellTargetAbilityIndex => pendingCellTargetAbilityIndex;
    public bool CanEndTurn => canEndTurn;
    public bool IsRewardMenuOpen => isRewardMenuOpen;
    public bool IsArenaTransitionRunning => isArenaTransitionRunning;
    public bool IsLoseMenuOpen => isLoseMenuOpen;
    public event Action<TurnSide> TurnChanged;
    public event Action<int> PendingAbilityChanged;
    public event Action<bool> EndTurnAvailabilityChanged;

    private void Start()
    {
        if (board == null)
        {
            board = GetComponent<BoardManager>();
        }

        if (uiGame == null)
        {
            uiGame = FindFirstObjectByType<UIGame>();
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
        if (board == null || board.Player == null || board.Player.ControlledCharacter == null || isEnemyTurnRunning || isArenaTransitionRunning || isRewardMenuOpen || isLoseMenuOpen)
        {
            return;
        }

        if (CurrentTurn != TurnSide.Player)
        {
            return;
        }

        HandleSwipeInput();
        HandleDebugKeyboardInput();
    }

    public void RequestEndTurn()
    {
        if (CurrentTurn != TurnSide.Player || isEnemyTurnRunning || isArenaTransitionRunning || !canEndTurn)
        {
            return;
        }

        ClearPendingAbility();
        board.Player?.ControlledCharacter?.HandlePlayerTurnEnded();
        enemyTurnCoroutine = StartCoroutine(RunEnemyTurn());
    }

    public void RestartForNewBoard()
    {
        if (!hasStarted)
        {
            return;
        }

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
        ClearPendingAbility();
        uiGame?.HideRewards();
        uiGame?.HideYesNoPrompt();
        uiGame?.HideLoseMenu();
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
            if (TryGetSingleValidTargetCell(character, ability, out Vector2Int singleTargetCell))
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

    private bool HasAnyValidTargetCell(Character character, CharacterAbilityRuntime ability)
    {
        if (character == null || ability?.Definition == null || board == null)
        {
            return false;
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
#endif

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            RequestAbilityUse(0);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            RequestAbilityUse(1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            RequestAbilityUse(2);
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

        if (character.TryUseAbility(pendingCellTargetAbilityIndex, targetCell))
        {
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
        return pendingCellTargetAbilityIndex >= 0 || GetActiveCellSelectableAbilityIndex(character) >= 0;
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

        if (combatStartSequenceCoroutine != null)
        {
            StopCoroutine(combatStartSequenceCoroutine);
        }

        List<ItemRewardDefinition> promptDefinitions = board.GetCombatStartYesNoItemDefinitions();
        if (promptDefinitions == null || promptDefinitions.Count == 0 || uiGame == null)
        {
            isRewardMenuOpen = false;
            uiGame?.HideYesNoPrompt();
            BeginPlayerTurn();
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
                soundManager?.Play2DSound(promptDefinition.ActivationSound);
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
            }

            if (promptDefinition.ConsumeOnYes)
            {
                board.RunRewardState?.RemoveItem(promptDefinition.ItemKey);
                board.Player?.ControlledCharacter?.ApplyRunRewardState(board.RunRewardState);
            }
        }

        activeCombatStartPrompt = null;
        combatStartSequenceCoroutine = null;
        isRewardMenuOpen = false;
        uiGame?.HideYesNoPrompt();
        BeginPlayerTurn();
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
        }
    }

    private void UnbindTrackedCharacter()
    {
        if (trackedCharacter != null)
        {
            trackedCharacter.Died -= HandleTrackedCharacterDied;
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

    private IEnumerator ShowLoseMenuAfterDelay(string characterName, Sprite losePortrait)
    {
        if (loseMenuDelay > 0f)
        {
            yield return new WaitForSeconds(loseMenuDelay);
        }

        isLoseMenuOpen = true;
        loseMenuCoroutine = null;
        soundManager?.PlayLoseJingle();
        uiGame?.ShowLoseMenu(characterName, losePortrait, HandleRetryRequested);
    }

    private void HandleRetryRequested()
    {
        isLoseMenuOpen = false;
        uiGame?.HideLoseMenu();
        UnbindTrackedCharacter();
        board?.ResetArenaProgression();
        board?.GenerateBoard();
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
        StopEndTurnUnlockTimer();
        SetCanEndTurn(true);
    }

    private void StartEndTurnUnlockTimer()
    {
        StopEndTurnUnlockTimer();
        if (endTurnLockDuration <= 0f)
        {
            SetCanEndTurn(true);
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

        if (CurrentTurn == TurnSide.Player && !hasPlayerActedThisTurn)
        {
            SetCanEndTurn(true);
        }
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

        nextArenaCoroutine = StartCoroutine(LoadNextArenaAfterDelay());
    }

#if UNITY_EDITOR
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
        if (board == null || uiGame == null || isEnemyTurnRunning || isArenaTransitionRunning || isRewardMenuOpen)
        {
            return;
        }

        List<RewardOffer> rewardChoices = board.GenerateRewardChoices();
        if (rewardChoices == null || rewardChoices.Count == 0)
        {
            return;
        }

        isPointerTracking = false;
        ClearPendingAbility();
        isRewardMenuOpen = true;
        SetCanEndTurn(false);
        uiGame.ShowRewards(rewardChoices, HandleDebugRewardSelected, HandleDebugRewardIgnored);
        soundManager?.PlayVictoryChoiceMusic();
    }

    private void HandleDebugRewardSelected(RewardOffer rewardOffer)
    {
        if (board != null && rewardOffer != null)
        {
            board.ApplyReward(rewardOffer);
        }

        CloseDebugRewardChoice();
    }

    private void HandleDebugRewardIgnored()
    {
        CloseDebugRewardChoice();
    }

    private void CloseDebugRewardChoice()
    {
        isRewardMenuOpen = false;
        uiGame?.HideRewards();
        bool shouldAllowEndTurn = CurrentTurn == TurnSide.Player
            && (hasPlayerActedThisTurn || endTurnLockDuration <= 0f || endTurnUnlockCoroutine == null);
        SetCanEndTurn(shouldAllowEndTurn);
    }
#endif

    private IEnumerator LoadNextArenaAfterDelay()
    {
        yield return new WaitForSeconds(rewardMenuDelay);
        nextArenaCoroutine = null;

        if (board == null)
        {
            yield break;
        }

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
        }

        CompleteArenaTransition();
    }

    private void HandleRewardsIgnored()
    {
        CompleteArenaTransition();
    }

    private void CompleteArenaTransition()
    {
        isRewardMenuOpen = false;
        if (board != null)
        {
            board.GenerateNextArena();
        }
    }
}
