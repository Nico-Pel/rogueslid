using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Secondary Effects/Radial Damage", fileName = "SecondaryEffect_RadialDamage")]
public class RadialDamageSecondaryEffectDefinition : SecondaryAbilityEffectDefinition
{
    [SerializeField] private SecondaryEffectAnchor anchor = SecondaryEffectAnchor.CharacterCurrentCell;
    [Min(1)]
    [SerializeField] private int range = 1;
    [Min(1)]
    [SerializeField] private int damage = 2;
    [SerializeField] private bool includeDiagonals = true;

    public override void Execute(Character character, AbilityExecutionContext context)
    {
        if (character == null)
        {
            return;
        }

        PlayFeedback(character, context);
        Vector2Int centerCell = ResolveAnchorCell(character, context, anchor);
        character.DamageEnemiesAround(centerCell, range, damage, includeDiagonals, context.SourceAbility);
    }
}
