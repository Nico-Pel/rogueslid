using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum EnemyAttackPattern
{
    AdjacentOrthogonal,
    Projectile
}

public class Enemy : MonoBehaviour
{
    [Header("Core Stats")]
    [SerializeField] private int maxHealth = 8;
    [SerializeField] [ReadOnly] private int currentHealth = 8;
    [SerializeField] private int force = 2;
    [SerializeField] private int resistance;

    [Header("Behaviour")]
    [SerializeField] private EnemyAttackPattern attackPattern = EnemyAttackPattern.AdjacentOrthogonal;
    [SerializeField] private int attackRange = 1;
    [SerializeField] private bool directVision = true;
    [SerializeField] private bool requireAlignedShot;
    [SerializeField] private bool allowPerfectDiagonalShot;
    [SerializeField] private bool hasMaxRange = true;
    [SerializeField] private bool ignoreObstacles;
    [SerializeField] private bool attackAlways;
    [SerializeField] private bool flee;
    [SerializeField] private bool attackFirst;
    [SerializeField] [Range(0f, 100f)] private float fleeThresholdPercent;

    [Header("Board")]
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private int mobility = 2;
    [SerializeField] private float moveDuration = 0.16f;
    [SerializeField] private float spawnHeight = 0.08f;
    [SerializeField] private float deathDestroyDelay = 0.12f;
    [SerializeField] private Image hpFillBar;

    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileTravelHeight = 0.5f;
    [SerializeField] private float projectileTravelSpeed = 10f;

    private Tween moveTween;
    private RendererBlinkFeedback blinkFeedback;

    public Vector2Int GridPosition => gridPosition;
    public int Mobility => mobility;
    public int CurrentHealth => currentHealth;
    public bool IsMoving { get; private set; }
    public BoardManager Board { get; private set; }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
    }

    public void Assign(Vector2Int spawnGridPosition, BoardManager board)
    {
        gridPosition = spawnGridPosition;
        Board = board;
        currentHealth = maxHealth;
        blinkFeedback = GetComponent<RendererBlinkFeedback>();
        CacheHpBar();
        RefreshHpBar();
        SnapToGrid();
    }

    public int TakeDamage(int incomingDamage)
    {
        int finalDamage = Mathf.Max(1, incomingDamage - resistance);
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);

        blinkFeedback?.Blink(Color.white, 0.5f, 0.12f);
        cam.Instance?.CamShake(finalDamage);
        RefreshHpBar();

        if (currentHealth <= 0)
        {
            Die();
        }

        return finalDamage;
    }

    public bool TryMoveOneStep(Character target, bool useFleeBehaviour)
    {
        if (Board == null || target == null || IsMoving)
        {
            return false;
        }

        Vector2Int bestStep = gridPosition;
        bool bestCanAttack = CanAttackTargetFrom(gridPosition, target, false);
        int bestScore = useFleeBehaviour ? int.MinValue : int.MaxValue;

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

            bool canAttackFromCandidate = CanAttackTargetFrom(candidate, target, false);
            int distanceScore = Board.GetPathDistance(candidate, target.GridPosition, true);
            if (distanceScore == int.MaxValue)
            {
                continue;
            }

            if (!useFleeBehaviour)
            {
                if (canAttackFromCandidate && !bestCanAttack)
                {
                    bestCanAttack = true;
                    bestScore = distanceScore;
                    bestStep = candidate;
                    continue;
                }

                if (canAttackFromCandidate == bestCanAttack && distanceScore < bestScore)
                {
                    bestScore = distanceScore;
                    bestStep = candidate;
                }
            }
            else
            {
                int fleeScore = Board.GetManhattanDistance(candidate, target.GridPosition);
                if (fleeScore > bestScore)
                {
                    bestScore = fleeScore;
                    bestStep = candidate;
                }
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

        Character target = Board.Player != null ? Board.Player.ControlledCharacter : null;
        if (target == null)
        {
            yield break;
        }

        bool temporaryFleeMove = attackFirst && attackAlways && !ShouldUseFleeBehaviour();

        if (attackFirst)
        {
            yield return AttackIfPossible(target, attackAlways);
        }

        bool useFleeBehaviour = ShouldUseFleeBehaviour() || temporaryFleeMove;
        for (int step = 0; step < mobility; step++)
        {
            if (!useFleeBehaviour && CanAttackTargetFrom(gridPosition, target, false))
            {
                break;
            }

            bool moved = TryMoveOneStep(target, useFleeBehaviour);
            if (!moved)
            {
                break;
            }

            yield return new WaitUntil(() => !IsMoving);
            yield return new WaitForSeconds(0.05f);
        }

        if (!attackFirst)
        {
            yield return AttackIfPossible(target, attackAlways);
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

    private void CacheHpBar()
    {
        if (hpFillBar != null)
        {
            return;
        }

        Transform hpFillTransform = transform.Find("Canvas/hpBar/hpFillBar");
        if (hpFillTransform != null)
        {
            hpFillBar = hpFillTransform.GetComponent<Image>();
        }
    }

    private void RefreshHpBar()
    {
        if (hpFillBar == null)
        {
            return;
        }

        float fillRatio = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        hpFillBar.fillAmount = Mathf.Clamp01(fillRatio);
    }

    private bool ShouldUseFleeBehaviour()
    {
        if (flee)
        {
            return true;
        }

        if (fleeThresholdPercent <= 0f || maxHealth <= 0)
        {
            return false;
        }

        float healthPercent = (float)currentHealth / maxHealth * 100f;
        return healthPercent <= fleeThresholdPercent;
    }

    private bool CanAttackTargetFrom(Vector2Int origin, Character target, bool ignoreRequirements)
    {
        if (target == null || Board == null)
        {
            return false;
        }

        if (ignoreRequirements || attackAlways)
        {
            return true;
        }

        Vector2Int delta = target.GridPosition - origin;
        int manhattanDistance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);

        switch (attackPattern)
        {
            case EnemyAttackPattern.AdjacentOrthogonal:
                return manhattanDistance == 1;

            case EnemyAttackPattern.Projectile:
                int deltaX = Mathf.Abs(delta.x);
                int deltaY = Mathf.Abs(delta.y);
                bool sameRowOrColumn = origin.x == target.GridPosition.x || origin.y == target.GridPosition.y;
                bool perfectDiagonal = deltaX == deltaY && deltaX > 0;

                if (requireAlignedShot)
                {
                    bool aligned = sameRowOrColumn || (allowPerfectDiagonalShot && perfectDiagonal);
                    if (!aligned)
                    {
                        return false;
                    }
                }

                if (hasMaxRange)
                {
                    float distance = requireAlignedShot && sameRowOrColumn
                        ? manhattanDistance
                        : Vector2.Distance(origin, target.GridPosition);

                    if (distance > attackRange)
                    {
                        return false;
                    }
                }

                if (ignoreObstacles)
                {
                    return true;
                }

                if (directVision)
                {
                    return Board.HasLineOfSight(origin, target.GridPosition);
                }

                return true;
        }

        return false;
    }

    private IEnumerator AttackIfPossible(Character target, bool ignoreRequirements)
    {
        if (!CanAttackTargetFrom(gridPosition, target, ignoreRequirements))
        {
            yield break;
        }

        if (projectilePrefab != null && attackPattern == EnemyAttackPattern.Projectile)
        {
            yield return FireProjectileAt(target);
        }

        target.TakeDamage(force);
        yield return new WaitForSeconds(0.08f);
    }

    private IEnumerator FireProjectileAt(Character target)
    {
        if (target == null)
        {
            yield break;
        }

        Vector3 startPosition = transform.position + Vector3.up * projectileTravelHeight;
        Vector3 targetPosition = target.transform.position + Vector3.up * projectileTravelHeight;
        GameObject projectile = Instantiate(projectilePrefab, startPosition, Quaternion.identity);

        Vector3 direction = targetPosition - startPosition;
        if (direction.sqrMagnitude > 0.0001f)
        {
            projectile.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        float duration = Mathf.Max(0.08f, direction.magnitude / Mathf.Max(0.01f, projectileTravelSpeed));
        Tween projectileTween = projectile.transform.DOMove(targetPosition, duration).SetEase(Ease.Linear);
        yield return projectileTween.WaitForCompletion();

        Destroy(projectile);
    }

    private void Die()
    {
        if (Board != null)
        {
            Board.RemoveEnemy(this);
        }

        Destroy(gameObject, deathDestroyDelay);
    }
}
