using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Secondary Effects/Single Target Damage", fileName = "SecondaryEffect_SingleTargetDamage")]
public class SingleTargetDamageSecondaryEffectDefinition : SecondaryAbilityEffectDefinition
{
    [Min(1)]
    [SerializeField] private int damage = 1;
    [SerializeField] private bool addBonusDamage;
    [SerializeField] private bool countsAsAbilityDamage = true;

    public override void Execute(Character character, AbilityExecutionContext context)
    {
        if (character == null || context.TargetEnemy == null || context.TargetEnemy.CurrentHealth <= 0)
        {
            return;
        }

        PlayFeedback(character, context);
        character.DealDamageToEnemy(
            context.TargetEnemy,
            damage,
            addBonusDamage,
            countsAsAbilityDamage,
            DamageSoundType.Default,
            context.SourceAbility);
    }
}
