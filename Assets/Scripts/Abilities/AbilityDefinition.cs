using UnityEngine;
using System.Collections.Generic;

public enum AbilityTargetingMode
{
    Immediate,
    FreeCell
}

public enum AbilityFxSpawnAnchor
{
    Caster,
    EachHitTarget
}

public enum AbilityFxOffsetReference
{
    World,
    CasterRotation,
    TargetRotation
}

public enum AbilityTrailApplicationMode
{
    Disabled,
    OnActivation,
    WhileActive
}

public enum AbilityCategory
{
    BasicAttack,
    MobilitySkill,
    SpecialPower
}

[System.Serializable]
public class AbilityFxSpawnConfig
{
    [SerializeField] private GameObject fxPrefab;
    [Min(0f)]
    [SerializeField] private float spawnDelay;
    [Min(0f)]
    [SerializeField] private float destroyAfterSeconds = 1f;
    [SerializeField] private AbilityFxSpawnAnchor spawnAnchor = AbilityFxSpawnAnchor.Caster;
    [SerializeField] private AbilityFxOffsetReference offsetReference = AbilityFxOffsetReference.CasterRotation;
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private bool parentToAnchor;

    public GameObject FxPrefab => fxPrefab;
    public float SpawnDelay => spawnDelay;
    public float DestroyAfterSeconds => destroyAfterSeconds;
    public AbilityFxSpawnAnchor SpawnAnchor => spawnAnchor;
    public AbilityFxOffsetReference OffsetReference => offsetReference;
    public Vector3 PositionOffset => positionOffset;
    public bool ParentToAnchor => parentToAnchor;

    public AbilityFxSpawnConfig CreateRuntimeCopy(GameObject prefabOverride = null)
    {
        AbilityFxSpawnConfig runtimeCopy = new AbilityFxSpawnConfig();
        runtimeCopy.fxPrefab = prefabOverride != null ? prefabOverride : fxPrefab;
        runtimeCopy.spawnDelay = spawnDelay;
        runtimeCopy.destroyAfterSeconds = destroyAfterSeconds;
        runtimeCopy.spawnAnchor = spawnAnchor;
        runtimeCopy.offsetReference = offsetReference;
        runtimeCopy.positionOffset = positionOffset;
        runtimeCopy.parentToAnchor = parentToAnchor;
        return runtimeCopy;
    }
}

public abstract class AbilityDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string abilityName;
    [SerializeField] private Sprite icon;
    [SerializeField] private AbilityCategory category = AbilityCategory.BasicAttack;
    [SerializeField] private AnimationClip attackAnimationClip;
    [SerializeField] private List<AbilityUpgradeRewardDefinition> linkedUpgradeRewards = new List<AbilityUpgradeRewardDefinition>();

    [Header("Usage")]
    [Min(0)]
    [SerializeField] private int usesPerTurn;
    [Min(0)]
    [SerializeField] private int usesPerCombat;
    [Min(0)]
    [SerializeField] private int cooldownTurns;
    [SerializeField] private bool consumesMovementPoint;
    [SerializeField] private bool isToggle;

    [Header("FX")]
    [SerializeField] private List<AbilityFxSpawnConfig> fxSpawns = new List<AbilityFxSpawnConfig>();

    [Header("Damage Timing")]
    [Min(0f)]
    [SerializeField] private float damageApplyDelay;
    [Min(0f)]
    [SerializeField] private float damageDelayDistanceMultiplier = 1f;

    [Header("Damage FX Override")]
    [SerializeField] private GameObject damageFxOverridePrefab;
    [SerializeField] private Vector3 damageFxOverridePositionOffset;
    [Min(0f)]
    [SerializeField] private float damageFxOverrideDestroyAfterSeconds = 1f;

    [Header("Trail")]
    [SerializeField] private AbilityTrailApplicationMode trailColorMode;
    [SerializeField] private Color trailMaterialColor = Color.white;
    [Min(0f)]
    [SerializeField] private float temporaryTrailColorDuration = 0.5f;
    [SerializeField] private AbilityTrailApplicationMode trailReplacementMode;
    [SerializeField] private GameObject replacementTrailPrefab;
    [Min(0f)]
    [SerializeField] private float temporaryTrailReplacementDuration = 0.5f;

    public string AbilityName => string.IsNullOrWhiteSpace(abilityName) ? name : abilityName;
    public Sprite Icon => icon;
    public AbilityCategory Category => category;
    public AnimationClip AttackAnimationClip => attackAnimationClip;
    public IReadOnlyList<AbilityUpgradeRewardDefinition> LinkedUpgradeRewards => linkedUpgradeRewards;
    public int UsesPerTurn => usesPerTurn;
    public int UsesPerCombat => usesPerCombat;
    public int CooldownTurns => cooldownTurns;
    public bool ConsumesMovementPoint => consumesMovementPoint;
    public bool IsToggle => isToggle;
    public IReadOnlyList<AbilityFxSpawnConfig> FxSpawns => fxSpawns;
    public float DamageApplyDelay => damageApplyDelay;
    public float DamageDelayDistanceMultiplier => damageDelayDistanceMultiplier;
    public GameObject DamageFxOverridePrefab => damageFxOverridePrefab;
    public Vector3 DamageFxOverridePositionOffset => damageFxOverridePositionOffset;
    public float DamageFxOverrideDestroyAfterSeconds => damageFxOverrideDestroyAfterSeconds;
    public bool HasDamageFxOverride => damageFxOverridePrefab != null;
    public virtual AbilityTargetingMode TargetingMode => AbilityTargetingMode.Immediate;
    public virtual bool KeepsActiveStateBetweenTurns => true;
    public virtual bool RefundUseIfDeactivatedWithoutConsumption => false;
    public virtual bool DeactivateAfterSelectedCellActivation => true;

    public virtual bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        return character != null && runtime != null;
    }

    public virtual bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanActivate(character, runtime);
    }

    public virtual bool AllowsUnitTraversal(Character character, CharacterAbilityRuntime runtime)
    {
        return false;
    }

    public virtual bool LimitsNextSlideToOneCell(Character character, CharacterAbilityRuntime runtime)
    {
        return false;
    }

    public virtual bool SupportsCellSelectionWhileActive(Character character, CharacterAbilityRuntime runtime)
    {
        return false;
    }

    public virtual bool CanActivateFromSelectedCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return false;
    }

    public virtual bool TryActivateFromSelectedCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return false;
    }

    public virtual int GetTraversalDamage(Character character, CharacterAbilityRuntime runtime, int traversedEnemyCount)
    {
        return 0;
    }

    public virtual float GetDamageApplyDelay(Vector2Int originCell, Vector2Int targetCell)
    {
        if (damageApplyDelay <= 0f)
        {
            return 0f;
        }

        int distance = Mathf.Abs(targetCell.x - originCell.x) + Mathf.Abs(targetCell.y - originCell.y);
        if (distance <= 1)
        {
            return damageApplyDelay;
        }

        float multiplier = Mathf.Max(0f, damageDelayDistanceMultiplier);
        if (Mathf.Approximately(multiplier, 1f))
        {
            return damageApplyDelay;
        }

        return damageApplyDelay * Mathf.Pow(multiplier, distance - 1);
    }

    public virtual string GetCounterText(CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return string.Empty;
        }

        if (IsToggle && runtime.IsActive)
        {
            return "ON";
        }

        if (runtime.RemainingCooldown > 0)
        {
            return runtime.RemainingCooldown.ToString();
        }

        if (UsesPerTurn > 0)
        {
            return runtime.RemainingUsesThisTurn.ToString();
        }

        if (UsesPerCombat > 0)
        {
            return runtime.RemainingUsesThisCombat.ToString();
        }

        return string.Empty;
    }

    protected void PlayConfiguredFx(Character character, IEnumerable<Enemy> hitTargets = null)
    {
        if (character == null || fxSpawns == null || fxSpawns.Count == 0)
        {
            return;
        }

        character.PlayAbilityFx(fxSpawns, hitTargets);
    }

    public virtual void PlayActivationAnimation(Character character)
    {
        if (character == null || attackAnimationClip == null)
        {
            return;
        }

        character.PlayAttackAnimation(attackAnimationClip);
    }

    public virtual void PlayTraversalFx(Character character, IEnumerable<Enemy> hitTargets)
    {
        PlayConfiguredFx(character, hitTargets);
    }

    public virtual void OnAbilityActivated(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null)
        {
            return;
        }

        ApplyTrailColorEffect(character, runtime);
        ApplyTrailReplacementEffect(character, runtime);
    }

    public virtual void OnAbilityDeactivated(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null)
        {
            return;
        }

        if (trailColorMode == AbilityTrailApplicationMode.WhileActive)
        {
            character.ClearTrailColorOverride(this);
        }

        if (trailReplacementMode == AbilityTrailApplicationMode.WhileActive)
        {
            character.ClearTrailReplacementOverride(this);
        }
    }

    public abstract bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell);

    protected void ExecuteUnlockedSecondaryEffects(
        Character character,
        CharacterAbilityRuntime runtime,
        AbilityExecutionContext context,
        IReadOnlyList<UpgradedSecondaryEffectEntry> secondaryEffects,
        SecondaryEffectTiming timing)
    {
        if (character == null || secondaryEffects == null)
        {
            return;
        }

        for (int index = 0; index < secondaryEffects.Count; index++)
        {
            UpgradedSecondaryEffectEntry entry = secondaryEffects[index];
            if (entry == null
                || entry.Timing != timing
                || !entry.IsUnlocked(character)
                || entry.Effect == null)
            {
                continue;
            }

            entry.Effect.Execute(character, context);
        }
    }

    private void ApplyTrailColorEffect(Character character, CharacterAbilityRuntime runtime)
    {
        switch (ResolveTrailMode(trailColorMode, runtime))
        {
            case AbilityTrailApplicationMode.OnActivation:
                character.PlayTemporaryTrailColor(trailMaterialColor, temporaryTrailColorDuration);
                break;
            case AbilityTrailApplicationMode.WhileActive:
                character.SetTrailColorOverride(this, trailMaterialColor);
                break;
        }
    }

    private void ApplyTrailReplacementEffect(Character character, CharacterAbilityRuntime runtime)
    {
        if (replacementTrailPrefab == null)
        {
            return;
        }

        switch (ResolveTrailMode(trailReplacementMode, runtime))
        {
            case AbilityTrailApplicationMode.OnActivation:
                character.PlayTemporaryTrailReplacement(replacementTrailPrefab, temporaryTrailReplacementDuration);
                break;
            case AbilityTrailApplicationMode.WhileActive:
                character.SetTrailReplacementOverride(this, replacementTrailPrefab);
                break;
        }
    }

    private AbilityTrailApplicationMode ResolveTrailMode(AbilityTrailApplicationMode mode, CharacterAbilityRuntime runtime)
    {
        if (mode != AbilityTrailApplicationMode.WhileActive)
        {
            return mode;
        }

        return runtime != null && runtime.IsActive
            ? AbilityTrailApplicationMode.WhileActive
            : AbilityTrailApplicationMode.OnActivation;
    }
}
