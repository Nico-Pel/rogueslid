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
    [SerializeField] private KeyCode debugEndTurnKey = KeyCode.Space;
    [SerializeField] private float swipeThreshold = 60f;
    [SerializeField] private float endTurnLockDuration = 6f;
    [SerializeField] private float rewardMenuDelay = 2f;

    private bool isEnemyTurnRunning;
    private bool isPointerTracking;
    private bool hasStarted;
    private bool hasPlayerActedThisTurn;
    private bool isArenaTransitionRunning;
    private bool isRewardMenuOpen;
    private bool canEndTurn = true;
    private Coroutine endTurnUnlockCoroutine;
    private Coroutine nextArenaCoroutine;
    private Vector2 pointerStartPosition;
    private int pendingCellTargetAbilityIndex = -1;

    public TurnSide CurrentTurn { get; private set; } = TurnSide.Player;
    public BoardManager Board => board;
    public int PendingCellTargetAbilityIndex => pendingCellTargetAbilityIndex;
    public bool CanEndTurn => canEndTurn;
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

        if (board != null)
        {
            board.AllEnemiesDefeated += HandleAllEnemiesDefeated;
        }

        hasStarted = true;
        BeginPlayerTurn();
    }

    private void OnDestroy()
    {
        if (board != null)
        {
            board.AllEnemiesDefeated -= HandleAllEnemiesDefeated;
        }
    }

    private void Update()
    {
        if (board == null || board.Player == null || board.Player.ControlledCharacter == null || isEnemyTurnRunning || isArenaTransitionRunning || isRewardMenuOpen)
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
        StartCoroutine(RunEnemyTurn());
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
        nextArenaCoroutine = null;
        ClearPendingAbility();
        uiGame?.HideRewards();
        BeginPlayerTurn();
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

    private void HandleDebugKeyboardInput()
    {
        if (Input.GetKeyDown(debugEndTurnKey))
        {
            RequestEndTurn();
            return;
        }

        if (pendingCellTargetAbilityIndex >= 0)
        {
            return;
        }

        Vector2Int direction = Vector2Int.zero;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            direction = Vector2Int.up;
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            direction = Vector2Int.right;
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            direction = Vector2Int.down;
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
        if (character == null || character.IsMoving)
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
            TryHandlePointerRelease(character, Input.mousePosition);
        }
    }

    private void TryHandlePointerRelease(Character character, Vector2 pointerEndPosition)
    {
        if (pendingCellTargetAbilityIndex >= 0)
        {
            TryUseTargetedAbility(character, pointerEndPosition);
            return;
        }

        TrySwipe(character, pointerEndPosition - pointerStartPosition);
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
        }

        hasPlayerActedThisTurn = false;
        SetCanEndTurn(false);
        StartEndTurnUnlockTimer();
        TurnChanged?.Invoke(CurrentTurn);
    }

    private IEnumerator RunEnemyTurn()
    {
        isEnemyTurnRunning = true;
        CurrentTurn = TurnSide.Enemy;
        board?.Player?.ControlledCharacter?.BeginEnemyTurn();
        StopEndTurnUnlockTimer();
        SetCanEndTurn(false);
        TurnChanged?.Invoke(CurrentTurn);

        foreach (Enemy enemy in board.SpawnedEnemies)
        {
            if (enemy == null)
            {
                continue;
            }

            yield return enemy.ExecuteTurn();
            yield return new WaitForSeconds(0.08f);
        }

        board?.Player?.ControlledCharacter?.HandleEnemyTurnEnded();
        isEnemyTurnRunning = false;

        if (isArenaTransitionRunning)
        {
            yield break;
        }

        BeginPlayerTurn();
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

        if (nextArenaCoroutine != null)
        {
            StopCoroutine(nextArenaCoroutine);
        }

        nextArenaCoroutine = StartCoroutine(LoadNextArenaAfterDelay());
    }

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
