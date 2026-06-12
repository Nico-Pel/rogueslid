using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Nighttime Menace", fileName = "NighttimeMenace")]
public class NighttimeMenaceAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseRange = 3;
    [SerializeField] private List<UpgradedSecondaryEffectEntry> secondaryEffects = new List<UpgradedSecondaryEffectEntry>();

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return character != null
            && character.Board != null
            && character.Board.IsCellWalkable(targetCell)
            && Mathf.Max(
                Mathf.Abs(targetCell.x - character.GridPosition.x),
                Mathf.Abs(targetCell.y - character.GridPosition.y))
                <= baseRange + character.GetUpgradeStacks(AbilityUpgradeKey.NighttimeMenaceShadowArea);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue)
        {
            return false;
        }

        Vector2Int previousCell = character.GridPosition;
        AbilityExecutionContext context = new AbilityExecutionContext(this, runtime, previousCell, targetCell.Value);
        ExecuteUnlockedSecondaryEffects(character, runtime, context, secondaryEffects, SecondaryEffectTiming.BeforeMovement);
        bool success = character.TryTeleportTo(targetCell.Value);

        if (success)
        {
            ExecuteUnlockedSecondaryEffects(character, runtime, context, secondaryEffects, SecondaryEffectTiming.AfterMovement);

            PlayConfiguredFx(character);
        }

        return success;
    }
}
