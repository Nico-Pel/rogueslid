using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Bonus Void Glove", fileName = "BonusVoidGlove")]
public class BonusVoidGloveAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int range = 5;

    public override bool IsBonusAbility => true;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        return character.Board.IsInsideBoard(targetCell)
            && character.Board.IsCellWalkable(targetCell)
            && GetChebyshevDistance(character.GridPosition, targetCell) <= range
            && TrySelectPullTarget(character, targetCell, out _);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanActivateOnCell(character, runtime, targetCell);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || !targetCell.HasValue || !TrySelectPullTarget(character, targetCell.Value, out Enemy enemy) || enemy == null)
        {
            return false;
        }

        return enemy.TryForcedMoveTo(targetCell.Value);
    }

    private bool TrySelectPullTarget(Character character, Vector2Int targetCell, out Enemy selectedEnemy)
    {
        selectedEnemy = null;
        if (character == null || character.Board == null)
        {
            return false;
        }

        if (!character.Board.TryGetCell(targetCell, out BoardCell cell) || cell.HasBlockingTerrain || cell.IsOccupied)
        {
            return false;
        }

        List<Enemy> candidates = new List<Enemy>();
        for (int deltaX = -1; deltaX <= 1; deltaX++)
        {
            for (int deltaY = -1; deltaY <= 1; deltaY++)
            {
                if (deltaX == 0 && deltaY == 0)
                {
                    continue;
                }

                Vector2Int candidateCell = targetCell + new Vector2Int(deltaX, deltaY);
                if (character.Board.TryGetEnemy(candidateCell, out Enemy enemy) && enemy != null)
                {
                    candidates.Add(enemy);
                }
            }
        }

        if (candidates.Count <= 0)
        {
            return false;
        }

        candidates.Sort((left, right) =>
        {
            int healthComparison = left.CurrentHealth.CompareTo(right.CurrentHealth);
            if (healthComparison != 0)
            {
                return healthComparison;
            }

            int leftDistance = Mathf.Abs(left.GridPosition.x - character.GridPosition.x) + Mathf.Abs(left.GridPosition.y - character.GridPosition.y);
            int rightDistance = Mathf.Abs(right.GridPosition.x - character.GridPosition.x) + Mathf.Abs(right.GridPosition.y - character.GridPosition.y);
            int distanceComparison = leftDistance.CompareTo(rightDistance);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            return Random.Range(-1, 2);
        });

        selectedEnemy = candidates[0];
        return selectedEnemy != null;
    }

    private static int GetChebyshevDistance(Vector2Int from, Vector2Int to)
    {
        return Mathf.Max(Mathf.Abs(from.x - to.x), Mathf.Abs(from.y - to.y));
    }
}
