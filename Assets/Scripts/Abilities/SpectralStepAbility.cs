using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Spectral Step", fileName = "SpectralStep")]
public class SpectralStepAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int maxCharges = 1;
    [Min(0)]
    [SerializeField] private int healAmount = 1;

    public int MaxCharges => Mathf.Max(1, maxCharges);

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        return $"Pandora passes through an adjacent enemy and recovers {healAmount} HP.";
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        for (int index = 0; index < directions.Length; index++)
        {
            Vector2Int targetCell = character.GridPosition + directions[index];
            if (CanActivateOnCell(character, runtime, targetCell))
            {
                return true;
            }
        }

        if (character.GetUpgradeStacks(AbilityUpgradeKey.SpectralClawsSpectralCrossing) > 0)
        {
            Vector2Int[] diagonalDirections =
            {
                new Vector2Int(1, 1),
                new Vector2Int(1, -1),
                new Vector2Int(-1, -1),
                new Vector2Int(-1, 1)
            };

            for (int index = 0; index < diagonalDirections.Length; index++)
            {
                Vector2Int targetCell = character.GridPosition + diagonalDirections[index];
                if (CanActivateOnCell(character, runtime, targetCell))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        if (!TryGetTargetDirection(character, targetCell, out Vector2Int direction))
        {
            return false;
        }

        if (!character.Board.TryGetEnemy(targetCell, out Enemy enemy) || enemy == null)
        {
            return false;
        }

        return TryGetPassThroughDestination(character, enemy.GridPosition, direction, out _);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        return TryGetTargetDirection(character, targetCell, out _)
            && character.Board.IsInsideBoard(targetCell);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue || character.Board == null)
        {
            return false;
        }

        if (!TryGetTargetDirection(character, targetCell.Value, out Vector2Int direction))
        {
            return false;
        }

        if (!character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) || enemy == null)
        {
            return false;
        }

        if (!TryGetPassThroughDestination(character, enemy.GridPosition, direction, out Vector2Int passThroughCell))
        {
            return false;
        }

        character.FaceTargetCell(targetCell.Value);
        character.StartCoroutine(ResolveSpectralStep(character, passThroughCell));
        return true;
    }

    private IEnumerator ResolveSpectralStep(Character character, Vector2Int passThroughCell)
    {
        if (character == null)
        {
            yield break;
        }

        character.BeginActionLock();
        bool success = character.TryTeleportTo(passThroughCell);
        if (!success)
        {
            character.EndActionLock();
            yield break;
        }

        yield return new WaitUntil(() => !character.IsMoving);

        if (healAmount > 0)
        {
            int previousHealth = character.CurrentHealth;
            character.Heal(healAmount);
            if (character.CurrentHealth > previousHealth)
            {
                SoundManager.Instance?.PlayHeal(character.transform.position);
            }
        }

        PlayConfiguredFx(character);
        character.EndActionLock();
    }

    private bool TryGetTargetDirection(Character character, Vector2Int targetCell, out Vector2Int direction)
    {
        direction = Vector2Int.zero;
        if (character == null)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        int absX = Mathf.Abs(delta.x);
        int absY = Mathf.Abs(delta.y);
        bool isOrthogonalAdjacent = (absX == 1 && absY == 0) || (absX == 0 && absY == 1);
        bool isDiagonalAdjacent = absX == 1 && absY == 1
            && character.GetUpgradeStacks(AbilityUpgradeKey.SpectralClawsSpectralCrossing) > 0;
        if (!isOrthogonalAdjacent && !isDiagonalAdjacent)
        {
            return false;
        }

        direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        return direction != Vector2Int.zero;
    }

    private bool TryGetPassThroughDestination(Character character, Vector2Int enemyCell, Vector2Int direction, out Vector2Int destination)
    {
        destination = enemyCell + direction;
        return character != null
            && character.Board != null
            && character.Board.IsCellWalkable(destination);
    }
}
