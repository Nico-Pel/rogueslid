using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Swift Step", fileName = "SwiftStep")]
public class SwiftStepAbility : SideStepAbility
{
    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        return character != null && runtime != null;
    }

    public override bool TryActivateFromSelectedCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanActivateFromSelectedCell(character, runtime, targetCell) && character.TryTeleportTo(targetCell);
    }
}
