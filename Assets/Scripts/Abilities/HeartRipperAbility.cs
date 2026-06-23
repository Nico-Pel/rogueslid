using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Heart Ripper", fileName = "HeartRipper")]
public class HeartRipperAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 3;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        if (!character.Board.TryGetEnemy(targetCell, out Enemy enemy) || enemy == null)
        {
            return false;
        }

        return TryGetLineInfo(character, targetCell, out _, out _);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return TryGetLineInfo(character, targetCell, out _, out _);
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
        if (character == null || character.Board == null || !targetCell.HasValue || !TryGetLineInfo(character, targetCell.Value, out Vector2Int direction, out int distance))
        {
            return false;
        }

        if (!character.Board.TryGetEnemy(targetCell.Value, out Enemy primaryTarget) || primaryTarget == null)
        {
            return false;
        }

        character.FaceTargetCell(targetCell.Value);

        int damage = baseDamage + character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperHookedFist);
        if (character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperDemonicDuel) > 0 && CountLivingEnemies(character.Board) == 1)
        {
            damage += 1;
        }

        int healAmountPerKill = 2 + character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperRevigoratingHeart);
        int totalHeal = 0;

        if (character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperDemonicHand) > 0 && distance == 2)
        {
            Vector2Int middleCell = character.GridPosition + direction;
            if (character.Board.TryGetEnemy(middleCell, out Enemy middleEnemy) && middleEnemy != null)
            {
                character.DealDamageToEnemy(middleEnemy, damage, true, true, DamageSoundType.Sword, this);
                if (middleEnemy.CurrentHealth <= 0)
                {
                    totalHeal += healAmountPerKill;
                }
            }
        }

        character.DealDamageToEnemy(primaryTarget, damage, true, true, DamageSoundType.Sword, this);
        if (primaryTarget.CurrentHealth <= 0)
        {
            totalHeal += healAmountPerKill;
        }

        if (totalHeal > 0)
        {
            character.Heal(totalHeal);
        }

        if (primaryTarget.CurrentHealth > 0)
        {
            character.TakeDamage(2, null, false, DamageSoundType.Default);
        }

        PlayConfiguredFx(character, new[] { primaryTarget });
        return true;
    }

    private bool TryGetLineInfo(Character character, Vector2Int targetCell, out Vector2Int direction, out int distance)
    {
        direction = Vector2Int.zero;
        distance = 0;
        if (character == null || character.Board == null)
        {
            return false;
        }

        int maxRange = 1 + (character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperDemonicHand) > 0 ? 1 : 0);
        if (!HectorAbilityUtils.TryResolveAlignedDirection(character.GridPosition, targetCell, false, maxRange, out direction, out distance))
        {
            return false;
        }

        if (distance == 2)
        {
            Vector2Int middleCell = character.GridPosition + direction;
            if (!character.Board.TryGetCell(middleCell, out BoardCell boardCell) || boardCell.HasBlockingTerrain)
            {
                return false;
            }
        }

        return distance == 1 || distance == 2;
    }

    private int CountLivingEnemies(BoardManager board)
    {
        if (board == null)
        {
            return 0;
        }

        int count = 0;
        IReadOnlyList<Enemy> enemies = board.SpawnedEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            if (enemies[index] != null && enemies[index].CurrentHealth > 0)
            {
                count++;
            }
        }

        return count;
    }
}
