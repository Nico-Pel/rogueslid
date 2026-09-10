using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Bonus Void Glove", fileName = "BonusVoidGlove")]
public class BonusVoidGloveAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int range = 5;

    public override bool IsBonusAbility => true;
    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || character.Board == null) return false;
        for (int x = 0; x < character.Board.Width; x++)
        for (int y = 0; y < character.Board.Height; y++)
            if (CanActivateOnCell(character, runtime, new Vector2Int(x, y))) return true;
        return false;
    }

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
        if (!targetCell.HasValue || !CanActivateOnCell(character, runtime, targetCell.Value)
            || !TrySelectPullTarget(character, targetCell.Value, out Enemy enemy, randomizeTies: true))
        {
            return false;
        }

        if (!enemy.TryForcedMoveTo(targetCell.Value)) return false;
        character.FaceTargetCell(targetCell.Value);
        PlayConfiguredFx(character);
        return true;
    }

    private bool TrySelectPullTarget(Character character, Vector2Int targetCell, out Enemy selectedEnemy, bool randomizeTies = false)
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

        int bestHealth = int.MaxValue;
        int bestDistance = int.MaxValue;
        int ties = 0;
        for (int deltaX = -1; deltaX <= 1; deltaX++)
        {
            for (int deltaY = -1; deltaY <= 1; deltaY++)
            {
                if (deltaX == 0 && deltaY == 0)
                {
                    continue;
                }

                Vector2Int candidateCell = targetCell + new Vector2Int(deltaX, deltaY);
                if (character.Board.TryGetEnemy(candidateCell, out Enemy enemy) && enemy != null && enemy.CurrentHealth > 0)
                {
                    int distance = Mathf.Abs(candidateCell.x - character.GridPosition.x) + Mathf.Abs(candidateCell.y - character.GridPosition.y);
                    if (enemy.CurrentHealth < bestHealth || (enemy.CurrentHealth == bestHealth && distance < bestDistance))
                    {
                        selectedEnemy = enemy;
                        bestHealth = enemy.CurrentHealth;
                        bestDistance = distance;
                        ties = 1;
                    }
                    else if (enemy.CurrentHealth == bestHealth && distance == bestDistance)
                    {
                        ties++;
                        // Preview checks must not consume randomness; choose fairly only on use.
                        if (randomizeTies && Random.Range(0, ties) == 0) selectedEnemy = enemy;
                    }
                }
            }
        }

        return selectedEnemy != null;
    }

    private static int GetChebyshevDistance(Vector2Int from, Vector2Int to)
    {
        return Mathf.Max(Mathf.Abs(from.x - to.x), Mathf.Abs(from.y - to.y));
    }
}
