using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Misty Spirit", fileName = "MistySpirit")]
public class MistySpiritAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseRange = 5;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        int range = baseRange + character.GetUpgradeStacks(AbilityUpgradeKey.MistySpiritMistDispersion);
        Vector2Int delta = targetCell - character.GridPosition;
        int radialDistance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (radialDistance <= 0 || radialDistance > range)
        {
            return false;
        }

        return character.Board.TryGetEnemy(targetCell, out Enemy enemy)
            && enemy != null
            && enemy.CanAttackAnyEnemyAlly();
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null)
        {
            return false;
        }

        int range = baseRange + character.GetUpgradeStacks(AbilityUpgradeKey.MistySpiritMistDispersion);
        Vector2Int delta = targetCell - character.GridPosition;
        int radialDistance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        return radialDistance > 0 && radialDistance <= range;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || character.Board == null || !targetCell.HasValue)
        {
            return false;
        }

        if (!character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) || enemy == null || !enemy.CanAttackAnyEnemyAlly())
        {
            return false;
        }

        character.FaceTargetCell(targetCell.Value);
        enemy.ApplyMistyConfusion(
            character.GetUpgradeStacks(AbilityUpgradeKey.MistySpiritParanoia) > 0,
            character.GetUpgradeStacks(AbilityUpgradeKey.MistySpiritBrokenHeart) > 0,
            character.GetUpgradeStacks(AbilityUpgradeKey.MistySpiritUnsteadySteps) > 0);
        PlayConfiguredFx(character, new[] { enemy });
        return true;
    }
}
