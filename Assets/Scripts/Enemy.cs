using System.Collections;
using System.Collections.Generic;
using System;
using DG.Tweening;
using UnityEngine;

public enum EnemyAttackPattern
{
    AdjacentOrthogonal,
    Radial,
    Projectile
}

public enum EnemyOptionalActionType
{
    None,
    HealMostWoundedAlly
}

public class Enemy : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EnemyData enemyData;

    [Header("Core Stats")]
    [ReadOnly] [SerializeField] private int maxHealth = 8;
    [SerializeField] [ReadOnly] private int currentHealth = 8;
    [ReadOnly] [SerializeField] private int force = 2;
    [ReadOnly] [SerializeField] private int resistance;

    [Header("Behaviour")]
    [ReadOnly] [SerializeField] private EnemyAttackPattern attackPattern = EnemyAttackPattern.AdjacentOrthogonal;
    [ReadOnly] [SerializeField] private int attackRange = 1;
    [ReadOnly] [SerializeField] private DamageSoundType damageSoundType;
    [ReadOnly] [SerializeField] private bool directVision = true;
    [ReadOnly] [SerializeField] private bool requireAlignedShot;
    [ReadOnly] [SerializeField] private bool allowPerfectDiagonalShot;
    [ReadOnly] [SerializeField] private bool hasMaxRange = true;
    [ReadOnly] [SerializeField] private bool ignoreObstacles;
    [ReadOnly] [SerializeField] private bool attackAlways;
    [ReadOnly] [SerializeField] private bool flee;
    [Min(0)]
    [ReadOnly] [SerializeField] private int maxFleeTurns = 2;
    [ReadOnly] [SerializeField] private bool attackFirst;
    [ReadOnly] [SerializeField] [Range(0f, 100f)] private float fleeThresholdPercent;
    [ReadOnly] [SerializeField] private bool ignoreObstaclesForMovement;
    [ReadOnly] [SerializeField] private bool canEndTurnOnObstacle;
    [ReadOnly] [SerializeField] private bool advanceTowardsCharacterWhenAlreadyInRange;
    [ReadOnly] [SerializeField] private float attackDamageDelay = 0.2f;
    [ReadOnly] [SerializeField] private bool multiplyAttackDamageDelayByDistance;
    [ReadOnly] [SerializeField] private bool lookAtTargetWhenAttacking;
    [ReadOnly] [SerializeField] private EnemyOptionalActionType optionalActionType;
    [ReadOnly] [SerializeField] private int optionalHealAmount = 4;

    [Header("Board")]
    [SerializeField] private Vector2Int gridPosition;
    [ReadOnly] [SerializeField] private int mobility = 2;
    [ReadOnly] [SerializeField] private float moveDuration = 0.16f;
    [SerializeField] private float spawnHeight = 0.08f;
    [SerializeField] private float deathDestroyDelay = 0.12f;
    [SerializeField] private GameObject fxDeathPrefab;
    [SerializeField] private Transform deathMarkAnchor;
    [SerializeField] private GameObject fxDeathMarkPrefab;
    [SerializeField] private CanvasUnitUI canvasUnitUI;

    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [ReadOnly] [SerializeField] private float projectileTravelHeight = 0.5f;
    [ReadOnly] [SerializeField] private float projectileTravelSpeed = 10f;

    [Header("Animation")]
    [SerializeField] private Transform enemyBody;
    [SerializeField] private Transform enemyCanvas;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private string attackTriggerParameter = "Attack";
    [SerializeField] private string optionalActionTriggerParameter = "AltAttack";
    [SerializeField] private float bodySpawnScaleDuration = 0.5f;
    [ReadOnly] [SerializeField] private bool useFlyAnimationOnObstacle;
    [SerializeField] private string flyingBoolParameter = "Flying";
    [SerializeField] private float obstacleCanvasHeightOffset = 1f;
    [SerializeField] private GameObject optionalActionCasterFxPrefab;
    [SerializeField] private Vector3 optionalActionCasterFxOffset;
    [SerializeField] private float optionalActionCasterFxDelay;
    [SerializeField] private GameObject optionalActionTargetFxPrefab;
    [SerializeField] private Vector3 optionalActionTargetFxOffset;
    [SerializeField] private float optionalActionTargetFxDelay;
    [SerializeField] private float optionalActionFxLifetime = 1f;

    private Tween moveTween;
    private Tween bodySpawnScaleTween;
    private RendererBlinkFeedback blinkFeedback;
    private bool isDying;
    private GameObject activeDeathMarkFxInstance;
    private Vector3 defaultCanvasLocalPosition;
    private bool hasCachedCanvasDefaultPosition;
    private int consecutiveFleeTurns;
    private bool fleePermanentlyDisabled;
    private Vector3 cachedBodyOriginalScale = Vector3.one;

    public Vector2Int GridPosition => gridPosition;
    public int MaxHealth => maxHealth;
    public int Force => force;
    public int Mobility => mobility;
    public int CurrentHealth => currentHealth;
    public int Resistance => resistance;
    public EnemyAttackPattern AttackPattern => attackPattern;
    public DamageSoundType DamageSoundType => damageSoundType;
    public bool IsMoving { get; private set; }
    public BoardManager Board { get; private set; }
    public Transform EffectAnchor => deathMarkAnchor != null ? deathMarkAnchor : transform;
    public EnemyData Data => enemyData;
    public string EnemyName => enemyData != null && !string.IsNullOrWhiteSpace(enemyData.EnemyName)
        ? enemyData.EnemyName
        : GetFallbackDisplayName();
    public Sprite EnemyPortrait => enemyData != null ? enemyData.Portrait : null;
    public string EnemySpecialInfo => enemyData != null ? enemyData.SpecialInfo : "The goblin is a classic enemy.";

    private int pendingMobilityPenalty;

    private void OnValidate()
    {
        ApplyEnemyDataDefinition();
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
    }

    private void ApplyEnemyDataDefinition()
    {
        if (enemyData == null)
        {
            return;
        }

        maxHealth = enemyData.MaxHealth;
        force = enemyData.Force;
        resistance = enemyData.Resistance;
        attackPattern = enemyData.AttackPattern;
        attackRange = enemyData.AttackRange;
        damageSoundType = enemyData.DamageSoundType;
        directVision = enemyData.DirectVision;
        requireAlignedShot = enemyData.RequireAlignedShot;
        allowPerfectDiagonalShot = enemyData.AllowPerfectDiagonalShot;
        hasMaxRange = enemyData.HasMaxRange;
        ignoreObstacles = enemyData.IgnoreObstacles;
        attackAlways = enemyData.AttackAlways;
        flee = enemyData.Flee;
        maxFleeTurns = enemyData.MaxFleeTurns;
        attackFirst = enemyData.AttackFirst;
        fleeThresholdPercent = enemyData.FleeThresholdPercent;
        ignoreObstaclesForMovement = enemyData.IgnoreObstaclesForMovement;
        canEndTurnOnObstacle = enemyData.CanEndTurnOnObstacle;
        advanceTowardsCharacterWhenAlreadyInRange = enemyData.AdvanceTowardsCharacterWhenAlreadyInRange;
        attackDamageDelay = enemyData.AttackDamageDelay;
        multiplyAttackDamageDelayByDistance = enemyData.MultiplyAttackDamageDelayByDistance;
        lookAtTargetWhenAttacking = enemyData.LookAtTargetWhenAttacking;
        optionalActionType = enemyData.OptionalActionType;
        optionalHealAmount = enemyData.OptionalHealAmount;
        mobility = enemyData.Mobility;
        moveDuration = enemyData.MoveDuration;
        projectileTravelHeight = enemyData.ProjectileTravelHeight;
        projectileTravelSpeed = enemyData.ProjectileTravelSpeed;
        useFlyAnimationOnObstacle = enemyData.UseFlyAnimationOnObstacle;
    }

    private string GetFallbackDisplayName()
    {
        string displayName = name.Replace("(Clone)", string.Empty).Trim();
        if (displayName.StartsWith("Enemy-", StringComparison.OrdinalIgnoreCase))
        {
            displayName = displayName.Substring("Enemy-".Length);
        }

        return displayName;
    }

    public void Assign(Vector2Int spawnGridPosition, BoardManager board)
    {
        ApplyEnemyDataDefinition();
        gridPosition = spawnGridPosition;
        Board = board;
        currentHealth = maxHealth;
        blinkFeedback = GetComponent<RendererBlinkFeedback>();
        CacheBody();
        CacheCanvas();
        CacheAnimator();
        CacheHpBar();
        RefreshHpBar();
        SnapToGrid();
        consecutiveFleeTurns = 0;
        fleePermanentlyDisabled = false;
        RefreshFlyingAnimationState();
        PlayBodySpawnTween();
    }

    public void SetDeathMarkActive(bool isActive)
    {
        if (isActive)
        {
            EnsureDeathMarkFx();
            return;
        }

        ClearDeathMarkFx();
    }

    public int TakeDamage(int incomingDamage, DamageSoundType hitSoundType = DamageSoundType.Default)
    {
        if (isDying)
        {
            return 0;
        }

        int finalDamage = Mathf.Max(1, incomingDamage - resistance);
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);

        blinkFeedback?.Blink(Color.white, 0.5f, 0.12f);
        cam.Instance?.CamShake(finalDamage);
        SoundManager.Instance?.PlayDamageSound(hitSoundType, EffectAnchor.position);
        RefreshHpBar();

        if (currentHealth <= 0)
        {
            Die();
        }

        return finalDamage;
    }

    public int Heal(int amount)
    {
        if (amount <= 0 || isDying || currentHealth <= 0)
        {
            return 0;
        }

        int previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        int healedAmount = currentHealth - previousHealth;
        if (healedAmount > 0)
        {
            RefreshHpBar();
        }

        return healedAmount;
    }

    public bool TryMoveOneStep(Character target, bool useFleeBehaviour, int remainingMovesAfterThisStep)
    {
        if (Board == null || target == null || IsMoving)
        {
            return false;
        }

        Vector2Int bestStep = gridPosition;
        if (!useFleeBehaviour)
        {
            if (!TryGetPursuitStep(target, remainingMovesAfterThisStep, out bestStep))
            {
                return false;
            }
        }
        else if (!TryGetFleeStep(target, remainingMovesAfterThisStep, out bestStep))
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

        FaceMovementDirection(bestStep - gridPosition);
        gridPosition = bestStep;
        AnimateToGrid();
        RefreshFlyingAnimationState();
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

        int effectiveMobility = Mathf.Max(0, mobility - pendingMobilityPenalty);
        pendingMobilityPenalty = 0;

        bool persistentFleeBehaviour = ShouldUseFleeBehaviour();
        if (persistentFleeBehaviour && maxFleeTurns > 0 && consecutiveFleeTurns >= maxFleeTurns)
        {
            persistentFleeBehaviour = false;
            flee = false;
            fleePermanentlyDisabled = true;
            consecutiveFleeTurns = 0;
        }

        if (persistentFleeBehaviour && ShouldStopFleePermanently(target))
        {
            persistentFleeBehaviour = false;
            flee = false;
            fleePermanentlyDisabled = true;
            consecutiveFleeTurns = 0;
        }

        bool temporaryFleeMove = attackFirst && attackAlways && !persistentFleeBehaviour;

        if (attackFirst)
        {
            if (CanAttackTargetFrom(gridPosition, target, attackAlways))
            {
                yield return AttackIfPossible(target, attackAlways);
            }
            else
            {
                yield return TryPerformOptionalAction();
            }
        }

        bool useFleeBehaviour = persistentFleeBehaviour || temporaryFleeMove;
        List<Vector2Int> plannedPath = BuildMovementPlan(target, useFleeBehaviour, effectiveMobility);
        for (int step = 0; step < plannedPath.Count; step++)
        {
            if (!useFleeBehaviour
                && CanAttackTargetFrom(gridPosition, target, false)
                && !advanceTowardsCharacterWhenAlreadyInRange)
            {
                break;
            }

            bool moved = TryMoveToPlannedStep(plannedPath[step]);
            if (!moved)
            {
                break;
            }

            yield return new WaitUntil(() => !IsMoving);
            yield return new WaitForSeconds(0.05f);
        }

        if (!attackFirst)
        {
            if (CanAttackTargetFrom(gridPosition, target, attackAlways))
            {
                yield return AttackIfPossible(target, attackAlways);
            }
            else
            {
                yield return TryPerformOptionalAction();
            }
        }

        if (persistentFleeBehaviour)
        {
            consecutiveFleeTurns++;
        }
        else
        {
            consecutiveFleeTurns = 0;
        }
    }

    private List<Vector2Int> BuildMovementPlan(Character target, bool useFleeBehaviour, int maxSteps)
    {
        List<Vector2Int> plannedPath = new List<Vector2Int>();
        if (Board == null || target == null || maxSteps <= 0)
        {
            return plannedPath;
        }

        if (!useFleeBehaviour
            && CanAttackTargetFrom(gridPosition, target, false)
            && !advanceTowardsCharacterWhenAlreadyInRange)
        {
            return plannedPath;
        }

        if (useFleeBehaviour)
        {
            BuildReachabilityMap(maxSteps, out Dictionary<Vector2Int, int> distances, out Dictionary<Vector2Int, Vector2Int> cameFrom);
            int bestFleeScore = int.MinValue;
            int bestFleePathDistance = -1;
            Vector2Int bestDestination = gridPosition;
            bool foundDestination = false;
            foreach (KeyValuePair<Vector2Int, int> entry in distances)
            {
                Vector2Int cellPosition = entry.Key;
                int pathDistance = entry.Value;
                if (cellPosition == gridPosition)
                {
                    continue;
                }

                if (!Board.TryGetCell(cellPosition, out BoardCell cell) || !CanEndMovementOnCell(cell))
                {
                    continue;
                }

                int fleeScore = Board.GetManhattanDistance(cellPosition, target.GridPosition);
                if (!foundDestination
                    || fleeScore > bestFleeScore
                    || (fleeScore == bestFleeScore && pathDistance > bestFleePathDistance))
                {
                    foundDestination = true;
                    bestDestination = cellPosition;
                    bestFleeScore = fleeScore;
                    bestFleePathDistance = pathDistance;
                }
            }

            if (!foundDestination || bestDestination == gridPosition || !cameFrom.ContainsKey(bestDestination))
            {
                return plannedPath;
            }

            Vector2Int currentStep = bestDestination;
            while (currentStep != gridPosition)
            {
                plannedPath.Add(currentStep);
                currentStep = cameFrom[currentStep];
            }

            plannedPath.Reverse();
            return plannedPath;
        }

        bool shouldAdvanceAggressively = advanceTowardsCharacterWhenAlreadyInRange;
        if (!shouldAdvanceAggressively
            && TryFindBestAttackObjective(target, out Vector2Int attackObjective)
            && TryBuildPathForCurrentMovementRules(gridPosition, attackObjective, out List<Vector2Int> attackPath))
        {
            AppendPathPrefix(plannedPath, attackPath, maxSteps);
            return plannedPath;
        }

        if (TryBuildPathForCurrentMovementRules(gridPosition, target.GridPosition, out List<Vector2Int> targetPath, true))
        {
            AppendPathPrefix(plannedPath, targetPath, maxSteps);
        }

        return plannedPath;
    }

    private static void AppendPathPrefix(List<Vector2Int> destination, List<Vector2Int> fullPath, int maxSteps)
    {
        if (destination == null || fullPath == null || maxSteps <= 0)
        {
            return;
        }

        int stepCount = Mathf.Min(maxSteps, fullPath.Count);
        for (int index = 0; index < stepCount; index++)
        {
            destination.Add(fullPath[index]);
        }
    }

    private void BuildReachabilityMap(int maxSteps, out Dictionary<Vector2Int, int> distances, out Dictionary<Vector2Int, Vector2Int> cameFrom)
    {
        distances = new Dictionary<Vector2Int, int>();
        cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        if (Board == null || maxSteps <= 0)
        {
            return;
        }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        frontier.Enqueue(gridPosition);
        distances[gridPosition] = 0;
        cameFrom[gridPosition] = gridPosition;

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
            if (currentDistance >= maxSteps)
            {
                continue;
            }

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (distances.ContainsKey(next) || !Board.TryGetCell(next, out BoardCell nextCell) || !CanPathThroughCell(nextCell))
                {
                    continue;
                }

                distances[next] = currentDistance + 1;
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }
    }

    private bool TryMoveToPlannedStep(Vector2Int nextStep)
    {
        if (Board == null || IsMoving || nextStep == gridPosition)
        {
            return false;
        }

        bool allowBlockedDestination = ignoreObstaclesForMovement
            && Board.TryGetCell(nextStep, out BoardCell destinationCell)
            && destinationCell.HasBlockingTerrain;

        if (!Board.MoveOccupant(gridPosition, nextStep, BoardOccupantKind.Enemy, allowBlockedDestination))
        {
            return false;
        }

        FaceMovementDirection(nextStep - gridPosition);
        gridPosition = nextStep;
        AnimateToGrid();
        RefreshFlyingAnimationState();
        return true;
    }

    public void ApplyMobilityPenaltyNextTurn(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        pendingMobilityPenalty += amount;
    }

    public bool TryForcedMoveTo(Vector2Int targetCell)
    {
        if (Board == null || targetCell == gridPosition)
        {
            return false;
        }

        if (!Board.MoveOccupant(gridPosition, targetCell, BoardOccupantKind.Enemy))
        {
            return false;
        }

        FaceMovementDirection(targetCell - gridPosition);
        gridPosition = targetCell;
        AnimateToGrid();
        RefreshFlyingAnimationState();
        return true;
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
        bodySpawnScaleTween?.Kill();
        IsMoving = false;
        SetFlyingAnimation(false);
        RefreshCanvasHeight(false);
        ClearDeathMarkFx();
    }

    private void CacheHpBar()
    {
        if (canvasUnitUI == null)
        {
            canvasUnitUI = GetComponentInChildren<CanvasUnitUI>(true);
        }
    }

    private void CacheCanvas()
    {
        if (enemyCanvas == null)
        {
            CacheHpBar();
            enemyCanvas = canvasUnitUI != null ? canvasUnitUI.RootTransform : transform.Find("Canvas");
        }

        if (!hasCachedCanvasDefaultPosition && enemyCanvas != null)
        {
            defaultCanvasLocalPosition = enemyCanvas.localPosition;
            hasCachedCanvasDefaultPosition = true;
        }
    }

    private void CacheAnimator()
    {
        if (enemyAnimator == null)
        {
            enemyAnimator = GetComponentInChildren<Animator>();
        }
    }

    private void CacheBody()
    {
        if (enemyBody == null && transform.childCount > 0)
        {
            enemyBody = transform.GetChild(0);
        }

        if (enemyBody != null)
        {
            cachedBodyOriginalScale = enemyBody.localScale;
        }
    }

    private void PlayBodySpawnTween()
    {
        CacheBody();
        Transform targetBody = enemyBody != null ? enemyBody : transform;
        Vector3 targetScale = enemyBody != null ? cachedBodyOriginalScale : targetBody.localScale;

        bodySpawnScaleTween?.Kill();
        targetBody.localScale = Vector3.zero;
        bodySpawnScaleTween = targetBody.DOScale(targetScale, bodySpawnScaleDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() => bodySpawnScaleTween = null);
    }

    private void RefreshHpBar()
    {
        CacheHpBar();
        if (canvasUnitUI == null)
        {
            return;
        }

        canvasUnitUI.RefreshHealth(currentHealth, maxHealth);
    }

    private bool ShouldUseFleeBehaviour()
    {
        if (fleePermanentlyDisabled)
        {
            return false;
        }

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

    private bool ShouldStopFleePermanently(Character target)
    {
        if (target == null || consecutiveFleeTurns <= 1)
        {
            return false;
        }

        int pathDistanceToTarget = GetPathDistanceForCurrentMovementRules(gridPosition, target.GridPosition, true);
        if (pathDistanceToTarget == int.MaxValue)
        {
            pathDistanceToTarget = Board != null
                ? Board.GetManhattanDistance(gridPosition, target.GridPosition)
                : int.MaxValue;
        }

        return pathDistanceToTarget <= 2;
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

    private bool TryGetPursuitStep(Character target, int remainingMovesAfterThisStep, out Vector2Int bestStep)
    {
        bestStep = gridPosition;
        if (Board == null || target == null)
        {
            return false;
        }

        Vector2Int bestDestination = gridPosition;
        int bestDestinationPathDistance = int.MaxValue;
        int bestFallbackScore = int.MaxValue;
        int bestFallbackPathDistance = int.MaxValue;
        bool foundAttackDestination = false;
        bool foundFallbackDestination = false;

        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                if (cellPosition == gridPosition)
                {
                    continue;
                }

                if (!Board.TryGetCell(cellPosition, out BoardCell cell) || !CanEndMovementOnCell(cell))
                {
                    continue;
                }

                int pathDistance = GetPathDistanceForCurrentMovementRules(gridPosition, cellPosition);
                if (pathDistance == int.MaxValue)
                {
                    continue;
                }

                if (CanAttackTargetFrom(cellPosition, target, false))
                {
                    if (!foundAttackDestination
                        || pathDistance < bestDestinationPathDistance
                        || (pathDistance == bestDestinationPathDistance
                            && Board.GetManhattanDistance(cellPosition, target.GridPosition) < Board.GetManhattanDistance(bestDestination, target.GridPosition)))
                    {
                        foundAttackDestination = true;
                        bestDestination = cellPosition;
                        bestDestinationPathDistance = pathDistance;
                    }

                    continue;
                }

                int fallbackScore = GetFutureAttackDistance(cellPosition, target);
                if (fallbackScore == int.MaxValue)
                {
                    fallbackScore = GetBestReachableDistanceToTarget(cellPosition, target.GridPosition);
                }

                if (!foundFallbackDestination
                    || fallbackScore < bestFallbackScore
                    || (fallbackScore == bestFallbackScore && pathDistance < bestFallbackPathDistance))
                {
                    foundFallbackDestination = true;
                    bestDestination = cellPosition;
                    bestFallbackScore = fallbackScore;
                    bestFallbackPathDistance = pathDistance;
                }
            }
        }

        if (!foundAttackDestination && !foundFallbackDestination)
        {
            return false;
        }

        if (!TryGetNextPathStepForCurrentMovementRules(gridPosition, bestDestination, out Vector2Int nextStep))
        {
            return false;
        }

        if (!Board.TryGetCell(nextStep, out BoardCell nextCell) || !CanUseMovementCandidate(nextCell, remainingMovesAfterThisStep))
        {
            return false;
        }

        bestStep = nextStep;
        return bestStep != gridPosition;
    }

    private bool TryGetFleeStep(Character target, int remainingMovesAfterThisStep, out Vector2Int bestStep)
    {
        bestStep = gridPosition;
        if (Board == null || target == null)
        {
            return false;
        }

        int bestScore = int.MinValue;
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
            if (!Board.TryGetCell(candidate, out BoardCell candidateCell) || !CanUseMovementCandidate(candidateCell, remainingMovesAfterThisStep))
            {
                continue;
            }

            int fleeScore = Board.GetManhattanDistance(candidate, target.GridPosition);
            if (fleeScore > bestScore)
            {
                bestScore = fleeScore;
                bestStep = candidate;
            }
        }

        return bestStep != gridPosition;
    }

    private int GetFutureAttackDistance(Vector2Int start, Character target)
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

                int pathDistance = GetPathDistanceForCurrentMovementRules(start, cellPosition);
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

                if (!CanPathThroughCell(nextCell))
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

    private bool CanPathThroughCell(BoardCell cell)
    {
        if (cell == null || cell.IsOccupied)
        {
            return false;
        }

        return cell.Walkable || ignoreObstaclesForMovement;
    }

    private int GetPathDistanceForCurrentMovementRules(Vector2Int start, Vector2Int goal)
    {
        return GetPathDistanceForCurrentMovementRules(start, goal, false);
    }

    private int GetPathDistanceForCurrentMovementRules(Vector2Int start, Vector2Int goal, bool allowOccupiedGoal)
    {
        if (Board == null || !Board.IsInsideBoard(start) || !Board.IsInsideBoard(goal))
        {
            return int.MaxValue;
        }

        if (!Board.TryGetCell(goal, out BoardCell goalCell) || (!allowOccupiedGoal && !CanEndMovementOnCell(goalCell)))
        {
            return int.MaxValue;
        }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        frontier.Enqueue(start);
        distances[start] = 0;

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
            if (current == goal)
            {
                return distances[current];
            }

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (!Board.TryGetCell(next, out BoardCell nextCell) || distances.ContainsKey(next))
                {
                    continue;
                }

                bool canVisit = next == goal
                    ? (allowOccupiedGoal || CanEndMovementOnCell(nextCell))
                    : CanPathThroughCell(nextCell);
                if (!canVisit)
                {
                    continue;
                }

                distances[next] = distances[current] + 1;
                frontier.Enqueue(next);
            }
        }

        return int.MaxValue;
    }

    private bool TryFindBestAttackObjective(Character target, out Vector2Int bestObjective)
    {
        bestObjective = gridPosition;
        if (Board == null || target == null || Board.Cells == null)
        {
            return false;
        }

        bool foundObjective = false;
        int bestPathDistance = int.MaxValue;
        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                if (cellPosition == gridPosition)
                {
                    continue;
                }

                if (!Board.TryGetCell(cellPosition, out BoardCell cell) || !CanEndMovementOnCell(cell) || !CanAttackTargetFrom(cellPosition, target, false))
                {
                    continue;
                }

                int pathDistance = GetPathDistanceForCurrentMovementRules(gridPosition, cellPosition);
                if (pathDistance == int.MaxValue)
                {
                    continue;
                }

                if (!foundObjective
                    || pathDistance < bestPathDistance
                    || (pathDistance == bestPathDistance
                        && Board.GetManhattanDistance(cellPosition, target.GridPosition) < Board.GetManhattanDistance(bestObjective, target.GridPosition)))
                {
                    foundObjective = true;
                    bestObjective = cellPosition;
                    bestPathDistance = pathDistance;
                }
            }
        }

        return foundObjective;
    }

    private bool TryBuildPathForCurrentMovementRules(Vector2Int start, Vector2Int goal, out List<Vector2Int> path, bool allowOccupiedGoal = false)
    {
        path = new List<Vector2Int>();
        if (Board == null || !Board.IsInsideBoard(start) || !Board.IsInsideBoard(goal) || start == goal)
        {
            return false;
        }

        if (!Board.TryGetCell(goal, out BoardCell goalCell) || (!allowOccupiedGoal && !CanEndMovementOnCell(goalCell)))
        {
            return false;
        }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        frontier.Enqueue(start);
        cameFrom[start] = start;

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
            if (current == goal)
            {
                break;
            }

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (!Board.TryGetCell(next, out BoardCell nextCell) || cameFrom.ContainsKey(next))
                {
                    continue;
                }

                bool canVisit = next == goal
                    ? (allowOccupiedGoal || CanEndMovementOnCell(nextCell))
                    : CanPathThroughCell(nextCell);
                if (!canVisit)
                {
                    continue;
                }

                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(goal))
        {
            return false;
        }

        Vector2Int currentStep = goal;
        while (currentStep != start)
        {
            path.Add(currentStep);
            currentStep = cameFrom[currentStep];
        }

        path.Reverse();
        return path.Count > 0;
    }

    private bool TryGetNextPathStepForCurrentMovementRules(Vector2Int start, Vector2Int goal, out Vector2Int nextStep)
    {
        nextStep = start;
        if (Board == null || !Board.IsInsideBoard(start) || !Board.IsInsideBoard(goal) || start == goal)
        {
            return false;
        }

        if (!Board.TryGetCell(goal, out BoardCell goalCell) || !CanEndMovementOnCell(goalCell))
        {
            return false;
        }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        frontier.Enqueue(start);
        cameFrom[start] = start;

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
            if (current == goal)
            {
                break;
            }

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (!Board.TryGetCell(next, out BoardCell nextCell) || cameFrom.ContainsKey(next))
                {
                    continue;
                }

                bool canVisit = next == goal ? CanEndMovementOnCell(nextCell) : CanPathThroughCell(nextCell);
                if (!canVisit)
                {
                    continue;
                }

                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(goal))
        {
            return false;
        }

        Vector2Int currentStep = goal;
        while (cameFrom[currentStep] != start)
        {
            currentStep = cameFrom[currentStep];
        }

        nextStep = currentStep;
        return true;
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

                if (!CanPathThroughCell(nextCell))
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

    private void TriggerOptionalActionAnimation()
    {
        CacheAnimator();
        if (enemyAnimator != null && !string.IsNullOrEmpty(optionalActionTriggerParameter))
        {
            enemyAnimator.SetTrigger(optionalActionTriggerParameter);
        }
    }

    private void RefreshFlyingAnimationState()
    {
        if (Board == null || !Board.TryGetCell(gridPosition, out BoardCell currentCell))
        {
            SetFlyingAnimation(false);
            RefreshCanvasHeight(false);
            return;
        }

        bool isOnObstacle = currentCell.HasBlockingTerrain;
        SetFlyingAnimation(useFlyAnimationOnObstacle && isOnObstacle);
        RefreshCanvasHeight(isOnObstacle);
    }

    private void SetFlyingAnimation(bool isFlying)
    {
        CacheAnimator();
        if (enemyAnimator == null || string.IsNullOrWhiteSpace(flyingBoolParameter))
        {
            return;
        }

        enemyAnimator.SetBool(flyingBoolParameter, isFlying);
    }

    private void RefreshCanvasHeight(bool isOnObstacle)
    {
        CacheCanvas();
        if (enemyCanvas == null || !hasCachedCanvasDefaultPosition)
        {
            return;
        }

        Vector3 targetLocalPosition = defaultCanvasLocalPosition;
        if (isOnObstacle)
        {
            targetLocalPosition.y += obstacleCanvasHeightOffset;
        }

        enemyCanvas.localPosition = targetLocalPosition;
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

        CacheBody();
        Transform targetBody = enemyBody != null ? enemyBody : transform;
        float targetYaw = Quaternion.LookRotation((-targetDirection).normalized, Vector3.up).eulerAngles.y;
        Vector3 localEulerAngles = targetBody.localEulerAngles;
        localEulerAngles.y = targetYaw;
        targetBody.localEulerAngles = localEulerAngles;
    }

    private void FaceMovementDirection(Vector2Int gridDirection)
    {
        if (gridDirection == Vector2Int.zero)
        {
            return;
        }

        Vector3 moveDirection = new Vector3(gridDirection.x, 0f, -gridDirection.y);
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        CacheBody();
        Transform targetBody = enemyBody != null ? enemyBody : transform;
        float targetYaw = Quaternion.LookRotation((-moveDirection).normalized, Vector3.up).eulerAngles.y;
        Vector3 localEulerAngles = targetBody.localEulerAngles;
        localEulerAngles.y = targetYaw;
        targetBody.localEulerAngles = localEulerAngles;
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

        yield return ApplyDamageToTarget(target, attackPattern == EnemyAttackPattern.Projectile);
        yield return new WaitForSeconds(0.08f);
    }

    private IEnumerator TryPerformOptionalAction()
    {
        switch (optionalActionType)
        {
            case EnemyOptionalActionType.HealMostWoundedAlly:
                yield return HealMostWoundedAlly();
                yield break;
            default:
                yield break;
        }
    }

    private IEnumerator HealMostWoundedAlly()
    {
        Enemy targetAlly = FindMostWoundedAlly();
        if (targetAlly == null || optionalHealAmount <= 0)
        {
            yield break;
        }

        TriggerOptionalActionAnimation();
        PlayOptionalActionFx(optionalActionCasterFxPrefab, transform, optionalActionCasterFxOffset, optionalActionCasterFxDelay);
        PlayOptionalActionFx(optionalActionTargetFxPrefab, targetAlly.EffectAnchor, optionalActionTargetFxOffset, optionalActionTargetFxDelay);
        targetAlly.Heal(optionalHealAmount);
        yield return new WaitForSeconds(0.08f);
    }

    private Enemy FindMostWoundedAlly()
    {
        if (Board == null)
        {
            return null;
        }

        Enemy bestTarget = null;
        int mostMissingHealth = 0;
        for (int index = 0; index < Board.SpawnedEnemies.Count; index++)
        {
            Enemy ally = Board.SpawnedEnemies[index];
            if (ally == null || ally.isDying)
            {
                continue;
            }

            int missingHealth = Mathf.Max(0, ally.MaxHealth - ally.CurrentHealth);
            if (missingHealth <= 0)
            {
                continue;
            }

            if (bestTarget == null || missingHealth > mostMissingHealth)
            {
                bestTarget = ally;
                mostMissingHealth = missingHealth;
            }
        }

        return bestTarget;
    }

    private void PlayOptionalActionFx(GameObject fxPrefab, Transform anchor, Vector3 offset, float delay)
    {
        if (fxPrefab == null || anchor == null)
        {
            return;
        }

        StartCoroutine(SpawnOptionalActionFxAfterDelay(fxPrefab, anchor, offset, delay));
    }

    private IEnumerator SpawnOptionalActionFxAfterDelay(GameObject fxPrefab, Transform anchor, Vector3 offset, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (fxPrefab == null || anchor == null)
        {
            yield break;
        }

        GameObject spawnedFx = Instantiate(fxPrefab, anchor.position + offset, fxPrefab.transform.rotation);
        spawnedFx.transform.localScale = fxPrefab.transform.localScale;
        if (optionalActionFxLifetime > 0f)
        {
            Destroy(spawnedFx, optionalActionFxLifetime);
        }
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
                if (Board.TryGetBarrel(cell.GridPosition, out BarrelObstacle barrel) && barrel != null)
                {
                    barrel.TakeHit();
                }

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

            target.TakeDamage(force, this, false, damageSoundType);
        }

        if (targetsInRange.Count > 0)
        {
            yield return new WaitForSeconds(0.08f);
        }
    }

    private IEnumerator ApplyDamageToTarget(Character target, bool wasProjectile)
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

        target.TakeDamage(force, this, wasProjectile, damageSoundType);
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

        SoundManager.Instance?.PlayArrowShot(EffectAnchor.position);

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
        if (isDying)
        {
            return;
        }

        isDying = true;
        ClearDeathMarkFx();
        SpawnDeathFx();
        if (Board != null)
        {
            Board.RemoveEnemy(this);
        }

        Destroy(gameObject, deathDestroyDelay);
    }

    private void SpawnDeathFx()
    {
        if (fxDeathPrefab == null)
        {
            return;
        }

        GameObject deathFx = Instantiate(fxDeathPrefab, transform.position, fxDeathPrefab.transform.rotation);
        deathFx.transform.localScale = fxDeathPrefab.transform.localScale;
    }

    private void EnsureDeathMarkFx()
    {
        if (activeDeathMarkFxInstance != null || fxDeathMarkPrefab == null)
        {
            return;
        }

        Transform anchor = deathMarkAnchor != null ? deathMarkAnchor : transform;
        activeDeathMarkFxInstance = Instantiate(fxDeathMarkPrefab, anchor.position, fxDeathMarkPrefab.transform.rotation, anchor);
        activeDeathMarkFxInstance.transform.localScale = fxDeathMarkPrefab.transform.localScale;
    }

    private void ClearDeathMarkFx()
    {
        if (activeDeathMarkFxInstance == null)
        {
            return;
        }

        Destroy(activeDeathMarkFxInstance);
        activeDeathMarkFxInstance = null;
    }
}
