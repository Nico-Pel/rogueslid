using System.Collections.Generic;
using UnityEngine;

public enum SecondaryEffectAnchor
{
    OriginCell,
    TargetCell,
    CharacterCurrentCell
}

public enum SecondaryEffectTiming
{
    BeforeMovement,
    AfterMovement
}

public enum SecondaryEffectTargetMode
{
    None,
    TargetEnemy
}

public enum SecondaryEffectOffsetReference
{
    World,
    CharacterRotation
}

[System.Serializable]
public class SecondaryEffectFxSpawnConfig
{
    [SerializeField] private GameObject fxPrefab;
    [Min(0f)]
    [SerializeField] private float spawnDelay;
    [Min(0f)]
    [SerializeField] private float destroyAfterSeconds = 1f;
    [SerializeField] private SecondaryEffectAnchor spawnAnchor = SecondaryEffectAnchor.CharacterCurrentCell;
    [SerializeField] private SecondaryEffectOffsetReference offsetReference = SecondaryEffectOffsetReference.World;
    [SerializeField] private Vector3 positionOffset;

    public GameObject FxPrefab => fxPrefab;
    public float SpawnDelay => spawnDelay;
    public float DestroyAfterSeconds => destroyAfterSeconds;
    public SecondaryEffectAnchor SpawnAnchor => spawnAnchor;
    public SecondaryEffectOffsetReference OffsetReference => offsetReference;
    public Vector3 PositionOffset => positionOffset;
}

[System.Serializable]
public class UpgradedSecondaryEffectEntry
{
    [SerializeField] private bool requiresUpgrade = true;
    [SerializeField] private AbilityUpgradeKey requiredUpgradeKey;
    [SerializeField] private SecondaryEffectTiming timing = SecondaryEffectTiming.AfterMovement;
    [SerializeField] private SecondaryAbilityEffectDefinition effect;

    public bool RequiresUpgrade => requiresUpgrade;
    public AbilityUpgradeKey RequiredUpgradeKey => requiredUpgradeKey;
    public SecondaryEffectTiming Timing => timing;
    public SecondaryAbilityEffectDefinition Effect => effect;

    public bool IsUnlocked(Character character)
    {
        if (effect == null || character == null)
        {
            return false;
        }

        return !requiresUpgrade || character.GetUpgradeStacks(requiredUpgradeKey) > 0;
    }
}

public readonly struct AbilityExecutionContext
{
    public AbilityExecutionContext(
        AbilityDefinition sourceAbility,
        CharacterAbilityRuntime runtime,
        Vector2Int originCell,
        Vector2Int targetCell,
        Enemy targetEnemy = null)
    {
        SourceAbility = sourceAbility;
        Runtime = runtime;
        OriginCell = originCell;
        TargetCell = targetCell;
        TargetEnemy = targetEnemy;
    }

    public AbilityDefinition SourceAbility { get; }
    public CharacterAbilityRuntime Runtime { get; }
    public Vector2Int OriginCell { get; }
    public Vector2Int TargetCell { get; }
    public Enemy TargetEnemy { get; }
}

public abstract class SecondaryAbilityEffectDefinition : ScriptableObject
{
    [Header("Feedback")]
    [SerializeField] private AnimationClip characterAnimationClip;
    [SerializeField] private List<SecondaryEffectFxSpawnConfig> fxSpawns = new List<SecondaryEffectFxSpawnConfig>();

    public abstract void Execute(Character character, AbilityExecutionContext context);

    protected void PlayFeedback(Character character, AbilityExecutionContext context)
    {
        if (character == null)
        {
            return;
        }

        if (characterAnimationClip != null)
        {
            character.PlayAttackAnimation(characterAnimationClip);
        }

        if (fxSpawns != null && fxSpawns.Count > 0)
        {
            character.PlaySecondaryEffectFx(fxSpawns, context);
        }
    }

    protected static Vector2Int ResolveAnchorCell(Character character, AbilityExecutionContext context, SecondaryEffectAnchor anchor)
    {
        switch (anchor)
        {
            case SecondaryEffectAnchor.OriginCell:
                return context.OriginCell;
            case SecondaryEffectAnchor.TargetCell:
                return context.TargetCell;
            default:
                return character != null ? character.GridPosition : context.TargetCell;
        }
    }
}
