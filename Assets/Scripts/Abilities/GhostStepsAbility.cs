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

    public override int GetTraversalDamage(Character character, CharacterAbilityRuntime runtime, int traversedEnemyCount)
    {
        if (runtime == null || !runtime.IsActive || character == null)
        {
            return 0;
        }

        int damage = traversalDamage;
        int frenzyStacks = character.GetUpgradeStacks(AbilityUpgradeKey.GhostStepsFrenzy);
        if (frenzyStacks > 0 && traversedEnemyCount > 0)
        {
            damage += 2 * frenzyStacks * traversedEnemyCount;
        }

        if (character.GetUpgradeStacks(AbilityUpgradeKey.GhostStepsHeartPiercer) > 0
            && character.RemainingMovementPoints == 1)
        {
            damage += 1;
        }

        return damage;
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
