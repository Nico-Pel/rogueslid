using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Assassin's Rush", fileName = "AssassinsRush")]
public class AssassinsRushAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseRange = 3;
    [SerializeField] private List<UpgradedSecondaryEffectEntry> secondaryEffects = new List<UpgradedSecondaryEffectEntry>();

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null || targetCell == character.GridPosition)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        bool aligned = delta.x == 0 || delta.y == 0;
        if (!aligned)
        {
            return false;
        }

        int range = baseRange + character.GetUpgradeStacks(AbilityUpgradeKey.AssassinsRushShadowPulse);
        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance <= 0 || distance > range || !character.Board.IsCellWalkable(targetCell))
        {
            return false;
        }

        Vector2Int direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        bool canTraverseUnits = character.GetUpgradeStacks(AbilityUpgradeKey.AssassinsRushSpectralForm) > 0;
        Vector2Int scan = character.GridPosition + direction;
        while (scan != targetCell)
        {
            if (!character.Board.TryGetCell(scan, out BoardCell cell) || cell.HasBlockingTerrain)
            {
                return false;
            }

            if (!canTraverseUnits && cell.IsOccupied)
            {
                return false;
            }

            scan += direction;
        }

        return true;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue)
        {
            return false;
        }

        Vector2Int originCell = character.GridPosition;

        if (!character.TryTeleportTo(targetCell.Value))
        {
            return false;
        }

        int tasteStacks = character.GetUpgradeStacks(AbilityUpgradeKey.AssassinsRushTasteOfBlood);
        if (tasteStacks > 0)
        {
            character.AddTemporaryTurnBonusDamage(tasteStacks);
        }

        AbilityExecutionContext context = new AbilityExecutionContext(this, runtime, originCell, targetCell.Value);
        ExecuteUnlockedSecondaryEffects(character, runtime, context, secondaryEffects, SecondaryEffectTiming.AfterMovement);

        PlayConfiguredFx(character);
        return true;
    }
}
