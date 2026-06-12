using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Death Mark", fileName = "DeathMark")]
public class DeathMarkAbility : AbilityDefinition
{
    [SerializeField] private SecondaryAbilityEffectDefinition struckByFearEffect;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return character != null
            && character.Board != null
            && character.Board.TryGetEnemy(targetCell, out Enemy enemy)
            && enemy != null;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || !targetCell.HasValue || character.Board == null)
        {
            return false;
        }

        if (!character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) || enemy == null)
        {
            return false;
        }

        character.ApplyDeathMark(enemy, this);
        int struckStacks = character.GetUpgradeStacks(AbilityUpgradeKey.DeathMarkStruckByFear);
        if (struckStacks > 0)
        {
            AbilityExecutionContext struckContext = new AbilityExecutionContext(
                this,
                runtime,
                character.GridPosition,
                enemy.GridPosition,
                enemy);

            if (struckByFearEffect != null)
            {
                for (int index = 0; index < struckStacks; index++)
                {
                    struckByFearEffect.Execute(character, struckContext);
                }
            }
            else
            {
                character.DealDamageToEnemy(enemy, struckStacks, false, true);
            }
        }

        if (character.GetUpgradeStacks(AbilityUpgradeKey.DeathMarkPetrify) > 0)
        {
            enemy.ApplyMobilityPenaltyNextTurn(enemy.Mobility);
        }

        PlayConfiguredFx(character, new[] { enemy });
        return true;
    }
}
