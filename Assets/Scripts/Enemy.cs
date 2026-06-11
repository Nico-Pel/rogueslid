using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum EnemyAttackPattern
{
    AdjacentOrthogonal,
    Radial,
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
    [SerializeField] private bool ignoreObstaclesForMovement;
    [SerializeField] private bool canEndTurnOnObstacle;
    [SerializeField] private bool advanceTowardsCharacterWhenAlreadyInRange;
    [SerializeField] private float attackDamageDelay = 0.2f;
    [SerializeField] private bool multiplyAttackDamageDelayByDistance;
    [SerializeField] private bool lookAtTargetWhenAttacking;

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

    [Header("Animation")]
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private string attackTriggerParameter = "Attack";

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
        CacheAnimator();
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

    public bool TryMoveOneStep(Character target, bool useFleeBehaviour, int remainingMovesAfterThisStep)
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
            if (!Board.TryGetCell(candidate, out BoardCell candidateCell))
            {
                continue;
            }

            if (!CanUseMovementCandidate(candidateCell, remainingMovesAfterThisStep))
            {
                continue;
            }

            bool canAttackFromCandidate = CanAttackTargetFrom(candidate, target, false);
            int distanceScore = GetPursuitScore(candidate, target);
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

        bool allowBlockedDestination = ignoreObstaclesForMovement
            && Board.TryGetCell(bestStep, out BoardCell destinationCell)
            && destinationCell.HasBlockingTerrain;

        if (!Board.MoveOccupant(gridPosition, bestStep, BoardOccupantKind.Enemy, allowBlockedDestination))
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
            if (!useFleeBehaviour
                && CanAttackTargetFrom(gridPosition, target, false)
                && !advanceTowardsCharacterWhenAlreadyInRange)
            {
                break;
            }

            int remainingMovesAfterThisStep = mobility - step - 1;
            bool moved = TryMoveOneStep(target, useFleeBehaviour, remainingMovesAfterThisStep);
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

    private void CacheAnimator()
    {
        if (enemyAnimator == null)
        {
            enemyAnimator = GetComponentInChildren<Animator>();
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

        if (ignoreRequirements)
        {
            return true;
        }

        Vector2Int delta = target.GridPosition - origin;
        int manhattanDistance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);

        switch (attackPattern)
        {
            case EnemyAttackPattern.AdjacentOrthogonal:
                return manhattanDistance == 1;

            case EnemyAttackPattern.Radial:
                int radialDistance = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
                int maxRadialRange = Mathf.Max(1, attackRange);
                return radialDistance > 0 && radialDistance <= maxRadialRange;

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

    private bool CanUseMovementCandidate(BoardCell candidateCell, int remainingMovesAfterThisStep)
    {
        if (candidateCell == null || candidateCell.IsOccupied)
        {
            return false;
        }

        if (!candidateCell.HasBlockingTerrain)
        {
            return true;
        }

        if (!ignoreObstaclesForMovement)
        {
            return false;
        }

        if (canEndTurnOnObstacle)
        {
            return true;
        }

        return remainingMovesAfterThisStep > 0 && CanExitObstacleZone(candidateCell.GridPosition, remainingMovesAfterThisStep);
    }

    private int GetPursuitScore(Vector2Int candidate, Character target)
    {
        if (Board == null || target == null)
        {
            return int.MaxValue;
        }

        if (CanAttackTargetFrom(candidate, target, false))
        {
            return 0;
        }

        int attackPositionDistance = GetDistanceToClosestAttackPosition(candidate, target);
        if (attackPositionDistance != int.MaxValue)
        {
            return attackPositionDistance;
        }

        return GetBestReachableDistanceToTarget(candidate, target.GridPosition);
    }

    private int GetDistanceToClosestAttackPosition(Vector2Int start, Character target)
    {
        if (Board == null || target == null || Board.Cells == null)
        {
            return int.MaxValue;
        }

        int bestDistance = int.MaxValue;
        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                if (cellPosition == start)
                {
                    if (CanAttackTargetFrom(cellPosition, target, false))
                    {
                        return 0;
                    }

                    continue;
                }

                if (!Board.TryGetCell(cellPosition, out BoardCell cell) || !CanEndMovementOnCell(cell))
                {
                    continue;
                }

                if (!CanAttackTargetFrom(cellPosition, target, false))
                {
                    continue;
                }

                int pathDistance = Board.GetPathDistance(start, cellPosition, false, ignoreObstaclesForMovement);
                if (pathDistance < bestDistance)
                {
                    bestDistance = pathDistance;
                }
            }
        }

        return bestDistance;
    }

    private int GetBestReachableDistanceToTarget(Vector2Int start, Vector2Int targetPosition)
    {
        if (Board == null)
        {
            return int.MaxValue;
        }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        frontier.Enqueue(start);
        visited.Add(start);

        int bestReachableDistance = Board.GetManhattanDistance(start, targetPosition);
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            bestReachableDistance = Mathf.Min(bestReachableDistance, Board.GetManhattanDistance(current, targetPosition));

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (visited.Contains(next) || !Board.TryGetCell(next, out BoardCell nextCell))
                {
                    continue;
                }

                if (nextCell.IsOccupied || (!nextCell.Walkable && !ignoreObstaclesForMovement))
                {
                    continue;
                }

                visited.Add(next);
                frontier.Enqueue(next);
            }
        }

        return bestReachableDistance;
    }

    private bool CanEndMovementOnCell(BoardCell cell)
    {
        if (cell == null || cell.IsOccupied)
        {
            return false;
        }

        if (cell.Walkable)
        {
            return true;
        }

        return ignoreObstaclesForMovement && canEndTurnOnObstacle;
    }

    private bool CanExitObstacleZone(Vector2Int startPosition, int stepsAvailable)
    {
        if (Board == null || stepsAvailable <= 0)
        {
            return false;
        }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        frontier.Enqueue(startPosition);
        distances[startPosition] = 0;

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            int currentDistance = distances[current];

            if (currentDistance >= stepsAvailable)
            {
                continue;
            }

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (distances.ContainsKey(next) || !Board.TryGetCell(next, out BoardCell nextCell) || nextCell.IsOccupied)
                {
                    continue;
                }

                if (!nextCell.Walkable && !ignoreObstaclesForMovement)
                {
                    continue;
                }

                int nextDistance = currentDistance + 1;
                if (nextCell.Walkable)
                {
                    return true;
                }

                distances[next] = nextDistance;
                frontier.Enqueue(next);
            }
        }

        return false;
    }

    private void TriggerAttackAnimation()
    {
        CacheAnimator();
        if (enemyAnimator != null && !string.IsNullOrEmpty(attackTriggerParameter))
        {
            enemyAnimator.SetTrigger(attackTriggerParameter);
        }
    }

    private void FaceTargetForAttack(Character target)
    {
        if (!lookAtTargetWhenAttacking || target == null)
        {
            return;
        }

        Vector3 targetDirection = target.transform.position - transform.position;
        targetDirection.y = 0f;
        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(targetDirection.normalized, Vector3.up);
    }

    private IEnumerator AttackIfPossible(Character target, bool ignoreRequirements)
    {
        if (!CanAttackTargetFrom(gridPosition, target, ignoreRequirements))
        {
            yield break;
        }

        FaceTargetForAttack(target);

        if (attackPattern == EnemyAttackPattern.Radial)
        {
            TriggerAttackAnimation();
            yield return AttackRadialTargets();
            yield break;
        }

        TriggerAttackAnimation();
        if (projectilePrefab != null && attackPattern == EnemyAttackPattern.Projectile)
        {
            yield return FireProjectileAt(target);
        }

        yield return ApplyDamageToTarget(target);
        yield return new WaitForSeconds(0.08f);
    }

    private IEnumerator AttackRadialTargets()
    {
        if (Board == null || Board.Cells == null)
        {
            yield break;
        }

        int maxRadialRange = Mathf.Max(1, attackRange);
        List<Character> targetsInRange = new List<Character>();

        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                BoardCell cell = Board.Cells[x, y];
                if (cell == null || cell.OccupantKind != BoardOccupantKind.PlayerCharacter || cell.Occupant == null)
                {
                    continue;
                }

                int radialDistance = Mathf.Max(Mathf.Abs(cell.GridPosition.x - gridPosition.x), Mathf.Abs(cell.GridPosition.y - gridPosition.y));
                if (radialDistance == 0 || radialDistance > maxRadialRange)
                {
                    continue;
                }

                Character targetCharacter = cell.Occupant.GetComponent<Character>();
                if (targetCharacter == null)
                {
                    continue;
                }

                targetsInRange.Add(targetCharacter);
            }
        }

        if (targetsInRange.Count == 0)
        {
            yield break;
        }

        targetsInRange.Sort((left, right) => GetTargetDamageDelay(left).CompareTo(GetTargetDamageDelay(right)));

        float elapsedDelay = 0f;
        for (int index = 0; index < targetsInRange.Count; index++)
        {
            Character target = targetsInRange[index];
            if (target == null)
            {
                continue;
            }

            float targetDelay = GetTargetDamageDelay(target);
            float waitDuration = Mathf.Max(0f, targetDelay - elapsedDelay);
            if (waitDuration > 0f)
            {
                yield return new WaitForSeconds(waitDuration);
                elapsedDelay += waitDuration;
            }

            target.TakeDamage(force);
        }

        if (targetsInRange.Count > 0)
        {
            yield return new WaitForSeconds(0.08f);
        }
    }

    private IEnumerator ApplyDamageToTarget(Character target)
    {
        if (target == null)
        {
            yield break;
        }

        float delay = GetTargetDamageDelay(target);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        target.TakeDamage(force);
    }

    private float GetTargetDamageDelay(Character target)
    {
        if (target == null)
        {
            return 0f;
        }

        float delay = Mathf.Max(0f, attackDamageDelay);
        if (!multiplyAttackDamageDelayByDistance)
        {
            return delay;
        }

        int distance = Mathf.Max(
            Mathf.Abs(target.GridPosition.x - gridPosition.x),
            Mathf.Abs(target.GridPosition.y - gridPosition.y));

        return delay * Mathf.Max(1, distance);
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
