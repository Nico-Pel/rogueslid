using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Zhuang Black Turtle Step")]
public sealed class ZhuangBlackTurtleStepAbility : ZhuangAbility
{
    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int cell)
    {
        if (character == null || character.Board == null || !character.Board.IsCellWalkable(cell)) return false;
        int distance = ZhuangAbilityUtils.GetManhattanDistance(character.GridPosition, cell);
        return distance <= 1 + Stacks(character, AbilityUpgradeKey.ZhuangDoubleStep) && ZhuangAbilityUtils.ClearLine(character.Board, character.GridPosition, cell);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (!targetCell.HasValue || !CanActivateOnCell(character, runtime, targetCell.Value)) return false;
        character.FaceTargetCell(targetCell.Value);
        Cast(character);
        Run(character, Step(character, targetCell.Value));
        return true;
    }

    private IEnumerator Step(Character character, Vector2Int cell)
    {
        yield return Move(character, cell, false);
        Secondary(character, character.transform.position);
        ZhuangTurtleGuard guard = character.GetComponent<ZhuangTurtleGuard>();
        if (guard == null) guard = character.gameObject.AddComponent<ZhuangTurtleGuard>();
        guard.Activate(character, this);
    }
}
