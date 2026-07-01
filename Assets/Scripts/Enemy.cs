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
    private struct PathPlan
    {
        public List<Vector2Int> Path;
        public int TotalCost;
        public int StepCount;
        public bool UsesLethalHazard;

        public bool IsValid => Path != null && Path.Count > 0;
    }

    private enum GiantWormAttackMode
    {
        None,
        Adjacent,
        Ranged
    }

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
    [ReadOnly] [SerializeField] private bool fleeAfterAttacking;
    [ReadOnly] [SerializeField] [Range(0f, 100f)] private float fleeThresholdPercent;
    [ReadOnly] [SerializeField] private float fearBodyWiggleStrength;
    [SerializeField] private float fearFeedbackDelay = 0.25f;
    [Min(0f)]
    [SerializeField] private float bleedingDamageRandomDelayMax = 0.33f;
    [SerializeField] private Color bleedingRimGlowColor = Color.red;
    [Min(0f)]
    [SerializeField] private float bleedingRimGlowIntensity = 1f;
    [Min(0.25f)]
    [SerializeField] private float bleedingRimGlowPower = 2.5f;
    [SerializeField] private GameObject mistyConfusionFxPrefab;
    [Min(0f)]
    [SerializeField] private float mistyConfusionFxMaxDuration = 2f;
    [ReadOnly] [SerializeField] private bool ignoreObstaclesForMovement;
    [ReadOnly] [SerializeField] private bool canEndTurnOnObstacle;
    [ReadOnly] [SerializeField] private bool advanceTowardsCharacterWhenAlreadyInRange;
    [ReadOnly] [SerializeField] private float attackDamageDelay = 0.2f;
    [ReadOnly] [SerializeField] private bool multiplyAttackDamageDelayByDistance;
    [ReadOnly] [SerializeField] private bool lookAtTargetWhenAttacking;
    [ReadOnly] [SerializeField] private int regenPerTurn;
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
    [SerializeField] private Transform projectileSpawnPos;
    [ReadOnly] [SerializeField] private float projectileTravelHeight = 0.5f;
    [ReadOnly] [SerializeField] private float projectileTravelSpeed = 10f;
    [ReadOnly] [SerializeField] private float projectileSpawnDelay;

    [Header("Animation")]
    [SerializeField] private Transform enemyBody;
    [SerializeField] private Transform enemyCanvas;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private string attackTriggerParameter = "Attack";
    [SerializeField] private string optionalActionTriggerParameter = "AltAttack";
    [SerializeField] private string optionalActionSecondaryTriggerParameter = "AltAttack2";
    [SerializeField] private AnimationClip attackStateSourceClip;
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
    [SerializeField] private GameObject wormHolePrefab;
    [SerializeField] private GameObject selfPowerUpFxPrefab;

    private Tween moveTween;
    private Tween bodySpawnScaleTween;
    private Tween bodyRotationTween;
    private Tween fearBodyWiggleTween;
    private Tween impactTween;
    private RendererBlinkFeedback blinkFeedback;
    private Renderer[] statusRenderers;
    private readonly Dictionary<Renderer, MaterialPropertyBlock> statusPropertyBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();
    private bool isDying;
    private GameObject activeDeathMarkFxInstance;
    private Vector3 defaultCanvasLocalPosition;
    private bool hasCachedCanvasDefaultPosition;
    private int consecutiveFleeTurns;
    private bool fleePermanentlyDisabled;
    private Vector3 cachedBodyOriginalScale = Vector3.one;
    private Vector3 cachedBodyOriginalLocalEulerAngles = Vector3.zero;
    private Vector3 cachedBodyOriginalLocalPosition = Vector3.zero;
    private readonly Dictionary<CombatStatusType, int> statusDurations = new Dictionary<CombatStatusType, int>();
    private readonly Dictionary<CombatStatusType, int> statusPotencies = new Dictionary<CombatStatusType, int>();
    private int forceModifierFromStatuses;
    private bool mistyConfusionParanoia;
    private bool mistyConfusionBrokenHeart;
    private float mistyConfusionAllyDamageMultiplier = 1f;
    private float mistyConfusionBrokenHeartSelfDamageRatio;
    private GameObject activeMistyConfusionFxInstance;
    private readonly Dictionary<CombatStatusType, GameObject> activePersistentStatusFxInstances = new Dictionary<CombatStatusType, GameObject>();
    private bool isBleedingRimGlowApplied;
    private Coroutine mistyConfusionFxTimeoutCoroutine;
    private AnimatorOverrideController enemyAnimatorOverrideController;
    private DragoonRiderEnemyData dragoonRiderData;
    private LichEnemyData lichData;
    private GameObject specialAlternateProjectilePrefab;
    private Transform fireBallSpawnPos;
    private int movementInterruptCount;
    private int combatTurnCount;
    private Enemy linkedSummoner;
    private bool isInFearState;
    private Coroutine pendingFearFeedbackCoroutine;
    private EnemySpecialBehavior specialBehavior;
    private int specialDamage;
    private float specialWindupDuration;
    private float specialStartDelay;
    private float specialJumpDuration;
    private float specialJumpPower;
    private float specialRecoveryDelay;
    private int specialMinimumDistance;
    private int specialLandingDistance;
    private float specialImpactShakeRatio;
    private float specialPerDistanceDelay;
    private float specialBumpHeight;
    private float specialBumpDurationPerDistance;
    private bool combatStartActionUsed;
    private readonly Dictionary<Vector2Int, GameObject> wormTunnelInstances = new Dictionary<Vector2Int, GameObject>();
    private bool lockWormRootPosition;
    private Vector2Int lockedWormRootCell;
    private readonly List<Vector2Int> extraOccupiedCells = new List<Vector2Int>();
    private int trollAttackBonusDamage;
    private int mobilityBonusFromSpecials;
    private bool hasFlyingAnimationOverride;
    private bool flyingAnimationOverrideValue;
    private bool dragoonTwinAttackSuppressed;
    private int forcedFearTurnsRemaining;
    private CombatStatusFxLibrary statusFxLibrary;

    public Vector2Int GridPosition => gridPosition;
    public int MaxHealth => maxHealth;
    public int Force => force;
    public int EffectiveForce => Mathf.Max(0, force + forceModifierFromStatuses + trollAttackBonusDamage);
    public int Mobility => Mathf.Max(0, mobility + mobilityBonusFromSpecials);
    public int CurrentHealth => currentHealth;
    public int Resistance => resistance;
    public bool IsImmuneToFire => enemyData != null && enemyData.ImmuneToFire;
    public EnemyAttackPattern AttackPattern => attackPattern;
    public DamageSoundType DamageSoundType => damageSoundType;
    public bool IsMoving { get; private set; }
    public bool IsMovementInterrupted => movementInterruptCount > 0;
    public BoardManager Board { get; private set; }
    public Transform EffectAnchor => deathMarkAnchor != null ? deathMarkAnchor : transform;
    public bool HasStatusEffect(CombatStatusType statusType) => statusPotencies.TryGetValue(statusType, out int potency) && potency > 0;
    public int GetStatusPotency(CombatStatusType statusType) => statusPotencies.TryGetValue(statusType, out int potency) ? potency : 0;
    public EnemyData Data => enemyData;
    public string EnemyName => enemyData != null && !string.IsNullOrWhiteSpace(enemyData.EnemyName)
        ? enemyData.EnemyName
        : GetFallbackDisplayName();
    public Sprite EnemyPortrait => enemyData != null ? enemyData.Portrait : null;
    public string EnemySpecialInfo => enemyData != null ? enemyData.SpecialInfo : "The goblin is a classic enemy.";
    public EnemySpecialBehavior SpecialBehavior => specialBehavior;
    public GameObject SpecialCompanionPrefab => enemyData != null ? enemyData.SpecialCompanionPrefab : null;

    private int pendingMobilityPenalty;
    private static readonly int RimGlowColorId = Shader.PropertyToID("_RimGlowColor");
    private static readonly int RimGlowIntensityId = Shader.PropertyToID("_RimGlowIntensity");
    private static readonly int RimGlowPowerId = Shader.PropertyToID("_RimGlowPower");

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
        fleeAfterAttacking = enemyData.FleeAfterAttacking;
        fleeThresholdPercent = enemyData.FleeThresholdPercent;
        fearBodyWiggleStrength = enemyData.FearBodyWiggleStrength;
        ignoreObstaclesForMovement = enemyData.IgnoreObstaclesForMovement;
        canEndTurnOnObstacle = enemyData.CanEndTurnOnObstacle;
        advanceTowardsCharacterWhenAlreadyInRange = enemyData.AdvanceTowardsCharacterWhenAlreadyInRange;
        attackDamageDelay = enemyData.AttackDamageDelay;
        multiplyAttackDamageDelayByDistance = enemyData.MultiplyAttackDamageDelayByDistance;
        lookAtTargetWhenAttacking = enemyData.LookAtTargetWhenAttacking;
        regenPerTurn = enemyData.RegenPerTurn;
        optionalActionType = enemyData.OptionalActionType;
        optionalHealAmount = enemyData.OptionalHealAmount;
        mobility = enemyData.Mobility;
        moveDuration = enemyData.MoveDuration;
        projectileTravelHeight = enemyData.ProjectileTravelHeight;
        projectileTravelSpeed = enemyData.ProjectileTravelSpeed;
        projectileSpawnDelay = enemyData.ProjectileSpawnDelay;
        useFlyAnimationOnObstacle = enemyData.UseFlyAnimationOnObstacle;
        specialBehavior = enemyData.SpecialBehavior;
        specialDamage = enemyData.SpecialDamage;
        specialWindupDuration = enemyData.SpecialWindupDuration;
        specialStartDelay = enemyData.SpecialStartDelay;
        specialJumpDuration = enemyData.SpecialJumpDuration;
        specialJumpPower = enemyData.SpecialJumpPower;
        specialRecoveryDelay = enemyData.SpecialRecoveryDelay;
        specialMinimumDistance = enemyData.SpecialMinimumDistance;
        specialLandingDistance = enemyData.SpecialLandingDistance;
        specialImpactShakeRatio = enemyData.SpecialImpactShakeRatio;
        specialPerDistanceDelay = enemyData.SpecialPerDistanceDelay;
        specialBumpHeight = enemyData.SpecialBumpHeight;
        specialBumpDurationPerDistance = enemyData.SpecialBumpDurationPerDistance;
        if (enemyData.SpecialSelfBuffFxPrefab != null)
        {
            selfPowerUpFxPrefab = enemyData.SpecialSelfBuffFxPrefab;
        }

        specialAlternateProjectilePrefab = enemyData.SpecialAlternateProjectilePrefab;

        dragoonRiderData = enemyData as DragoonRiderEnemyData;
        lichData = enemyData as LichEnemyData;
        if (dragoonRiderData != null && dragoonRiderData.AttackSourceClip != null)
        {
            attackStateSourceClip = dragoonRiderData.AttackSourceClip;
        }
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
        statusDurations.Clear();
        statusPotencies.Clear();
        forceModifierFromStatuses = 0;
        blinkFeedback = GetComponent<RendererBlinkFeedback>();
        CacheStatusRenderers();
        CacheBody();
        CacheCanvas();
        CacheAnimator();
        CacheSpecialAnchors();
        CacheHpBar();
        RefreshHpBar();
        SnapToGrid();
        consecutiveFleeTurns = 0;
        fleePermanentlyDisabled = false;
        combatTurnCount = 0;
        linkedSummoner = null;
        isInFearState = false;
        combatStartActionUsed = false;
        trollAttackBonusDamage = 0;
        mobilityBonusFromSpecials = 0;
        dragoonTwinAttackSuppressed = false;
        hasFlyingAnimationOverride = false;
        flyingAnimationOverrideValue = false;
        forcedFearTurnsRemaining = 0;
        ReleaseExtraOccupiedCells();
        ClearWormTunnels();
        if (specialBehavior == EnemySpecialBehavior.GiantWormTunnelBoss)
        {
            EnsureWormTunnelAt(gridPosition);
        }
        else if (specialBehavior == EnemySpecialBehavior.TrollShockwaveBoss)
        {
            ClaimTrollOccupiedCells();
        }
        else if (specialBehavior == EnemySpecialBehavior.DragoonTwinBoss)
        {
            SetFlyingOverride(true);
        }
        RefreshStatusVisuals();
        RefreshFlyingAnimationState();
        PlayBodySpawnTween();
    }

    public void ApplyTourment(TourmentData tourmentData)
    {
        if (tourmentData == null)
        {
            return;
        }

        int previousMaxHealth = maxHealth;
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * tourmentData.EnemyHealthMultiplier));
        force = Mathf.Max(0, force + tourmentData.EnemyForceBonus);
        resistance = Mathf.Max(0, resistance + tourmentData.EnemyResistanceBonus);
        regenPerTurn = Mathf.Max(0, regenPerTurn + tourmentData.EnemyRegenBonus);

        if (currentHealth >= previousMaxHealth)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(0, maxHealth - previousMaxHealth), 0, maxHealth);
        }

        RefreshHpBar();
    }

    public void SetLinkedSummoner(Enemy summoner)
    {
        linkedSummoner = summoner;
    }

    public void HandleLinkedSummonEliminated(Enemy summon)
    {
        if (summon == null || isDying || currentHealth <= 0)
        {
            return;
        }

        if (specialBehavior == EnemySpecialBehavior.RagnarWarboss
            && summon.SpecialBehavior == EnemySpecialBehavior.RagnarOgreMinion)
        {
            mobilityBonusFromSpecials++;
            StartCoroutine(PlayRagnarOgreDeathBuffFeedback());
        }
    }

    public bool HasCombatStartActionReady()
    {
        return specialBehavior == EnemySpecialBehavior.SnakePoisonOpener
            && !combatStartActionUsed
            && currentHealth > 0
            && !isDying;
    }

    public IEnumerator ExecuteCombatStartAction(Character target)
    {
        if (!HasCombatStartActionReady() || target == null)
        {
            yield break;
        }

        combatStartActionUsed = true;
        if (specialStartDelay > 0f)
        {
            yield return new WaitForSeconds(specialStartDelay);
        }

        FaceTargetForAttack(target);
        TriggerOptionalActionAnimation();
        if (projectilePrefab != null)
        {
            yield return FireJumpProjectileAt(target, specialJumpDuration, specialJumpPower);
        }
        else if (specialWindupDuration > 0f)
        {
            yield return new WaitForSeconds(specialWindupDuration);
        }

        target.ApplyPoisoned();
        yield return new WaitForSeconds(0.08f);
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

    public int TakeDamage(int incomingDamage, DamageSoundType hitSoundType = DamageSoundType.Default, bool wasAbilityDamage = false)
    {
        if (isDying)
        {
            return 0;
        }

        bool wasEligibleForFear = ShouldUseFleeBehaviour();
        int finalDamage = Mathf.Max(1, incomingDamage - resistance);
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);

        blinkFeedback?.Blink(Color.white, 0.5f, 0.12f);
        cam.Instance?.CamShake(finalDamage);
        SoundManager.Instance?.PlayDamageSound(hitSoundType, EffectAnchor.position);
        RefreshHpBar();

        bool isNowEligibleForFear = ShouldUseFleeBehaviour();
        if (!wasEligibleForFear && isNowEligibleForFear)
        {
            ScheduleFearFeedback();
        }

        if (wasAbilityDamage && finalDamage > 0 && specialBehavior == EnemySpecialBehavior.DragoonTwinBoss)
        {
            dragoonTwinAttackSuppressed = true;
            SetFlyingOverride(false);
        }

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

    public void PlayCharacterCollisionBump(Vector3 characterWorldPosition, float duration)
    {
        if (duration <= 0f || !isActiveAndEnabled)
        {
            return;
        }

        Vector3 basePosition = transform.position;
        Vector3 awayDirection = basePosition - characterWorldPosition;
        awayDirection.y = 0f;
        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            awayDirection = -transform.forward;
            awayDirection.y = 0f;
        }

        awayDirection = awayDirection.sqrMagnitude > 0.0001f
            ? awayDirection.normalized
            : Vector3.back;

        Vector3 impactPeakPosition = basePosition + awayDirection * 0.18f + Vector3.up * 0.12f;
        float halfDuration = Mathf.Max(0.01f, duration * 0.5f);

        impactTween?.Kill();
        impactTween = DOTween.Sequence()
            .Append(transform.DOMove(impactPeakPosition, halfDuration).SetEase(Ease.OutQuad))
            .Append(transform.DOMove(basePosition, halfDuration).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                impactTween = null;
                transform.position = basePosition;
            })
            .OnKill(() =>
            {
                impactTween = null;
                if (this != null && isActiveAndEnabled)
                {
                    transform.position = basePosition;
                }
            });
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
        Board.NotifyEnemyEnteredCell(this);
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

        if (dragoonRiderData != null)
        {
            yield return ExecuteDragoonRiderTurn(target);
            yield break;
        }

        if (lichData != null)
        {
            yield return ExecuteLichTurn(target);
            yield break;
        }

        int effectiveMobility = Mathf.Max(0, Mobility - pendingMobilityPenalty);
        pendingMobilityPenalty = 0;

        yield return ProcessStartOfTurnStatusEffects();
        if (isDying || currentHealth <= 0)
        {
            yield break;
        }

        if (specialBehavior == EnemySpecialBehavior.GiantWormTunnelBoss)
        {
            yield return ExecuteGiantWormTurn(target);
            yield return PerformEndOfTurnRegen();
            yield break;
        }

        if (specialBehavior == EnemySpecialBehavior.TrollShockwaveBoss)
        {
            yield return ExecuteTrollShockwaveTurn(target);
            yield return PerformEndOfTurnRegen();
            yield break;
        }

        if (specialBehavior == EnemySpecialBehavior.DragoonTwinBoss)
        {
            yield return ExecuteDragoonTwinTurn(target, effectiveMobility);
            yield return PerformEndOfTurnRegen();
            yield break;
        }

        if (specialBehavior == EnemySpecialBehavior.WolfPounce)
        {
            yield return TryExecuteWolfPounce(target);
        }
        else if (specialBehavior == EnemySpecialBehavior.BoarCharge && CanExecuteBoarCharge(target))
        {
            yield return ExecuteBoarCharge(target);
            yield break;
        }

        bool persistentFleeBehaviour = ShouldUseFleeBehaviour();
        if (forcedFearTurnsRemaining > 0)
        {
            forcedFearTurnsRemaining--;
        }

        UpdateFearState(persistentFleeBehaviour);
        if (persistentFleeBehaviour && maxFleeTurns > 0 && consecutiveFleeTurns >= maxFleeTurns)
        {
            persistentFleeBehaviour = false;
            DisablePersistentFlee();
        }

        if (persistentFleeBehaviour && ShouldStopFleePermanently(target))
        {
            persistentFleeBehaviour = false;
            DisablePersistentFlee();
        }

        bool temporaryFleeMove = attackFirst && attackAlways && !persistentFleeBehaviour;

        if (attackFirst)
        {
            if (CanAttackTargetFrom(gridPosition, target, attackAlways))
            {
                yield return AttackIfPossible(target, attackAlways);
                if (fleeAfterAttacking)
                {
                    yield return ExecutePostAttackFlee(target, effectiveMobility);
                    yield return PerformEndOfTurnRegen();
                    yield break;
                }
            }
            else
            {
                yield return TryPerformOptionalAction();
            }
        }

        bool useFleeBehaviour = persistentFleeBehaviour || temporaryFleeMove;
        List<Vector2Int> plannedPath = BuildMovementPlan(target, useFleeBehaviour, effectiveMobility);
        int movementSpentThisTurn = 0;
        if (persistentFleeBehaviour && effectiveMobility > 0 && plannedPath.Count == 0)
        {
            persistentFleeBehaviour = false;
            DisablePersistentFlee();
        }

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

            movementSpentThisTurn++;
            yield return WaitForMovementStepResolution();

            if (specialBehavior == EnemySpecialBehavior.BoarCharge && CanExecuteBoarCharge(target))
            {
                yield return ExecuteBoarCharge(target);
                yield break;
            }
        }

        if (!attackFirst)
        {
            if (CanAttackTargetFrom(gridPosition, target, attackAlways))
            {
                yield return AttackIfPossible(target, attackAlways);
                if (fleeAfterAttacking)
                {
                    yield return ExecutePostAttackFlee(target, effectiveMobility);
                    yield return PerformEndOfTurnRegen();
                    yield break;
                }
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

        HandleMovementExhaustionDamage(movementSpentThisTurn, effectiveMobility);
        yield return PerformEndOfTurnRegen();
    }

    private IEnumerator ExecuteLichTurn(Character target)
    {
        if (Board == null || target == null || lichData == null)
        {
            yield break;
        }

        int effectiveMobility = Mathf.Max(0, Mobility - pendingMobilityPenalty);
        pendingMobilityPenalty = 0;

        yield return ProcessStartOfTurnStatusEffects();
        if (isDying || currentHealth <= 0)
        {
            yield break;
        }

        combatTurnCount++;
        if (ShouldUseLichSummonTurn())
        {
            consecutiveFleeTurns = 0;
            yield return PerformLichSummonTurn();
            yield break;
        }

        bool persistentFleeBehaviour = ShouldUseFleeBehaviour();
        if (forcedFearTurnsRemaining > 0)
        {
            forcedFearTurnsRemaining--;
        }

        UpdateFearState(persistentFleeBehaviour);
        if (persistentFleeBehaviour && maxFleeTurns > 0 && consecutiveFleeTurns >= maxFleeTurns)
        {
            persistentFleeBehaviour = false;
            DisablePersistentFlee();
        }

        if (persistentFleeBehaviour && ShouldStopFleePermanently(target))
        {
            persistentFleeBehaviour = false;
            DisablePersistentFlee();
        }

        bool temporaryFleeMove = attackFirst && attackAlways && !persistentFleeBehaviour;

        if (attackFirst)
        {
            if (CanAttackTargetFrom(gridPosition, target, attackAlways))
            {
                yield return AttackIfPossible(target, attackAlways);
                if (fleeAfterAttacking)
                {
                    yield return ExecutePostAttackFlee(target, effectiveMobility);
                    yield break;
                }
            }
            else
            {
                yield return TryPerformOptionalAction();
            }
        }

        bool useFleeBehaviour = persistentFleeBehaviour || temporaryFleeMove;
        List<Vector2Int> plannedPath = BuildMovementPlan(target, useFleeBehaviour, effectiveMobility);
        int movementSpentThisTurn = 0;
        if (persistentFleeBehaviour && effectiveMobility > 0 && plannedPath.Count == 0)
        {
            persistentFleeBehaviour = false;
            DisablePersistentFlee();
        }

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

            movementSpentThisTurn++;
            yield return WaitForMovementStepResolution();
        }

        if (!attackFirst)
        {
            if (CanAttackTargetFrom(gridPosition, target, attackAlways))
            {
                yield return AttackIfPossible(target, attackAlways);
                if (fleeAfterAttacking)
                {
                    yield return ExecutePostAttackFlee(target, effectiveMobility);
                    yield break;
                }
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

        HandleMovementExhaustionDamage(movementSpentThisTurn, effectiveMobility);
    }

    private IEnumerator ExecutePostAttackFlee(Character target, int movementPointsToSpend)
    {
        if (Board == null || target == null || movementPointsToSpend <= 0)
        {
            yield break;
        }

        List<Vector2Int> fleePath = BuildMovementPlan(target, true, movementPointsToSpend);
        int movementSpent = 0;
        for (int step = 0; step < fleePath.Count; step++)
        {
            bool moved = TryMoveToPlannedStep(fleePath[step]);
            if (!moved)
            {
                yield break;
            }

            movementSpent++;
            yield return WaitForMovementStepResolution();
        }

        HandleMovementExhaustionDamage(movementSpent, movementPointsToSpend);
    }

    private IEnumerator PerformEndOfTurnRegen()
    {
        if (regenPerTurn <= 0 || isDying || currentHealth <= 0)
        {
            yield break;
        }

        int healedAmount = Heal(regenPerTurn);
        if (healedAmount <= 0)
        {
            yield break;
        }

        SoundManager.Instance?.PlayHeal(EffectAnchor.position);
        yield return new WaitForSeconds(0.08f);
    }

    public void BeginMovementInterrupt()
    {
        movementInterruptCount++;
    }

    public void EndMovementInterrupt()
    {
        movementInterruptCount = Mathf.Max(0, movementInterruptCount - 1);
    }

    private IEnumerator WaitForMovementStepResolution()
    {
        yield return new WaitUntil(() => !IsMoving && !IsMovementInterrupted);
        yield return new WaitForSeconds(0.05f);
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
            int currentDistanceFromTarget = Board.GetManhattanDistance(gridPosition, target.GridPosition);
            int bestFleeScore = int.MinValue;
            int bestFleePathDistance = -1;
            int bestRangedAttackFleeScore = int.MinValue;
            int bestRangedAttackPathDistance = -1;
            Vector2Int bestDestination = gridPosition;
            Vector2Int bestRangedAttackDestination = gridPosition;
            bool foundDestination = false;
            bool foundRangedAttackDestination = false;
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
                bool supportsRangedRetreatAttack = attackPattern == EnemyAttackPattern.Projectile
                    && fleeScore >= currentDistanceFromTarget
                    && CanAttackTargetFrom(cellPosition, target, false);

                if (supportsRangedRetreatAttack
                    && (!foundRangedAttackDestination
                        || fleeScore > bestRangedAttackFleeScore
                        || (fleeScore == bestRangedAttackFleeScore && pathDistance > bestRangedAttackPathDistance)))
                {
                    foundRangedAttackDestination = true;
                    bestRangedAttackDestination = cellPosition;
                    bestRangedAttackFleeScore = fleeScore;
                    bestRangedAttackPathDistance = pathDistance;
                }

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

            if (foundRangedAttackDestination)
            {
                bestDestination = bestRangedAttackDestination;
            }

            if (!foundRangedAttackDestination && bestFleeScore <= currentDistanceFromTarget)
            {
                return plannedPath;
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
            if (!foundRangedAttackDestination && plannedPath.Count <= 1)
            {
                plannedPath.Clear();
            }

            return plannedPath;
        }

        bool shouldAdvanceAggressively = advanceTowardsCharacterWhenAlreadyInRange;
        PathPlan attackPlan = default;
        PathPlan approachPlan = default;
        PathPlan fallbackApproachPlan = default;
        bool hasAttackObjective = !shouldAdvanceAggressively
            && TryFindBestAttackObjective(target, maxSteps, out _, out attackPlan);
        bool hasApproachPath = TryBuildWeightedPathForCurrentMovementRules(gridPosition, target.GridPosition, out approachPlan, true);
        bool hasFallbackApproachObjective = !hasApproachPath
            && TryFindBestApproachObjective(target, out _, out fallbackApproachPlan);

        if (hasAttackObjective)
        {
            bool preferApproachPath = attackPlan.UsesLethalHazard
                && hasApproachPath
                && !approachPlan.UsesLethalHazard
                && approachPlan.StepCount <= attackPlan.StepCount + 5;

            if (!preferApproachPath)
            {
                AppendPathPrefix(plannedPath, attackPlan.Path, maxSteps);
                return plannedPath;
            }
        }

        if (hasApproachPath)
        {
            AppendPathPrefix(plannedPath, approachPlan.Path, maxSteps);
        }
        else if (hasFallbackApproachObjective)
        {
            AppendPathPrefix(plannedPath, fallbackApproachPlan.Path, maxSteps);
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
        Board.NotifyEnemyEnteredCell(this);
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

    public void ApplyFear(int turnCount = 1)
    {
        if (turnCount <= 0)
        {
            return;
        }

        forcedFearTurnsRemaining = Mathf.Max(forcedFearTurnsRemaining, turnCount);
        UpdateFearState(true);
    }

    public IEnumerator ApplyMistyConfusion(bool paranoia, bool brokenHeart, bool reduceMobilityNextTurn, float allyDamageMultiplier = 1f, float brokenHeartSelfDamageRatio = 0f)
    {
        mistyConfusionParanoia = paranoia;
        mistyConfusionBrokenHeart = brokenHeart;
        mistyConfusionAllyDamageMultiplier = Mathf.Max(1f, allyDamageMultiplier);
        mistyConfusionBrokenHeartSelfDamageRatio = Mathf.Clamp01(brokenHeartSelfDamageRatio);
        ShowMistyConfusionFx();

        if (reduceMobilityNextTurn)
        {
            ApplyMobilityPenaltyNextTurn(1);
        }

        if (currentHealth <= 0 || !CanAttackAnyEnemyAlly(true))
        {
            ClearMistyConfusion();
            yield break;
        }

        yield return ResolveMistyConfusionTurn();
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
        Board.NotifyEnemyEnteredCell(this);
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
        bodyRotationTween?.Kill();
        fearBodyWiggleTween?.Kill();
        impactTween?.Kill();
        if (pendingFearFeedbackCoroutine != null)
        {
            StopCoroutine(pendingFearFeedbackCoroutine);
            pendingFearFeedbackCoroutine = null;
        }
        lockWormRootPosition = false;
        IsMoving = false;
        SetFlyingAnimation(false);
        RefreshCanvasHeight(false);
        ClearMistyConfusion();
        ClearDeathMarkFx();
    }

    private void LateUpdate()
    {
        if (!lockWormRootPosition || specialBehavior != EnemySpecialBehavior.GiantWormTunnelBoss || Board == null)
        {
            return;
        }

        transform.position = Board.GridToWorldPosition(lockedWormRootCell) + Vector3.up * spawnHeight;
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

    private void CacheSpecialAnchors()
    {
        if (projectileSpawnPos == null)
        {
            projectileSpawnPos = FindDeepChild(transform, "ProjectileSpawnPos");
        }

        if (fireBallSpawnPos == null)
        {
            fireBallSpawnPos = FindDeepChild(transform, "FireBallSpawnPos");
        }

        if (fireBallSpawnPos == null)
        {
            fireBallSpawnPos = FindDeepChild(transform, "Mouth");
        }

        if (fireBallSpawnPos == null)
        {
            fireBallSpawnPos = FindDeepChild(transform, "HandR");
        }

        if (fireBallSpawnPos == null)
        {
            fireBallSpawnPos = enemyBody != null ? enemyBody : EffectAnchor;
        }
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == childName)
            {
                return child;
            }

            Transform nestedChild = FindDeepChild(child, childName);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
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
            cachedBodyOriginalLocalEulerAngles = enemyBody.localEulerAngles;
            cachedBodyOriginalLocalPosition = enemyBody.localPosition;
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
        if (forcedFearTurnsRemaining > 0)
        {
            return true;
        }

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

    private void HandleMovementExhaustionDamage(int movementSpent, int availableMovement)
    {
        if (movementSpent <= 0 || availableMovement <= 0 || movementSpent < availableMovement)
        {
            return;
        }

        Character playerCharacter = Board != null && Board.Player != null ? Board.Player.ControlledCharacter : null;
        if (playerCharacter == null || !playerCharacter.HasItem(ItemRewardKey.MouseTrap) || currentHealth <= 0)
        {
            return;
        }

        TakeDamage(1, DamageSoundType.Default);
    }

    private void DisablePersistentFlee()
    {
        flee = false;
        fleePermanentlyDisabled = true;
        consecutiveFleeTurns = 0;
        UpdateFearState(false);
    }

    private void UpdateFearState(bool shouldBeInFearState)
    {
        if (shouldBeInFearState == isInFearState)
        {
            return;
        }

        isInFearState = shouldBeInFearState;
        if (isInFearState)
        {
            PlayFearFeedback();
        }
        else
        {
            StopFearWiggle();
        }
    }

    private void ScheduleFearFeedback()
    {
        if (pendingFearFeedbackCoroutine != null)
        {
            StopCoroutine(pendingFearFeedbackCoroutine);
        }

        pendingFearFeedbackCoroutine = StartCoroutine(PlayFearFeedbackAfterDelay());
    }

    private IEnumerator PlayFearFeedbackAfterDelay()
    {
        if (fearFeedbackDelay > 0f)
        {
            yield return new WaitForSeconds(fearFeedbackDelay);
        }

        pendingFearFeedbackCoroutine = null;
        if (!ShouldUseFleeBehaviour())
        {
            yield break;
        }

        UpdateFearState(true);
    }

    private void PlayFearFeedback()
    {
        PlayFearFx();
        PlayFearWiggle();
    }

    private void PlayFearFx()
    {
        (Transform anchor, Vector3 offset) = GetEnemyHeadFxAnchor();
        GetStatusFxLibrary()?.SpawnFearFx(anchor, offset);
    }

    private CombatStatusFxLibrary GetStatusFxLibrary()
    {
        if (statusFxLibrary == null)
        {
            statusFxLibrary = CombatStatusFxLibrary.LoadDefault();
        }

        return statusFxLibrary;
    }

    private void PlayStatusApplyFx(CombatStatusType statusType)
    {
        (Transform anchor, Vector3 offset) = GetEnemyStatusFxAnchor(statusType, false);
        GetStatusFxLibrary()?.SpawnApplyFx(statusType, anchor, offset);
    }

    private void RefreshPersistentStatusFx(CombatStatusType statusType)
    {
        bool shouldBeActive = HasStatusEffect(statusType);
        if (!shouldBeActive)
        {
            ClearPersistentStatusFx(statusType);
            return;
        }

        if (activePersistentStatusFxInstances.TryGetValue(statusType, out GameObject existingInstance) && existingInstance != null)
        {
            return;
        }

        (Transform anchor, Vector3 offset) = GetEnemyStatusFxAnchor(statusType, true);
        GameObject spawnedFx = GetStatusFxLibrary()?.SpawnPersistentFx(statusType, anchor, offset);
        if (spawnedFx != null)
        {
            activePersistentStatusFxInstances[statusType] = spawnedFx;
        }
    }

    private (Transform anchor, Vector3 localOffset) GetEnemyStatusFxAnchor(CombatStatusType statusType, bool persistent)
    {
        switch (statusType)
        {
            case CombatStatusType.Frozen:
            case CombatStatusType.Poisoned:
                return (enemyBody != null ? enemyBody : transform, Vector3.zero);
            case CombatStatusType.Bleeding:
            default:
                return GetEnemyHeadFxAnchor();
        }
    }

    private (Transform anchor, Vector3 localOffset) GetEnemyHeadFxAnchor()
    {
        return (EffectAnchor != null ? EffectAnchor : transform, Vector3.zero);
    }

    private void ClearPersistentStatusFx(CombatStatusType statusType)
    {
        if (!activePersistentStatusFxInstances.TryGetValue(statusType, out GameObject existingInstance))
        {
            return;
        }

        activePersistentStatusFxInstances.Remove(statusType);
        if (existingInstance != null)
        {
            Destroy(existingInstance);
        }
    }

    private void PlayFearWiggle()
    {
        CacheBody();
        Transform targetBody = enemyBody != null ? enemyBody : null;
        if (targetBody == null || fearBodyWiggleStrength <= 0f)
        {
            return;
        }

        fearBodyWiggleTween?.Kill();
        targetBody.localPosition = cachedBodyOriginalLocalPosition;
        fearBodyWiggleTween = targetBody.DOShakePosition(
                0.45f,
                new Vector3(fearBodyWiggleStrength * 0.03f, fearBodyWiggleStrength * 0.015f, fearBodyWiggleStrength * 0.03f),
                18,
                90f,
                false,
                true)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                fearBodyWiggleTween = null;
                if (targetBody != null)
                {
                    targetBody.localPosition = cachedBodyOriginalLocalPosition;
                }
            })
            .OnKill(() =>
            {
                fearBodyWiggleTween = null;
                if (targetBody != null)
                {
                    targetBody.localPosition = cachedBodyOriginalLocalPosition;
                }
            });
    }

    private void StopFearWiggle()
    {
        fearBodyWiggleTween?.Kill();
        if (enemyBody != null)
        {
            enemyBody.localPosition = cachedBodyOriginalLocalPosition;
        }
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

                if (target.HasItem(ItemRewardKey.Hood) && manhattanDistance >= 5)
                {
                    return false;
                }

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

        if (Board != null && Board.TryGetSkullObject(candidateCell.GridPosition, out _))
        {
            return remainingMovesAfterThisStep > 0 && CanExitObstacleZone(candidateCell.GridPosition, remainingMovesAfterThisStep);
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

        int currentDistanceFromTarget = Board.GetManhattanDistance(gridPosition, target.GridPosition);
        int bestScore = int.MinValue;
        int bestRangedAttackScore = int.MinValue;
        Vector2Int bestRangedAttackStep = gridPosition;
        bool foundRangedAttackStep = false;
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
            bool supportsRangedRetreatAttack = attackPattern == EnemyAttackPattern.Projectile
                && fleeScore >= currentDistanceFromTarget
                && CanAttackTargetFrom(candidate, target, false);

            if (supportsRangedRetreatAttack && fleeScore > bestRangedAttackScore)
            {
                bestRangedAttackScore = fleeScore;
                bestRangedAttackStep = candidate;
                foundRangedAttackStep = true;
            }

            if (fleeScore > bestScore)
            {
                bestScore = fleeScore;
                bestStep = candidate;
            }
        }

        if (foundRangedAttackStep)
        {
            bestStep = bestRangedAttackStep;
            return true;
        }

        if (bestScore <= currentDistanceFromTarget)
        {
            bestStep = gridPosition;
            return false;
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

        if (Board != null && Board.TryGetSkullObject(cell.GridPosition, out _))
        {
            return false;
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
        return TryFindBestAttackObjective(target, int.MaxValue, out bestObjective, out _);
    }

    private bool TryFindBestAttackObjective(Character target, out Vector2Int bestObjective, out PathPlan bestPlan)
    {
        return TryFindBestAttackObjective(target, int.MaxValue, out bestObjective, out bestPlan);
    }

    private bool TryFindBestAttackObjective(Character target, int maxReachableSteps, out Vector2Int bestObjective, out PathPlan bestPlan)
    {
        bestObjective = gridPosition;
        bestPlan = default;
        if (Board == null || target == null || Board.Cells == null)
        {
            return false;
        }

        bool foundObjective = false;
        bool foundReachableObjective = false;
        int bestPathCost = int.MaxValue;
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

                if (!TryBuildWeightedPathForCurrentMovementRules(gridPosition, cellPosition, out PathPlan candidatePlan))
                {
                    continue;
                }

                bool candidateIsReachableThisTurn = candidatePlan.StepCount <= Mathf.Max(0, maxReachableSteps);
                if (!foundObjective
                    || (candidateIsReachableThisTurn && !foundReachableObjective)
                    || candidatePlan.TotalCost < bestPathCost
                    || (candidatePlan.TotalCost == bestPathCost
                        && candidatePlan.StepCount < bestPathDistance)
                    || (candidatePlan.TotalCost == bestPathCost
                        && candidatePlan.StepCount == bestPathDistance
                        && Board.GetManhattanDistance(cellPosition, target.GridPosition) < Board.GetManhattanDistance(bestObjective, target.GridPosition)))
                {
                    if (foundReachableObjective && !candidateIsReachableThisTurn)
                    {
                        continue;
                    }

                    foundObjective = true;
                    foundReachableObjective = candidateIsReachableThisTurn;
                    bestObjective = cellPosition;
                    bestPathCost = candidatePlan.TotalCost;
                    bestPathDistance = candidatePlan.StepCount;
                    bestPlan = candidatePlan;
                }
            }
        }

        return foundObjective;
    }

    private bool TryFindBestApproachObjective(Character target, out Vector2Int bestObjective, out PathPlan bestPlan)
    {
        bestObjective = gridPosition;
        bestPlan = default;
        if (Board == null || target == null || Board.Cells == null)
        {
            return false;
        }

        bool foundObjective = false;
        int bestFallbackScore = int.MaxValue;
        int bestPathCost = int.MaxValue;
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

                if (!Board.TryGetCell(cellPosition, out BoardCell cell) || !CanEndMovementOnCell(cell))
                {
                    continue;
                }

                if (!TryBuildWeightedPathForCurrentMovementRules(gridPosition, cellPosition, out PathPlan candidatePlan))
                {
                    continue;
                }

                int fallbackScore = GetFutureAttackDistance(cellPosition, target);
                if (fallbackScore == int.MaxValue)
                {
                    fallbackScore = GetBestReachableDistanceToTarget(cellPosition, target.GridPosition);
                }

                if (!foundObjective
                    || fallbackScore < bestFallbackScore
                    || (fallbackScore == bestFallbackScore && candidatePlan.TotalCost < bestPathCost)
                    || (fallbackScore == bestFallbackScore && candidatePlan.TotalCost == bestPathCost && candidatePlan.StepCount < bestPathDistance)
                    || (fallbackScore == bestFallbackScore
                        && candidatePlan.TotalCost == bestPathCost
                        && candidatePlan.StepCount == bestPathDistance
                        && Board.GetManhattanDistance(cellPosition, target.GridPosition) < Board.GetManhattanDistance(bestObjective, target.GridPosition)))
                {
                    foundObjective = true;
                    bestObjective = cellPosition;
                    bestPlan = candidatePlan;
                    bestFallbackScore = fallbackScore;
                    bestPathCost = candidatePlan.TotalCost;
                    bestPathDistance = candidatePlan.StepCount;
                }
            }
        }

        return foundObjective;
    }

    private bool TryBuildWeightedPathForCurrentMovementRules(Vector2Int start, Vector2Int goal, out PathPlan plan, bool allowOccupiedGoal = false)
    {
        plan = default;
        if (Board == null || !Board.IsInsideBoard(start) || !Board.IsInsideBoard(goal) || start == goal)
        {
            return false;
        }

        if (!Board.TryGetCell(goal, out BoardCell goalCell) || (!allowOccupiedGoal && !CanEndMovementOnCell(goalCell)))
        {
            return false;
        }

        List<Vector2Int> openNodes = new List<Vector2Int> { start };
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
        Dictionary<Vector2Int, int> totalCost = new Dictionary<Vector2Int, int> { [start] = 0 };
        Dictionary<Vector2Int, int> stepCount = new Dictionary<Vector2Int, int> { [start] = 0 };
        Dictionary<Vector2Int, bool> usesLethalHazard = new Dictionary<Vector2Int, bool> { [start] = false };

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        while (openNodes.Count > 0)
        {
            int bestIndex = 0;
            Vector2Int current = openNodes[0];
            for (int index = 1; index < openNodes.Count; index++)
            {
                Vector2Int candidate = openNodes[index];
                if (totalCost[candidate] < totalCost[current]
                    || (totalCost[candidate] == totalCost[current] && stepCount[candidate] < stepCount[current]))
                {
                    current = candidate;
                    bestIndex = index;
                }
            }

            openNodes.RemoveAt(bestIndex);
            if (current == goal)
            {
                break;
            }

            for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                Vector2Int next = current + directions[directionIndex];
                if (!Board.TryGetCell(next, out BoardCell nextCell))
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

                int extraCost = 1 + GetPathPenaltyForCell(nextCell, out bool isLethalHazard);
                int candidateCost = totalCost[current] + extraCost;
                int candidateSteps = stepCount[current] + 1;
                bool candidateUsesLethal = usesLethalHazard[current] || isLethalHazard;

                if (!totalCost.TryGetValue(next, out int knownCost)
                    || candidateCost < knownCost
                    || (candidateCost == knownCost && candidateSteps < stepCount[next]))
                {
                    totalCost[next] = candidateCost;
                    stepCount[next] = candidateSteps;
                    usesLethalHazard[next] = candidateUsesLethal;
                    cameFrom[next] = current;
                    if (!openNodes.Contains(next))
                    {
                        openNodes.Add(next);
                    }
                }
            }
        }

        if (!cameFrom.ContainsKey(goal))
        {
            return false;
        }

        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int currentStep = goal;
        while (currentStep != start)
        {
            path.Add(currentStep);
            currentStep = cameFrom[currentStep];
        }

        path.Reverse();
        plan = new PathPlan
        {
            Path = path,
            TotalCost = totalCost[goal],
            StepCount = stepCount[goal],
            UsesLethalHazard = usesLethalHazard[goal]
        };
        return path.Count > 0;
    }

    private int GetPathPenaltyForCell(BoardCell cell, out bool isLethalHazard)
    {
        isLethalHazard = false;
        if (cell?.Hazard == null || !cell.Hazard.IsVisibleToEnemies)
        {
            return 0;
        }

        int penalty = Mathf.Max(0, cell.Hazard.GetEnemyPathPenalty(this));
        isLethalHazard = cell.Hazard.WouldKillEnemy(this);
        if (isLethalHazard)
        {
            penalty += 1000;
        }

        return penalty;
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

    private void TriggerAttackAnimation(AnimationClip attackClipOverride = null)
    {
        CacheAnimator();
        if (enemyAnimator != null && !string.IsNullOrEmpty(attackTriggerParameter))
        {
            ApplyAttackAnimationOverride(attackClipOverride);
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

    private void TriggerOptionalSecondaryActionAnimation()
    {
        CacheAnimator();
        if (enemyAnimator != null && !string.IsNullOrEmpty(optionalActionSecondaryTriggerParameter))
        {
            enemyAnimator.SetTrigger(optionalActionSecondaryTriggerParameter);
        }
    }

    private void ApplyAttackAnimationOverride(AnimationClip attackClipOverride)
    {
        if (enemyAnimator == null || attackStateSourceClip == null || attackClipOverride == null)
        {
            return;
        }

        EnsureEnemyAnimatorOverrideController();
        if (enemyAnimatorOverrideController == null)
        {
            return;
        }

        enemyAnimatorOverrideController[attackStateSourceClip] = attackClipOverride;
    }

    private void EnsureEnemyAnimatorOverrideController()
    {
        if (enemyAnimator == null || attackStateSourceClip == null)
        {
            return;
        }

        if (enemyAnimatorOverrideController != null)
        {
            return;
        }

        RuntimeAnimatorController runtimeController = enemyAnimator.runtimeAnimatorController;
        if (runtimeController == null)
        {
            return;
        }

        enemyAnimatorOverrideController = new AnimatorOverrideController(runtimeController);
        enemyAnimator.runtimeAnimatorController = enemyAnimatorOverrideController;
    }

    private void RefreshFlyingAnimationState()
    {
        if (Board == null || !Board.TryGetCell(gridPosition, out BoardCell currentCell))
        {
            SetFlyingAnimation(false);
            RefreshCanvasHeight(false);
            return;
        }

        if (hasFlyingAnimationOverride)
        {
            SetFlyingAnimation(flyingAnimationOverrideValue);
            bool shouldRaiseCanvas = flyingAnimationOverrideValue
                && specialBehavior != EnemySpecialBehavior.DragoonTwinBoss;
            RefreshCanvasHeight(shouldRaiseCanvas);
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

        enemyAnimator.speed = 1f;
        enemyAnimator.SetBool(flyingBoolParameter, isFlying);
    }

    private void SetFlyingOverride(bool isFlying)
    {
        hasFlyingAnimationOverride = true;
        flyingAnimationOverrideValue = isFlying;
        SetFlyingAnimation(isFlying);
        bool shouldRaiseCanvas = isFlying && specialBehavior != EnemySpecialBehavior.DragoonTwinBoss;
        RefreshCanvasHeight(shouldRaiseCanvas);
    }

    private void ClearAnimationTriggers()
    {
        CacheAnimator();
        if (enemyAnimator == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(attackTriggerParameter))
        {
            enemyAnimator.ResetTrigger(attackTriggerParameter);
        }

        if (!string.IsNullOrWhiteSpace(optionalActionTriggerParameter))
        {
            enemyAnimator.ResetTrigger(optionalActionTriggerParameter);
        }

        if (!string.IsNullOrWhiteSpace(optionalActionSecondaryTriggerParameter))
        {
            enemyAnimator.ResetTrigger(optionalActionSecondaryTriggerParameter);
        }
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
        FaceTargetForAttack(target != null ? target.transform : null);
    }

    private void FaceTargetForAttack(Transform targetTransform)
    {
        if (!lookAtTargetWhenAttacking || targetTransform == null)
        {
            return;
        }

        Vector3 targetDirection = targetTransform.position - transform.position;
        targetDirection.y = 0f;
        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        CacheBody();
        Transform targetBody = enemyBody != null ? enemyBody : transform;
        float targetYaw = Quaternion.LookRotation((-targetDirection).normalized, Vector3.up).eulerAngles.y;
        ApplyBodyLocalYaw(targetBody, targetYaw);
    }

    private IEnumerator ResetBodyRotationToOrigin(float duration)
    {
        CacheBody();
        Transform targetBody = enemyBody != null ? enemyBody : transform;
        if (targetBody == null)
        {
            yield break;
        }

        bodyRotationTween?.Kill();
        if (duration <= 0f)
        {
            ApplyBodyLocalYaw(targetBody, cachedBodyOriginalLocalEulerAngles.y);
            yield break;
        }

        bodyRotationTween = TweenBodyLocalYaw(targetBody, cachedBodyOriginalLocalEulerAngles.y, duration);
        yield return bodyRotationTween.WaitForCompletion();
    }

    private IEnumerator RotateBodyTowardsTarget(Character target, float duration)
    {
        if (!lookAtTargetWhenAttacking || target == null)
        {
            yield break;
        }

        Vector3 targetDirection = target.transform.position - transform.position;
        targetDirection.y = 0f;
        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            yield break;
        }

        CacheBody();
        Transform targetBody = enemyBody != null ? enemyBody : transform;
        float targetYaw = Quaternion.LookRotation((-targetDirection).normalized, Vector3.up).eulerAngles.y;

        bodyRotationTween?.Kill();
        if (duration <= 0f)
        {
            ApplyBodyLocalYaw(targetBody, targetYaw);
            yield break;
        }

        bodyRotationTween = TweenBodyLocalYaw(targetBody, targetYaw, duration);
        yield return bodyRotationTween.WaitForCompletion();
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
        ApplyBodyLocalYaw(targetBody, targetYaw);
    }

    private Tween TweenBodyLocalYaw(Transform targetBody, float targetYaw, float duration)
    {
        if (targetBody == null)
        {
            return null;
        }

        float startYaw = targetBody.localEulerAngles.y;
        return DOVirtual.Float(startYaw, targetYaw, Mathf.Max(0.01f, duration), value =>
            {
                ApplyBodyLocalYaw(targetBody, value);
            })
            .SetEase(Ease.OutQuad)
            .OnComplete(() => bodyRotationTween = null)
            .OnKill(() => bodyRotationTween = null);
    }

    private void ApplyBodyLocalYaw(Transform targetBody, float yaw)
    {
        if (targetBody == null)
        {
            return;
        }

        Vector3 localEulerAngles = targetBody.localEulerAngles;
        localEulerAngles.x = cachedBodyOriginalLocalEulerAngles.x;
        localEulerAngles.y = yaw;
        localEulerAngles.z = cachedBodyOriginalLocalEulerAngles.z;
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

    private IEnumerator ExecuteDragoonRiderTurn(Character target)
    {
        if (dragoonRiderData == null || Board == null || target == null)
        {
            yield break;
        }

        int effectiveMobility = Mathf.Max(0, Mobility - pendingMobilityPenalty);
        pendingMobilityPenalty = 0;

        yield return ProcessStartOfTurnStatusEffects();
        if (isDying || currentHealth <= 0)
        {
            yield break;
        }

        if (CanPerformDragoonMeleeFrom(gridPosition, target))
        {
            yield return PerformDragoonMeleeAttack(target);
            yield break;
        }

        if (TryFindBestDragoonMeleeObjective(target, effectiveMobility, out PathPlan meleePlan))
        {
            yield return FollowPathPlan(meleePlan.Path);
            if (CanPerformDragoonMeleeFrom(gridPosition, target))
            {
                yield return PerformDragoonMeleeAttack(target);
            }

            yield break;
        }

        if (CanPerformDragoonRangedFrom(gridPosition, target))
        {
            yield return PerformDragoonRangedAttack(target);
            yield break;
        }

        if (TryFindBestDragoonRangedObjective(target, effectiveMobility, out PathPlan rangedPlan))
        {
            yield return FollowPathPlan(rangedPlan.Path);
            if (CanPerformDragoonRangedFrom(gridPosition, target))
            {
                yield return PerformDragoonRangedAttack(target);
                yield break;
            }
        }

        yield return PerformDragoonFlightAndSummon(target);
    }

    private IEnumerator FollowPathPlan(List<Vector2Int> path)
    {
        if (path == null)
        {
            yield break;
        }

        for (int index = 0; index < path.Count; index++)
        {
            if (!TryMoveToPlannedStep(path[index]))
            {
                yield break;
            }

            yield return WaitForMovementStepResolution();
        }
    }

    private bool CanPerformDragoonMeleeFrom(Vector2Int origin, Character target)
    {
        if (target == null)
        {
            return false;
        }

        Vector2Int delta = target.GridPosition - origin;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
    }

    private bool CanPerformDragoonRangedFrom(Vector2Int origin, Character target)
    {
        if (target == null || Board == null || dragoonRiderData == null)
        {
            return false;
        }

        if (CanPerformDragoonMeleeFrom(origin, target))
        {
            return false;
        }

        int distance = Mathf.Abs(target.GridPosition.x - origin.x) + Mathf.Abs(target.GridPosition.y - origin.y);
        if (distance <= 0 || distance > dragoonRiderData.RangedAttackRange)
        {
            return false;
        }

        return Board.HasLineOfSight(origin, target.GridPosition);
    }

    private bool TryFindBestDragoonMeleeObjective(Character target, int maxSteps, out PathPlan bestPlan)
    {
        bestPlan = default;
        if (Board == null || target == null || maxSteps <= 0)
        {
            return false;
        }

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        bool foundPlan = false;
        int bestCost = int.MaxValue;
        int bestSteps = int.MaxValue;
        for (int index = 0; index < directions.Length; index++)
        {
            Vector2Int candidateCell = target.GridPosition + directions[index];
            if (!Board.TryGetCell(candidateCell, out BoardCell cell) || !CanEndMovementOnCell(cell))
            {
                continue;
            }

            if (!TryBuildWeightedPathForCurrentMovementRules(gridPosition, candidateCell, out PathPlan candidatePlan))
            {
                continue;
            }

            if (candidatePlan.StepCount > maxSteps)
            {
                continue;
            }

            if (!foundPlan
                || candidatePlan.TotalCost < bestCost
                || (candidatePlan.TotalCost == bestCost && candidatePlan.StepCount < bestSteps))
            {
                foundPlan = true;
                bestPlan = candidatePlan;
                bestCost = candidatePlan.TotalCost;
                bestSteps = candidatePlan.StepCount;
            }
        }

        return foundPlan;
    }

    private bool TryFindBestDragoonRangedObjective(Character target, int maxSteps, out PathPlan bestPlan)
    {
        bestPlan = default;
        if (Board == null || target == null || maxSteps <= 0)
        {
            return false;
        }

        bool foundPlan = false;
        int bestCost = int.MaxValue;
        int bestSteps = int.MaxValue;
        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int candidateCell = new Vector2Int(x, y);
                if (candidateCell == gridPosition)
                {
                    continue;
                }

                if (!Board.TryGetCell(candidateCell, out BoardCell cell) || !CanEndMovementOnCell(cell) || !CanPerformDragoonRangedFrom(candidateCell, target))
                {
                    continue;
                }

                if (!TryBuildWeightedPathForCurrentMovementRules(gridPosition, candidateCell, out PathPlan candidatePlan))
                {
                    continue;
                }

                if (candidatePlan.StepCount > maxSteps)
                {
                    continue;
                }

                if (!foundPlan
                    || candidatePlan.TotalCost < bestCost
                    || (candidatePlan.TotalCost == bestCost && candidatePlan.StepCount < bestSteps))
                {
                    foundPlan = true;
                    bestPlan = candidatePlan;
                    bestCost = candidatePlan.TotalCost;
                    bestSteps = candidatePlan.StepCount;
                }
            }
        }

        return foundPlan;
    }

    private IEnumerator PerformDragoonMeleeAttack(Character target)
    {
        if (target == null || dragoonRiderData == null)
        {
            yield break;
        }

        FaceTargetForAttack(target);
        TriggerAttackAnimation();
        float damageDelay = GetTargetDamageDelay(target);
        if (damageDelay > 0f)
        {
            yield return new WaitForSeconds(damageDelay);
        }

        target.TakeDamage(dragoonRiderData.MeleeDamage, this, false, dragoonRiderData.MeleeDamageSoundType);
        yield return new WaitForSeconds(0.08f);
    }

    private IEnumerator PerformDragoonRangedAttack(Character target)
    {
        if (target == null || dragoonRiderData == null)
        {
            yield break;
        }

        FaceTargetForAttack(target);
        TriggerOptionalSecondaryActionAnimation();

        if (projectilePrefab != null)
        {
            yield return FireProjectileAt(target, fireBallSpawnPos != null ? fireBallSpawnPos : EffectAnchor, false);
        }

        float damageDelay = dragoonRiderData.RangedImpactDamageDelay;
        if (damageDelay > 0f)
        {
            yield return new WaitForSeconds(damageDelay);
        }

        target.TakeDamage(dragoonRiderData.RangedDamage, this, projectilePrefab != null, dragoonRiderData.RangedDamageSoundType);
        yield return new WaitForSeconds(0.08f);
    }

    private IEnumerator PerformDragoonFlightAndSummon(Character target)
    {
        if (dragoonRiderData == null || Board == null)
        {
            yield break;
        }

        yield return ResetBodyRotationToOrigin(dragoonRiderData.FlightPreparationDuration);
        ClearAnimationTriggers();

        if (TryGetRandomSafeFlightDestination(target, out Vector2Int flightDestination))
        {
            SetFlyingAnimation(true);
            yield return FlyToCell(flightDestination, dragoonRiderData.FlightJumpDuration);
            SetFlyingAnimation(false);
        }

        yield return RotateBodyTowardsTarget(target, dragoonRiderData.AltAttackRotateDuration);
        yield return SummonDragoonFireballs();
    }

    private bool TryGetRandomSafeFlightDestination(Character target, out Vector2Int destination)
    {
        destination = gridPosition;
        if (Board == null || target == null)
        {
            return false;
        }

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                if (cellPosition == gridPosition)
                {
                    continue;
                }

                if (!Board.TryGetCell(cellPosition, out BoardCell cell) || !cell.Walkable || cell.IsOccupied)
                {
                    continue;
                }

                if (Board.TryGetHazard(cellPosition, out BoardHazard hazard) && hazard is FireTileHazard)
                {
                    continue;
                }

                int distanceToPlayer = Mathf.Abs(cellPosition.x - target.GridPosition.x) + Mathf.Abs(cellPosition.y - target.GridPosition.y);
                if (distanceToPlayer <= 3)
                {
                    continue;
                }

                candidates.Add(cellPosition);
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        destination = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return true;
    }

    private IEnumerator SummonDragoonFireballs()
    {
        if (dragoonRiderData == null || Board == null)
        {
            yield break;
        }

        int fireballCount = dragoonRiderData.SummonedFireballCount;
        Character playerCharacter = Board.Player != null ? Board.Player.ControlledCharacter : null;
        bool shouldTargetCurrentPlayerCellFirst = playerCharacter != null && !playerCharacter.MovedLastTurn;
        float perFireballDuration = Mathf.Max(0.15f, dragoonRiderData.FireballVolleyDuration / Mathf.Max(1, fireballCount));
        for (int index = 0; index < fireballCount; index++)
        {
            Vector2Int targetCell;
            if (shouldTargetCurrentPlayerCellFirst && index == 0)
            {
                targetCell = playerCharacter.GridPosition;
            }
            else if (!TryGetRandomFreeNonFireCell(out targetCell))
            {
                yield break;
            }

            TriggerOptionalActionAnimation();
            if (dragoonRiderData.FireballSpawnDelay > 0f)
            {
                yield return new WaitForSeconds(dragoonRiderData.FireballSpawnDelay);
            }

            yield return SpawnSingleDragoonFireball(targetCell, perFireballDuration);
            if (index < fireballCount - 1 && dragoonRiderData.DelayBetweenFireballs > 0f)
            {
                yield return new WaitForSeconds(dragoonRiderData.DelayBetweenFireballs);
            }
        }
    }

    private bool ShouldUseLichSummonTurn()
    {
        if (lichData == null)
        {
            return false;
        }

        if (combatTurnCount <= 1)
        {
            return true;
        }

        int interval = lichData.SummonIntervalTurns;
        return interval > 0 && ((combatTurnCount - 1) % interval) == 0;
    }

    private IEnumerator PerformLichSummonTurn()
    {
        if (lichData == null || Board == null)
        {
            yield break;
        }

        TriggerOptionalActionAnimation();
        if (lichData.SummonDelayBeforeSkulls > 0f)
        {
            yield return new WaitForSeconds(lichData.SummonDelayBeforeSkulls);
        }

        List<Vector2Int> freeCells = GetRandomFreeCellsForLichSkulls(lichData.SummonedSkullCount);
        IReadOnlyList<GameObject> summonablePrefabs = lichData.SummonableEnemyPrefabs;
        for (int index = 0; index < freeCells.Count; index++)
        {
            if (summonablePrefabs == null || summonablePrefabs.Count == 0)
            {
                yield break;
            }

            GameObject summonedEnemyPrefab = summonablePrefabs[UnityEngine.Random.Range(0, summonablePrefabs.Count)];
            if (summonedEnemyPrefab == null)
            {
                continue;
            }

            Board.SpawnLichSkull(freeCells[index], lichData, summonedEnemyPrefab, this);
            if (index < freeCells.Count - 1 && lichData.DelayBetweenSummonedSkulls > 0f)
            {
                yield return new WaitForSeconds(lichData.DelayBetweenSummonedSkulls);
            }
        }
    }

    private List<Vector2Int> GetRandomFreeCellsForLichSkulls(int desiredCount)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        if (Board == null || desiredCount <= 0)
        {
            return candidates;
        }

        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                if (!Board.TryGetCell(cellPosition, out BoardCell cell))
                {
                    continue;
                }

                if (cell.IsOccupied || cell.HasBlockingTerrain || cell.Hazard != null)
                {
                    continue;
                }

                candidates.Add(cellPosition);
            }
        }

        for (int index = candidates.Count - 1; index > 0; index--)
        {
            int swapIndex = UnityEngine.Random.Range(0, index + 1);
            (candidates[index], candidates[swapIndex]) = (candidates[swapIndex], candidates[index]);
        }

        if (candidates.Count > desiredCount)
        {
            candidates.RemoveRange(desiredCount, candidates.Count - desiredCount);
        }

        return candidates;
    }

    private bool TryGetRandomFreeNonFireCell(out Vector2Int targetCell)
    {
        targetCell = gridPosition;
        if (Board == null)
        {
            return false;
        }

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                if (!Board.TryGetCell(cellPosition, out BoardCell cell) || !cell.Walkable || cell.IsOccupied)
                {
                    continue;
                }

                if (Board.TryGetHazard(cellPosition, out BoardHazard hazard) && hazard is FireTileHazard)
                {
                    continue;
                }

                candidates.Add(cellPosition);
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        targetCell = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return true;
    }

    private IEnumerator SpawnSingleDragoonFireball(Vector2Int targetCell, float jumpDuration)
    {
        if (dragoonRiderData == null || Board == null)
        {
            yield break;
        }

        Transform spawnAnchor = fireBallSpawnPos != null ? fireBallSpawnPos : EffectAnchor;
        Vector3 spawnPosition = spawnAnchor != null ? spawnAnchor.position : transform.position;
        Vector3 targetPosition = Board.GridToWorldPosition(targetCell) + Vector3.up * spawnHeight;
        GameObject projectile = projectilePrefab != null
            ? Instantiate(projectilePrefab, spawnPosition, projectilePrefab.transform.rotation)
            : new GameObject("DragoonFireball");

        if (projectilePrefab != null)
        {
            projectile.transform.localScale = projectilePrefab.transform.localScale;
        }

        Tween jumpTween = projectile.transform.DOJump(
            targetPosition,
            dragoonRiderData.FireballJumpPower,
            1,
            Mathf.Max(0.01f, jumpDuration));
        yield return jumpTween.WaitForCompletion();

        CreateFireTile(targetCell);
        Destroy(projectile);
    }

    private IEnumerator FireProjectileAtEnemy(Enemy target, Transform launchAnchor = null, bool playArrowShotSound = true)
    {
        if (target == null)
        {
            yield break;
        }

        Transform resolvedLaunchAnchor = launchAnchor != null ? launchAnchor : projectileSpawnPos;
        Vector3 launchPosition = resolvedLaunchAnchor != null
            ? resolvedLaunchAnchor.position
            : transform.position + Vector3.up * 0.5f;
        if (playArrowShotSound)
        {
            SoundManager.Instance?.PlayArrowShot(launchPosition);
        }

        Vector3 targetPosition = target.transform.position + Vector3.up * projectileTravelHeight;
        GameObject projectile = Instantiate(projectilePrefab, launchPosition, Quaternion.identity);

        Vector3 direction = targetPosition - launchPosition;
        if (direction.sqrMagnitude > 0.0001f)
        {
            projectile.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        float duration = Mathf.Max(0.08f, direction.magnitude / Mathf.Max(0.01f, projectileTravelSpeed));
        Tween projectileTween = projectile.transform.DOMove(targetPosition, duration).SetEase(Ease.Linear);
        yield return projectileTween.WaitForCompletion();

        Destroy(projectile);
    }

    private IEnumerator FlyToCell(Vector2Int targetCell, float duration)
    {
        if (Board == null || targetCell == gridPosition)
        {
            yield break;
        }

        Vector2Int startGridPosition = gridPosition;
        if (!Board.MoveOccupant(gridPosition, targetCell, BoardOccupantKind.Enemy))
        {
            yield break;
        }

        gridPosition = targetCell;
        Vector3 targetPosition = Board.GridToWorldPosition(gridPosition) + Vector3.up * spawnHeight;
        FaceMovementDirection(targetCell - startGridPosition);
        SetFlyingAnimation(true);

        moveTween?.Kill();
        IsMoving = true;
        moveTween = transform.DOJump(targetPosition, 0.65f, 1, Mathf.Max(0.01f, duration))
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                IsMoving = false;
                moveTween = null;
                transform.position = targetPosition;
                RefreshFlyingAnimationState();
                Board.NotifyEnemyEnteredCell(this);
            });
        yield return moveTween.WaitForCompletion();
        SetFlyingAnimation(false);
    }

    private void CreateFireTile(Vector2Int targetCell)
    {
        if (Board == null || dragoonRiderData == null)
        {
            return;
        }

        if (Board.TryGetHazard(targetCell, out BoardHazard existingHazard) && existingHazard is FireTileHazard)
        {
            return;
        }

        GameObject fireHazardObject = new GameObject("FireTileHazard");
        FireTileHazard fireHazard = fireHazardObject.AddComponent<FireTileHazard>();
        fireHazard.Configure(
            Board,
            Board.Player != null ? Board.Player.ControlledCharacter : null,
            targetCell,
            dragoonRiderData.FireTileDamage,
            dragoonRiderData.FireObjectPrefab,
            dragoonRiderData.FireImpactFxPrefab,
            dragoonRiderData.FireDamageSoundParametersPrefab);
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

            target.TakeDamage(EffectiveForce, this, false, damageSoundType);
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

        target.TakeDamage(EffectiveForce, this, wasProjectile, damageSoundType);
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

    private IEnumerator FireProjectileAt(Character target, Transform launchAnchor = null, bool playArrowShotSound = true)
    {
        if (target == null || projectilePrefab == null)
        {
            yield break;
        }

        if (projectileSpawnDelay > 0f)
        {
            yield return new WaitForSeconds(projectileSpawnDelay);
        }

        Transform resolvedLaunchAnchor = launchAnchor != null ? launchAnchor : projectileSpawnPos;
        Vector3 launchPosition = resolvedLaunchAnchor != null
            ? resolvedLaunchAnchor.position
            : transform.position + Vector3.up * 0.5f;
        if (playArrowShotSound)
        {
            SoundManager.Instance?.PlayArrowShot(launchPosition);
        }

        Vector3 targetPosition = target.transform.position + Vector3.up * projectileTravelHeight;
        GameObject projectile = Instantiate(projectilePrefab, launchPosition, Quaternion.identity);

        Vector3 direction = targetPosition - launchPosition;
        if (direction.sqrMagnitude > 0.0001f)
        {
            projectile.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        float duration = Mathf.Max(0.08f, direction.magnitude / Mathf.Max(0.01f, projectileTravelSpeed));
        Tween projectileTween = projectile.transform.DOMove(targetPosition, duration).SetEase(Ease.Linear);
        yield return projectileTween.WaitForCompletion();

        Destroy(projectile);
    }

    private IEnumerator FireJumpProjectileAt(Character target, float duration, float jumpPower)
    {
        if (target == null || projectilePrefab == null)
        {
            if (specialWindupDuration > 0f)
            {
                yield return new WaitForSeconds(specialWindupDuration);
            }

            yield break;
        }

        if (specialWindupDuration > 0f)
        {
            yield return new WaitForSeconds(specialWindupDuration);
        }

        Transform resolvedLaunchAnchor = projectileSpawnPos != null ? projectileSpawnPos : EffectAnchor;
        Vector3 launchPosition = resolvedLaunchAnchor != null
            ? resolvedLaunchAnchor.position
            : transform.position + Vector3.up * 0.5f;
        SoundManager.Instance?.PlayArrowShot(launchPosition);

        Vector3 targetPosition = target.transform.position + Vector3.up * projectileTravelHeight;
        GameObject projectile = Instantiate(projectilePrefab, launchPosition, Quaternion.identity);
        projectile.transform.localScale = projectilePrefab.transform.localScale;

        Vector3 direction = targetPosition - launchPosition;
        if (direction.sqrMagnitude > 0.0001f)
        {
            projectile.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        Tween jumpTween = projectile.transform.DOJump(
            targetPosition,
            Mathf.Max(0.01f, jumpPower),
            1,
            Mathf.Max(0.01f, duration));
        yield return jumpTween.WaitForCompletion();

        Destroy(projectile);
    }

    private IEnumerator TryExecuteWolfPounce(Character target)
    {
        if (target == null || Board == null)
        {
            yield break;
        }

        int currentPathDistance = GetPathDistanceForCurrentMovementRules(gridPosition, target.GridPosition, true);
        if (currentPathDistance <= 2 || currentPathDistance == int.MaxValue)
        {
            yield break;
        }

        if (!TryFindBestWolfPounceDestination(target, out Vector2Int destination))
        {
            yield break;
        }

        FaceMovementDirection(destination - gridPosition);
        TriggerOptionalActionAnimation();
        if (specialWindupDuration > 0f)
        {
            yield return new WaitForSeconds(specialWindupDuration);
        }

        yield return JumpToCell(destination, specialJumpDuration, specialJumpPower);
        if (specialRecoveryDelay > 0f)
        {
            yield return new WaitForSeconds(specialRecoveryDelay);
        }
    }

    private bool TryFindBestWolfPounceDestination(Character target, out Vector2Int destination)
    {
        destination = gridPosition;
        if (target == null || Board == null)
        {
            return false;
        }

        int currentPathDistance = GetPathDistanceForCurrentMovementRules(gridPosition, target.GridPosition, true);
        bool found = false;
        int bestDistanceToTarget = int.MaxValue;
        int bestManhattanDistanceToTarget = int.MaxValue;

        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int candidateCell = new Vector2Int(x, y);
                if (candidateCell == gridPosition)
                {
                    continue;
                }

                if (!Board.TryGetCell(candidateCell, out BoardCell cell) || !CanEndMovementOnCell(cell))
                {
                    continue;
                }

                int jumpDistanceFromWolf = Board.GetManhattanDistance(gridPosition, candidateCell);
                if (jumpDistanceFromWolf != specialLandingDistance)
                {
                    continue;
                }

                int candidateDistanceToTarget = GetPathDistanceForCurrentMovementRules(candidateCell, target.GridPosition, true);
                if (candidateDistanceToTarget == int.MaxValue || candidateDistanceToTarget >= currentPathDistance)
                {
                    continue;
                }

                int candidateManhattanDistanceToTarget = Board.GetManhattanDistance(candidateCell, target.GridPosition);

                if (!found
                    || candidateDistanceToTarget < bestDistanceToTarget
                    || (candidateDistanceToTarget == bestDistanceToTarget
                        && candidateManhattanDistanceToTarget < bestManhattanDistanceToTarget))
                {
                    found = true;
                    destination = candidateCell;
                    bestDistanceToTarget = candidateDistanceToTarget;
                    bestManhattanDistanceToTarget = candidateManhattanDistanceToTarget;
                }
            }
        }

        return found;
    }

    private bool CanExecuteBoarCharge(Character target)
    {
        if (target == null || Board == null)
        {
            return false;
        }

        Vector2Int delta = target.GridPosition - gridPosition;
        if (!(delta.x == 0 || delta.y == 0))
        {
            return false;
        }

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance < Mathf.Max(3, specialMinimumDistance))
        {
            return false;
        }

        Vector2Int direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        Vector2Int destination = target.GridPosition - direction;
        if (!Board.TryGetCell(destination, out BoardCell destinationCell) || !CanEndMovementOnCell(destinationCell))
        {
            return false;
        }

        Vector2Int scan = gridPosition + direction;
        while (scan != target.GridPosition)
        {
            if (!Board.TryGetCell(scan, out BoardCell cell))
            {
                return false;
            }

            if (scan == destination)
            {
                if (!CanEndMovementOnCell(cell))
                {
                    return false;
                }
            }
            else if (cell.HasBlockingTerrain || cell.IsOccupied)
            {
                return false;
            }

            scan += direction;
        }

        return true;
    }

    private IEnumerator ExecuteBoarCharge(Character target)
    {
        if (target == null || Board == null)
        {
            yield break;
        }

        Vector2Int delta = target.GridPosition - gridPosition;
        Vector2Int direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        Vector2Int destination = target.GridPosition - direction;

        FaceTargetForAttack(target);
        TriggerOptionalActionAnimation();
        if (specialWindupDuration > 0f)
        {
            yield return new WaitForSeconds(specialWindupDuration);
        }

        yield return ChargeToCellAndImpact(target, destination, Mathf.Max(0.01f, specialJumpDuration));
        yield return new WaitForSeconds(0.08f);
    }

    private IEnumerator ExecuteGiantWormTurn(Character target)
    {
        if (Board == null || target == null)
        {
            yield break;
        }

        combatTurnCount++;

        Vector2Int previousTunnelCell = gridPosition;
        EnsureWormTunnelAt(previousTunnelCell);
        if (!TrySelectGiantWormDestination(target, previousTunnelCell, out Vector2Int destination, out GiantWormAttackMode attackMode, out bool createTunnelAtDestination))
        {
            yield break;
        }

        TriggerOptionalActionAnimation();
        if (specialStartDelay > 0f)
        {
            yield return new WaitForSeconds(specialStartDelay);
        }

        if (specialWindupDuration > 0f)
        {
            yield return new WaitForSeconds(specialWindupDuration);
        }

        RemoveWormOccupancyFromCurrentCell();

        if (createTunnelAtDestination)
        {
            EnsureWormTunnelAt(destination);
        }

        if (destination == previousTunnelCell)
        {
            yield break;
        }

        PlaceWormAtCell(destination);
        yield return null;
        TriggerOptionalSecondaryActionAnimation();
        StartWormPositionLock(destination);
        FaceMovementDirection(target.GridPosition - destination);

        if (specialRecoveryDelay > 0f)
        {
            yield return new WaitForSeconds(specialRecoveryDelay);
        }

        StopWormPositionLock();

        if (ShouldLaunchGiantWormTunnelBoulder() && TryGetGiantWormTunnelBoulderTargetCell(target, out Vector2Int boulderTargetCell))
        {
            yield return LaunchGiantWormTunnelBoulder(boulderTargetCell);
        }

        if (attackMode == GiantWormAttackMode.Adjacent && IsRadiallyAdjacentToTarget(target))
        {
            yield return PerformGiantWormAdjacentAttack(target);
        }
        else if (attackMode == GiantWormAttackMode.Ranged && CanGiantWormShootTarget(target))
        {
            yield return PerformGiantWormRangedAttack(target);
        }
    }

    private IEnumerator ExecuteTrollShockwaveTurn(Character target)
    {
        if (Board == null || target == null)
        {
            yield break;
        }

        TriggerAttackAnimation();
        if (specialStartDelay > 0f)
        {
            yield return new WaitForSeconds(specialStartDelay);
        }

        if (specialImpactShakeRatio > 0f)
        {
            cam.Instance?.Shake(specialImpactShakeRatio);
        }

        StartCoroutine(ApplyTrollShockwaveToBarrels());

        int distanceFromTroll = GetChebyshevDistance(gridPosition, target.GridPosition);
        float impactDelay = Mathf.Max(0f, attackDamageDelay) + (Mathf.Max(0, distanceFromTroll) * Mathf.Max(0f, specialPerDistanceDelay));
        if (impactDelay > 0f)
        {
            yield return new WaitForSeconds(impactDelay);
        }

        int trollDamage = Mathf.Max(1, EffectiveForce);
        target.TakeDamage(trollDamage, this, false, damageSoundType);
        if (target.CurrentHealth <= 0)
        {
            IncreaseTrollForce();
            yield return new WaitForSeconds(0.08f);
            yield break;
        }

        yield return KnockCharacterAwayFromTroll(target, distanceFromTroll);
        IncreaseTrollForce();
        yield return new WaitForSeconds(0.08f);
    }

    private IEnumerator KnockCharacterAwayFromTroll(Character target, int distanceFromTroll)
    {
        if (target == null || Board == null)
        {
            yield break;
        }

        float bumpDuration = Mathf.Max(0.01f, Mathf.Max(0f, specialJumpDuration) + (Mathf.Max(1, distanceFromTroll) * Mathf.Max(0f, specialBumpDurationPerDistance)));
        Vector2Int? destination = null;
        if (TryFindBestTrollKnockbackCell(target, out Vector2Int resolvedDestination))
        {
            destination = resolvedDestination;
        }

        yield return target.PlayImpactBumpAndRelocate(
            transform.position,
            destination,
            bumpDuration,
            specialBumpHeight);
    }

    private IEnumerator ExecuteDragoonTwinTurn(Character target, int effectiveMobility)
    {
        if (Board == null || target == null)
        {
            yield break;
        }

        bool isGroundedThisTurn = dragoonTwinAttackSuppressed;
        SetFlyingOverride(!isGroundedThisTurn);

        if (isGroundedThisTurn)
        {
            yield return PerformDragoonTwinGroundedTurn(target, effectiveMobility);
        }
        else
        {
            yield return PerformDragoonTwinRangedAttack(target);
        }

        yield return PerformDragoonTwinSpecialMove(target);
        dragoonTwinAttackSuppressed = false;
    }

    private IEnumerator PerformDragoonTwinGroundedTurn(Character target, int effectiveMobility)
    {
        if (target == null)
        {
            yield break;
        }

        if (CanPerformDragoonMeleeFrom(gridPosition, target))
        {
            yield return PerformDragoonTwinGroundAttack(target);
            yield break;
        }

        if (effectiveMobility > 0 && TryFindBestDragoonMeleeObjective(target, effectiveMobility, out PathPlan meleePlan))
        {
            yield return FollowPathPlan(meleePlan.Path);
        }

        if (CanPerformDragoonMeleeFrom(gridPosition, target))
        {
            yield return PerformDragoonTwinGroundAttack(target);
        }
    }

    private IEnumerator PerformDragoonTwinRangedAttack(Character target)
    {
        if (target == null)
        {
            yield break;
        }

        FaceTargetForAttack(target);
        TriggerOptionalSecondaryActionAnimation();
        if (specialStartDelay > 0f)
        {
            yield return new WaitForSeconds(specialStartDelay);
        }

        yield return FireFracturingJumpProjectileAt(target);
        target.TakeDamage(Mathf.Max(1, EffectiveForce), this, true, damageSoundType);
        yield return new WaitForSeconds(0.08f);
    }

    private IEnumerator PerformDragoonTwinGroundAttack(Character target)
    {
        if (target == null)
        {
            yield break;
        }

        FaceTargetForAttack(target);
        TriggerOptionalSecondaryActionAnimation();
        float damageDelay = GetTargetDamageDelay(target);
        if (damageDelay > 0f)
        {
            yield return new WaitForSeconds(damageDelay);
        }

        target.TakeDamage(Mathf.Max(1, EffectiveForce), this, false, damageSoundType);
        yield return new WaitForSeconds(0.08f);
    }

    private IEnumerator PerformDragoonTwinSpecialMove(Character target)
    {
        if (!TryGetDragoonTwinDestination(target, out Vector2Int destination))
        {
            yield break;
        }

        yield return JumpToCell(destination, Mathf.Max(0.01f, specialJumpDuration), Mathf.Max(0.01f, specialJumpPower));
        SetFlyingOverride(true);
    }

    private bool TryGetDragoonTwinDestination(Character target, out Vector2Int destination)
    {
        destination = gridPosition;
        if (Board == null)
        {
            return false;
        }

        Enemy otherTwin = FindOtherDragoonTwin();
        List<Vector2Int> preferredCells = new List<Vector2Int>();
        List<Vector2Int> fallbackCells = new List<Vector2Int>();

        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int candidateCell = new Vector2Int(x, y);
                if (candidateCell == gridPosition || !Board.TryGetCell(candidateCell, out BoardCell cell) || !cell.Walkable || cell.IsOccupied)
                {
                    continue;
                }

                fallbackCells.Add(candidateCell);
                bool farEnoughFromCharacter = target == null || Board.GetManhattanDistance(candidateCell, target.GridPosition) >= 3;
                bool farEnoughFromTwin = otherTwin == null || Board.GetManhattanDistance(candidateCell, otherTwin.GridPosition) >= 3;
                if (farEnoughFromCharacter && farEnoughFromTwin)
                {
                    preferredCells.Add(candidateCell);
                }
            }
        }

        List<Vector2Int> candidatePool = preferredCells.Count > 0 ? preferredCells : fallbackCells;
        if (candidatePool.Count == 0)
        {
            return false;
        }

        destination = candidatePool[UnityEngine.Random.Range(0, candidatePool.Count)];
        return true;
    }

    private Enemy FindOtherDragoonTwin()
    {
        if (Board == null)
        {
            return null;
        }

        IReadOnlyList<Enemy> enemies = Board.SpawnedEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy candidate = enemies[index];
            if (candidate == null || candidate == this || candidate.CurrentHealth <= 0 || candidate.SpecialBehavior != EnemySpecialBehavior.DragoonTwinBoss)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private IEnumerator FireFracturingJumpProjectileAt(Character target)
    {
        if (target == null || projectilePrefab == null)
        {
            yield break;
        }

        Transform resolvedLaunchAnchor = projectileSpawnPos != null ? projectileSpawnPos : EffectAnchor;
        Vector3 launchPosition = resolvedLaunchAnchor != null
            ? resolvedLaunchAnchor.position
            : transform.position + Vector3.up * 0.5f;
        SoundManager.Instance?.PlayArrowShot(launchPosition);

        Vector3 targetPosition = target.transform.position + Vector3.up * projectileTravelHeight;
        GameObject projectile = Instantiate(projectilePrefab, launchPosition, projectilePrefab.transform.rotation);
        projectile.transform.localScale = projectilePrefab.transform.localScale;

        float travelDuration = Mathf.Max(0.2f, Vector3.Distance(launchPosition, targetPosition) / Mathf.Max(0.01f, projectileTravelSpeed));
        Tween jumpTween = projectile.transform.DOJump(
            targetPosition,
            Mathf.Max(0.01f, specialJumpPower),
            1,
            travelDuration)
            .SetEase(Ease.Linear);
        yield return jumpTween.WaitForCompletion();

        FractureProjectileInstance(projectile);
    }

    private void FractureProjectileInstance(GameObject projectile)
    {
        if (projectile == null)
        {
            return;
        }

        RockBoulderProjectile configuredProjectile = projectile.GetComponent<RockBoulderProjectile>();
        if (configuredProjectile != null)
        {
            configuredProjectile.Fracture(Board);
            return;
        }

        Transform fracturesRoot = projectile.transform.Find("Fractures");
        if (fracturesRoot == null)
        {
            Destroy(projectile);
            return;
        }

        fracturesRoot.SetParent(Board != null ? Board.transform : null, true);
        fracturesRoot.gameObject.SetActive(true);
        List<Rigidbody> rigidbodies = new List<Rigidbody>(fracturesRoot.GetComponentsInChildren<Rigidbody>(true));
        for (int index = 0; index < rigidbodies.Count; index++)
        {
            Rigidbody body = rigidbodies[index];
            if (body == null)
            {
                continue;
            }

            body.isKinematic = false;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Vector3 direction = (body.worldCenterOfMass - projectile.transform.position);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = UnityEngine.Random.onUnitSphere;
            }

            Vector3 force = (direction.normalized * 3.5f) + (Vector3.up * 1.25f);
            body.AddForce(force, ForceMode.Impulse);
        }

        BarrelFractureCleanup cleanup = fracturesRoot.gameObject.GetComponent<BarrelFractureCleanup>();
        if (cleanup == null)
        {
            cleanup = fracturesRoot.gameObject.AddComponent<BarrelFractureCleanup>();
        }

        cleanup.Begin(rigidbodies, 0.4f, new Vector2(1f, 1.5f), 0.25f, 0.1f);
        Destroy(projectile);
    }

    private IEnumerator ApplyTrollShockwaveToBarrels()
    {
        if (Board == null)
        {
            yield break;
        }

        List<(BarrelObstacle barrel, float delay)> delayedBarrels = new List<(BarrelObstacle, float)>();
        HashSet<BarrelObstacle> uniqueBarrels = new HashSet<BarrelObstacle>();

        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                if (!Board.TryGetBarrel(cellPosition, out BarrelObstacle barrel) || barrel == null || !uniqueBarrels.Add(barrel))
                {
                    continue;
                }

                int distance = GetChebyshevDistance(gridPosition, cellPosition);
                float delay = Mathf.Max(0f, attackDamageDelay) + (Mathf.Max(0, distance) * Mathf.Max(0f, specialPerDistanceDelay));
                delayedBarrels.Add((barrel, delay));
            }
        }

        if (delayedBarrels.Count == 0)
        {
            yield break;
        }

        delayedBarrels.Sort((left, right) => left.delay.CompareTo(right.delay));
        float elapsedDelay = 0f;
        for (int index = 0; index < delayedBarrels.Count; index++)
        {
            (BarrelObstacle barrel, float delay) = delayedBarrels[index];
            float waitDuration = Mathf.Max(0f, delay - elapsedDelay);
            if (waitDuration > 0f)
            {
                yield return new WaitForSeconds(waitDuration);
                elapsedDelay += waitDuration;
            }

            if (barrel != null && !barrel.IsDestroyed)
            {
                barrel.TakeHit();
            }
        }
    }

    private bool TryFindBestTrollKnockbackCell(Character target, out Vector2Int destination)
    {
        destination = target != null ? target.GridPosition : gridPosition;
        if (Board == null || target == null)
        {
            return false;
        }

        int currentDistance = GetChebyshevDistance(gridPosition, target.GridPosition);
        int bestDistance = currentDistance;
        List<Vector2Int> bestCells = new List<Vector2Int>();

        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int candidateCell = new Vector2Int(x, y);
                if (!Board.TryGetCell(candidateCell, out BoardCell cell) || !cell.Walkable || cell.IsOccupied)
                {
                    continue;
                }

                int candidateDistance = GetChebyshevDistance(gridPosition, candidateCell);
                if (candidateDistance <= currentDistance)
                {
                    continue;
                }

                if (candidateDistance > bestDistance)
                {
                    bestDistance = candidateDistance;
                    bestCells.Clear();
                    bestCells.Add(candidateCell);
                }
                else if (candidateDistance == bestDistance)
                {
                    bestCells.Add(candidateCell);
                }
            }
        }

        if (bestCells.Count == 0)
        {
            return false;
        }

        destination = bestCells[UnityEngine.Random.Range(0, bestCells.Count)];
        return true;
    }

    private void ClaimTrollOccupiedCells()
    {
        if (Board == null)
        {
            return;
        }

        ReleaseExtraOccupiedCells();
        for (int offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                Vector2Int occupiedCell = gridPosition + new Vector2Int(offsetX, offsetY);
                if (occupiedCell == gridPosition || !Board.TryGetCell(occupiedCell, out BoardCell cell))
                {
                    continue;
                }

                if (cell.IsOccupied && cell.Occupant != gameObject)
                {
                    continue;
                }

                cell.SetOccupant(gameObject, BoardOccupantKind.Enemy);
                extraOccupiedCells.Add(occupiedCell);
            }
        }
    }

    private void ReleaseExtraOccupiedCells()
    {
        if (Board == null || extraOccupiedCells.Count == 0)
        {
            extraOccupiedCells.Clear();
            return;
        }

        for (int index = 0; index < extraOccupiedCells.Count; index++)
        {
            Vector2Int occupiedCell = extraOccupiedCells[index];
            if (!Board.TryGetCell(occupiedCell, out BoardCell cell))
            {
                continue;
            }

            if (cell.Occupant == gameObject)
            {
                cell.ClearOccupant();
            }
        }

        extraOccupiedCells.Clear();
    }

    private static int GetChebyshevDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    private void IncreaseTrollForce()
    {
        if (specialBehavior != EnemySpecialBehavior.TrollShockwaveBoss)
        {
            return;
        }

        trollAttackBonusDamage++;
        PlaySelfBuffFeedback();
    }

    private IEnumerator PlayRagnarOgreDeathBuffFeedback()
    {
        yield return new WaitForSeconds(0.5f);
        if (this == null || !isActiveAndEnabled || isDying || currentHealth <= 0)
        {
            yield break;
        }

        PlaySelfBuffFeedback();
    }

    private void PlaySelfBuffFeedback()
    {
        SoundManager.Instance?.PlayPowerUp(transform.position);
        if (selfPowerUpFxPrefab == null)
        {
            return;
        }

        Transform anchor = EffectAnchor != null ? EffectAnchor : transform;
        GameObject fxInstance = Instantiate(selfPowerUpFxPrefab, anchor.position, selfPowerUpFxPrefab.transform.rotation);
        fxInstance.transform.localScale = selfPowerUpFxPrefab.transform.localScale;
        if (optionalActionFxLifetime > 0f)
        {
            Destroy(fxInstance, optionalActionFxLifetime);
        }
    }

    private bool TrySelectGiantWormDestination(
        Character target,
        Vector2Int previousTunnelCell,
        out Vector2Int destination,
        out GiantWormAttackMode attackMode,
        out bool createTunnelAtDestination)
    {
        destination = gridPosition;
        attackMode = GiantWormAttackMode.None;
        createTunnelAtDestination = false;

        if (Board == null || target == null)
        {
            return false;
        }

        List<Vector2Int> adjacentTunnelCandidates = new List<Vector2Int>();
        List<Vector2Int> rangedTunnelCandidates = new List<Vector2Int>();
        foreach (KeyValuePair<Vector2Int, GameObject> entry in wormTunnelInstances)
        {
            Vector2Int candidateCell = entry.Key;
            if (candidateCell == previousTunnelCell || !IsCellValidForWormEmergence(candidateCell))
            {
                continue;
            }

            if (IsRadiallyAdjacent(candidateCell, target.GridPosition))
            {
                adjacentTunnelCandidates.Add(candidateCell);
                continue;
            }

            if (Board.HasLineOfSight(candidateCell, target.GridPosition))
            {
                rangedTunnelCandidates.Add(candidateCell);
            }
        }

        if (adjacentTunnelCandidates.Count > 0)
        {
            destination = adjacentTunnelCandidates[UnityEngine.Random.Range(0, adjacentTunnelCandidates.Count)];
            attackMode = GiantWormAttackMode.Adjacent;
            return true;
        }

        if (rangedTunnelCandidates.Count > 0)
        {
            destination = rangedTunnelCandidates[UnityEngine.Random.Range(0, rangedTunnelCandidates.Count)];
            attackMode = GiantWormAttackMode.Ranged;
            return true;
        }

        List<Vector2Int> hiddenFreeCells = new List<Vector2Int>();
        List<Vector2Int> fallbackFreeCells = new List<Vector2Int>();
        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int candidateCell = new Vector2Int(x, y);
                if (candidateCell == previousTunnelCell || wormTunnelInstances.ContainsKey(candidateCell) || !IsCellValidForWormEmergence(candidateCell))
                {
                    continue;
                }

                fallbackFreeCells.Add(candidateCell);
                if (!Board.HasLineOfSight(candidateCell, target.GridPosition))
                {
                    hiddenFreeCells.Add(candidateCell);
                }
            }
        }

        List<Vector2Int> preferredPool = hiddenFreeCells.Count > 0 ? hiddenFreeCells : fallbackFreeCells;
        if (preferredPool.Count == 0)
        {
            return false;
        }

        destination = preferredPool[UnityEngine.Random.Range(0, preferredPool.Count)];
        attackMode = GiantWormAttackMode.None;
        createTunnelAtDestination = true;
        return true;
    }

    private bool IsCellValidForWormEmergence(Vector2Int candidateCell)
    {
        if (Board == null || !Board.TryGetCell(candidateCell, out BoardCell cell))
        {
            return false;
        }

        return cell.Walkable && !cell.IsOccupied;
    }

    private bool IsRadiallyAdjacent(Vector2Int origin, Vector2Int targetCell)
    {
        int deltaX = Mathf.Abs(origin.x - targetCell.x);
        int deltaY = Mathf.Abs(origin.y - targetCell.y);
        return (deltaX > 0 || deltaY > 0) && deltaX <= 1 && deltaY <= 1;
    }

    private bool IsRadiallyAdjacentToTarget(Character target)
    {
        return target != null && IsRadiallyAdjacent(gridPosition, target.GridPosition);
    }

    private bool CanGiantWormShootTarget(Character target)
    {
        return target != null && Board != null && Board.HasLineOfSight(gridPosition, target.GridPosition);
    }

    private bool ShouldLaunchGiantWormTunnelBoulder()
    {
        return combatTurnCount > 0 && combatTurnCount % 3 == 0;
    }

    private bool TryGetGiantWormTunnelBoulderTargetCell(Character target, out Vector2Int targetCell)
    {
        targetCell = gridPosition;
        if (Board == null || target == null)
        {
            return false;
        }

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 0; x < Board.Width; x++)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                Vector2Int candidateCell = new Vector2Int(x, y);
                if (candidateCell == target.GridPosition || wormTunnelInstances.ContainsKey(candidateCell) || !IsCellValidForWormEmergence(candidateCell))
                {
                    continue;
                }

                if (!Board.HasLineOfSight(candidateCell, target.GridPosition))
                {
                    continue;
                }

                candidates.Add(candidateCell);
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        targetCell = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return true;
    }

    private IEnumerator LaunchGiantWormTunnelBoulder(Vector2Int targetCell)
    {
        if (Board == null || specialAlternateProjectilePrefab == null)
        {
            yield break;
        }

        FaceMovementDirection(targetCell - gridPosition);
        TriggerAttackAnimation();

        if (projectileSpawnDelay > 0f)
        {
            yield return new WaitForSeconds(projectileSpawnDelay);
        }

        Transform resolvedLaunchAnchor = projectileSpawnPos != null ? projectileSpawnPos : EffectAnchor;
        Vector3 launchPosition = resolvedLaunchAnchor != null
            ? resolvedLaunchAnchor.position
            : transform.position + Vector3.up * 0.5f;
        Vector3 targetPosition = Board.GridToWorldPosition(targetCell) + Vector3.up * projectileTravelHeight;
        SoundManager.Instance?.PlayArrowShot(launchPosition);

        GameObject projectile = Instantiate(specialAlternateProjectilePrefab, launchPosition, specialAlternateProjectilePrefab.transform.rotation);
        projectile.transform.localScale = specialAlternateProjectilePrefab.transform.localScale;

        float travelDuration = Mathf.Max(0.2f, (Vector3.Distance(launchPosition, targetPosition) / Mathf.Max(0.01f, projectileTravelSpeed)) * 2f);
        Tween jumpTween = projectile.transform.DOJump(
            targetPosition,
            Mathf.Max(0.01f, specialJumpPower),
            1,
            travelDuration);
        yield return jumpTween.WaitForCompletion();

        SoundManager.Instance?.PlayDamageSound(DamageSoundType.Default, targetPosition);
        FractureProjectileInstance(projectile);
        EnsureWormTunnelAt(targetCell);
    }

    private IEnumerator PerformGiantWormAdjacentAttack(Character target)
    {
        if (target == null)
        {
            yield break;
        }

        FaceTargetForAttack(target);
        TriggerAttackAnimation();
        yield return ApplyDamageToTarget(target, false);
        yield return new WaitForSeconds(0.08f);
    }

    private IEnumerator PerformGiantWormRangedAttack(Character target)
    {
        if (target == null)
        {
            yield break;
        }

        FaceTargetForAttack(target);
        TriggerAttackAnimation();
        if (projectilePrefab != null)
        {
            yield return FireProjectileAt(target);
        }

        yield return ApplyDamageToTarget(target, projectilePrefab != null);
        yield return new WaitForSeconds(0.08f);
    }

    private void RemoveWormOccupancyFromCurrentCell()
    {
        if (Board == null || !Board.TryGetCell(gridPosition, out BoardCell cell))
        {
            return;
        }

        if (cell.Occupant == gameObject)
        {
            cell.ClearOccupant();
        }
    }

    private void PlaceWormAtCell(Vector2Int targetCell)
    {
        if (Board == null || !Board.TryGetCell(targetCell, out BoardCell cell))
        {
            return;
        }

        moveTween?.Kill();
        impactTween?.Kill();
        IsMoving = false;
        cell.SetOccupant(gameObject, BoardOccupantKind.Enemy);
        gridPosition = targetCell;
        transform.position = Board.GridToWorldPosition(targetCell) + Vector3.up * spawnHeight;
        RefreshFlyingAnimationState();
    }

    private void StartWormPositionLock(Vector2Int targetCell)
    {
        lockedWormRootCell = targetCell;
        lockWormRootPosition = true;
        PlaceWormAtCell(targetCell);
    }

    private void StopWormPositionLock()
    {
        lockWormRootPosition = false;
    }

    private void EnsureWormTunnelAt(Vector2Int cellPosition)
    {
        if (wormHolePrefab == null || Board == null || wormTunnelInstances.ContainsKey(cellPosition))
        {
            return;
        }

        Vector3 worldPosition = Board.GridToWorldPosition(cellPosition);
        Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 4) * 90f, 0f);
        Transform tunnelParent = Board.ObstaclesRoot != null ? Board.ObstaclesRoot : Board.transform;
        GameObject tunnelInstance = Instantiate(wormHolePrefab, worldPosition, rotation, tunnelParent);
        tunnelInstance.transform.localScale = wormHolePrefab.transform.localScale;
        tunnelInstance.transform.DOScale(wormHolePrefab.transform.localScale, 0.2f).From(wormHolePrefab.transform.localScale * 0.1f).SetEase(Ease.OutBack);
        wormTunnelInstances[cellPosition] = tunnelInstance;
    }

    private void ClearWormTunnels()
    {
        if (wormTunnelInstances.Count == 0)
        {
            return;
        }

        foreach (GameObject tunnelInstance in wormTunnelInstances.Values)
        {
            if (tunnelInstance != null)
            {
                Destroy(tunnelInstance);
            }
        }

        wormTunnelInstances.Clear();
    }

    private IEnumerator JumpToCell(Vector2Int targetCell, float duration, float jumpPower)
    {
        if (Board == null || targetCell == gridPosition)
        {
            yield break;
        }

        Vector2Int startGridPosition = gridPosition;
        if (!Board.MoveOccupant(gridPosition, targetCell, BoardOccupantKind.Enemy))
        {
            yield break;
        }

        gridPosition = targetCell;
        Vector3 targetPosition = Board.GridToWorldPosition(gridPosition) + Vector3.up * spawnHeight;
        FaceMovementDirection(targetCell - startGridPosition);

        moveTween?.Kill();
        IsMoving = true;
        moveTween = transform.DOJump(targetPosition, Mathf.Max(0.01f, jumpPower), 1, Mathf.Max(0.01f, duration))
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                IsMoving = false;
                moveTween = null;
                transform.position = targetPosition;
                RefreshFlyingAnimationState();
                Board.NotifyEnemyEnteredCell(this);
            });
        yield return moveTween.WaitForCompletion();
    }

    private IEnumerator ChargeToCell(Vector2Int targetCell, float duration)
    {
        if (Board == null || targetCell == gridPosition)
        {
            yield break;
        }

        Vector2Int startGridPosition = gridPosition;
        if (!Board.MoveOccupant(gridPosition, targetCell, BoardOccupantKind.Enemy))
        {
            yield break;
        }

        gridPosition = targetCell;
        Vector3 targetPosition = Board.GridToWorldPosition(gridPosition) + Vector3.up * spawnHeight;
        FaceMovementDirection(targetCell - startGridPosition);

        moveTween?.Kill();
        IsMoving = true;
        moveTween = transform.DOMove(targetPosition, Mathf.Max(0.01f, duration))
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                IsMoving = false;
                moveTween = null;
                transform.position = targetPosition;
                RefreshFlyingAnimationState();
                Board.NotifyEnemyEnteredCell(this);
            });
        yield return moveTween.WaitForCompletion();
    }

    private IEnumerator ChargeToCellAndImpact(Character target, Vector2Int targetCell, float duration)
    {
        if (Board == null || targetCell == gridPosition)
        {
            yield break;
        }

        Vector2Int startGridPosition = gridPosition;
        if (!Board.MoveOccupant(gridPosition, targetCell, BoardOccupantKind.Enemy))
        {
            yield break;
        }

        gridPosition = targetCell;
        Vector3 targetPosition = Board.GridToWorldPosition(gridPosition) + Vector3.up * spawnHeight;
        FaceMovementDirection(targetCell - startGridPosition);

        moveTween?.Kill();
        IsMoving = true;
        bool damageApplied = false;
        float impactLeadTime = 0.2f;
        float impactDelay = Mathf.Max(0f, duration - impactLeadTime);
        if (target != null)
        {
            DOVirtual.DelayedCall(impactDelay, () =>
            {
                if (damageApplied || target == null)
                {
                    return;
                }

                damageApplied = true;
                target.TakeDamage(Mathf.Max(1, specialDamage), this, false, damageSoundType);
            });
        }

        moveTween = transform.DOMove(targetPosition, duration)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                IsMoving = false;
                moveTween = null;
                transform.position = targetPosition;
                RefreshFlyingAnimationState();
                Board.NotifyEnemyEnteredCell(this);
                if (!damageApplied && target != null)
                {
                    damageApplied = true;
                    target.TakeDamage(Mathf.Max(1, specialDamage), this, false, damageSoundType);
                }
            });
        yield return moveTween.WaitForCompletion();
    }

    public void ApplyStatusEffect(CombatStatusType statusType, int durationInTurns = -1, int potency = 1, bool stackPotency = false)
    {
        potency = Mathf.Max(1, potency);
        bool alreadyHadStatus = statusPotencies.TryGetValue(statusType, out int currentPotency) && currentPotency > 0;
        if (!statusPotencies.ContainsKey(statusType))
        {
            statusPotencies[statusType] = potency;
        }
        else
        {
            statusPotencies[statusType] = stackPotency
                ? statusPotencies[statusType] + potency
                : Mathf.Max(statusPotencies[statusType], potency);
        }

        if (statusType == CombatStatusType.Bleeding && !alreadyHadStatus)
        {
            PlayStatusApplyFx(CombatStatusType.Bleeding);
        }

        if (statusType == CombatStatusType.Frozen && !alreadyHadStatus)
        {
            PlayStatusApplyFx(CombatStatusType.Frozen);
        }

        if (statusType == CombatStatusType.Poisoned && !alreadyHadStatus)
        {
            PlayStatusApplyFx(CombatStatusType.Poisoned);
        }

        RefreshStatusVisuals();

        if (!statusDurations.ContainsKey(statusType))
        {
            statusDurations[statusType] = durationInTurns;
            return;
        }

        int currentDuration = statusDurations[statusType];
        if (currentDuration < 0 || durationInTurns < 0)
        {
            statusDurations[statusType] = -1;
        }
        else
        {
            statusDurations[statusType] = Mathf.Max(currentDuration, durationInTurns);
        }
    }

    public void ApplyForceModifierUntilEndOfCombat(int amount)
    {
        forceModifierFromStatuses += amount;
    }

    private IEnumerator ProcessStartOfTurnStatusEffects()
    {
        if (statusPotencies.TryGetValue(CombatStatusType.Poisoned, out int poisonDamage) && poisonDamage > 0)
        {
            TakeDamage(poisonDamage, DamageSoundType.MagicHit);
        }

        if (statusPotencies.TryGetValue(CombatStatusType.Bleeding, out int bleedingDamage) && bleedingDamage > 0 && currentHealth > 0)
        {
            float delay = bleedingDamageRandomDelayMax > 0f ? UnityEngine.Random.Range(0f, bleedingDamageRandomDelayMax) : 0f;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (currentHealth <= 0 || isDying)
            {
                yield break;
            }

            PlayStatusApplyFx(CombatStatusType.Bleeding);
            TakeDamage(bleedingDamage, DamageSoundType.MagicHit);
        }

        if (currentHealth <= 0)
        {
            yield break;
        }

        List<CombatStatusType> expiredStatuses = null;
        List<CombatStatusType> activeStatuses = new List<CombatStatusType>(statusDurations.Keys);
        for (int index = 0; index < activeStatuses.Count; index++)
        {
            CombatStatusType statusType = activeStatuses[index];
            int remainingDuration = statusDurations[statusType];
            if (remainingDuration < 0)
            {
                continue;
            }

            remainingDuration--;
            if (remainingDuration <= 0)
            {
                expiredStatuses ??= new List<CombatStatusType>();
                expiredStatuses.Add(statusType);
                continue;
            }

            statusDurations[statusType] = remainingDuration;
        }

        if (expiredStatuses == null)
        {
            yield break;
        }

        for (int index = 0; index < expiredStatuses.Count; index++)
        {
            CombatStatusType statusType = expiredStatuses[index];
            statusDurations.Remove(statusType);
            statusPotencies.Remove(statusType);
        }

        RefreshStatusVisuals();
    }

    private void CacheStatusRenderers()
    {
        if (statusRenderers != null && statusRenderers.Length > 0)
        {
            return;
        }

        statusRenderers = GetComponentsInChildren<Renderer>(true);
        statusPropertyBlocks.Clear();
        for (int index = 0; index < statusRenderers.Length; index++)
        {
            Renderer renderer = statusRenderers[index];
            if (renderer == null)
            {
                continue;
            }

            statusPropertyBlocks[renderer] = new MaterialPropertyBlock();
        }
    }

    private void RefreshStatusVisuals()
    {
        RefreshPersistentStatusFx(CombatStatusType.Frozen);
        RefreshPersistentStatusFx(CombatStatusType.Poisoned);
        RefreshBleedingRimGlow();
    }

    private void RefreshBleedingRimGlow()
    {
        bool hasBleeding = HasStatusEffect(CombatStatusType.Bleeding);
        if (hasBleeding == isBleedingRimGlowApplied)
        {
            return;
        }

        CacheStatusRenderers();
        if (statusRenderers == null || statusRenderers.Length == 0)
        {
            return;
        }

        float rimIntensity = hasBleeding ? bleedingRimGlowIntensity : 0f;
        Color rimColor = hasBleeding ? bleedingRimGlowColor : Color.white;
        float rimPower = Mathf.Max(0.25f, bleedingRimGlowPower);

        for (int index = 0; index < statusRenderers.Length; index++)
        {
            Renderer renderer = statusRenderers[index];
            if (renderer == null || !statusPropertyBlocks.TryGetValue(renderer, out MaterialPropertyBlock block))
            {
                continue;
            }

            renderer.GetPropertyBlock(block);
            block.SetColor(RimGlowColorId, rimColor);
            block.SetFloat(RimGlowIntensityId, rimIntensity);
            block.SetFloat(RimGlowPowerId, rimPower);
            renderer.SetPropertyBlock(block);
        }

        isBleedingRimGlowApplied = hasBleeding;
    }

    private IEnumerator ResolveMistyConfusionTurn()
    {
        List<Enemy> targets = GatherConfusionTargets(true);
        if (targets.Count == 0)
        {
            ClearMistyConfusion();
            yield break;
        }

        List<Enemy> targetsToAttack = mistyConfusionParanoia
            ? targets
            : new List<Enemy> { SelectWeakestEnemy(targets) };

        for (int index = 0; index < targetsToAttack.Count; index++)
        {
            Enemy target = targetsToAttack[index];
            if (currentHealth <= 0)
            {
                break;
            }

            if (target == null || target == this || target.CurrentHealth <= 0 || !CanAttackEnemyTargetFrom(gridPosition, target, true))
            {
                continue;
            }

            FaceTargetForAttack(target.transform);
            TriggerAttackAnimation();

            if (attackPattern == EnemyAttackPattern.Projectile && projectilePrefab != null)
            {
                yield return FireProjectileAtEnemy(target);
            }
            else
            {
                float damageDelay = GetEnemyTargetDamageDelay(target);
                if (damageDelay > 0f)
                {
                    yield return new WaitForSeconds(damageDelay);
                }
            }

            int allyDamage = Mathf.Max(1, Mathf.RoundToInt(EffectiveForce * mistyConfusionAllyDamageMultiplier));
            int appliedDamage = target.TakeDamage(allyDamage, damageSoundType);
            if (mistyConfusionBrokenHeart && currentHealth > 0 && appliedDamage > 0)
            {
                int selfDamage = Mathf.Max(1, Mathf.RoundToInt(appliedDamage * mistyConfusionBrokenHeartSelfDamageRatio));
                StartCoroutine(ApplyMistyBrokenHeartSelfDamageAfterDelay(selfDamage, 1f));
            }
            yield return new WaitForSeconds(0.08f);
        }

        ClearMistyConfusion();
    }

    private IEnumerator ApplyMistyBrokenHeartSelfDamageAfterDelay(int damage, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (currentHealth > 0)
        {
            TakeDamage(Mathf.Max(1, damage), DamageSoundType.Default);
        }
    }

    private void ClearMistyConfusion()
    {
        mistyConfusionParanoia = false;
        mistyConfusionBrokenHeart = false;
        mistyConfusionAllyDamageMultiplier = 1f;
        mistyConfusionBrokenHeartSelfDamageRatio = 0f;
        ClearMistyConfusionFx();
    }

    private List<Enemy> GatherConfusionTargets(bool includeDirectDiagonalTargets = false)
    {
        List<Enemy> targets = new List<Enemy>();
        if (Board == null)
        {
            return targets;
        }

        IReadOnlyList<Enemy> enemies = Board.SpawnedEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy candidate = enemies[index];
            if (candidate == null || candidate == this || candidate.CurrentHealth <= 0)
            {
                continue;
            }

            if (CanAttackEnemyTargetFrom(gridPosition, candidate, includeDirectDiagonalTargets))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private Enemy SelectWeakestEnemy(List<Enemy> enemies)
    {
        Enemy weakestEnemy = null;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy candidate = enemies[index];
            if (candidate == null)
            {
                continue;
            }

            if (weakestEnemy == null
                || candidate.CurrentHealth < weakestEnemy.CurrentHealth
                || (candidate.CurrentHealth == weakestEnemy.CurrentHealth && candidate.MaxHealth < weakestEnemy.MaxHealth))
            {
                weakestEnemy = candidate;
            }
        }

        return weakestEnemy;
    }

    public bool CanAttackAnyEnemyAlly(bool includeDirectDiagonalTargets = false)
    {
        return GatherConfusionTargets(includeDirectDiagonalTargets).Count > 0;
    }

    private void ShowMistyConfusionFx()
    {
        if (mistyConfusionFxPrefab == null)
        {
            return;
        }

        ClearMistyConfusionFx();

        Transform anchor = EffectAnchor != null ? EffectAnchor : transform;
        activeMistyConfusionFxInstance = Instantiate(mistyConfusionFxPrefab, anchor);
        activeMistyConfusionFxInstance.transform.localPosition = Vector3.zero;
        activeMistyConfusionFxInstance.transform.localRotation = mistyConfusionFxPrefab.transform.localRotation;
        activeMistyConfusionFxInstance.transform.localScale = mistyConfusionFxPrefab.transform.localScale;

        if (mistyConfusionFxMaxDuration > 0f)
        {
            mistyConfusionFxTimeoutCoroutine = StartCoroutine(ClearMistyConfusionFxAfterDelay(mistyConfusionFxMaxDuration));
        }
    }

    private IEnumerator ClearMistyConfusionFxAfterDelay(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        mistyConfusionFxTimeoutCoroutine = null;
        ClearMistyConfusionFx();
    }

    private void ClearMistyConfusionFx()
    {
        if (mistyConfusionFxTimeoutCoroutine != null)
        {
            StopCoroutine(mistyConfusionFxTimeoutCoroutine);
            mistyConfusionFxTimeoutCoroutine = null;
        }

        if (activeMistyConfusionFxInstance == null)
        {
            return;
        }

        Destroy(activeMistyConfusionFxInstance);
        activeMistyConfusionFxInstance = null;
    }

    private bool CanAttackEnemyTargetFrom(Vector2Int origin, Enemy target, bool includeDirectDiagonalTargets = false)
    {
        if (target == null || Board == null)
        {
            return false;
        }

        Vector2Int delta = target.GridPosition - origin;
        int manhattanDistance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        int diagonalDistance = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));

        if (includeDirectDiagonalTargets && manhattanDistance == 2 && diagonalDistance == 1)
        {
            return true;
        }

        switch (attackPattern)
        {
            case EnemyAttackPattern.AdjacentOrthogonal:
                return manhattanDistance == 1;
            case EnemyAttackPattern.Radial:
                int radialDistance = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
                return radialDistance > 0 && radialDistance <= Mathf.Max(1, attackRange);
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

                return !directVision || Board.HasLineOfSight(origin, target.GridPosition);
            default:
                return false;
        }
    }

    private float GetEnemyTargetDamageDelay(Enemy target)
    {
        float delay = Mathf.Max(0f, attackDamageDelay);
        if (!multiplyAttackDamageDelayByDistance || target == null)
        {
            return delay;
        }

        int distance = Mathf.Abs(target.GridPosition.x - gridPosition.x) + Mathf.Abs(target.GridPosition.y - gridPosition.y);
        return delay * Mathf.Max(1, distance);
    }

    private void Die()
    {
        DieInternal(true, true);
    }

    public void ForceEliminateLinkedSummon()
    {
        DieInternal(false, false);
    }

    private void DieInternal(bool allowSpecialDeathSpawn, bool resolveOwnedLinkedSummons)
    {
        if (isDying)
        {
            return;
        }

        isDying = true;
        ClearDeathMarkFx();
        SpawnDeathFx();
        if (resolveOwnedLinkedSummons)
        {
            Board?.HandleLinkedSummonsForDeadOwner(this);
        }

        if (allowSpecialDeathSpawn)
        {
            bool spawnedSkull = Board != null && Board.SpawnSkeletonSkull(this);
            if (!spawnedSkull)
            {
                Board?.AwardGoldForEnemy(this);
            }
        }
        else
        {
            Board?.AwardGoldForEnemy(this);
        }

        ReleaseExtraOccupiedCells();
        if (Board != null)
        {
            Board.RemoveEnemy(this);
        }

        Destroy(gameObject, deathDestroyDelay);
    }

    public void PlayReviveAnimation()
    {
        CacheBody();
        CacheAnimator();
        ClearAnimationTriggers();
        SetFlyingAnimation(false);

        if (enemyBody != null)
        {
            ApplyBodyLocalYaw(enemyBody, cachedBodyOriginalLocalEulerAngles.y);
        }

        if (enemyAnimator != null && !string.IsNullOrWhiteSpace(optionalActionTriggerParameter))
        {
            enemyAnimator.SetTrigger(optionalActionTriggerParameter);
        }
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
