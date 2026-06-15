using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Thief's Dagger", fileName = "ThiefsDagger")]
public class ThiefsDaggerAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 5;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        bool isOrthogonallyAdjacent = Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
        if (!isOrthogonallyAdjacent)
        {
            return false;
        }

        return character.Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue || character.Board == null)
        {
            return false;
        }

        if (!character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) || enemy == null)
        {
            return false;
        }

        PlayActivationAnimation(character);
        character.DealDamageToEnemy(enemy, baseDamage, true, true, DamageSoundType.Sword);
        PlayConfiguredFx(character, new[] { enemy });
        return true;
    }
}
