using DG.Tweening;
using System;
using UnityEngine;

public class Character : MonoBehaviour
{
    [Header("Core Stats")]
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private int currentHealth = 10;
    [SerializeField] private int bonusDamage;
    [SerializeField] private int resistance;
    [SerializeField] private int movementPointsPerTurn = 2;

    [Header("Board")]
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private float moveDuration = 0.18f;
    [SerializeField] private float spawnHeight = 0.08f;

    private Renderer[] renderers;
    private Color[] baseColors;
    private Tween moveTween;
    private int remainingMovementPoints;

    public Vector2Int GridPosition => gridPosition;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public int BonusDamage => bonusDamage;
    public int Resistance => resistance;
    public int BaseMovementPoints => movementPointsPerTurn;
    public int RemainingMovementPoints => remainingMovementPoints;
    public bool CanAct => remainingMovementPoints > 0 && !IsMoving;
    public bool IsMoving { get; private set; }
    public Player Owner { get; private set; }
    public BoardManager Board { get; private set; }
    public event Action<Character> MovementPointsChanged;

    public void Assign(Player owner, Vector2Int spawnGridPosition, BoardManager board)
    {
        Owner = owner;
        Board = board;
        gridPosition = spawnGridPosition;
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
        CacheRenderers();
        SnapToGrid();
        ResetTurn();
        NotifyMovementPointsChanged();
    }

    public void ResetTurn()
    {
        remainingMovementPoints = movementPointsPerTurn;
        NotifyMovementPointsChanged();
    }

    public bool TrySlide(Vector2Int direction)
    {
        if (Board == null || remainingMovementPoints <= 0 || IsMoving || direction == Vector2Int.zero)
        {
            return false;
        }

        Vector2Int destination = Board.GetSlideDestination(gridPosition, direction);
        if (destination == gridPosition)
        {
            return false;
        }

        if (!Board.MoveOccupant(gridPosition, destination, BoardOccupantKind.PlayerCharacter))
        {
            return false;
        }

        gridPosition = destination;
        remainingMovementPoints--;
        NotifyMovementPointsChanged();
        AnimateToGrid();
        return true;
    }

    public void SetSelected(bool isSelected)
    {
        CacheRenderers();
        if (renderers == null)
        {
            return;
        }

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer targetRenderer = renderers[index];
            if (targetRenderer == null || targetRenderer.material == null)
            {
                continue;
            }

            targetRenderer.material.color = isSelected
                ? Color.Lerp(baseColors[index], Color.white, 0.35f)
                : baseColors[index];
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

    private void CacheRenderers()
    {
        if (renderers != null)
        {
            return;
        }

        renderers = GetComponentsInChildren<Renderer>(true);
        baseColors = new Color[renderers.Length];
        for (int index = 0; index < renderers.Length; index++)
        {
            baseColors[index] = renderers[index] != null && renderers[index].material != null
                ? renderers[index].material.color
                : Color.white;
        }
    }

    private void OnDisable()
    {
        moveTween?.Kill();
        IsMoving = false;
    }

    private void NotifyMovementPointsChanged()
    {
        MovementPointsChanged?.Invoke(this);
    }
}
