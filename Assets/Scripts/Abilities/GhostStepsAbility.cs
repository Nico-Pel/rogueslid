using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Ghost Steps", fileName = "GhostSteps")]
public class GhostStepsAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int traversalDamage = 3;

    public override bool AllowsUnitTraversal(Character character, CharacterAbilityRuntime runtime)
    {
        return runtime != null && runtime.IsActive;
    }

    public override int GetTraversalDamage(Character character, CharacterAbilityRuntime runtime)
    {
        return runtime != null && runtime.IsActive ? traversalDamage : 0;
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
