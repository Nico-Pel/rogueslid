using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Bonus Melee Knife", fileName = "BonusMeleeKnife")]
public class BonusMeleeKnifeAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 2;
    [Min(1)]
    [SerializeField] private int maxDamage = 5;

    public override bool IsBonusAbility => true;
    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        IReadOnlyList<Enemy> enemies = character.Board.SpawnedEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy enemy = enemies[index];
            if (enemy != null && enemy.CurrentHealth > 0 && CanActivateOnCell(character, runtime, enemy.GridPosition))
            {
                return true;
            }
        }

        return false;
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
        {
            return false;
        }

        return character.Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null;
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanActivateOnCell(character, runtime, targetCell);
    }

    public override bool ShouldAutoUseWhenOnlyOneValidTarget(Character character, CharacterAbilityRuntime runtime)
    {
        return true;
    }

    public override bool TryGetAutomaticTargetCell(Character character, CharacterAbilityRuntime runtime, out Vector2Int targetCell)
    {
        targetCell = default;
        if (character == null || character.Board == null)
        {
            return false;
        }

        Vector2Int singleValidCell = default;
        int validTargetCount = 0;
        IReadOnlyList<Enemy> enemies = character.Board.SpawnedEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy enemy = enemies[index];
            if (enemy == null || enemy.CurrentHealth <= 0 || !CanActivateOnCell(character, runtime, enemy.GridPosition))
            {
                continue;
            }

            validTargetCount++;
            if (validTargetCount > 1)
            {
                return false;
            }

            singleValidCell = enemy.GridPosition;
        }

        if (validTargetCount != 1)
        {
            return false;
        }

        targetCell = singleValidCell;
        return true;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || !targetCell.HasValue || !character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) || enemy == null)
        {
            return false;
        }

        character.FaceTargetCell(targetCell.Value);
        int appliedDamage = character.DealDamageToEnemy(enemy, GetDamage(character), true, true, DamageSoundType.Sword, this);
        if (appliedDamage > 0 && enemy.CurrentHealth <= 0)
        {
            character.SetBonusAbilityPersistentValue(GetPersistentKey(), Mathf.Min(maxDamage - baseDamage, GetStoredBonus(character) + 1));
        }

        return true;
    }

    public override string GetCounterText(CharacterAbilityRuntime runtime)
    {
        return string.Empty;
    }

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        return $"Strike an adjacent enemy for {GetDamage(character)} damage. Gains +1 persistent damage on kill during this run, up to {maxDamage}.";
    }

    private int GetDamage(Character character)
    {
        return Mathf.Clamp(baseDamage + GetStoredBonus(character), baseDamage, maxDamage);
    }

    private int GetStoredBonus(Character character)
    {
        return character != null ? character.GetBonusAbilityPersistentValue(GetPersistentKey()) : 0;
    }

    private string GetPersistentKey()
    {
        return $"{name}_damage_bonus";
    }
}
