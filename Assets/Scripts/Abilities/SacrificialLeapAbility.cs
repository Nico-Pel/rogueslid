using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Sacrificial Leap", fileName = "SacrificialLeap")]
public class SacrificialLeapAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 4;
    [Min(1)]
    [SerializeField] private int baseRange = 2;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        int range = baseRange + (character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.SacrificialLeapSacrificialWind) : 0);
        int executeThreshold = GetBaseDamage(character, runtime, false);
        return $"Execute an enemy with {executeThreshold} HP or less and leap onto its tile. Range {range}.";
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        int distance = Mathf.Abs(targetCell.x - character.GridPosition.x) + Mathf.Abs(targetCell.y - character.GridPosition.y);
        int range = baseRange + character.GetUpgradeStacks(AbilityUpgradeKey.SacrificialLeapSacrificialWind);
        if (distance <= 0 || distance > range)
        {
            return false;
        }

        if (character.Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null)
        {
            int projectedDamage = GetProjectedDamageAgainstEnemy(character, runtime, enemy);
            return enemy.CurrentHealth <= projectedDamage;
        }

        if (character.Board.TryGetLichSkullObject(targetCell, out LichSkullObject lichSkull) && lichSkull != null)
        {
            int projectedDamage = GetProjectedDamageAgainstLichSkull(character, runtime, lichSkull);
            return lichSkull.CurrentHealth <= projectedDamage;
        }

        return false;
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        int distance = Mathf.Abs(targetCell.x - character.GridPosition.x) + Mathf.Abs(targetCell.y - character.GridPosition.y);
        int range = baseRange + character.GetUpgradeStacks(AbilityUpgradeKey.SacrificialLeapSacrificialWind);
        return distance > 0
            && distance <= range
            && character.Board.IsInsideBoard(targetCell);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue || character.Board == null)
        {
            return false;
        }

        character.FaceTargetCell(targetCell.Value);
        if (character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) && enemy != null)
        {
            int enemyHealthBeforeHit = enemy.CurrentHealth;
            int appliedDamage = character.DealDamageToEnemy(enemy, GetBaseDamage(character, runtime, true), true, true, DamageSoundType.MagicHit, this);
            if (appliedDamage <= 0 || enemy.CurrentHealth > 0)
            {
                return false;
            }

            if (!character.TryTeleportTo(targetCell.Value))
            {
                return false;
            }

            if (character.GetUpgradeStacks(AbilityUpgradeKey.SacrificialLeapBloodthirst) > 0)
            {
                character.Heal(enemyHealthBeforeHit);
            }

            if (character.GetUpgradeStacks(AbilityUpgradeKey.SacrificialLeapSacrificialRite) > 0)
            {
                runtime.GrantBonusTurnUse(1);
                runtime.AddPendingBaseDamageModifierForNextUse(-2);
            }

            PlayConfiguredFx(character);
            return true;
        }

        if (character.Board.TryGetLichSkullObject(targetCell.Value, out LichSkullObject lichSkull) && lichSkull != null)
        {
            int skullHealthBeforeHit = lichSkull.CurrentHealth;
            int appliedDamage = character.DealDamageToLichSkull(lichSkull, GetBaseDamage(character, runtime, true), true, DamageSoundType.MagicHit, this);
            if (appliedDamage <= 0 || lichSkull == null || !lichSkull.IsResolving)
            {
                return false;
            }

            if (!character.TryTeleportTo(targetCell.Value))
            {
                return false;
            }

            if (character.GetUpgradeStacks(AbilityUpgradeKey.SacrificialLeapBloodthirst) > 0)
            {
                character.Heal(skullHealthBeforeHit);
            }

            if (character.GetUpgradeStacks(AbilityUpgradeKey.SacrificialLeapSacrificialRite) > 0)
            {
                runtime.GrantBonusTurnUse(1);
                runtime.AddPendingBaseDamageModifierForNextUse(-2);
            }

            PlayConfiguredFx(character);
            return true;
        }

        return false;
    }

    private int GetProjectedDamageAgainstEnemy(Character character, CharacterAbilityRuntime runtime, Enemy enemy)
    {
        if (character == null || enemy == null)
        {
            return 0;
        }

        int rawDamage = GetBaseDamage(character, runtime, false) + character.BonusDamage;
        if (character.HasItem(ItemRewardKey.PumpkinHead))
        {
            rawDamage += 1;
        }

        if (character.HasItem(ItemRewardKey.ScopeGlasses))
        {
            int distance = Mathf.Abs(enemy.GridPosition.x - character.GridPosition.x) + Mathf.Abs(enemy.GridPosition.y - character.GridPosition.y);
            if (distance >= 3)
            {
                rawDamage += 1;
            }
        }

        return Mathf.Max(1, rawDamage - enemy.Resistance);
    }

    private int GetProjectedDamageAgainstLichSkull(Character character, CharacterAbilityRuntime runtime, LichSkullObject lichSkull)
    {
        if (character == null || lichSkull == null)
        {
            return 0;
        }

        int rawDamage = GetBaseDamage(character, runtime, false) + character.BonusDamage;
        rawDamage += GetDistanceBonusDamage(character, lichSkull.GridPosition);
        if (character.HasItem(ItemRewardKey.PumpkinHead))
        {
            rawDamage += 1;
        }

        return Mathf.Max(1, rawDamage);
    }

    private int GetDistanceBonusDamage(Character character, Vector2Int targetCell)
    {
        if (character == null || !character.HasItem(ItemRewardKey.ScopeGlasses))
        {
            return 0;
        }

        int distance = Mathf.Abs(targetCell.x - character.GridPosition.x) + Mathf.Abs(targetCell.y - character.GridPosition.y);
        return distance >= 3 ? 1 : 0;
    }

    private int GetBaseDamage(Character character, CharacterAbilityRuntime runtime, bool consumePendingModifier)
    {
        int damage = baseDamage + character.GetUpgradeStacks(AbilityUpgradeKey.SacrificialLeapSacrificialBlade);
        damage += consumePendingModifier
            ? runtime.ConsumePendingBaseDamageModifierForNextUse()
            : runtime.PeekPendingBaseDamageModifierForNextUse();
        return Mathf.Max(1, damage);
    }
}
