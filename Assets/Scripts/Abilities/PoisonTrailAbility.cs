using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Poison Trail", fileName = "PoisonTrail")]
public class PoisonTrailAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseRange = 4;
    [Min(1)]
    [SerializeField] private int poisonCellLifetimeInPlayerTurns = 1;
    [Min(1)]
    [SerializeField] private int poisonStatusDurationInTurns = 1;
    [SerializeField] private GameObject fxPoisonField;
    [SerializeField] private GameObject dashSoundParametersPrefab;

    private readonly Dictionary<CharacterAbilityRuntime, int> remainingRangeBudgetByRuntime = new Dictionary<CharacterAbilityRuntime, int>();

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        int range = GetMaxRange(character);
        int poisonFieldDuration = GetPoisonFieldLifetime(character);
        bool hasReuse = character != null && character.GetUpgradeStacks(AbilityUpgradeKey.PoisonTrailToxicPath) > 0;
        return $"Pandora se deplace jusqu'a {range} cases en ligne droite en laissant des cases empoisonnees pendant {poisonFieldDuration} tour(s). Les ennemis qui entrent sur ces cases deviennent empoisonnes de facon persistante. Reutilisation ce tour: {(hasReuse ? "Activee" : "Verrouillee")}.";
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        int maxDistance = GetAvailableRangeThisUse(character, runtime);
        if (maxDistance <= 0)
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

        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            Vector2Int direction = directions[directionIndex];
            for (int distance = 1; distance <= maxDistance; distance++)
            {
                Vector2Int targetCell = character.GridPosition + (direction * distance);
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
        if (character == null || runtime == null || character.Board == null || targetCell == character.GridPosition)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        bool isOrthogonal = delta.x == 0 || delta.y == 0;
        if (!isOrthogonal)
        {
            return false;
        }

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        int maxDistance = GetAvailableRangeThisUse(character, runtime);
        if (distance <= 0 || distance > maxDistance)
        {
            return false;
        }

        if (!character.Board.TryGetCell(targetCell, out BoardCell targetBoardCell) || !targetBoardCell.Walkable || targetBoardCell.IsOccupied)
        {
            return false;
        }

        Vector2Int direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        Vector2Int scan = character.GridPosition + direction;
        while (scan != targetCell)
        {
            if (!character.Board.TryGetCell(scan, out BoardCell cell) || cell.HasBlockingTerrain || cell.IsOccupied)
            {
                return false;
            }

            scan += direction;
        }

        return true;
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        bool isOrthogonal = delta.x == 0 || delta.y == 0;
        if (!isOrthogonal)
        {
            return false;
        }

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        return distance > 0 && distance <= GetAvailableRangeThisUse(character, runtime);
    }

    public override void OnTurnStarted(Character character, CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return;
        }

        remainingRangeBudgetByRuntime[runtime] = 0;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue)
        {
            return false;
        }

        if (!CanActivateOnCell(character, runtime, targetCell.Value))
        {
            return false;
        }

        character.StartCoroutine(ResolvePoisonTrail(character, runtime, targetCell.Value));
        return true;
    }

    private IEnumerator ResolvePoisonTrail(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null)
        {
            yield break;
        }

        Vector2Int startCell = character.GridPosition;
        Vector2Int delta = targetCell - startCell;
        Vector2Int direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        int poisonFieldLifetime = GetPoisonFieldLifetime(character);

        character.BeginActionLock();
        PlayDashSound(character);

        bool success = character.TryTeleportTo(targetCell);
        if (!success)
        {
            character.EndActionLock();
            yield break;
        }

        yield return new WaitUntil(() => !character.IsMoving);

        CreateTrailHazards(character, startCell, direction, distance, poisonFieldLifetime);
        if (character.GetUpgradeStacks(AbilityUpgradeKey.PoisonTrailUnholyAura) > 0)
        {
            CreateAuraHazards(character, poisonFieldLifetime);
        }

        UpdateRangeBudget(character, runtime, distance);
        PlayConfiguredFx(character);
        character.EndActionLock();
    }

    private void CreateTrailHazards(Character character, Vector2Int startCell, Vector2Int direction, int distance, int poisonFieldLifetime)
    {
        if (character == null || character.Board == null || direction == Vector2Int.zero || distance <= 0)
        {
            return;
        }

        PlacePoisonHazard(character, startCell, poisonFieldLifetime);
        for (int step = 1; step <= distance; step++)
        {
            PlacePoisonHazard(character, startCell + (direction * step), poisonFieldLifetime);
        }
    }

    private void CreateAuraHazards(Character character, int poisonFieldLifetime)
    {
        if (character == null || character.Board == null)
        {
            return;
        }

        for (int offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                Vector2Int cell = character.GridPosition + new Vector2Int(offsetX, offsetY);
                PlacePoisonHazard(character, cell, poisonFieldLifetime);
            }
        }
    }

    private void PlacePoisonHazard(Character character, Vector2Int gridPosition, int poisonFieldLifetime)
    {
        if (character == null || character.Board == null || !character.Board.TryGetCell(gridPosition, out BoardCell cell) || !cell.Walkable)
        {
            return;
        }

        if (character.Board.TryGetHazard(gridPosition, out BoardHazard existingHazard) && existingHazard != null)
        {
            if (existingHazard is PoisonTrailHazard poisonTrailHazard && poisonTrailHazard.Owner == character)
            {
                poisonTrailHazard.Refresh(poisonFieldLifetime);
            }

            return;
        }

        GameObject hazardObject = new GameObject("PoisonTrailHazard");
        PoisonTrailHazard poisonHazard = hazardObject.AddComponent<PoisonTrailHazard>();
        poisonHazard.Configure(character.Board, character, gridPosition, poisonFieldLifetime, fxPoisonField);
    }

    private void UpdateRangeBudget(Character character, CharacterAbilityRuntime runtime, int distanceSpent)
    {
        if (character == null || runtime == null)
        {
            return;
        }

        int maxRange = GetMaxRange(character);
        if (character.GetUpgradeStacks(AbilityUpgradeKey.PoisonTrailToxicPath) <= 0)
        {
            remainingRangeBudgetByRuntime[runtime] = 0;
            return;
        }

        int currentBudget = remainingRangeBudgetByRuntime.TryGetValue(runtime, out int savedBudget) && savedBudget > 0
            ? savedBudget
            : maxRange;
        currentBudget = Mathf.Max(0, currentBudget - Mathf.Max(1, distanceSpent));
        remainingRangeBudgetByRuntime[runtime] = currentBudget;
        if (currentBudget > 0)
        {
            runtime.GrantBonusTurnUse(1);
        }
    }

    private int GetAvailableRangeThisUse(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null)
        {
            return 0;
        }

        int maxRange = GetMaxRange(character);
        int savedBudget = 0;
        bool canReuseWithinBudget = character.GetUpgradeStacks(AbilityUpgradeKey.PoisonTrailToxicPath) > 0
            && runtime != null
            && remainingRangeBudgetByRuntime.TryGetValue(runtime, out savedBudget)
            && savedBudget > 0;
        return canReuseWithinBudget ? savedBudget : maxRange;
    }

    private int GetMaxRange(Character character)
    {
        return Mathf.Max(1, baseRange + (character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.PoisonTrailVenomousMomentum) : 0));
    }

    private int GetPoisonFieldLifetime(Character character)
    {
        return Mathf.Max(1, poisonCellLifetimeInPlayerTurns + (character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.PoisonTrailCorrosivePerfume) : 0));
    }

    private void PlayDashSound(Character character)
    {
        if (character == null || dashSoundParametersPrefab == null)
        {
            return;
        }

        GameObject soundObject = Instantiate(
            dashSoundParametersPrefab,
            character.transform.position,
            dashSoundParametersPrefab.transform.rotation);
        soundObject.transform.localScale = dashSoundParametersPrefab.transform.localScale;

        SoundParameters soundParameters = soundObject.GetComponent<SoundParameters>();
        if (soundParameters != null)
        {
            soundParameters.PlaySound(character.transform.position);
        }
    }
}
