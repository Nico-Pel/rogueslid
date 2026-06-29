using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Thief's Dagger", fileName = "ThiefsDagger")]
public class ThiefsDaggerAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 5;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        int range = 1 + (character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.RoyalDaggerLightStrike) : 0);
        int damage = baseDamage + (2 * (character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.RoyalDaggerBlessedBlade) : 0));
        return $"Strike a single enemy in a straight line within range {range} for {damage} damage.";
    }

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        bool isOrthogonal = delta.x == 0 || delta.y == 0;
        if (!isOrthogonal)
        {
            return false;
        }

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        int range = 1 + character.GetUpgradeStacks(AbilityUpgradeKey.RoyalDaggerLightStrike);
        if (distance <= 0 || distance > range)
        {
            return false;
        }

        Vector2Int direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        Vector2Int scan = character.GridPosition + direction;
        while (scan != targetCell)
        {
            if (!character.Board.TryGetCell(scan, out BoardCell cell) || cell.HasBlockingTerrain || cell.IsOccupied)
            {
                return false;
            }

            scan += direction;
        }

        return (character.Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null)
            || (character.Board.TryGetBarrel(targetCell, out BarrelObstacle barrel) && barrel != null)
            || (character.Board.TryGetLichSkullObject(targetCell, out LichSkullObject lichSkull) && lichSkull != null);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        bool isOrthogonal = delta.x == 0 || delta.y == 0;
        if (!isOrthogonal)
        {
            return false;
        }

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        int range = 1 + character.GetUpgradeStacks(AbilityUpgradeKey.RoyalDaggerLightStrike);
        if (distance <= 0 || distance > range)
        {
            return false;
        }

        Vector2Int direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        Vector2Int scan = character.GridPosition + direction;
        while (scan != targetCell)
        {
            if (!character.Board.TryGetCell(scan, out BoardCell cell) || cell.HasBlockingTerrain || cell.IsOccupied)
            {
                return false;
            }

            scan += direction;
        }

        return character.Board.IsInsideBoard(targetCell);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue || character.Board == null)
        {
            return false;
        }

        character.FaceTargetCell(targetCell.Value);
        PlayActivationAnimation(character);

        if (character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) && enemy != null)
        {
            int damage = baseDamage;
            damage += 2 * character.GetUpgradeStacks(AbilityUpgradeKey.RoyalDaggerBlessedBlade);
            if (character.GetUpgradeStacks(AbilityUpgradeKey.RoyalDaggerHolyBlade) > 0
                && enemy.CurrentHealth >= enemy.MaxHealth)
            {
                damage += 2;
            }

            int blessingStacks = character.GetUpgradeStacks(AbilityUpgradeKey.RoyalDaggerRoyalBlessing);
            character.DealDamageToEnemyWithAbilityTiming(
                this,
                enemy,
                damage,
                true,
                true,
                DamageSoundType.Sword,
                null,
                this,
                (targetEnemy, appliedDamage) =>
                {
                    if (targetEnemy != null && targetEnemy.CurrentHealth <= 0 && blessingStacks > 0)
                    {
                        character.Heal(2 * blessingStacks);
                    }
                });
            PlayConfiguredFx(character, new[] { enemy });

            bool targetWasNewThisTurn = character.MarkAbilityTargetHitThisTurn(this, enemy);
            if (character.GetUpgradeStacks(AbilityUpgradeKey.RoyalDaggerRoyalPunishment) > 0
                && targetWasNewThisTurn
                && HasAnotherUnhitTargetInRange(character, enemy))
            {
                runtime.QueueActivationUseDelta(-1);
            }

            return true;
        }

        if (character.Board.TryGetBarrel(targetCell.Value, out BarrelObstacle barrel) && barrel != null)
        {
            character.DealDamageToBarrelWithAbilityTiming(this, barrel);
            PlayConfiguredFx(character);
            return true;
        }

        if (character.Board.TryGetLichSkullObject(targetCell.Value, out LichSkullObject lichSkull) && lichSkull != null)
        {
            int damage = baseDamage + 2 * character.GetUpgradeStacks(AbilityUpgradeKey.RoyalDaggerBlessedBlade);
            character.DealDamageToLichSkullWithAbilityTiming(this, lichSkull, damage, true, DamageSoundType.Sword, this);
            PlayConfiguredFx(character);
            return true;
        }

        return false;
    }

    private bool HasAnotherUnhitTargetInRange(Character character, Enemy currentTarget)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        int range = 1 + character.GetUpgradeStacks(AbilityUpgradeKey.RoyalDaggerLightStrike);
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            Vector2Int direction = directions[directionIndex];
            for (int step = 1; step <= range; step++)
            {
                Vector2Int cell = character.GridPosition + (direction * step);
                if (!character.Board.TryGetCell(cell, out BoardCell boardCell) || boardCell.HasBlockingTerrain)
                {
                    break;
                }

                if (character.Board.TryGetEnemy(cell, out Enemy enemy) && enemy != null)
                {
                    if (enemy != currentTarget && !character.HasAbilityTargetHitThisTurn(this, enemy))
                    {
                        return true;
                    }

                    break;
                }

                if (boardCell.IsOccupied)
                {
                    break;
                }
            }
        }

        return false;
    }
}
