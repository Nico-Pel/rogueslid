using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Nighttime Menace", fileName = "NighttimeMenace")]
public class NighttimeMenaceAbility : AbilityDefinition
{
    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return character != null
            && character.Board != null
            && character.Board.IsCellWalkable(targetCell);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        return character != null
            && targetCell.HasValue
            && character.TryTeleportTo(targetCell.Value);
    }
}
