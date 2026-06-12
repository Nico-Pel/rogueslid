using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Side Step", fileName = "SideStep")]
public class SideStepAbility : AbilityDefinition
{
    public override bool KeepsActiveStateBetweenTurns => false;
    public override bool RefundUseIfDeactivatedWithoutConsumption => true;

    public override bool LimitsNextSlideToOneCell(Character character, CharacterAbilityRuntime runtime)
    {
        return runtime != null && runtime.IsActive;
    }

    public override bool SupportsCellSelectionWhileActive(Character character, CharacterAbilityRuntime runtime)
    {
        return runtime != null && runtime.IsActive;
    }

    public override bool CanActivateFromSelectedCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || !runtime.IsActive || character.Board == null)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1 && character.Board.IsCellWalkable(targetCell);
    }

    public override bool TryActivateFromSelectedCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (!CanActivateFromSelectedCell(character, runtime, targetCell) || !character.TryTeleportTo(targetCell))
        {
            return false;
        }

        character.ConsumeMovementPoint();
        return true;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null)
        {
            return false;
        }

        PlayConfiguredFx(character);
        return true;
    }
}
