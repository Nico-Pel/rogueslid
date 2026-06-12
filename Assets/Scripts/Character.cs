using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private Image hpFillBar;
    [SerializeField] private Transform characterBody;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private TrailRenderer characterTrail;
    [SerializeField] private string dashingBoolParameter = "Dashing";
    [SerializeField] private string attackTriggerParameter = "Attack";
    [SerializeField] private string attackPlaceholderClipName = "Attack_Spiral";
    [SerializeField] private bool orientTowardsDashDirection = true;
    [SerializeField] private bool resetBodyRotationAfterSlide = true;
    [SerializeField] private List<CharacterProcFxConfig> procFxConfigs = new List<CharacterProcFxConfig>();
    private float dashBodyRotateDuration = 0.05f;
    private float dashBodyResetDuration = 0.05f;

    private Renderer[] renderers;
    private Color[] baseColors;
    private Tween moveTween;
    private Tween bodyRotationTween;
    private int remainingMovementPoints;
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
    private bool isDying;
    private readonly HashSet<Enemy> frostCharmedEnemiesThisTurn = new HashSet<Enemy>();
    private int nextAttackBonusDamage;
    private GameObject nextAttackBonusAuraPrefab;
    private GameObject activeNextAttackBonusAuraInstance;
    private Enemy markedEnemy;
    private DeathMarkAbility deathMarkAbility;

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
    public CharacterData Data => characterData;
    public string CharacterName => characterData != null ? characterData.CharacterName : name;
    public string CharacterDescription => characterData != null ? characterData.Description : string.Empty;
    public Sprite CharacterPortrait => characterData != null ? characterData.Portrait : null;

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
    public IReadOnlyList<CharacterAbilityRuntime> Abilities => abilities;
    public PlayerRunRewardState RunRewardState => runRewardState;

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
        frostCharmedEnemiesThisTurn.Clear();
        ClearNextAttackBonusDamage();
        markedEnemy = null;
        deathMarkAbility = null;
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
        ApplyCharacterDataDefinition();
        CacheBaseStatsIfNeeded();
        runRewardState = state;
        RecalculateItemDrivenStats();
        ClearNextAttackBonusDamage();
        markedEnemy = null;
        deathMarkAbility = null;

        InitializeAbilities(runRewardState != null ? runRewardState.GetEquippedAbilities() : GetStartingAbilityDefinitions());

        int targetHealth = runRewardState != null && runRewardState.CurrentHealth >= 0
            ? runRewardState.CurrentHealth
            : maxHealth;
        currentHealth = Mathf.Clamp(targetHealth, 0, maxHealth);
        SyncRunStateHealth();

        ResetTurn();
        RefreshHpBar();
        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
    }

    public void ResetTurn()
    {
        RecalculateItemDrivenStats();
        remainingMovementPoints = movementPointsPerTurn;
        frostCharmedEnemiesThisTurn.Clear();
        ClearNextAttackBonusDamage();
        for (int index = 0; index < abilities.Count; index++)
        {
            abilities[index].BeginTurn(this);
        }

        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
    }

    public bool TrySlide(Vector2Int direction)
    {
        if (Board == null || remainingMovementPoints <= 0 || IsMoving || direction == Vector2Int.zero)
        {
            return false;
        }

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

        if (!Board.MoveOccupant(gridPosition, destination, BoardOccupantKind.PlayerCharacter))
        {
            return false;
        }

        if (orientTowardsDashDirection)
        {
            FaceDashDirection(direction);
        }

        gridPosition = destination;
        remainingMovementPoints--;
        NotifyMovementPointsChanged();
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
        if (runtime == null || IsMoving)
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
        }

        if (nextAttackBonusDamage > 0
            && runtime.Definition != null
            && runtime.Definition.Category == AbilityCategory.BasicAttack)
        {
            ConsumeNextAttackBonusDamage();
        }

        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
        return true;
    }

    public bool TryTeleportTo(Vector2Int targetCell)
    {
        if (Board == null || targetCell == gridPosition)
        {
            return false;
        }

        if (!Board.IsCellWalkable(targetCell))
        {
            return false;
        }

        if (!Board.MoveOccupant(gridPosition, targetCell, BoardOccupantKind.PlayerCharacter))
        {
            return false;
        }

        gridPosition = targetCell;
        AnimateToGrid();
        return true;
    }

    public void ConsumeMovementPoint()
    {
        if (remainingMovementPoints <= 0)
        {
            return;
        }

        remainingMovementPoints--;
        NotifyMovementPointsChanged();
    }

    public int DealDamageToEnemy(
        Enemy enemy,
        int baseDamage,
        bool addBonusDamage,
        bool isAbilityDamage = false,
        DamageSoundType hitSoundType = DamageSoundType.Default)
    {
        if (enemy == null)
        {
            return 0;
        }

        RecalculateItemDrivenStats();
        int totalDamage = Mathf.Max(1, baseDamage + (addBonusDamage ? bonusDamage : 0));
        if (HasItem(ItemRewardKey.ThornedBracer)
            && enemy.CurrentHealth <= Mathf.Max(1, totalDamage - enemy.Resistance))
        {
            totalDamage += 2;
        }

        int appliedDamage = enemy.TakeDamage(totalDamage, hitSoundType);
        if (isAbilityDamage)
        {
            HandleAbilityDamageSideEffects(enemy);
            if (enemy == markedEnemy && enemy.CurrentHealth > 0)
            {
                int bonusMarkDamage = enemy.TakeDamage(1, DamageSoundType.Default);
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

        return appliedDamage;
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
        if (HasItem(ItemRewardKey.LuckyCoin) && !luckyCoinUsedThisCombat && currentHealth - finalDamage <= 0)
        {
            luckyCoinUsedThisCombat = true;
            finalDamage = Mathf.Max(0, currentHealth - 1);
        }

        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        tookDamageDuringEnemyTurn = true;

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
            if (!wasProjectile && HasItem(ItemRewardKey.ThornArmor) && IsEnemyClose(sourceEnemy))
            {
                DealDamageToEnemy(sourceEnemy, 1, false);
                PlayProcFx(CharacterProcFxKey.RetaliationHit, sourceEnemy.EffectAnchor);
            }

            if (wasProjectile && HasItem(ItemRewardKey.Boomerang))
            {
                DealDamageToEnemy(sourceEnemy, 2, false);
                PlayProcFx(CharacterProcFxKey.RetaliationHit, sourceEnemy.EffectAnchor);
            }
        }

        if (currentHealth <= 0)
        {
            Die();
        }

        return finalDamage;
    }

    public void Heal(int amount)
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
            // SoundManager.Instance?.PlayHeal(transform.position);
            PlayProcFx(CharacterProcFxKey.Heal);
        }
    }

    public void GainMovementPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        remainingMovementPoints += amount;
        SoundManager.Instance?.PlayPowerUp(transform.position);
        NotifyMovementPointsChanged();
        PlayProcFx(CharacterProcFxKey.BonusMovement);
    }

    public void RefreshAbilityState()
    {
        NotifyAbilitiesChanged();
    }

    public int GetUpgradeStacks(AbilityUpgradeKey upgradeKey)
    {
        return runRewardState != null ? runRewardState.GetUpgradeStacks(upgradeKey) : 0;
    }

    public bool HasItem(ItemRewardKey itemKey)
    {
        return runRewardState != null && runRewardState.HasItem(itemKey);
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

        if (HasItem(ItemRewardKey.CursedPuppet))
        {
            DamageHighestHealthEnemy(1);
        }

        if (HasItem(ItemRewardKey.SacredChalice))
        {
            Heal(1);
        }

        if (HasItem(ItemRewardKey.SwiftAnklet) && remainingMovementPoints >= 2)
        {
            Heal(1);
        }

        isFirstPlayerTurnOfArena = false;
        ClearDeathMark();
        ClearNextAttackBonusDamage();
        RecalculateItemDrivenStats();
    }

    public void BeginEnemyTurn()
    {
        tookDamageDuringEnemyTurn = false;
    }

    public void HandleEnemyTurnEnded()
    {
        if (HasItem(ItemRewardKey.GuardMedal) && !tookDamageDuringEnemyTurn)
        {
            Heal(1);
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

    public void ApplyDeathMark(Enemy enemy, DeathMarkAbility sourceAbility)
    {
        if (markedEnemy != null && markedEnemy != enemy)
        {
            markedEnemy.SetDeathMarkActive(false);
        }

        markedEnemy = enemy;
        deathMarkAbility = sourceAbility;
        markedEnemy?.SetDeathMarkActive(true);
    }

    public int DamageEnemiesAround(Vector2Int centerCell, int range, int damage, bool includeDiagonals = true)
    {
        if (Board == null || range <= 0 || damage <= 0)
        {
            return 0;
        }

        int hits = 0;
        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
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
                if (!Board.TryGetEnemy(targetCell, out Enemy enemy) || enemy == null || !hitEnemies.Add(enemy))
                {
                    continue;
                }

                DealDamageToEnemy(enemy, damage, false, true);
                hits++;
            }
        }

        return hits;
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
        if (Board == null)
        {
            return;
        }

        moveTween?.Kill();
        IsMoving = true;
        SetDashingAnimation(true);
        SoundManager.Instance?.PlayDash(transform.position);
        Vector3 targetPosition = Board.GridToWorldPosition(gridPosition) + Vector3.up * spawnHeight;
        moveTween = transform.DOMove(targetPosition, moveDuration)
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
        IsMoving = false;
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
        if (defaultTrailObject != null)
        {
            defaultTrailObject.SetActive(true);
        }
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
            if (runtime.Definition == null || !runtime.Definition.LimitsNextSlideToOneCell(this, runtime))
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

            DealDamageToEnemy(enemy, traversalDamage, false, true);
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
            float refundChance = 0.05f * GetUpgradeStacks(AbilityUpgradeKey.GhostStepsAdrenaline);
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

    private void NotifyAbilitiesChanged()
    {
        AbilitiesChanged?.Invoke(this);
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
        if (HasItem(ItemRewardKey.BloodAmulet))
        {
            Heal(2);
        }

        if (HasItem(ItemRewardKey.RavenFeather))
        {
            GainMovementPoints(1);
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

        if (Board != null && Board.SpawnedEnemies.Count == 1)
        {
            HandleEnemyCountChanged(1);
        }
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

        enemy.ApplyMobilityPenaltyNextTurn(1);
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
        }
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
        int staticMaxHealth = baseMaxHealth + (runRewardState != null ? runRewardState.GetBonusMaxHealth() : 0);
        int staticBonusDamage = baseBonusDamage + (runRewardState != null ? runRewardState.GetBonusDamage() : 0);
        int staticResistance = baseResistance + (runRewardState != null ? runRewardState.GetBonusResistance() : 0);
        int staticMovementPoints = baseMovementPointsPerTurn + (runRewardState != null ? runRewardState.GetBonusMovementPoints() : 0);

        bool isAtBaseFullHealth = currentHealth >= staticMaxHealth;
        int adjacentEnemyCount = CountAdjacentEnemies();
        int remainingEnemies = GetRemainingEnemyCount();

        int dynamicMaxHealth = HasItem(ItemRewardKey.RunicBelt) && isAtBaseFullHealth ? 2 : 0;
        int dynamicBonusDamage = nextAttackBonusDamage;
        int dynamicResistance = 0;

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
            dynamicResistance += 2;
        }

        if (HasItem(ItemRewardKey.MoonLantern) && adjacentEnemyCount >= 2)
        {
            dynamicResistance += 1;
        }

        maxHealth = staticMaxHealth + dynamicMaxHealth;
        bonusDamage = staticBonusDamage + dynamicBonusDamage;
        resistance = staticResistance + dynamicResistance;
        movementPointsPerTurn = staticMovementPoints;
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

    private void ClearDeathMark()
    {
        if (markedEnemy != null)
        {
            markedEnemy.SetDeathMarkActive(false);
        }

        markedEnemy = null;
        deathMarkAbility = null;
    }

    private void Die()
    {
        if (isDying)
        {
            return;
        }

        isDying = true;
        SpawnDeathFx();
        moveTween?.Kill();
        bodyRotationTween?.Kill();
        IsMoving = false;

        if (Board != null && Board.TryGetCell(gridPosition, out BoardCell cell) && cell.Occupant == gameObject)
        {
            cell.ClearOccupant();
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
