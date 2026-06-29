using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterProcFxKey
{
    Heal,
    BonusMovement,
    Damage,
    RetaliationHit,
    Execute
}

[Serializable]
public class CharacterProcFxConfig
{
    [SerializeField] private CharacterProcFxKey key;
    [SerializeField] private GameObject fxPrefab;
    [SerializeField] private Vector3 positionOffset;
    [Min(0f)]
    [SerializeField] private float destroyAfterSeconds = 1f;

    public CharacterProcFxKey Key => key;
    public GameObject FxPrefab => fxPrefab;
    public Vector3 PositionOffset => positionOffset;
    public float DestroyAfterSeconds => destroyAfterSeconds;
}

[Serializable]
public class CharacterBasicAttackVisualConfig
{
    [SerializeField] private AbilityDefinition basicAttackAbility;
    [SerializeField] private List<GameObject> visualRoots = new List<GameObject>();

    public AbilityDefinition BasicAttackAbility => basicAttackAbility;
    public IReadOnlyList<GameObject> VisualRoots => visualRoots;
}

public class Character : MonoBehaviour
{
    [Header("Core Stats")]
    [SerializeField] private CharacterData characterData;
    [SerializeField] [ReadOnly] private int maxHealth = 10;
    [SerializeField] [ReadOnly] private int currentHealth = 10;
    [SerializeField] [ReadOnly] private int bonusDamage;
    [SerializeField] [ReadOnly] private int resistance;
    [SerializeField] [ReadOnly] private int movementPointsPerTurn = 2;

    [Header("Board")]
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private float moveDuration = 0.18f;
    [SerializeField] private float spawnHeight = 0.08f;
    [SerializeField] private float deathDestroyDelay = 0.12f;
    [SerializeField] private GameObject fxDeathPrefab;
    [SerializeField] private CanvasUnitUI canvasUnitUI;
    [SerializeField] private Transform characterBody;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private TrailRenderer characterTrail;
    [SerializeField] private string dashingBoolParameter = "Dashing";
    [SerializeField] private string attackTriggerParameter = "Attack";
    [SerializeField] private string attackPlaceholderClipName = "Attack_Spiral";
    [SerializeField] private bool orientTowardsDashDirection = true;
    [SerializeField] private bool resetBodyRotationAfterSlide = true;
    [SerializeField] private List<CharacterProcFxConfig> procFxConfigs = new List<CharacterProcFxConfig>();
    [SerializeField] private List<CharacterBasicAttackVisualConfig> basicAttackVisuals = new List<CharacterBasicAttackVisualConfig>();
    private float dashBodyRotateDuration = 0.05f;
    private float dashBodyResetDuration = 0.05f;

    private Renderer[] renderers;
    private Color[] baseColors;
    private Tween moveTween;
    private Tween bodyRotationTween;
    private Tween impactTween;
    private int remainingMovementPoints;
    private int wolfMovementPoints;
    private readonly List<CharacterAbilityRuntime> abilities = new List<CharacterAbilityRuntime>();
    private readonly List<Enemy> traversedEnemiesBuffer = new List<Enemy>();
    private RendererBlinkFeedback blinkFeedback;
    private AnimatorOverrideController animatorOverrideController;
    private AnimationClip attackPlaceholderClip;
    private Material runtimeTrailMaterial;
    private Color defaultTrailMaterialColor = Color.white;
    private GameObject defaultTrailObject;
    private GameObject activeReplacementTrailInstance;
    private AbilityDefinition activeTrailColorOwner;
    private AbilityDefinition activeTrailReplacementOwner;
    private Coroutine temporaryTrailColorCoroutine;
    private Coroutine temporaryTrailReplacementCoroutine;
    private readonly Quaternion defaultBodyLocalRotation = Quaternion.identity;
    private PlayerRunRewardState runRewardState;
    private bool baseStatsCached;
    private int baseMaxHealth;
    private int baseBonusDamage;
    private int baseResistance;
    private int baseMovementPointsPerTurn;
    private bool isFirstPlayerTurnOfArena;
    private bool luckyCoinUsedThisCombat;
    private bool sandglassTalismanTriggeredThisCombat;
    private bool tookDamageDuringEnemyTurn;
    private bool tookDamageSinceLastPlayerTurn;
    private bool isFirstEnemyTurnOfArena;
    private bool enemyTurnInProgress;
    private bool isDying;
    private bool attackedThisTurn;
    private bool attackedLastTurn;
    private bool movedThisTurn;
    private bool movedLastTurn;
    private bool cowardScarfTriggeredThisTurn;
    private bool corruptedBlouseTriggeredThisCombat;
    private readonly HashSet<Enemy> frostCharmedEnemiesThisTurn = new HashSet<Enemy>();
    private readonly HashSet<Enemy> makibishiTriggeredEnemiesThisCombat = new HashSet<Enemy>();
    private int nextAttackBonusDamage;
    private int samuraiMaskBonusDamage;
    private int bonusDamageUntilEndOfTurn;
    private int bonusDamageUntilNextMovement;
    private bool isPoisoned;
    private GameObject nextAttackBonusAuraPrefab;
    private GameObject activeNextAttackBonusAuraInstance;
    private GameObject temporaryDamageAuraPrefab;
    private GameObject activeTemporaryDamageAuraInstance;
    [SerializeField] private GameObject fxFortuneDaggerCombatStartPrefab;
    private readonly Dictionary<CombatStatusType, GameObject> activeStatusFxInstances = new Dictionary<CombatStatusType, GameObject>();
    private CombatStatusFxLibrary statusFxLibrary;
    private Enemy markedEnemy;
    private LichSkullObject markedLichSkull;
    private DeathMarkAbility deathMarkAbility;
    private int actionLockCount;
    private readonly Dictionary<AbilityDefinition, HashSet<Enemy>> abilityTargetsHitThisTurn = new Dictionary<AbilityDefinition, HashSet<Enemy>>();

    public Vector2Int GridPosition => gridPosition;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public int BonusDamage => bonusDamage;
    public int Resistance => resistance;
    public int BaseMovementPoints => movementPointsPerTurn;
    public int RemainingMovementPoints => remainingMovementPoints;
    public int WolfMovementPoints => wolfMovementPoints;
    public int ExtraMovementPointsBeyondBase => Mathf.Max(0, remainingMovementPoints - wolfMovementPoints - movementPointsPerTurn);
    public bool CanAct => (remainingMovementPoints > 0 || CanSpendHealthForExtraMovement()) && !IsBusy;
    public bool IsPoisoned => isPoisoned;
    public bool IsMoving { get; private set; }
    public bool IsBusy => IsMoving || actionLockCount > 0;
    public bool MovedLastTurn => movedLastTurn;
    public Player Owner { get; private set; }
    public BoardManager Board { get; private set; }
    public CharacterData Data => characterData;
    public string CharacterName => characterData != null ? characterData.CharacterName : name;
    public string CharacterDescription => characterData != null ? characterData.Description : string.Empty;
    public Sprite CharacterPortrait => characterData != null ? characterData.Portrait : null;
    public Sprite CharacterLosePortrait => characterData != null ? characterData.PortraitLose : null;
    public Sprite CharacterPathIcon => characterData != null ? characterData.PathIcon : null;

    public void SetCharacterData(CharacterData data)
    {
        if (data == null)
        {
            return;
        }

        characterData = data;
        ApplyCharacterDataDefinition();
    }

    public AbilityCategory GetAbilitySlotCategory(int slotIndex)
    {
        switch (slotIndex)
        {
            case 1:
                return AbilityCategory.MobilitySkill;
            case 2:
                return AbilityCategory.SpecialPower;
            default:
                return AbilityCategory.BasicAttack;
        }
    }
    public event Action<Character> MovementPointsChanged;
    public event Action<Character> AbilitiesChanged;
    public event Action<Character, ItemRewardKey, bool, float> ItemActivationChanged;
    public event Action<Character, Vector2Int, Vector2Int> Moved;
    public event Action<Character> Died;
    public IReadOnlyList<CharacterAbilityRuntime> Abilities => abilities;
    public PlayerRunRewardState RunRewardState => runRewardState;
    private float itemActivationPulseDuration = 2.5f;
    private readonly Dictionary<ItemRewardKey, bool> activeItemStates = new Dictionary<ItemRewardKey, bool>();

    private void OnValidate()
    {
        ApplyCharacterDataDefinition();
        currentHealth = maxHealth;
    }

    public void Assign(Player owner, Vector2Int spawnGridPosition, BoardManager board)
    {
        Owner = owner;
        Board = board;
        ApplyCharacterDataDefinition();
        CacheBaseStatsIfNeeded();
        gridPosition = spawnGridPosition;
        currentHealth = maxHealth;
        CacheRenderers();
        CacheBody();
        CacheAnimator();
        CacheTrail();
        blinkFeedback = GetComponent<RendererBlinkFeedback>();
        CacheHpBar();
        InitializeAbilities();
        isFirstPlayerTurnOfArena = true;
        luckyCoinUsedThisCombat = false;
        sandglassTalismanTriggeredThisCombat = false;
        tookDamageDuringEnemyTurn = false;
        tookDamageSinceLastPlayerTurn = false;
        isFirstEnemyTurnOfArena = true;
        enemyTurnInProgress = false;
        attackedThisTurn = false;
        attackedLastTurn = false;
        movedThisTurn = false;
        movedLastTurn = false;
        cowardScarfTriggeredThisTurn = false;
        corruptedBlouseTriggeredThisCombat = false;
        frostCharmedEnemiesThisTurn.Clear();
        makibishiTriggeredEnemiesThisCombat.Clear();
        ClearNextAttackBonusDamage();
        samuraiMaskBonusDamage = 0;
        ClearPoisoned(false);
        actionLockCount = 0;
        abilityTargetsHitThisTurn.Clear();
        activeItemStates.Clear();
        markedEnemy = null;
        markedLichSkull = null;
        deathMarkAbility = null;
        wolfMovementPoints = 0;
        SnapToGrid();
        ResetTurn();
        SetDashingAnimation(false);
        RefreshHpBar();
        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
    }

    public List<AbilityDefinition> GetCurrentAbilityDefinitions()
    {
        List<AbilityDefinition> currentDefinitions = new List<AbilityDefinition>();
        for (int index = 0; index < abilities.Count; index++)
        {
            AbilityDefinition definition = abilities[index].Definition;
            if (definition != null)
            {
                currentDefinitions.Add(definition);
            }
        }

        if (currentDefinitions.Count == 0)
        {
            currentDefinitions.AddRange(GetStartingAbilityDefinitions());
        }

        return currentDefinitions;
    }

    public void ApplyRunRewardState(PlayerRunRewardState state)
    {
        bool wasPoisoned = isPoisoned;
        ApplyCharacterDataDefinition();
        CacheBaseStatsIfNeeded();
        runRewardState = state;
        activeItemStates.Clear();
        RecalculateItemDrivenStats();
        ClearNextAttackBonusDamage();
        samuraiMaskBonusDamage = 0;
        ClearPoisoned(false);
        isFirstEnemyTurnOfArena = true;
        enemyTurnInProgress = false;
        abilityTargetsHitThisTurn.Clear();
        cowardScarfTriggeredThisTurn = false;
        corruptedBlouseTriggeredThisCombat = false;
        makibishiTriggeredEnemiesThisCombat.Clear();
        markedEnemy = null;
        markedLichSkull = null;
        deathMarkAbility = null;
        wolfMovementPoints = 0;

        InitializeAbilities(runRewardState != null ? runRewardState.GetEquippedAbilities() : GetStartingAbilityDefinitions());

        int targetHealth = runRewardState != null && runRewardState.CurrentHealth >= 0
            ? runRewardState.CurrentHealth
            : maxHealth;
        currentHealth = Mathf.Clamp(targetHealth, 0, maxHealth);
        SyncRunStateHealth();

        ResetTurn();
        if (wasPoisoned)
        {
            isPoisoned = true;
            RefreshPoisonFx();
        }

        RefreshHpBar();
        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
    }

    public void ResetTurn()
    {
        tookDamageSinceLastPlayerTurn = false;
        attackedThisTurn = false;
        movedThisTurn = false;
        cowardScarfTriggeredThisTurn = false;
        RecalculateItemDrivenStats();
        wolfMovementPoints = 0;
        remainingMovementPoints = movementPointsPerTurn;
        frostCharmedEnemiesThisTurn.Clear();
        ClearNextAttackBonusDamage();
        abilityTargetsHitThisTurn.Clear();
        for (int index = 0; index < abilities.Count; index++)
        {
            abilities[index].BeginTurn(this);
        }

        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
    }

    public bool TrySlide(Vector2Int direction)
    {
        bool consumesMovementPoint = ShouldConsumeMovementPointForSlide();
        bool canSpendBloodForMovement = consumesMovementPoint && remainingMovementPoints <= 0 && CanSpendHealthForExtraMovement();
        if (Board == null
            || (remainingMovementPoints <= 0 && consumesMovementPoint && !canSpendBloodForMovement)
            || IsBusy
            || direction == Vector2Int.zero)
        {
            return false;
        }

        Vector2Int startCell = gridPosition;
        traversedEnemiesBuffer.Clear();
        bool allowUnitTraversal = CanTraverseUnits();
        Vector2Int destination = ShouldLimitNextSlideToOneCell()
            ? GetSingleStepDestination(direction)
            : Board.GetSlideDestination(
                gridPosition,
                direction,
                allowUnitTraversal,
                traversedEnemiesBuffer);

        if (destination == gridPosition)
        {
            return false;
        }

        bool allowLandingOnSkull = Board.CanCharacterStandOnSkullCell(destination);
        if (!Board.MoveOccupant(gridPosition, destination, BoardOccupantKind.PlayerCharacter, allowLandingOnSkull))
        {
            return false;
        }

        ConsumeTemporaryBonusDamageOnMovement();

        if (orientTowardsDashDirection)
        {
            FaceDashDirection(direction);
        }

        gridPosition = destination;
        if (consumesMovementPoint)
        {
            ConsumeMovementPointInternal(true);
        }
        Board.NotifyCharacterTraversedPath(this, startCell, destination);
        NotifyAbilitiesCharacterMoved(startCell, destination, consumesMovementPoint);
        NotifyMoved(startCell, destination);
        ApplyTraversalEffects();
        ConsumeSingleStepModifiers();
        AnimateToGrid();
        return true;
    }

    public CharacterAbilityRuntime GetAbility(int abilityIndex)
    {
        return abilityIndex >= 0 && abilityIndex < abilities.Count ? abilities[abilityIndex] : null;
    }

    public CharacterAbilityRuntime GetAbilityForSlot(int slotIndex)
    {
        AbilityCategory slotCategory = GetAbilitySlotCategory(slotIndex);
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            if (runtime?.Definition != null && runtime.Definition.Category == slotCategory)
            {
                return runtime;
            }
        }

        return null;
    }

    public int GetRuntimeIndexForSlot(int slotIndex)
    {
        AbilityCategory slotCategory = GetAbilitySlotCategory(slotIndex);
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            if (runtime?.Definition != null && runtime.Definition.Category == slotCategory)
            {
                return index;
            }
        }

        return -1;
    }

    public bool TryUseAbility(int abilityIndex, Vector2Int? targetCell = null)
    {
        CharacterAbilityRuntime runtime = GetAbility(abilityIndex);
        if (runtime == null || IsBusy)
        {
            return false;
        }

        bool success = runtime.TryUse(this, targetCell);
        if (!success)
        {
            return false;
        }

        if (HasItem(ItemRewardKey.SandglassTalisman)
            && !sandglassTalismanTriggeredThisCombat
            && runtime.Definition != null
            && runtime.Definition.CooldownTurns > 0)
        {
            runtime.ReduceCooldown(1);
            sandglassTalismanTriggeredThisCombat = true;
            TriggerItemActivationPulse(ItemRewardKey.SandglassTalisman);
        }

        if (HasItem(ItemRewardKey.CowardsScarf)
            && !cowardScarfTriggeredThisTurn
            && HaveAllAbilitiesBeenSpentForTurn())
        {
            cowardScarfTriggeredThisTurn = true;
            GainMovementPoints(1, ItemRewardKey.CowardsScarf);
        }

        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
        return true;
    }

    public bool TryTeleportTo(Vector2Int targetCell)
    {
        return TryTeleportTo(targetCell, moveDuration);
    }

    public bool TryTeleportTo(Vector2Int targetCell, float animationDuration)
    {
        if (Board == null || targetCell == gridPosition)
        {
            return false;
        }

        Vector2Int startCell = gridPosition;
        if (!Board.IsCellWalkable(targetCell))
        {
            return false;
        }

        if (!Board.MoveOccupant(gridPosition, targetCell, BoardOccupantKind.PlayerCharacter))
        {
            return false;
        }

        ConsumeTemporaryBonusDamageOnMovement();

        gridPosition = targetCell;
        Board.NotifyCharacterTraversedPath(this, startCell, targetCell);
        NotifyAbilitiesCharacterMoved(startCell, targetCell, false);
        NotifyMoved(startCell, targetCell);
        AnimateToGrid(animationDuration);
        return true;
    }

    public bool TryTeleportToImmediate(Vector2Int targetCell)
    {
        if (!TryRelocateImmediateInternal(targetCell))
        {
            return false;
        }

        impactTween?.Kill();
        SnapToGrid();
        return true;
    }

    public void PlayImpactBump(Vector3 sourceWorldPosition, float duration, float height = 0.12f)
    {
        if (duration <= 0f || !isActiveAndEnabled)
        {
            return;
        }

        Vector3 basePosition = transform.position;
        Vector3 awayDirection = basePosition - sourceWorldPosition;
        awayDirection.y = 0f;
        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            awayDirection = -transform.forward;
            awayDirection.y = 0f;
        }

        awayDirection = awayDirection.sqrMagnitude > 0.0001f
            ? awayDirection.normalized
            : Vector3.back;

        Vector3 impactPeakPosition = basePosition + awayDirection * 0.18f + Vector3.up * Mathf.Max(0f, height);
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

    public IEnumerator PlayImpactBumpAndRelocate(
        Vector3 sourceWorldPosition,
        Vector2Int? targetCell,
        float duration,
        float height = 0.12f)
    {
        if (Board == null)
        {
            yield break;
        }

        Vector2Int startCell = gridPosition;
        bool relocate = targetCell.HasValue && targetCell.Value != startCell;
        if (duration <= 0f || !isActiveAndEnabled)
        {
            if (relocate)
            {
                TryTeleportToImmediate(targetCell.Value);
            }

            yield break;
        }

        BeginActionLock();
        try
        {
            moveTween?.Kill();
            moveTween = null;
            IsMoving = false;
            SetDashingAnimation(false);

            impactTween?.Kill();
            impactTween = null;

            Vector3 startPosition = transform.position;
            Vector3 awayDirection = startPosition - sourceWorldPosition;
            awayDirection.y = 0f;
            if (awayDirection.sqrMagnitude <= 0.0001f)
            {
                awayDirection = -transform.forward;
                awayDirection.y = 0f;
            }

            awayDirection = awayDirection.sqrMagnitude > 0.0001f
                ? awayDirection.normalized
                : Vector3.back;

            Vector3 impactPeakPosition = startPosition + awayDirection * 0.18f + Vector3.up * Mathf.Max(0f, height);
            float halfDuration = Mathf.Max(0.01f, duration * 0.5f);

            bool relocationApplied = false;
            Vector3 landingPosition = startPosition;
            if (relocate)
            {
                landingPosition = Board.GridToWorldPosition(targetCell.Value) + Vector3.up * spawnHeight;
                DetachFromCurrentGridCell();
            }

            if (relocate)
            {
                Sequence impactSequence = DOTween.Sequence()
                    .Append(DOVirtual.Float(0f, 1f, Mathf.Max(0.01f, duration), progress =>
                    {
                        Vector3 horizontalPosition = Vector3.Lerp(startPosition, landingPosition, progress);
                        float verticalOffset = 4f * Mathf.Max(0f, height) * progress * (1f - progress);
                        transform.position = horizontalPosition + Vector3.up * verticalOffset;
                    }).SetEase(Ease.Linear))
                    .AppendCallback(() =>
                    {
                        relocationApplied = TryAttachToGridCellInternal(startCell, targetCell.Value);
                        if (!relocationApplied)
                        {
                            transform.position = startPosition;
                            ReattachToCurrentGridCell(startCell);
                        }
                    });

                impactTween = impactSequence
                    .OnComplete(() =>
                    {
                        impactTween = null;
                        transform.position = relocationApplied ? landingPosition : startPosition;
                    })
                    .OnKill(() =>
                    {
                        impactTween = null;
                        if (this != null && isActiveAndEnabled)
                        {
                            if (relocate && !relocationApplied)
                            {
                                ReattachToCurrentGridCell(startCell);
                            }

                            transform.position = relocationApplied ? landingPosition : startPosition;
                        }
                    });
            }
            else
            {
                Sequence impactSequence = DOTween.Sequence()
                    .Append(transform.DOMove(impactPeakPosition, halfDuration).SetEase(Ease.OutQuad))
                    .Append(transform.DOMove(startPosition, halfDuration).SetEase(Ease.InQuad));

                impactTween = impactSequence
                    .OnComplete(() =>
                    {
                        impactTween = null;
                        transform.position = startPosition;
                    })
                    .OnKill(() =>
                    {
                        impactTween = null;
                        if (this != null && isActiveAndEnabled)
                        {
                            transform.position = startPosition;
                        }
                    });
            }

            yield return impactTween.WaitForCompletion();
        }
        finally
        {
            EndActionLock();
        }
    }

    private bool TryRelocateImmediateInternal(Vector2Int targetCell)
    {
        if (Board == null || targetCell == gridPosition)
        {
            return false;
        }

        Vector2Int startCell = gridPosition;
        if (!Board.IsCellWalkable(targetCell))
        {
            return false;
        }

        if (!Board.MoveOccupant(gridPosition, targetCell, BoardOccupantKind.PlayerCharacter))
        {
            return false;
        }

        ConsumeTemporaryBonusDamageOnMovement();
        moveTween?.Kill();
        moveTween = null;
        IsMoving = false;
        gridPosition = targetCell;
        Board.NotifyCharacterTraversedPath(this, startCell, targetCell);
        NotifyAbilitiesCharacterMoved(startCell, targetCell, false);
        NotifyMoved(startCell, targetCell);
        SetDashingAnimation(false);
        return true;
    }

    private void DetachFromCurrentGridCell()
    {
        if (Board == null || !Board.TryGetCell(gridPosition, out BoardCell currentCell))
        {
            return;
        }

        if (currentCell.Occupant == gameObject)
        {
            currentCell.ClearOccupant();
        }
    }

    private void ReattachToCurrentGridCell(Vector2Int cellPosition)
    {
        if (Board == null || !Board.TryGetCell(cellPosition, out BoardCell cell))
        {
            return;
        }

        cell.SetOccupant(gameObject, BoardOccupantKind.PlayerCharacter);
        gridPosition = cellPosition;
    }

    private bool TryAttachToGridCellInternal(Vector2Int startCell, Vector2Int targetCell)
    {
        if (Board == null || targetCell == startCell)
        {
            return false;
        }

        if (!Board.TryGetCell(targetCell, out BoardCell targetBoardCell))
        {
            return false;
        }

        if (!targetBoardCell.Walkable)
        {
            return false;
        }

        if (targetBoardCell.IsOccupied && targetBoardCell.Occupant != gameObject)
        {
            return false;
        }

        ConsumeTemporaryBonusDamageOnMovement();
        moveTween?.Kill();
        moveTween = null;
        IsMoving = false;
        gridPosition = targetCell;
        targetBoardCell.SetOccupant(gameObject, BoardOccupantKind.PlayerCharacter);
        Board.NotifyCharacterTraversedPath(this, startCell, targetCell);
        NotifyAbilitiesCharacterMoved(startCell, targetCell, false);
        NotifyMoved(startCell, targetCell);
        SetDashingAnimation(false);
        return true;
    }

    public void ConsumeMovementPoint()
    {
        ConsumeMovementPointInternal();
    }

    public void BeginActionLock()
    {
        actionLockCount++;
    }

    public void EndActionLock()
    {
        actionLockCount = Mathf.Max(0, actionLockCount - 1);
    }

    public Transform GetBodyTransform()
    {
        CacheBody();
        return characterBody != null ? characterBody : transform;
    }

    public int DealDamageToEnemy(
        Enemy enemy,
        int baseDamage,
        bool addBonusDamage,
        bool isAbilityDamage = false,
        DamageSoundType hitSoundType = DamageSoundType.Default,
        AbilityDefinition sourceAbility = null)
    {
        if (enemy == null)
        {
            return 0;
        }

        RecalculateItemDrivenStats();
        int nextWeaponAbilityBonusDamage = GetNextWeaponAbilityBonusDamage(sourceAbility);
        int totalDamage = Mathf.Max(1, baseDamage + (addBonusDamage ? bonusDamage : 0));
        totalDamage += nextWeaponAbilityBonusDamage;
        totalDamage += GetAbilityItemBonusDamage(sourceAbility);
        totalDamage += GetDistanceBonusDamageAgainstEnemy(enemy);
        if (HasItem(ItemRewardKey.ThornedBracer)
            && enemy.CurrentHealth <= Mathf.Max(1, totalDamage - enemy.Resistance))
        {
            totalDamage += 2;
        }

        int appliedDamage = enemy.TakeDamage(totalDamage, hitSoundType, isAbilityDamage);
        if (appliedDamage > 0)
        {
            HandleEnemyDamagedByPlayer(enemy, isAbilityDamage);
        }

        if (isAbilityDamage)
        {
            if (appliedDamage > 0)
            {
                PlayAbilityDamageFx(sourceAbility, enemy.EffectAnchor);
            }

            if (enemy == markedEnemy && enemy.CurrentHealth > 0)
            {
                int bonusMarkDamage = enemy.TakeDamage(1, DamageSoundType.Default, false);
                appliedDamage += bonusMarkDamage;
                if (enemy.CurrentHealth <= 0)
                {
                    HandleEnemyKilled(enemy);
                }
            }
        }
        if (enemy.CurrentHealth <= 0)
        {
            HandleEnemyKilled(enemy);
        }

        if (nextWeaponAbilityBonusDamage > 0)
        {
            ConsumeNextAttackBonusDamage();
        }

        return appliedDamage;
    }

    public void DealDamageToEnemyWithAbilityTiming(
        AbilityDefinition abilityDefinition,
        Enemy enemy,
        int baseDamage,
        bool addBonusDamage,
        bool isAbilityDamage = false,
        DamageSoundType hitSoundType = DamageSoundType.Default,
        Vector2Int? originCellOverride = null,
        AbilityDefinition sourceAbility = null,
        Action<Enemy, int> onDamageApplied = null)
    {
        if (enemy == null)
        {
            return;
        }

        Vector2Int originCell = originCellOverride ?? gridPosition;
        float delay = abilityDefinition != null
            ? abilityDefinition.GetDamageApplyDelay(originCell, enemy.GridPosition)
            : 0f;

        if (delay <= 0f)
        {
            int appliedDamage = DealDamageToEnemy(enemy, baseDamage, addBonusDamage, isAbilityDamage, hitSoundType, sourceAbility);
            onDamageApplied?.Invoke(enemy, appliedDamage);
            return;
        }

        StartCoroutine(DealDamageToEnemyAfterDelay(enemy, baseDamage, addBonusDamage, isAbilityDamage, hitSoundType, delay, sourceAbility, onDamageApplied));
    }

    public int TakeDamage(
        int incomingDamage,
        Enemy sourceEnemy = null,
        bool wasProjectile = false,
        DamageSoundType hitSoundType = DamageSoundType.Default)
    {
        if (isDying)
        {
            return 0;
        }

        RecalculateItemDrivenStats();
        int finalDamage = Mathf.Max(1, incomingDamage - resistance);
        if (HasItem(ItemRewardKey.WhiteFlag) && isFirstEnemyTurnOfArena)
        {
            finalDamage = Mathf.Min(finalDamage, 1);
        }

        if (HasItem(ItemRewardKey.LuckyCoin) && !luckyCoinUsedThisCombat && currentHealth - finalDamage <= 0)
        {
            luckyCoinUsedThisCombat = true;
            finalDamage = Mathf.Max(0, currentHealth - 1);
            TriggerItemActivationPulse(ItemRewardKey.LuckyCoin);
        }

        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        if (enemyTurnInProgress)
        {
            tookDamageDuringEnemyTurn = true;
        }

        blinkFeedback?.Blink(Color.red, 0.5f, 0.12f);
        cam.Instance?.CamShake(finalDamage);
        if (hitSoundType == DamageSoundType.Default && sourceEnemy != null)
        {
            hitSoundType = sourceEnemy.DamageSoundType;
        }

        SoundManager.Instance?.PlayDamageSound(hitSoundType, transform.position);
        RecalculateItemDrivenStats();
        RefreshHpBar();
        SyncRunStateHealth();

        if (sourceEnemy != null)
        {
            if (finalDamage > 0 && HasItem(ItemRewardKey.CorruptedBlouse) && !corruptedBlouseTriggeredThisCombat)
            {
                corruptedBlouseTriggeredThisCombat = true;
                sourceEnemy.ApplyStatusEffect(CombatStatusType.Poisoned, -1, 1);
                TriggerItemActivationPulse(ItemRewardKey.CorruptedBlouse);
            }

            if (!wasProjectile && HasItem(ItemRewardKey.ThornArmor) && IsEnemyClose(sourceEnemy))
            {
                DealDamageToEnemy(sourceEnemy, 1, false);
                PlayProcFx(CharacterProcFxKey.RetaliationHit, sourceEnemy.EffectAnchor);
                TriggerItemActivationPulse(ItemRewardKey.ThornArmor);
            }

            if (wasProjectile && HasItem(ItemRewardKey.Boomerang))
            {
                DealDamageToEnemy(sourceEnemy, 2, false);
                PlayProcFx(CharacterProcFxKey.RetaliationHit, sourceEnemy.EffectAnchor);
                TriggerItemActivationPulse(ItemRewardKey.Boomerang);
            }
        }

        if (currentHealth <= 0)
        {
            Die();
        }

        if (finalDamage > 0)
        {
            tookDamageSinceLastPlayerTurn = true;
        }

        return finalDamage;
    }

    public void Heal(int amount, ItemRewardKey? sourceItemKey = null, bool playHealFx = true)
    {
        if (amount <= 0 || currentHealth <= 0)
        {
            return;
        }

        int previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        RecalculateItemDrivenStats();
        RefreshHpBar();
        SyncRunStateHealth();
        if (currentHealth > previousHealth)
        {
            if (playHealFx)
            {
                PlayHealProcFx();
            }

            if (sourceItemKey.HasValue)
            {
                TriggerItemActivationPulse(sourceItemKey.Value);
            }
        }
    }

    public void GainMovementPoints(int amount, ItemRewardKey? sourceItemKey = null)
    {
        if (amount <= 0)
        {
            return;
        }

        remainingMovementPoints += amount;
        SoundManager.Instance?.PlayPowerUp(transform.position);
        NotifyMovementPointsChanged();
        PlayProcFx(CharacterProcFxKey.BonusMovement);
        if (sourceItemKey.HasValue)
        {
            TriggerItemActivationPulse(sourceItemKey.Value);
        }
    }

    public void GainWolfMovementPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        wolfMovementPoints += amount;
        remainingMovementPoints += amount;
        NotifyMovementPointsChanged();
    }

    public void ClearWolfMovementPoints()
    {
        if (wolfMovementPoints <= 0)
        {
            return;
        }

        remainingMovementPoints = Mathf.Max(0, remainingMovementPoints - wolfMovementPoints);
        wolfMovementPoints = 0;
        NotifyMovementPointsChanged();
    }

    public void RefreshAbilityState()
    {
        NotifyAbilitiesChanged();
    }

    public int GetUpgradeStacks(AbilityUpgradeKey upgradeKey)
    {
        return runRewardState != null ? runRewardState.GetUpgradeStacks(upgradeKey) : 0;
    }

    public int GetNaturalMaxHealth()
    {
        return baseMaxHealth + (runRewardState != null ? runRewardState.GetBonusMaxHealth() : 0);
    }

    public int GetNaturalBonusDamage()
    {
        return baseBonusDamage + (runRewardState != null ? runRewardState.GetBonusDamage() : 0);
    }

    public int GetNaturalResistance()
    {
        return baseResistance + (runRewardState != null ? runRewardState.GetBonusResistance() : 0);
    }

    public int GetNaturalMovementPointsPerTurn()
    {
        return baseMovementPointsPerTurn + (runRewardState != null ? runRewardState.GetBonusMovementPoints() : 0);
    }

    public int GetDisplayedBasicAttackDamage(bool includeTemporaryModifiers)
    {
        AbilityDefinition basicAttack = GetAbilityForSlot(0)?.Definition;
        if (basicAttack == null)
        {
            return 0;
        }

        int weaponDamage = 0;
        switch (basicAttack)
        {
            case SpinningBladesAbility:
                weaponDamage = 5 + GetUpgradeStacks(AbilityUpgradeKey.SpinningBladesSharpening);
                break;
            case DemonicChainAbility:
                weaponDamage = 4;
                break;
            case ThiefsDaggerAbility:
                weaponDamage = 5 + (2 * GetUpgradeStacks(AbilityUpgradeKey.RoyalDaggerBlessedBlade));
                break;
            case HectorCrossbowAbility:
                weaponDamage = 4;
                break;
            case DemonbaneAbility:
                weaponDamage = 4;
                break;
            case SacredCrossbowAbility:
                weaponDamage = 5;
                break;
            case WhisperfangAbility:
                weaponDamage = 1;
                break;
        }

        int bonus = includeTemporaryModifiers ? BonusDamage : GetNaturalBonusDamage();
        if (includeTemporaryModifiers)
        {
            bonus += nextAttackBonusDamage;
        }

        if (HasItem(ItemRewardKey.IronGauntlets))
        {
            bonus += 1;
        }

        return Mathf.Max(0, weaponDamage + bonus);
    }

    public bool HasItem(ItemRewardKey itemKey)
    {
        return runRewardState != null && runRewardState.HasItem(itemKey);
    }

    public bool TrySpendHealth(int amount)
    {
        if (amount <= 0 || currentHealth <= amount)
        {
            return false;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        tookDamageSinceLastPlayerTurn = true;
        blinkFeedback?.Blink(Color.red, 0.5f, 0.12f);
        cam.Instance?.CamShake(amount);
        RefreshHpBar();
        SyncRunStateHealth();
        return currentHealth > 0;
    }

    public bool IsItemActivationActive(ItemRewardKey itemKey)
    {
        return activeItemStates.TryGetValue(itemKey, out bool isActive) && isActive;
    }

    public void RefundAbilityTurnUse(AbilityDefinition ability, int amount = 1)
    {
        CharacterAbilityRuntime runtime = FindAbilityRuntime(ability);
        runtime?.RefundTurnUse(amount);
        NotifyAbilitiesChanged();
    }

    public void GrantAbilityBonusTurnUse(AbilityDefinition ability, int amount = 1)
    {
        CharacterAbilityRuntime runtime = FindAbilityRuntime(ability);
        runtime?.GrantBonusTurnUse(amount);
        NotifyAbilitiesChanged();
    }

    public void ResetAbilityAvailability(AbilityDefinition ability)
    {
        CharacterAbilityRuntime runtime = FindAbilityRuntime(ability);
        runtime?.ResetAvailability();
        NotifyAbilitiesChanged();
    }

    public bool HasAbilityTargetHitThisTurn(AbilityDefinition ability, Enemy enemy)
    {
        if (ability == null || enemy == null)
        {
            return false;
        }

        return abilityTargetsHitThisTurn.TryGetValue(ability, out HashSet<Enemy> hitTargets)
            && hitTargets != null
            && hitTargets.Contains(enemy);
    }

    public bool MarkAbilityTargetHitThisTurn(AbilityDefinition ability, Enemy enemy)
    {
        if (ability == null || enemy == null)
        {
            return false;
        }

        if (!abilityTargetsHitThisTurn.TryGetValue(ability, out HashSet<Enemy> hitTargets) || hitTargets == null)
        {
            hitTargets = new HashSet<Enemy>();
            abilityTargetsHitThisTurn[ability] = hitTargets;
        }

        return hitTargets.Add(enemy);
    }

    public void HandleEnemyCountChanged(int remainingEnemies)
    {
        RecalculateItemDrivenStats();

        if (remainingEnemies != 1 || GetUpgradeStacks(AbilityUpgradeKey.NighttimeMenaceDeadlyDuel) <= 0)
        {
            return;
        }

        for (int index = 0; index < abilities.Count; index++)
        {
            if (abilities[index].Definition is NighttimeMenaceAbility)
            {
                abilities[index].ResetAvailability();
                NotifyAbilitiesChanged();
                return;
            }
        }
    }

    public void HandlePlayerTurnEnded()
    {
        if (markedEnemy != null
            && markedEnemy.CurrentHealth > 0
            && GetUpgradeStacks(AbilityUpgradeKey.DeathMarkBleeding) > 0
            && markedEnemy.CurrentHealth <= 2)
        {
            PlayProcFx(CharacterProcFxKey.Execute, markedEnemy.EffectAnchor);
            int executeDamage = markedEnemy.CurrentHealth;
            markedEnemy.TakeDamage(executeDamage);
            if (markedEnemy.CurrentHealth <= 0)
            {
                HandleEnemyKilled(markedEnemy);
            }
        }

        if (markedLichSkull != null
            && !markedLichSkull.IsResolving
            && GetUpgradeStacks(AbilityUpgradeKey.DeathMarkBleeding) > 0
            && markedLichSkull.CurrentHealth <= 2)
        {
            PlayProcFx(CharacterProcFxKey.Execute, markedLichSkull.EffectAnchor);
            markedLichSkull.TakeDamage(markedLichSkull.CurrentHealth, DamageSoundType.Default);
            if (markedLichSkull.IsResolving)
            {
                HandleMarkedLichSkullDestroyed(markedLichSkull);
            }
        }

        if (HasItem(ItemRewardKey.CursedPuppet))
        {
            DamageHighestHealthEnemy(1);
        }

        if (HasItem(ItemRewardKey.SacredChalice))
        {
            Heal(1, ItemRewardKey.SacredChalice);
        }

        if (HasItem(ItemRewardKey.SwiftAnklet) && remainingMovementPoints >= 2)
        {
            Heal(1, ItemRewardKey.SwiftAnklet);
        }

        ApplyPoisonTickAtEndOfTurn();
        CommitCurrentTurnStateForNextTurn();
        isFirstPlayerTurnOfArena = false;
        ClearDeathMark();
        ClearNextAttackBonusDamage();
        ClearBonusDamageUntilEndOfTurn();
        samuraiMaskBonusDamage = 0;
        RecalculateItemDrivenStats();
    }

    public void CommitCurrentTurnStateForNextTurn()
    {
        attackedLastTurn = attackedThisTurn;
        movedLastTurn = movedThisTurn;

        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            runtime?.Definition?.OnTurnEnded(this, runtime);
        }
    }

    public void BeginEnemyTurn()
    {
        enemyTurnInProgress = true;
        tookDamageDuringEnemyTurn = false;
        RecalculateItemDrivenStats();
    }

    public void HandleEnemyTurnEnded()
    {
        enemyTurnInProgress = false;
        isFirstEnemyTurnOfArena = false;
        if (HasItem(ItemRewardKey.Sakura) && tookDamageDuringEnemyTurn)
        {
            Heal(1, ItemRewardKey.Sakura);
        }

        if (HasItem(ItemRewardKey.GuardMedal) && !tookDamageDuringEnemyTurn)
        {
            Heal(1, ItemRewardKey.GuardMedal);
        }

        RecalculateItemDrivenStats();
    }

    public void HandleCombatStarted()
    {
        if (!HasItem(ItemRewardKey.FortuneDagger) || currentHealth <= 0)
        {
            return;
        }

        int appliedDamage = TakeDamage(3);
        if (appliedDamage > 0)
        {
            PlayFeedbackFx(fxFortuneDaggerCombatStartPrefab, transform, destroyAfterSeconds: 2f);
        }
    }

    public void AddNextAttackBonusDamage(int amount, GameObject auraFxPrefab = null)
    {
        if (amount <= 0)
        {
            return;
        }

        nextAttackBonusDamage += amount;
        if (auraFxPrefab != null)
        {
            nextAttackBonusAuraPrefab = auraFxPrefab;
        }

        RefreshNextAttackBonusAura();
        RecalculateItemDrivenStats();
        NotifyAbilitiesChanged();
    }

    public void AddBonusDamageUntilEndOfTurn(
        int amount,
        GameObject boostFxPrefab = null,
        GameObject auraFxPrefab = null,
        float boostFxDuration = 1f)
    {
        if (amount <= 0)
        {
            return;
        }

        bonusDamageUntilEndOfTurn += amount;
        if (auraFxPrefab != null)
        {
            temporaryDamageAuraPrefab = auraFxPrefab;
        }

        if (boostFxPrefab != null)
        {
            PlayFeedbackFx(boostFxPrefab, destroyAfterSeconds: boostFxDuration);
        }

        RefreshTemporaryDamageAura();
        RecalculateItemDrivenStats();
        NotifyAbilitiesChanged();
    }

    public void AddBonusDamageUntilNextMovement(
        int amount,
        GameObject boostFxPrefab = null,
        GameObject auraFxPrefab = null,
        float boostFxDuration = 1f)
    {
        if (amount <= 0)
        {
            return;
        }

        bonusDamageUntilNextMovement += amount;
        if (auraFxPrefab != null)
        {
            temporaryDamageAuraPrefab = auraFxPrefab;
        }

        if (boostFxPrefab != null)
        {
            PlayFeedbackFx(boostFxPrefab, destroyAfterSeconds: boostFxDuration);
        }

        RefreshTemporaryDamageAura();
        RecalculateItemDrivenStats();
        NotifyAbilitiesChanged();
    }

    public void ApplyPoisoned()
    {
        if (isPoisoned)
        {
            return;
        }

        isPoisoned = true;
        PlayStatusApplyFx(CombatStatusType.Poisoned);
        RefreshPoisonFx();
    }

    public void ClearPoisoned(bool playHealFx = true)
    {
        bool wasPoisoned = isPoisoned;
        isPoisoned = false;
        ClearPersistentStatusFx(CombatStatusType.Poisoned);
        if (wasPoisoned && playHealFx)
        {
            PlayHealProcFx();
        }
    }

    public void ApplyDeathMark(Enemy enemy, DeathMarkAbility sourceAbility)
    {
        markedLichSkull = null;
        if (markedEnemy != null && markedEnemy != enemy)
        {
            markedEnemy.SetDeathMarkActive(false);
        }

        markedEnemy = enemy;
        deathMarkAbility = sourceAbility;
        markedEnemy?.SetDeathMarkActive(true);
    }

    public void ApplyDeathMark(LichSkullObject skullObject, DeathMarkAbility sourceAbility)
    {
        if (markedEnemy != null)
        {
            markedEnemy.SetDeathMarkActive(false);
            markedEnemy = null;
        }

        markedLichSkull = skullObject;
        deathMarkAbility = sourceAbility;
    }

    public int DamageEnemiesAround(
        Vector2Int centerCell,
        int range,
        int damage,
        bool includeDiagonals = true,
        AbilityDefinition sourceAbility = null)
    {
        if (Board == null || range <= 0 || damage <= 0)
        {
            return 0;
        }

        int hits = 0;
        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
        HashSet<LichSkullObject> hitLichSkulls = new HashSet<LichSkullObject>();
        for (int offsetX = -range; offsetX <= range; offsetX++)
        {
            for (int offsetY = -range; offsetY <= range; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                if (!includeDiagonals && offsetX != 0 && offsetY != 0)
                {
                    continue;
                }

                if (Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)) > range)
                {
                    continue;
                }

                Vector2Int targetCell = centerCell + new Vector2Int(offsetX, offsetY);
                if (Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null && hitEnemies.Add(enemy))
                {
                    DealDamageToEnemy(enemy, damage, false, true, DamageSoundType.Default, sourceAbility);
                    hits++;
                }
                else if (Board.TryGetLichSkullObject(targetCell, out LichSkullObject lichSkull)
                    && lichSkull != null
                    && hitLichSkulls.Add(lichSkull))
                {
                    DealDamageToLichSkull(lichSkull, damage, false, DamageSoundType.Default, sourceAbility);
                    hits++;
                }
                else if (Board.TryGetBarrel(targetCell, out BarrelObstacle barrel) && barrel != null)
                {
                    barrel.TakeHit();
                    hits++;
                }
            }
        }

        return hits;
    }

    public bool DealDamageToEnemyLikeTargetAtCell(
        Vector2Int targetCell,
        int baseDamage,
        bool addBonusDamage,
        DamageSoundType hitSoundType = DamageSoundType.Default,
        AbilityDefinition sourceAbility = null)
    {
        if (Board == null)
        {
            return false;
        }

        if (Board.TryGetEnemyLikeTarget(targetCell, out Enemy enemy, out LichSkullObject lichSkull))
        {
            if (enemy != null)
            {
                DealDamageToEnemy(enemy, baseDamage, addBonusDamage, true, hitSoundType, sourceAbility);
                return true;
            }

            if (lichSkull != null)
            {
                DealDamageToLichSkull(lichSkull, baseDamage, addBonusDamage, hitSoundType, sourceAbility);
                return true;
            }
        }

        return false;
    }

    public void DealDamageToBarrelWithAbilityTiming(AbilityDefinition sourceAbility, BarrelObstacle barrel)
    {
        if (sourceAbility == null || barrel == null || Board == null)
        {
            return;
        }

        float delay = sourceAbility.GetDamageApplyDelay(gridPosition, barrel.GridPosition);
        if (delay <= 0f)
        {
            barrel.TakeHit();
            return;
        }

        StartCoroutine(DamageBarrelAfterDelay(barrel, delay));
    }

    public int DealDamageToLichSkull(
        LichSkullObject skullObject,
        int baseDamage,
        bool addBonusDamage,
        DamageSoundType hitSoundType = DamageSoundType.Default,
        AbilityDefinition sourceAbility = null)
    {
        if (skullObject == null)
        {
            return 0;
        }

        RecalculateItemDrivenStats();
        int nextWeaponAbilityBonusDamage = GetNextWeaponAbilityBonusDamage(sourceAbility);
        int totalDamage = Mathf.Max(1, baseDamage + (addBonusDamage ? bonusDamage : 0));
        totalDamage += nextWeaponAbilityBonusDamage;
        totalDamage += GetAbilityItemBonusDamage(sourceAbility);

        int appliedDamage = skullObject.TakeDamage(totalDamage, hitSoundType);
        if (appliedDamage > 0 && sourceAbility != null)
        {
            PlayAbilityDamageFx(sourceAbility, skullObject.EffectAnchor);
        }

        if (skullObject == markedLichSkull && !skullObject.IsResolving)
        {
            int bonusMarkDamage = skullObject.TakeDamage(1, DamageSoundType.Default);
            appliedDamage += bonusMarkDamage;
        }

        if (skullObject.IsResolving)
        {
            HandleMarkedLichSkullDestroyed(skullObject);
        }

        if (nextWeaponAbilityBonusDamage > 0)
        {
            ConsumeNextAttackBonusDamage();
        }

        return appliedDamage;
    }

    public void DealDamageToLichSkullWithAbilityTiming(
        AbilityDefinition abilityDefinition,
        LichSkullObject skullObject,
        int baseDamage,
        bool addBonusDamage,
        DamageSoundType hitSoundType = DamageSoundType.Default,
        AbilityDefinition sourceAbility = null)
    {
        if (skullObject == null)
        {
            return;
        }

        Vector2Int targetGridPosition = skullObject.GridPosition;
        float delay = abilityDefinition != null
            ? abilityDefinition.GetDamageApplyDelay(gridPosition, targetGridPosition)
            : 0f;

        if (delay <= 0f)
        {
            DealDamageToLichSkull(skullObject, baseDamage, addBonusDamage, hitSoundType, sourceAbility);
            return;
        }

        StartCoroutine(DamageLichSkullAfterDelay(skullObject, baseDamage, addBonusDamage, delay, hitSoundType, sourceAbility));
    }

    public void PlayAttackAnimation(AnimationClip attackAnimationClip)
    {
        if (attackAnimationClip == null)
        {
            return;
        }

        CacheAnimator();
        if (characterAnimator == null)
        {
            return;
        }

        EnsureAnimatorOverrideController();
        if (animatorOverrideController == null || attackPlaceholderClip == null)
        {
            return;
        }

        animatorOverrideController[attackPlaceholderClip] = attackAnimationClip;
        characterAnimator.ResetTrigger(attackTriggerParameter);
        characterAnimator.SetTrigger(attackTriggerParameter);
    }

    public void PlayAbilityFx(IReadOnlyList<AbilityFxSpawnConfig> fxConfigs, IEnumerable<Enemy> hitTargets = null)
    {
        if (fxConfigs == null || fxConfigs.Count == 0)
        {
            return;
        }

        List<Enemy> uniqueTargets = null;
        if (hitTargets != null)
        {
            uniqueTargets = new List<Enemy>();
            HashSet<Enemy> seenTargets = new HashSet<Enemy>();
            foreach (Enemy target in hitTargets)
            {
                if (target == null || !seenTargets.Add(target))
                {
                    continue;
                }

                uniqueTargets.Add(target);
            }
        }

        for (int index = 0; index < fxConfigs.Count; index++)
        {
            AbilityFxSpawnConfig fxConfig = fxConfigs[index];
            if (fxConfig == null || fxConfig.FxPrefab == null)
            {
                continue;
            }

            if (fxConfig.SpawnAnchor == AbilityFxSpawnAnchor.Caster)
            {
                StartCoroutine(SpawnAbilityFxRoutine(fxConfig, transform, null));
                continue;
            }

            if (uniqueTargets == null)
            {
                continue;
            }

            for (int targetIndex = 0; targetIndex < uniqueTargets.Count; targetIndex++)
            {
                Enemy target = uniqueTargets[targetIndex];
                if (target == null)
                {
                    continue;
                }

                StartCoroutine(SpawnAbilityFxRoutine(fxConfig, target.transform, target));
            }
        }
    }

    public void PlaySecondaryEffectFx(IReadOnlyList<SecondaryEffectFxSpawnConfig> fxConfigs, AbilityExecutionContext context)
    {
        if (fxConfigs == null || fxConfigs.Count == 0 || Board == null)
        {
            return;
        }

        for (int index = 0; index < fxConfigs.Count; index++)
        {
            SecondaryEffectFxSpawnConfig fxConfig = fxConfigs[index];
            if (fxConfig == null || fxConfig.FxPrefab == null)
            {
                continue;
            }

            StartCoroutine(SpawnSecondaryEffectFxRoutine(fxConfig, context));
        }
    }

    public void PlayTemporaryTrailColor(Color trailColor, float duration)
    {
        CacheTrail();
        if (runtimeTrailMaterial == null)
        {
            return;
        }

        if (temporaryTrailColorCoroutine != null)
        {
            StopCoroutine(temporaryTrailColorCoroutine);
        }

        runtimeTrailMaterial.color = trailColor;
        if (duration > 0f)
        {
            temporaryTrailColorCoroutine = StartCoroutine(ResetTemporaryTrailColorAfterDelay(duration));
        }
    }

    public void SetTrailColorOverride(AbilityDefinition owner, Color trailColor)
    {
        CacheTrail();
        if (runtimeTrailMaterial == null)
        {
            return;
        }

        activeTrailColorOwner = owner;
        if (temporaryTrailColorCoroutine != null)
        {
            StopCoroutine(temporaryTrailColorCoroutine);
            temporaryTrailColorCoroutine = null;
        }

        runtimeTrailMaterial.color = trailColor;
    }

    public void ClearTrailColorOverride(AbilityDefinition owner)
    {
        CacheTrail();
        if (runtimeTrailMaterial == null || activeTrailColorOwner != owner)
        {
            return;
        }

        activeTrailColorOwner = null;
        runtimeTrailMaterial.color = defaultTrailMaterialColor;
    }

    public void PlayTemporaryTrailReplacement(GameObject replacementTrailPrefab, float duration)
    {
        CacheTrail();
        if (defaultTrailObject == null || replacementTrailPrefab == null)
        {
            return;
        }

        if (temporaryTrailReplacementCoroutine != null)
        {
            StopCoroutine(temporaryTrailReplacementCoroutine);
        }

        ClearActiveReplacementTrailInstance();
        CreateReplacementTrailInstance(replacementTrailPrefab);
        defaultTrailObject.SetActive(false);

        if (duration > 0f)
        {
            temporaryTrailReplacementCoroutine = StartCoroutine(ResetTemporaryTrailReplacementAfterDelay(duration));
        }
    }

    public void SetTrailReplacementOverride(AbilityDefinition owner, GameObject replacementTrailPrefab)
    {
        CacheTrail();
        if (defaultTrailObject == null || replacementTrailPrefab == null)
        {
            return;
        }

        activeTrailReplacementOwner = owner;
        if (temporaryTrailReplacementCoroutine != null)
        {
            StopCoroutine(temporaryTrailReplacementCoroutine);
            temporaryTrailReplacementCoroutine = null;
        }

        ClearActiveReplacementTrailInstance();
        CreateReplacementTrailInstance(replacementTrailPrefab);
        defaultTrailObject.SetActive(false);
    }

    public void ClearTrailReplacementOverride(AbilityDefinition owner)
    {
        CacheTrail();
        if (defaultTrailObject == null || activeTrailReplacementOwner != owner)
        {
            return;
        }

        activeTrailReplacementOwner = null;
        ClearActiveReplacementTrailInstance();
        defaultTrailObject.SetActive(true);
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
        AnimateToGrid(moveDuration);
    }

    private void AnimateToGrid(float duration)
    {
        if (Board == null)
        {
            return;
        }

        moveTween?.Kill();
        IsMoving = true;
        SetDashingAnimation(true);
        SoundManager.Instance?.PlayDash(transform.position);
        Vector3 targetPosition = Board.GridToWorldPosition(gridPosition) + Vector3.up * spawnHeight;
        moveTween = transform.DOMove(targetPosition, Mathf.Max(0.01f, duration))
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                IsMoving = false;
                moveTween = null;
                if (resetBodyRotationAfterSlide)
                {
                    ResetBodyRotation();
                }
                SetDashingAnimation(false);
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
        bodyRotationTween?.Kill();
        impactTween?.Kill();
        IsMoving = false;
        actionLockCount = 0;
        ClearNextAttackBonusAura();
        SnapBodyRotationToDefault();
        SetDashingAnimation(false);
        if (temporaryTrailColorCoroutine != null)
        {
            StopCoroutine(temporaryTrailColorCoroutine);
            temporaryTrailColorCoroutine = null;
        }

        if (temporaryTrailReplacementCoroutine != null)
        {
            StopCoroutine(temporaryTrailReplacementCoroutine);
            temporaryTrailReplacementCoroutine = null;
        }

        if (runtimeTrailMaterial != null)
        {
            runtimeTrailMaterial.color = defaultTrailMaterialColor;
        }

        activeTrailColorOwner = null;
        activeTrailReplacementOwner = null;
        ClearActiveReplacementTrailInstance();
        ClearTemporaryDamageAura();
        ClearPersistentStatusFx(CombatStatusType.Poisoned);
        if (defaultTrailObject != null)
        {
            defaultTrailObject.SetActive(true);
        }
    }

    private void CacheHpBar()
    {
        if (canvasUnitUI != null)
        {
            return;
        }

        canvasUnitUI = GetComponentInChildren<CanvasUnitUI>(true);
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

    private void InitializeAbilities(IReadOnlyList<AbilityDefinition> abilityDefinitions = null)
    {
        abilities.Clear();

        IReadOnlyList<AbilityDefinition> sourceAbilities = abilityDefinitions ?? GetStartingAbilityDefinitions();
        for (int index = 0; index < sourceAbilities.Count; index++)
        {
            AbilityDefinition definition = sourceAbilities[index];
            if (definition == null)
            {
                continue;
            }

            CharacterAbilityRuntime runtime = new CharacterAbilityRuntime(definition);
            runtime.ResetCombat();
            abilities.Add(runtime);
        }

        RefreshBasicAttackVisuals();
    }

    private bool CanTraverseUnits()
    {
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            if (runtime.Definition != null && runtime.Definition.AllowsUnitTraversal(this, runtime))
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldLimitNextSlideToOneCell()
    {
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            if (runtime.Definition != null && runtime.Definition.LimitsNextSlideToOneCell(this, runtime))
            {
                return true;
            }
        }

        return false;
    }

    private Vector2Int GetSingleStepDestination(Vector2Int direction)
    {
        Vector2Int targetCell = gridPosition + direction;
        if (!Board.IsCellWalkable(targetCell))
        {
            return gridPosition;
        }

        return targetCell;
    }

    private void ConsumeSingleStepModifiers()
    {
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            if (runtime.Definition == null
                || !runtime.Definition.LimitsNextSlideToOneCell(this, runtime)
                || !runtime.Definition.ConsumeSingleStepModifierAfterMovement(this, runtime))
            {
                continue;
            }

            runtime.ConsumePreparedActivation();
            runtime.Deactivate(this);
        }

        NotifyAbilitiesChanged();
    }

    private void ApplyTraversalEffects()
    {
        if (traversedEnemiesBuffer.Count == 0)
        {
            return;
        }

        int traversalDamage = 0;
        List<CharacterAbilityRuntime> traversalAbilities = new List<CharacterAbilityRuntime>();
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            if (runtime.Definition == null)
            {
                continue;
            }

            int runtimeTraversalDamage = runtime.Definition.GetTraversalDamage(this, runtime, traversedEnemiesBuffer.Count);
            if (runtimeTraversalDamage <= 0)
            {
                continue;
            }

            traversalDamage = Mathf.Max(traversalDamage, runtimeTraversalDamage);
            traversalAbilities.Add(runtime);
        }

        if (traversalDamage <= 0)
        {
            return;
        }

        HashSet<Enemy> damagedEnemies = new HashSet<Enemy>();
        for (int index = 0; index < traversedEnemiesBuffer.Count; index++)
        {
            Enemy enemy = traversedEnemiesBuffer[index];
            if (enemy == null || !damagedEnemies.Add(enemy))
            {
                continue;
            }

            AbilityDefinition traversalSourceAbility = traversalAbilities.Count > 0 ? traversalAbilities[0].Definition : null;
            DealDamageToEnemy(enemy, traversalDamage, false, true, DamageSoundType.Default, traversalSourceAbility);
        }

        if (damagedEnemies.Count == 0)
        {
            return;
        }

        for (int index = 0; index < traversalAbilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = traversalAbilities[index];
            runtime.Definition?.PlayTraversalFx(this, damagedEnemies);
        }

        if (damagedEnemies.Count > 0)
        {
            float refundChance = 0.10f * GetUpgradeStacks(AbilityUpgradeKey.GhostStepsAdrenaline);
            if (refundChance > 0f && UnityEngine.Random.value < refundChance)
            {
                GainMovementPoints(1);
            }
        }
    }

    private IEnumerator SpawnAbilityFxRoutine(AbilityFxSpawnConfig fxConfig, Transform anchor, Enemy target)
    {
        if (fxConfig == null || fxConfig.FxPrefab == null || anchor == null)
        {
            yield break;
        }

        if (fxConfig.SpawnDelay > 0f)
        {
            yield return new WaitForSeconds(fxConfig.SpawnDelay);
        }

        if (anchor == null)
        {
            yield break;
        }

        Quaternion referenceRotation = GetAbilityFxReferenceRotation(fxConfig, target);
        Vector3 spawnPosition = anchor.position + referenceRotation * fxConfig.PositionOffset;
        Quaternion defaultFxRotation = fxConfig.FxPrefab.transform.rotation;
        Vector3 defaultFxScale = fxConfig.FxPrefab.transform.localScale;
        GameObject spawnedFx = Instantiate(fxConfig.FxPrefab, spawnPosition, defaultFxRotation);

        if (fxConfig.ParentToAnchor && anchor != null)
        {
            spawnedFx.transform.SetParent(anchor, true);
        }

        spawnedFx.transform.rotation = defaultFxRotation;
        spawnedFx.transform.localScale = defaultFxScale;

        if (fxConfig.DestroyAfterSeconds > 0f)
        {
            Destroy(spawnedFx, fxConfig.DestroyAfterSeconds);
        }
    }

    private IEnumerator DealDamageToEnemyAfterDelay(
        Enemy enemy,
        int baseDamage,
        bool addBonusDamage,
        bool isAbilityDamage,
        DamageSoundType hitSoundType,
        float delay,
        AbilityDefinition sourceAbility,
        Action<Enemy, int> onDamageApplied)
    {
        yield return new WaitForSeconds(delay);

        if (enemy == null || enemy.CurrentHealth <= 0)
        {
            yield break;
        }

        int appliedDamage = DealDamageToEnemy(enemy, baseDamage, addBonusDamage, isAbilityDamage, hitSoundType, sourceAbility);
        onDamageApplied?.Invoke(enemy, appliedDamage);
    }

    private IEnumerator DamageBarrelAfterDelay(BarrelObstacle barrel, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (barrel != null && !barrel.IsDestroyed)
        {
            barrel.TakeHit();
        }
    }

    private IEnumerator DamageLichSkullAfterDelay(
        LichSkullObject skullObject,
        int baseDamage,
        bool addBonusDamage,
        float delay,
        DamageSoundType hitSoundType,
        AbilityDefinition sourceAbility)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (skullObject != null && !skullObject.IsResolving)
        {
            DealDamageToLichSkull(skullObject, baseDamage, addBonusDamage, hitSoundType, sourceAbility);
        }
    }

    private IEnumerator SpawnSecondaryEffectFxRoutine(SecondaryEffectFxSpawnConfig fxConfig, AbilityExecutionContext context)
    {
        if (fxConfig == null || fxConfig.FxPrefab == null || Board == null)
        {
            yield break;
        }

        if (fxConfig.SpawnDelay > 0f)
        {
            yield return new WaitForSeconds(fxConfig.SpawnDelay);
        }

        Vector2Int anchorCell = ResolveSecondaryEffectAnchorCell(context, fxConfig.SpawnAnchor);
        Vector3 anchorWorldPosition = Board.GridToWorldPosition(anchorCell) + Vector3.up * spawnHeight;
        Quaternion referenceRotation = fxConfig.OffsetReference == SecondaryEffectOffsetReference.CharacterRotation
            ? transform.rotation
            : Quaternion.identity;
        Vector3 spawnPosition = anchorWorldPosition + referenceRotation * fxConfig.PositionOffset;
        Quaternion defaultFxRotation = fxConfig.FxPrefab.transform.rotation;
        Vector3 defaultFxScale = fxConfig.FxPrefab.transform.localScale;
        GameObject spawnedFx = Instantiate(fxConfig.FxPrefab, spawnPosition, defaultFxRotation);
        spawnedFx.transform.rotation = defaultFxRotation;
        spawnedFx.transform.localScale = defaultFxScale;

        if (fxConfig.DestroyAfterSeconds > 0f)
        {
            Destroy(spawnedFx, fxConfig.DestroyAfterSeconds);
        }
    }

    private Quaternion GetAbilityFxReferenceRotation(AbilityFxSpawnConfig fxConfig, Enemy target)
    {
        switch (fxConfig.OffsetReference)
        {
            case AbilityFxOffsetReference.CasterRotation:
                return transform.rotation;
            case AbilityFxOffsetReference.TargetRotation:
                return target != null ? target.transform.rotation : transform.rotation;
            default:
                return Quaternion.identity;
        }
    }

    private Vector2Int ResolveSecondaryEffectAnchorCell(AbilityExecutionContext context, SecondaryEffectAnchor anchor)
    {
        switch (anchor)
        {
            case SecondaryEffectAnchor.OriginCell:
                return context.OriginCell;
            case SecondaryEffectAnchor.TargetCell:
                return context.TargetCell;
            default:
                return GridPosition;
        }
    }

    private void NotifyMovementPointsChanged()
    {
        MovementPointsChanged?.Invoke(this);
    }

    private bool ConsumeMovementPointInternal(bool allowBloodPayment = false)
    {
        if (remainingMovementPoints <= 0)
        {
            if (!allowBloodPayment || !HasItem(ItemRewardKey.IronSpikedSandals) || !TrySpendHealth(2))
            {
                return false;
            }

            TriggerItemActivationPulse(ItemRewardKey.IronSpikedSandals);
            NotifyMovementPointsChanged();
            return true;
        }

        remainingMovementPoints--;
        if (wolfMovementPoints > 0)
        {
            wolfMovementPoints = Mathf.Max(0, wolfMovementPoints - 1);
        }

        NotifyMovementPointsChanged();
        return true;
    }

    private bool CanSpendHealthForExtraMovement()
    {
        return HasItem(ItemRewardKey.IronSpikedSandals) && currentHealth > 2;
    }

    private bool HaveAllAbilitiesBeenSpentForTurn()
    {
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            if (runtime != null && runtime.IsUsable(this))
            {
                return false;
            }
        }

        return abilities.Count > 0;
    }

    private void TriggerAdjacentDeathExplosion(Enemy killedEnemy, Vector2Int centerCell)
    {
        if (Board == null)
        {
            return;
        }

        bool hitAnyEnemy = false;
        for (int offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                Vector2Int targetCell = centerCell + new Vector2Int(offsetX, offsetY);
                if (!Board.TryGetEnemy(targetCell, out Enemy enemy) || enemy == null || enemy == killedEnemy)
                {
                    continue;
                }

                int appliedDamage = DealDamageToEnemy(enemy, 1, false);
                hitAnyEnemy |= appliedDamage > 0;
            }
        }

        if (hitAnyEnemy)
        {
            TriggerItemActivationPulse(ItemRewardKey.ScrapBomb);
        }
    }

    private void NotifyMoved(Vector2Int previousCell, Vector2Int currentCell)
    {
        if (previousCell == currentCell)
        {
            return;
        }

        movedThisTurn = true;
        Moved?.Invoke(this, previousCell, currentCell);
    }

    private void NotifyAbilitiesChanged()
    {
        RefreshBasicAttackVisuals();
        AbilitiesChanged?.Invoke(this);
    }

    private void RefreshBasicAttackVisuals()
    {
        AbilityDefinition equippedBasicAttack = GetAbilityForSlot(0)?.Definition;
        for (int index = 0; index < basicAttackVisuals.Count; index++)
        {
            CharacterBasicAttackVisualConfig config = basicAttackVisuals[index];
            if (config == null || config.VisualRoots == null)
            {
                continue;
            }

            bool shouldBeVisible = equippedBasicAttack != null
                && config.BasicAttackAbility != null
                && config.BasicAttackAbility == equippedBasicAttack;

            for (int visualIndex = 0; visualIndex < config.VisualRoots.Count; visualIndex++)
            {
                GameObject visualRoot = config.VisualRoots[visualIndex];
                if (visualRoot != null)
                {
                    visualRoot.SetActive(shouldBeVisible);
                }
            }
        }
    }

    private void CacheAnimator()
    {
        if (characterAnimator == null)
        {
            characterAnimator = GetComponentInChildren<Animator>(true);
        }
    }

    private void CacheBaseStatsIfNeeded()
    {
        ApplyCharacterDataDefinition();
        if (baseStatsCached)
        {
            return;
        }

        baseMaxHealth = maxHealth;
        baseBonusDamage = bonusDamage;
        baseResistance = resistance;
        baseMovementPointsPerTurn = movementPointsPerTurn;
        baseStatsCached = true;
    }

    private void ApplyCharacterDataDefinition()
    {
        if (characterData == null)
        {
            return;
        }

        maxHealth = characterData.MaxHealth;
        bonusDamage = characterData.BonusDamage;
        resistance = characterData.Resistance;
        movementPointsPerTurn = characterData.MovementPointsPerTurn;
        if (baseStatsCached)
        {
            baseMaxHealth = maxHealth;
            baseBonusDamage = bonusDamage;
            baseResistance = resistance;
            baseMovementPointsPerTurn = movementPointsPerTurn;
        }
    }

    private IReadOnlyList<AbilityDefinition> GetStartingAbilityDefinitions()
    {
        return characterData != null ? characterData.StartingAbilities : Array.Empty<AbilityDefinition>();
    }

    private void CacheBody()
    {
        if (characterBody == null)
        {
            characterBody = transform.Find("Pandora");
        }

        if (characterBody == null)
        {
            characterBody = transform.Find("Pandora_Body");
        }

        if (characterBody == null && transform.childCount > 0)
        {
            characterBody = transform.GetChild(0);
        }
    }

    private void NotifyAbilitiesCharacterMoved(Vector2Int previousCell, Vector2Int currentCell, bool consumedMovementPoint)
    {
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            runtime?.Definition?.OnCharacterMoved(this, runtime, previousCell, currentCell, consumedMovementPoint);
        }

        NotifyAbilitiesChanged();
    }

    private void CacheTrail()
    {
        if (characterTrail == null)
        {
            characterTrail = GetComponentInChildren<TrailRenderer>(true);
        }

        if (characterTrail == null)
        {
            return;
        }

        defaultTrailObject = characterTrail.gameObject;
        if (runtimeTrailMaterial == null)
        {
            runtimeTrailMaterial = characterTrail.material;
            if (runtimeTrailMaterial != null)
            {
                defaultTrailMaterialColor = runtimeTrailMaterial.color;
            }
        }
    }

    private void EnsureAnimatorOverrideController()
    {
        if (characterAnimator == null)
        {
            return;
        }

        if (animatorOverrideController == null)
        {
            RuntimeAnimatorController currentController = characterAnimator.runtimeAnimatorController;
            if (currentController == null)
            {
                return;
            }

            if (currentController is AnimatorOverrideController existingOverrideController)
            {
                animatorOverrideController = existingOverrideController;
            }
            else
            {
                animatorOverrideController = new AnimatorOverrideController(currentController);
                characterAnimator.runtimeAnimatorController = animatorOverrideController;
            }
        }

        if (attackPlaceholderClip != null)
        {
            return;
        }

        AnimationClip[] animationClips = animatorOverrideController.animationClips;
        for (int index = 0; index < animationClips.Length; index++)
        {
            AnimationClip clip = animationClips[index];
            if (clip == null)
            {
                continue;
            }

            if (clip.name == attackPlaceholderClipName)
            {
                attackPlaceholderClip = clip;
                return;
            }
        }

        for (int index = 0; index < animationClips.Length; index++)
        {
            AnimationClip clip = animationClips[index];
            if (clip != null && clip.name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                attackPlaceholderClip = clip;
                return;
            }
        }
    }

    private void SetDashingAnimation(bool isDashing)
    {
        CacheAnimator();
        if (characterAnimator == null || string.IsNullOrWhiteSpace(dashingBoolParameter))
        {
            return;
        }

        characterAnimator.SetBool(dashingBoolParameter, isDashing);
    }

    private void FaceDashDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
        {
            return;
        }

        Vector3 worldDirection = new Vector3(direction.x, 0f, -direction.y);
        worldDirection = Board != null ? Board.transform.TransformDirection(worldDirection) : worldDirection;
        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        CacheBody();
        Transform targetBody = characterBody != null ? characterBody : transform;
        Quaternion targetRotation = Quaternion.LookRotation((-worldDirection).normalized, Vector3.up);

        bodyRotationTween?.Kill();
        bodyRotationTween = targetBody.DORotateQuaternion(targetRotation, dashBodyRotateDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => bodyRotationTween = null);
    }

    public void FaceTargetCell(Vector2Int targetCell)
    {
        Vector2Int direction = targetCell - gridPosition;
        if (direction == Vector2Int.zero)
        {
            return;
        }

        FaceDashDirection(new Vector2Int(
            Mathf.Clamp(direction.x, -1, 1),
            Mathf.Clamp(direction.y, -1, 1)));
    }

    private void ResetBodyRotation()
    {
        CacheBody();
        Transform targetBody = characterBody != null ? characterBody : transform;
        if (targetBody == null)
        {
            return;
        }

        bodyRotationTween?.Kill();
        bodyRotationTween = targetBody.DOLocalRotateQuaternion(defaultBodyLocalRotation, dashBodyResetDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => bodyRotationTween = null);
    }

    private void SnapBodyRotationToDefault()
    {
        CacheBody();
        Transform targetBody = characterBody != null ? characterBody : transform;
        if (targetBody == null)
        {
            return;
        }

        targetBody.localRotation = defaultBodyLocalRotation;
    }

    private CharacterAbilityRuntime FindAbilityRuntime(AbilityDefinition ability)
    {
        if (ability == null)
        {
            return null;
        }

        for (int index = 0; index < abilities.Count; index++)
        {
            if (abilities[index].Definition == ability)
            {
                return abilities[index];
            }
        }

        return null;
    }

    private void HandleEnemyKilled(Enemy enemy)
    {
        Vector2Int enemyCell = enemy != null ? enemy.GridPosition : gridPosition;

        if (HasItem(ItemRewardKey.BloodAmulet))
        {
            Heal(1, ItemRewardKey.BloodAmulet);
        }

        if (HasItem(ItemRewardKey.RavenFeather))
        {
            GainMovementPoints(1, ItemRewardKey.RavenFeather);
        }

        if (HasItem(ItemRewardKey.SamuraiMask))
        {
            samuraiMaskBonusDamage += 2;
            TriggerItemActivationPulse(ItemRewardKey.SamuraiMask);
            RecalculateItemDrivenStats();
        }

        if (enemy != null && enemy.SpecialBehavior == EnemySpecialBehavior.SnakePoisonOpener)
        {
            ClearPoisoned();
        }

        if (enemy != null && enemy == markedEnemy)
        {
            if (GetUpgradeStacks(AbilityUpgradeKey.DeathMarkHuntingBonus) > 0)
            {
                Heal(2);
            }

            if (GetUpgradeStacks(AbilityUpgradeKey.DeathMarkCycleOfDeath) > 0 && deathMarkAbility != null)
            {
                ResetAbilityAvailability(deathMarkAbility);
            }

            ClearDeathMark();
        }

        if (HasItem(ItemRewardKey.ScrapBomb))
        {
            TriggerAdjacentDeathExplosion(enemy, enemyCell);
        }

        if (Board != null && Board.SpawnedEnemies.Count == 1)
        {
            HandleEnemyCountChanged(1);
        }
    }

    private void HandleMarkedLichSkullDestroyed(LichSkullObject skullObject)
    {
        if (skullObject == null || skullObject != markedLichSkull)
        {
            return;
        }

        if (GetUpgradeStacks(AbilityUpgradeKey.DeathMarkHuntingBonus) > 0)
        {
            Heal(2);
        }

        if (GetUpgradeStacks(AbilityUpgradeKey.DeathMarkCycleOfDeath) > 0 && deathMarkAbility != null)
        {
            ResetAbilityAvailability(deathMarkAbility);
        }

        ClearDeathMark();
    }

    private bool IsEnemyClose(Enemy enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        return Mathf.Max(
                   Mathf.Abs(enemy.GridPosition.x - gridPosition.x),
                   Mathf.Abs(enemy.GridPosition.y - gridPosition.y))
               <= 1;
    }

    private void SyncRunStateHealth()
    {
        runRewardState?.SetCurrentHealth(currentHealth);
    }

    private void HandleAbilityDamageSideEffects(Enemy enemy)
    {
        if (enemy == null || !HasItem(ItemRewardKey.FrostCharm) || !frostCharmedEnemiesThisTurn.Add(enemy))
        {
            return;
        }

        enemy.ApplyStatusEffect(CombatStatusType.Frozen, 1, 1);
        enemy.ApplyMobilityPenaltyNextTurn(1);
        TriggerItemActivationPulse(ItemRewardKey.FrostCharm);
    }

    private int GetAbilityItemBonusDamage(AbilityDefinition sourceAbility)
    {
        if (sourceAbility == null)
        {
            return 0;
        }

        attackedThisTurn = true;

        if (HasItem(ItemRewardKey.PumpkinHead) && sourceAbility.Category != AbilityCategory.BasicAttack)
        {
            TriggerItemActivationPulse(ItemRewardKey.PumpkinHead);
            return 1;
        }

        if (HasItem(ItemRewardKey.IronGauntlets) && sourceAbility.Category == AbilityCategory.BasicAttack)
        {
            TriggerItemActivationPulse(ItemRewardKey.IronGauntlets);
            return 1;
        }

        return 0;
    }

    private int GetNextWeaponAbilityBonusDamage(AbilityDefinition sourceAbility)
    {
        if (nextAttackBonusDamage <= 0 || sourceAbility == null || sourceAbility.Category != AbilityCategory.BasicAttack)
        {
            return 0;
        }

        return nextAttackBonusDamage;
    }

    private void HandleEnemyDamagedByPlayer(Enemy enemy, bool isAbilityDamage)
    {
        if (enemy == null)
        {
            return;
        }

        if (HasItem(ItemRewardKey.Makibishi) && makibishiTriggeredEnemiesThisCombat.Add(enemy))
        {
            enemy.ApplyMobilityPenaltyNextTurn(1);
            TriggerItemActivationPulse(ItemRewardKey.Makibishi);
        }

        if (isAbilityDamage)
        {
            HandleAbilityDamageSideEffects(enemy);
        }
    }

    private int GetDistanceBonusDamageAgainstEnemy(Enemy enemy)
    {
        if (enemy == null || !HasItem(ItemRewardKey.ScopeGlasses))
        {
            return 0;
        }

        int distance = Mathf.Abs(enemy.GridPosition.x - gridPosition.x) + Mathf.Abs(enemy.GridPosition.y - gridPosition.y);
        if (distance >= 3)
        {
            TriggerItemActivationPulse(ItemRewardKey.ScopeGlasses);
            return 1;
        }

        return 0;
    }

    private void PlayAbilityDamageFx(AbilityDefinition sourceAbility, Transform targetAnchor)
    {
        if (sourceAbility != null && sourceAbility.HasDamageFxOverride)
        {
            PlayFeedbackFx(
                sourceAbility.DamageFxOverridePrefab,
                targetAnchor,
                sourceAbility.DamageFxOverridePositionOffset,
                sourceAbility.DamageFxOverrideDestroyAfterSeconds);
            return;
        }

        PlayProcFx(CharacterProcFxKey.Damage, targetAnchor);
    }

    private void DamageHighestHealthEnemy(int damage)
    {
        if (Board == null || damage <= 0)
        {
            return;
        }

        Enemy targetEnemy = null;
        int highestHealth = int.MinValue;
        for (int index = 0; index < Board.SpawnedEnemies.Count; index++)
        {
            Enemy enemy = Board.SpawnedEnemies[index];
            if (enemy == null)
            {
                continue;
            }

            if (enemy.CurrentHealth > highestHealth)
            {
                highestHealth = enemy.CurrentHealth;
                targetEnemy = enemy;
            }
        }

        if (targetEnemy != null)
        {
            DealDamageToEnemy(targetEnemy, damage, false);
            PlayProcFx(CharacterProcFxKey.Damage, targetEnemy.EffectAnchor);
            TriggerItemActivationPulse(ItemRewardKey.CursedPuppet);
        }
    }

    private void TriggerItemActivationPulse(ItemRewardKey itemKey, float duration = -1f)
    {
        if (!HasItem(itemKey))
        {
            return;
        }

        float resolvedDuration = duration > 0f ? duration : itemActivationPulseDuration;
        ItemActivationChanged?.Invoke(this, itemKey, true, resolvedDuration);
    }

    private void SetItemActivationState(ItemRewardKey itemKey, bool isActive)
    {
        if (!HasItem(itemKey))
        {
            return;
        }

        bool wasActive = activeItemStates.TryGetValue(itemKey, out bool cachedIsActive) && cachedIsActive;
        if (wasActive == isActive)
        {
            return;
        }

        activeItemStates[itemKey] = isActive;
        ItemActivationChanged?.Invoke(this, itemKey, isActive, 0f);
    }

    private void RefreshItemActivationStates(bool isAtBaseFullHealth, int adjacentEnemyCount, int remainingEnemies)
    {
        SetItemActivationState(ItemRewardKey.WarBanner, isFirstPlayerTurnOfArena);
        SetItemActivationState(ItemRewardKey.WhiteFlag, enemyTurnInProgress && isFirstEnemyTurnOfArena);
        SetItemActivationState(ItemRewardKey.SamuraiMask, samuraiMaskBonusDamage > 0);
        SetItemActivationState(ItemRewardKey.IronGreaves, isAtBaseFullHealth);
        SetItemActivationState(ItemRewardKey.RunicBelt, isAtBaseFullHealth);
        SetItemActivationState(ItemRewardKey.MoonLantern, adjacentEnemyCount >= 2);
        SetItemActivationState(ItemRewardKey.ScholarsCape, !attackedLastTurn);
        SetItemActivationState(ItemRewardKey.DuelistsSpur, remainingEnemies == 1);
    }

    private void PlayProcFx(CharacterProcFxKey key, Transform targetAnchor = null)
    {
        CharacterProcFxConfig fxConfig = FindProcFxConfig(key);
        if (fxConfig == null || fxConfig.FxPrefab == null)
        {
            return;
        }

        Transform anchor = targetAnchor != null ? targetAnchor : transform;
        Vector3 spawnPosition = anchor.position + fxConfig.PositionOffset;
        Quaternion defaultFxRotation = fxConfig.FxPrefab.transform.rotation;
        Vector3 defaultFxScale = fxConfig.FxPrefab.transform.localScale;
        GameObject spawnedFx = Instantiate(fxConfig.FxPrefab, spawnPosition, defaultFxRotation);
        spawnedFx.transform.rotation = defaultFxRotation;
        spawnedFx.transform.localScale = defaultFxScale;

        if (fxConfig.DestroyAfterSeconds > 0f)
        {
            Destroy(spawnedFx, fxConfig.DestroyAfterSeconds);
        }
    }

    public void PlayFeedbackFx(
        GameObject fxPrefab,
        Transform targetAnchor = null,
        Vector3? positionOffset = null,
        float destroyAfterSeconds = 1f,
        bool parentToAnchor = false)
    {
        if (fxPrefab == null)
        {
            return;
        }

        Transform anchor = targetAnchor != null ? targetAnchor : transform;
        Vector3 offset = positionOffset ?? Vector3.zero;
        Vector3 spawnPosition = anchor.position + offset;
        Quaternion defaultFxRotation = fxPrefab.transform.rotation;
        Vector3 defaultFxScale = fxPrefab.transform.localScale;
        GameObject spawnedFx = Instantiate(fxPrefab, spawnPosition, defaultFxRotation);
        if (parentToAnchor)
        {
            spawnedFx.transform.SetParent(anchor, true);
        }

        spawnedFx.transform.rotation = defaultFxRotation;
        spawnedFx.transform.localScale = defaultFxScale;

        if (destroyAfterSeconds > 0f)
        {
            Destroy(spawnedFx, destroyAfterSeconds);
        }
    }

    public void PlayExecuteProcFx(Transform targetAnchor = null)
    {
        PlayProcFx(CharacterProcFxKey.Execute, targetAnchor);
    }

    public void PlayHealProcFx(Transform targetAnchor = null)
    {
        PlayProcFx(CharacterProcFxKey.Heal, targetAnchor);
    }

    private CharacterProcFxConfig FindProcFxConfig(CharacterProcFxKey key)
    {
        for (int index = 0; index < procFxConfigs.Count; index++)
        {
            CharacterProcFxConfig fxConfig = procFxConfigs[index];
            if (fxConfig != null && fxConfig.Key == key)
            {
                return fxConfig;
            }
        }

        return null;
    }

    private void RecalculateItemDrivenStats()
    {
        int previousMaxHealth = maxHealth;
        int staticMaxHealth = baseMaxHealth + (runRewardState != null ? runRewardState.GetBonusMaxHealth() : 0);
        int staticBonusDamage = baseBonusDamage + (runRewardState != null ? runRewardState.GetBonusDamage() : 0);
        int staticResistance = baseResistance + (runRewardState != null ? runRewardState.GetBonusResistance() : 0);
        int staticMovementPoints = baseMovementPointsPerTurn + (runRewardState != null ? runRewardState.GetBonusMovementPoints() : 0);

        bool isAtBaseFullHealth = currentHealth >= staticMaxHealth && currentHealth >= maxHealth;
        int adjacentEnemyCount = CountAdjacentEnemies();
        int remainingEnemies = GetRemainingEnemyCount();

        int dynamicMaxHealth = HasItem(ItemRewardKey.RunicBelt) && isAtBaseFullHealth ? 2 : 0;
        int dynamicBonusDamage = samuraiMaskBonusDamage + bonusDamageUntilEndOfTurn + bonusDamageUntilNextMovement;
        int dynamicResistance = 0;
        int dynamicMovementPoints = 0;

        if (HasItem(ItemRewardKey.DuelistsSpur) && remainingEnemies == 1)
        {
            dynamicBonusDamage += 1;
        }

        if (HasItem(ItemRewardKey.WarBanner) && isFirstPlayerTurnOfArena)
        {
            dynamicBonusDamage += 2;
        }

        if (HasItem(ItemRewardKey.RunicBelt) && isAtBaseFullHealth)
        {
            dynamicBonusDamage += 1;
        }

        if (HasItem(ItemRewardKey.IronGreaves) && isAtBaseFullHealth)
        {
            dynamicResistance += 1;
        }

        if (HasItem(ItemRewardKey.MoonLantern) && adjacentEnemyCount >= 2)
        {
            dynamicResistance += 1;
        }

        if (HasItem(ItemRewardKey.ScholarsCape) && !attackedLastTurn)
        {
            dynamicMovementPoints += 1;
        }

        RefreshItemActivationStates(isAtBaseFullHealth, adjacentEnemyCount, remainingEnemies);

        maxHealth = staticMaxHealth + dynamicMaxHealth;
        bonusDamage = staticBonusDamage + dynamicBonusDamage;
        resistance = staticResistance + dynamicResistance;
        movementPointsPerTurn = staticMovementPoints + dynamicMovementPoints;
        if (maxHealth > previousMaxHealth)
        {
            currentHealth += maxHealth - previousMaxHealth;
        }

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        RefreshHpBar();
        SyncRunStateHealth();
    }

    private void ConsumeNextAttackBonusDamage()
    {
        if (nextAttackBonusDamage <= 0)
        {
            return;
        }

        nextAttackBonusDamage = 0;
        ClearNextAttackBonusAura();
        RecalculateItemDrivenStats();
        NotifyAbilitiesChanged();
    }

    private void ClearNextAttackBonusDamage()
    {
        nextAttackBonusDamage = 0;
        nextAttackBonusAuraPrefab = null;
        ClearNextAttackBonusAura();
    }

    private void ClearBonusDamageUntilEndOfTurn()
    {
        bonusDamageUntilEndOfTurn = 0;
        RefreshTemporaryDamageAura();
    }

    private void ClearBonusDamageUntilNextMovement(bool refreshStats = true)
    {
        if (bonusDamageUntilNextMovement <= 0)
        {
            return;
        }

        bonusDamageUntilNextMovement = 0;
        RefreshTemporaryDamageAura();
        if (refreshStats)
        {
            RecalculateItemDrivenStats();
            NotifyAbilitiesChanged();
        }
    }

    private void RefreshNextAttackBonusAura()
    {
        if (nextAttackBonusDamage <= 0 || nextAttackBonusAuraPrefab == null)
        {
            ClearNextAttackBonusAura();
            return;
        }

        if (activeNextAttackBonusAuraInstance != null
            && activeNextAttackBonusAuraInstance.name.StartsWith(nextAttackBonusAuraPrefab.name, StringComparison.Ordinal))
        {
            return;
        }

        ClearNextAttackBonusAura();
        Quaternion defaultFxRotation = nextAttackBonusAuraPrefab.transform.rotation;
        Vector3 defaultFxScale = nextAttackBonusAuraPrefab.transform.localScale;
        activeNextAttackBonusAuraInstance = Instantiate(nextAttackBonusAuraPrefab, transform.position, defaultFxRotation);
        activeNextAttackBonusAuraInstance.transform.SetParent(transform, true);
        activeNextAttackBonusAuraInstance.transform.rotation = defaultFxRotation;
        activeNextAttackBonusAuraInstance.transform.localScale = defaultFxScale;
    }

    private void ClearNextAttackBonusAura()
    {
        if (activeNextAttackBonusAuraInstance != null)
        {
            Destroy(activeNextAttackBonusAuraInstance);
            activeNextAttackBonusAuraInstance = null;
        }
    }

    private void RefreshTemporaryDamageAura()
    {
        if ((bonusDamageUntilEndOfTurn + bonusDamageUntilNextMovement) <= 0 || temporaryDamageAuraPrefab == null)
        {
            ClearTemporaryDamageAura();
            return;
        }

        if (activeTemporaryDamageAuraInstance != null
            && activeTemporaryDamageAuraInstance.name.StartsWith(temporaryDamageAuraPrefab.name, StringComparison.Ordinal))
        {
            return;
        }

        ClearTemporaryDamageAura();
        Quaternion defaultFxRotation = temporaryDamageAuraPrefab.transform.rotation;
        Vector3 defaultFxScale = temporaryDamageAuraPrefab.transform.localScale;
        activeTemporaryDamageAuraInstance = Instantiate(temporaryDamageAuraPrefab, transform.position, defaultFxRotation);
        activeTemporaryDamageAuraInstance.transform.SetParent(transform, true);
        activeTemporaryDamageAuraInstance.transform.rotation = defaultFxRotation;
        activeTemporaryDamageAuraInstance.transform.localScale = defaultFxScale;
    }

    private void ClearTemporaryDamageAura()
    {
        if (activeTemporaryDamageAuraInstance != null)
        {
            Destroy(activeTemporaryDamageAuraInstance);
            activeTemporaryDamageAuraInstance = null;
        }
    }

    private void RefreshPoisonFx()
    {
        if (!isPoisoned)
        {
            ClearPersistentStatusFx(CombatStatusType.Poisoned);
            return;
        }

        RefreshPersistentStatusFx(CombatStatusType.Poisoned);
    }

    private void ApplyPoisonTickAtEndOfTurn()
    {
        if (!isPoisoned || currentHealth <= 0)
        {
            return;
        }

        PlayStatusApplyFx(CombatStatusType.Poisoned);
        TakeDamage(1, null, false, DamageSoundType.MagicHit);
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
        GetStatusFxLibrary()?.SpawnApplyFx(statusType, transform, Vector3.zero);
    }

    private void RefreshPersistentStatusFx(CombatStatusType statusType)
    {
        if (activeStatusFxInstances.TryGetValue(statusType, out GameObject existingInstance) && existingInstance != null)
        {
            return;
        }

        GameObject spawnedFx = GetStatusFxLibrary()?.SpawnPersistentFx(statusType, transform, Vector3.zero);
        if (spawnedFx != null)
        {
            activeStatusFxInstances[statusType] = spawnedFx;
        }
    }

    private void ClearPersistentStatusFx(CombatStatusType statusType)
    {
        if (!activeStatusFxInstances.TryGetValue(statusType, out GameObject existingInstance))
        {
            return;
        }

        activeStatusFxInstances.Remove(statusType);
        if (existingInstance != null)
        {
            Destroy(existingInstance);
        }
    }

    private void ConsumeTemporaryBonusDamageOnMovement()
    {
        if (bonusDamageUntilNextMovement > 0)
        {
            ClearBonusDamageUntilNextMovement();
        }
    }

    private bool ShouldConsumeMovementPointForSlide()
    {
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            if (runtime?.Definition != null && runtime.Definition.PreventsSlideMovementPointConsumption(this, runtime))
            {
                return false;
            }
        }

        return true;
    }

    private void ClearDeathMark()
    {
        if (markedEnemy != null)
        {
            markedEnemy.SetDeathMarkActive(false);
        }

        markedEnemy = null;
        markedLichSkull = null;
        deathMarkAbility = null;
    }

    private void Die()
    {
        if (isDying)
        {
            return;
        }

        isDying = true;
        Died?.Invoke(this);
        SpawnDeathFx();
        moveTween?.Kill();
        bodyRotationTween?.Kill();
        IsMoving = false;

        if (Board != null && Board.TryGetCell(gridPosition, out BoardCell cell) && cell.Occupant == gameObject)
        {
            cell.ClearOccupant();
        }

        Owner?.AssignCharacter(null);

        Destroy(gameObject, deathDestroyDelay);
    }

    private void SpawnDeathFx()
    {
        GameObject deathFxPrefab = Board != null && Board.DefaultSpawnFxPrefab != null
            ? Board.DefaultSpawnFxPrefab
            : fxDeathPrefab;
        if (deathFxPrefab == null)
        {
            return;
        }

        GameObject deathFx = Instantiate(deathFxPrefab, transform.position, deathFxPrefab.transform.rotation);
        deathFx.transform.localScale = deathFxPrefab.transform.localScale;
        float destroyAfterSeconds = Board != null && Board.DefaultSpawnFxPrefab == deathFxPrefab
            ? Board.DefaultSpawnFxLifetime
            : 0f;
        if (destroyAfterSeconds > 0f)
        {
            Destroy(deathFx, destroyAfterSeconds);
        }
    }

    private int CountAdjacentEnemies()
    {
        if (Board == null || Board.Cells == null)
        {
            return 0;
        }

        int count = 0;
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                Vector2Int targetCell = gridPosition + new Vector2Int(x, y);
                if (Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private int GetRemainingEnemyCount()
    {
        if (Board == null)
        {
            return 0;
        }

        int count = 0;
        for (int index = 0; index < Board.SpawnedEnemies.Count; index++)
        {
            if (Board.SpawnedEnemies[index] != null)
            {
                count++;
            }
        }

        return count;
    }

    private IEnumerator ResetTemporaryTrailColorAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        temporaryTrailColorCoroutine = null;

        if (runtimeTrailMaterial != null && activeTrailColorOwner == null)
        {
            runtimeTrailMaterial.color = defaultTrailMaterialColor;
        }
    }

    private IEnumerator ResetTemporaryTrailReplacementAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        temporaryTrailReplacementCoroutine = null;

        if (activeTrailReplacementOwner != null)
        {
            yield break;
        }

        ClearActiveReplacementTrailInstance();
        if (defaultTrailObject != null)
        {
            defaultTrailObject.SetActive(true);
        }
    }

    private void CreateReplacementTrailInstance(GameObject replacementTrailPrefab)
    {
        if (defaultTrailObject == null || replacementTrailPrefab == null)
        {
            return;
        }

        Transform defaultTrailTransform = defaultTrailObject.transform;
        Transform parent = defaultTrailTransform.parent;
        activeReplacementTrailInstance = Instantiate(replacementTrailPrefab, parent);
        activeReplacementTrailInstance.transform.localPosition = defaultTrailTransform.localPosition;
        activeReplacementTrailInstance.transform.localRotation = defaultTrailTransform.localRotation;
    }

    private void ClearActiveReplacementTrailInstance()
    {
        if (activeReplacementTrailInstance == null)
        {
            return;
        }

        Destroy(activeReplacementTrailInstance);
        activeReplacementTrailInstance = null;
    }
}
