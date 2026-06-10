using System;
using System.Collections;
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
    [SerializeField] private KeyCode debugEndTurnKey = KeyCode.Space;
    [SerializeField] private float swipeThreshold = 60f;

    private bool isEnemyTurnRunning;
    private bool isPointerTracking;
    private Vector2 pointerStartPosition;

    public TurnSide CurrentTurn { get; private set; } = TurnSide.Player;
    public event Action<TurnSide> TurnChanged;

    private void Start()
    {
        if (board == null)
        {
            board = GetComponent<BoardManager>();
        }

        BeginPlayerTurn();
    }

    private void Update()
    {
        if (board == null || board.Player == null || board.Player.ControlledCharacter == null || isEnemyTurnRunning)
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
        if (CurrentTurn != TurnSide.Player || isEnemyTurnRunning)
        {
            return;
        }

        StartCoroutine(RunEnemyTurn());
    }

    private void HandleDebugKeyboardInput()
    {
        if (Input.GetKeyDown(debugEndTurnKey))
        {
            RequestEndTurn();
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
            board.Player.ControlledCharacter.TrySlide(direction);
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
                    TrySwipe(character, touch.position - pointerStartPosition);
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
            TrySwipe(character, (Vector2)Input.mousePosition - pointerStartPosition);
        }
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

        character.TrySlide(direction);
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

        TurnChanged?.Invoke(CurrentTurn);
    }

    private IEnumerator RunEnemyTurn()
    {
        isEnemyTurnRunning = true;
        CurrentTurn = TurnSide.Enemy;
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

        BeginPlayerTurn();
        isEnemyTurnRunning = false;
    }
}
