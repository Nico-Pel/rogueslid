using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Secondary Effects/Single Target Damage", fileName = "SecondaryEffect_SingleTargetDamage")]
public class SingleTargetDamageSecondaryEffectDefinition : SecondaryAbilityEffectDefinition
{
    [Min(1)]
    [SerializeField] private int damage = 1;
    [SerializeField] private bool addBonusDamage;
    [SerializeField] private bool countsAsAbilityDamage = true;
    [Min(0f)]
    [SerializeField] private float impactDelay;

    public override void Execute(Character character, AbilityExecutionContext context)
    {
        if (character == null || context.TargetEnemy == null || context.TargetEnemy.CurrentHealth <= 0)
        {
            return;
        }

        if (impactDelay <= 0f)
        {
            ApplyDamage(character, context);
            return;
        }

        character.StartCoroutine(ApplyDamageAfterDelay(character, context));
    }

    private System.Collections.IEnumerator ApplyDamageAfterDelay(Character character, AbilityExecutionContext context)
    {
        yield return new WaitForSeconds(impactDelay);
        ApplyDamage(character, context);
    }

    private void ApplyDamage(Character character, AbilityExecutionContext context)
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
