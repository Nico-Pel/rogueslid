using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Side Step", fileName = "SideStep")]
public class SideStepAbility : AbilityDefinition
{
    public override bool KeepsActiveStateBetweenTurns => false;

    public override bool LimitsNextSlideToOneCell(Character character, CharacterAbilityRuntime runtime)
    {
        return runtime != null && runtime.IsActive;
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
