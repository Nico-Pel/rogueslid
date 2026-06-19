using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Death Mark", fileName = "DeathMark")]
public class DeathMarkAbility : AbilityDefinition
{
    [SerializeField] private SecondaryAbilityEffectDefinition struckByFearEffect;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        return (character.Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null)
            || (character.Board.TryGetLichSkullObject(targetCell, out LichSkullObject lichSkull) && lichSkull != null);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return character != null
            && character.Board != null
            && character.Board.IsInsideBoard(targetCell);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || !targetCell.HasValue || character.Board == null)
        {
            return false;
        }

        if (character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) && enemy != null)
        {
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
                    character.DealDamageToEnemy(enemy, struckStacks, false, true, DamageSoundType.Default, this);
                }
            }

            if (character.GetUpgradeStacks(AbilityUpgradeKey.DeathMarkPetrify) > 0)
            {
                enemy.ApplyMobilityPenaltyNextTurn(enemy.Mobility);
            }

            PlayConfiguredFx(character, new[] { enemy });
            return true;
        }

        if (character.Board.TryGetLichSkullObject(targetCell.Value, out LichSkullObject lichSkull) && lichSkull != null)
        {
            character.ApplyDeathMark(lichSkull, this);
            int struckStacks = character.GetUpgradeStacks(AbilityUpgradeKey.DeathMarkStruckByFear);
            if (struckStacks > 0)
            {
                character.DealDamageToLichSkull(lichSkull, struckStacks, false, DamageSoundType.Default, this);
            }

            PlayConfiguredFx(character);
            return true;
        }

        return false;
    }
}
