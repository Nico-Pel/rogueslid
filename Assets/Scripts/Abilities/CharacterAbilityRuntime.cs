using UnityEngine;

public class CharacterAbilityRuntime
{
    public CharacterAbilityRuntime(AbilityDefinition definition)
    {
        Definition = definition;
    }

    public AbilityDefinition Definition { get; }
    public int UsesThisTurn { get; private set; }
    public int UsesThisCombat { get; private set; }
    public int RemainingCooldown { get; private set; }
    public bool IsActive { get; private set; }
    public int BonusUsesThisTurn { get; private set; }
    public int UsesThisTurnCount => UsesThisTurn;
    public float NextReusableTime { get; private set; }
    private bool hasPreparedActivationPendingConsumption;
    private int pendingBaseDamageModifierForNextUse;
    private bool suppressNextPrimaryEffectOnce;
    private int definitionBonusUsesThisTurn;
    private int pendingActivationUseDelta;
    public AbilityTargetingMode TargetingMode => Definition != null ? Definition.TargetingMode : AbilityTargetingMode.Immediate;
    public int RemainingUsesThisTurn => Definition == null || Definition.UsesPerTurn <= 0
        ? int.MaxValue
        : Mathf.Max(0, GetMaxUsesThisTurn() - UsesThisTurn);
    public int RemainingUsesThisCombat => Definition == null || Definition.UsesPerCombat <= 0
        ? int.MaxValue
        : Mathf.Max(0, Definition.UsesPerCombat - UsesThisCombat);

    public void ResetCombat()
    {
        UsesThisTurn = 0;
        UsesThisCombat = 0;
        RemainingCooldown = 0;
        IsActive = false;
        BonusUsesThisTurn = 0;
        definitionBonusUsesThisTurn = 0;
        NextReusableTime = 0f;
        hasPreparedActivationPendingConsumption = false;
        pendingBaseDamageModifierForNextUse = 0;
        suppressNextPrimaryEffectOnce = false;
        pendingActivationUseDelta = 0;
    }

    public void BeginTurn(Character character)
    {
        if (IsActive && Definition != null && !Definition.KeepsActiveStateBetweenTurns)
        {
            Deactivate(character);
        }

        UsesThisTurn = 0;
        BonusUsesThisTurn = 0;
        definitionBonusUsesThisTurn = Definition != null ? Mathf.Max(0, Definition.GetBonusUsesPerTurn(character, this)) : 0;
        if (RemainingCooldown > 0)
        {
            RemainingCooldown--;
        }

        pendingBaseDamageModifierForNextUse = 0;
        suppressNextPrimaryEffectOnce = false;
        pendingActivationUseDelta = 0;
        Definition?.OnTurnStarted(character, this);
    }

    public bool IsUsable(Character character)
    {
        if (Definition == null || character == null)
        {
            return false;
        }

        if (Definition.IsToggle && IsActive)
        {
            return true;
        }

        if (RemainingCooldown > 0)
        {
            return false;
        }

        if (Time.time < NextReusableTime)
        {
            return false;
        }

        if (Definition.UsesPerTurn > 0 && UsesThisTurn >= GetMaxUsesThisTurn())
        {
            return false;
        }

        if (Definition.UsesPerCombat > 0 && UsesThisCombat >= Definition.UsesPerCombat)
        {
            return false;
        }

        if (Definition.ConsumesMovementPoint && character.RemainingMovementPoints <= 0)
        {
            return false;
        }

        return Definition.CanActivate(character, this);
    }

    public void Deactivate(Character character)
    {
        if (!IsActive)
        {
            return;
        }

        if (Definition != null
            && Definition.RefundUseIfDeactivatedWithoutConsumption
            && hasPreparedActivationPendingConsumption)
        {
            UsesThisTurn = Mathf.Max(0, UsesThisTurn - 1);
            UsesThisCombat = Mathf.Max(0, UsesThisCombat - 1);
            RemainingCooldown = 0;
        }

        hasPreparedActivationPendingConsumption = false;
        IsActive = false;
        Definition?.OnAbilityDeactivated(character, this);
    }

    public bool TryUse(Character character, Vector2Int? targetCell)
    {
        if (Definition == null || character == null)
        {
            return false;
        }

        if (Definition.IsToggle && IsActive)
        {
            Deactivate(character);
            return true;
        }

        if (!IsUsable(character))
        {
            return false;
        }

        if (TargetingMode == AbilityTargetingMode.FreeCell)
        {
            Vector2Int automaticTargetCell = default;
            if (!targetCell.HasValue && !Definition.TryGetAutomaticTargetCell(character, this, out automaticTargetCell))
            {
                return false;
            }

            Vector2Int resolvedTargetCell = targetCell ?? automaticTargetCell;
            if (!Definition.CanActivateOnCell(character, this, resolvedTargetCell))
            {
                return false;
            }

            targetCell = resolvedTargetCell;
        }

        bool activated = Definition.TryActivate(character, this, targetCell);
        if (!activated)
        {
            pendingActivationUseDelta = 0;
            return false;
        }

        UsesThisTurn++;
        UsesThisCombat++;
        if (pendingActivationUseDelta != 0)
        {
            UsesThisTurn = Mathf.Max(0, UsesThisTurn + pendingActivationUseDelta);
            UsesThisCombat = Mathf.Max(0, UsesThisCombat + pendingActivationUseDelta);
            pendingActivationUseDelta = 0;
        }
        NextReusableTime = Time.time + (Definition != null ? Definition.ReuseDelaySeconds : 0f);

        if (ShouldStartCooldownAfterUse())
        {
            RemainingCooldown = Definition.CooldownTurns;
        }

        if (Definition.ConsumesMovementPoint)
        {
            character.ConsumeMovementPoint();
        }

        if (Definition.IsToggle)
        {
            IsActive = true;
            hasPreparedActivationPendingConsumption = Definition.RefundUseIfDeactivatedWithoutConsumption;
        }

        Definition.PlayActivationAnimation(character);
        Definition.OnAbilityActivated(character, this);

        return true;
    }

    public void RefundTurnUse(int amount = 1)
    {
        if (amount <= 0)
        {
            return;
        }

        UsesThisTurn = Mathf.Max(0, UsesThisTurn - amount);
    }

    public void GrantBonusTurnUse(int amount = 1)
    {
        if (amount <= 0)
        {
            return;
        }

        BonusUsesThisTurn += amount;
    }

    public void QueueActivationUseDelta(int amount)
    {
        pendingActivationUseDelta += amount;
    }

    public bool TryConsumeAutomaticUse()
    {
        if (Definition == null)
        {
            return false;
        }

        if (Definition.UsesPerTurn > 0 && UsesThisTurn >= GetMaxUsesThisTurn())
        {
            return false;
        }

        if (Definition.UsesPerCombat > 0 && UsesThisCombat >= Definition.UsesPerCombat)
        {
            return false;
        }

        UsesThisTurn++;
        UsesThisCombat++;

        if (ShouldStartCooldownAfterUse())
        {
            RemainingCooldown = Definition.CooldownTurns;
        }

        return true;
    }

    public void ResetAvailability()
    {
        UsesThisTurn = 0;
        BonusUsesThisTurn = 0;
        RemainingCooldown = 0;
        NextReusableTime = 0f;
        hasPreparedActivationPendingConsumption = false;
        pendingActivationUseDelta = 0;
    }

    public void ReduceCooldown(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        RemainingCooldown = Mathf.Max(0, RemainingCooldown - amount);
    }

    public void SetRemainingCooldown(int amount)
    {
        RemainingCooldown = Mathf.Max(0, amount);
    }

    public void ConsumePreparedActivation()
    {
        hasPreparedActivationPendingConsumption = false;
    }

    public void AddPendingBaseDamageModifierForNextUse(int amount)
    {
        pendingBaseDamageModifierForNextUse += amount;
    }

    public int ConsumePendingBaseDamageModifierForNextUse()
    {
        int modifier = pendingBaseDamageModifierForNextUse;
        pendingBaseDamageModifierForNextUse = 0;
        return modifier;
    }

    public int PeekPendingBaseDamageModifierForNextUse()
    {
        return pendingBaseDamageModifierForNextUse;
    }

    public void SuppressNextPrimaryEffectOnce()
    {
        suppressNextPrimaryEffectOnce = true;
    }

    public bool ConsumeSuppressNextPrimaryEffectOnce()
    {
        bool shouldSuppress = suppressNextPrimaryEffectOnce;
        suppressNextPrimaryEffectOnce = false;
        return shouldSuppress;
    }

    private int GetMaxUsesThisTurn()
    {
        if (Definition == null)
        {
            return 0;
        }

        return Mathf.Max(0, Definition.UsesPerTurn + definitionBonusUsesThisTurn + BonusUsesThisTurn);
    }

    private bool ShouldStartCooldownAfterUse()
    {
        if (Definition == null || Definition.CooldownTurns <= 0)
        {
            return false;
        }

        if (Definition.UsesPerCombat > 0 && UsesThisCombat >= Definition.UsesPerCombat)
        {
            return true;
        }

        if (Definition.UsesPerTurn > 0)
        {
            return UsesThisTurn >= GetMaxUsesThisTurn();
        }

        return true;
    }
}
