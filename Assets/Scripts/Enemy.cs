using System.Collections;
using DG.Tweening;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private int mobility = 2;
    [SerializeField] private float moveDuration = 0.16f;
    [SerializeField] private float spawnHeight = 0.08f;

    private Tween moveTween;

    public Vector2Int GridPosition => gridPosition;
    public int Mobility => mobility;
    public bool IsMoving { get; private set; }
    public BoardManager Board { get; private set; }

    public void Assign(Vector2Int spawnGridPosition, BoardManager board)
    {
        gridPosition = spawnGridPosition;
        Board = board;
        SnapToGrid();
    }

    public bool TryMoveOneStepTowards(Character target)
    {
        if (Board == null || target == null || IsMoving)
        {
            return false;
        }

        Vector2Int bestStep = gridPosition;
        int bestDistance = int.MaxValue;

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        foreach (Vector2Int direction in directions)
        {
            Vector2Int candidate = gridPosition + direction;
            if (!Board.IsCellWalkable(candidate))
            {
                continue;
            }

            int distance = Board.GetPathDistance(candidate, target.GridPosition, true);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestStep = candidate;
            }
        }

        if (bestStep == gridPosition)
        {
            return false;
        }

        if (!Board.MoveOccupant(gridPosition, bestStep, BoardOccupantKind.Enemy))
        {
            return false;
        }

        gridPosition = bestStep;
        AnimateToGrid();
        return true;
    }

    public IEnumerator ExecuteTurn()
    {
        if (Board == null)
        {
            yield break;
        }

        for (int step = 0; step < mobility; step++)
        {
            Character target = Board.Player != null ? Board.Player.ControlledCharacter : null;
            if (target == null)
            {
                yield break;
            }

            bool moved = TryMoveOneStepTowards(target);
            if (!moved)
            {
                yield break;
            }

            yield return new WaitUntil(() => !IsMoving);
            yield return new WaitForSeconds(0.05f);
        }
    }

    private void SnapToGrid()
    {
        if (Board == null)
        {
            return;
        }

        transform.position = Board.GridToWorldPosition(gridPosition) + Vector3.up * spawnHeight;
    }

    private void AnimateToGrid()
    {
        if (Board == null)
        {
            return;
        }

        moveTween?.Kill();
        IsMoving = true;
        Vector3 targetPosition = Board.GridToWorldPosition(gridPosition) + Vector3.up * spawnHeight;
        moveTween = transform.DOMove(targetPosition, moveDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                IsMoving = false;
                moveTween = null;
            });
    }

    private void OnDisable()
    {
        moveTween?.Kill();
        IsMoving = false;
    }
}
