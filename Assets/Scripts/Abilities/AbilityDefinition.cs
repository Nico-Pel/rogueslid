using UnityEngine;

public enum AbilityTargetingMode
{
    Immediate,
    FreeCell
}

public abstract class AbilityDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string abilityName;
    [SerializeField] private Sprite icon;

    [Header("Usage")]
    [Min(0)]
    [SerializeField] private int usesPerTurn;
    [Min(0)]
    [SerializeField] private int usesPerCombat;
    [Min(0)]
    [SerializeField] private int cooldownTurns;
    [SerializeField] private bool consumesMovementPoint;
    [SerializeField] private bool isToggle;

    public string AbilityName => string.IsNullOrWhiteSpace(abilityName) ? name : abilityName;
    public Sprite Icon => icon;
    public int UsesPerTurn => usesPerTurn;
    public int UsesPerCombat => usesPerCombat;
    public int CooldownTurns => cooldownTurns;
    public bool ConsumesMovementPoint => consumesMovementPoint;
    public bool IsToggle => isToggle;
    public virtual AbilityTargetingMode TargetingMode => AbilityTargetingMode.Immediate;

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

    public virtual int GetTraversalDamage(Character character, CharacterAbilityRuntime runtime)
    {
        return 0;
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

    public abstract bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell);
}
