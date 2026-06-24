using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Rain Of Bolts", fileName = "RainOfBolts")]
public class RainOfBoltsAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 2;
    [Min(1)]
    [SerializeField] private int range = 10;
    [Min(0)]
    [SerializeField] private int baseRadius = 3;
    [Min(0f)]
    [SerializeField] private float initialDelay = 0.4f;
    [SerializeField] private GameObject projectilePrefab;
    [Min(0.05f)]
    [SerializeField] private float projectileTravelSpeed = 12f;
    [Min(0.05f)]
    [SerializeField] private float minimumTravelDuration = 0.18f;
    [Min(0.01f)]
    [SerializeField] private float projectileJumpPower = 0.85f;
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 projectileImpactOffset = new Vector3(0f, 0.2f, 0f);
    [Min(0f)]
    [SerializeField] private float emptyImpactScale = 0.1f;
    [Min(0.01f)]
    [SerializeField] private float emptyImpactScaleDuration = 0.2f;
    [Header("Lucky Hunter FX")]
    [SerializeField] private GameObject luckyHunterBoostFxPrefab;
    [SerializeField] private GameObject luckyHunterAuraFxPrefab;
    [Min(0f)]
    [SerializeField] private float luckyHunterBoostFxDuration = 1f;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null || !character.Board.IsInsideBoard(targetCell))
        {
            return false;
        }

        int distance = Mathf.Max(
            Mathf.Abs(targetCell.x - character.GridPosition.x),
            Mathf.Abs(targetCell.y - character.GridPosition.y));
        return distance <= range;
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanActivateOnCell(character, runtime, targetCell);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || character.Board == null || !targetCell.HasValue || !CanActivateOnCell(character, runtime, targetCell.Value))
        {
            return false;
        }

        character.StartCoroutine(ResolveRainSequence(character, targetCell.Value));
        return true;
    }

    private IEnumerator ResolveRainSequence(Character character, Vector2Int centerCell)
    {
        if (character == null || character.Board == null)
        {
            yield break;
        }

        character.BeginActionLock();
        character.FaceTargetCell(centerCell);

        List<Vector2Int> primaryCells = GetPrimaryTargetCells(character, centerCell);
        HashSet<Vector2Int> primaryCellSet = new HashSet<Vector2Int>(primaryCells);
        List<Vector2Int> extraCells = GetExtraTargetCells(character, primaryCellSet);
        List<Vector2Int> allCells = new List<Vector2Int>(primaryCells);
        allCells.AddRange(extraCells);

        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        int hitCount = 0;
        float maxTravelDuration = 0f;
        for (int index = 0; index < allCells.Count; index++)
        {
            Vector2Int cellPosition = allCells[index];
            Vector3 targetPosition = character.Board.GridToWorldPosition(cellPosition) + projectileImpactOffset;
            float travelDuration = Mathf.Max(
                minimumTravelDuration,
                Vector3.Distance(character.transform.position + projectileSpawnOffset, targetPosition) / Mathf.Max(0.01f, projectileTravelSpeed));
            maxTravelDuration = Mathf.Max(maxTravelDuration, travelDuration);

            bool playArrowShotSound = index == 0;
            HectorAbilityUtils.TryPlayArcProjectile(
                character,
                projectilePrefab,
                targetPosition,
                travelDuration,
                projectileJumpPower,
                projectileSpawnOffset,
                0f,
                projectile =>
                {
                    bool didHit = ResolveProjectileImpact(character, cellPosition, projectile);
                    if (didHit)
                    {
                        hitCount++;
                    }

                    return didHit;
                },
                playArrowShotSound);
        }

        PlayConfiguredFx(character);
        yield return new WaitForSeconds(maxTravelDuration + emptyImpactScaleDuration + 0.05f);

        if (hitCount >= 2 && character.GetUpgradeStacks(AbilityUpgradeKey.RainOfBoltsLuckyHunter) > 0)
        {
            character.AddBonusDamageUntilEndOfTurn(1, luckyHunterBoostFxPrefab, luckyHunterAuraFxPrefab, luckyHunterBoostFxDuration);
        }

        character.EndActionLock();
    }

    private bool ResolveProjectileImpact(Character character, Vector2Int cellPosition, GameObject projectile)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        int damage = baseDamage + character.GetUpgradeStacks(AbilityUpgradeKey.RainOfBoltsIronBolts);
        if (character.Board.TryGetEnemy(cellPosition, out Enemy enemy) && enemy != null)
        {
            int appliedDamage = character.DealDamageToEnemy(enemy, damage, true, true, DamageSoundType.Default, this);
            return appliedDamage > 0;
        }

        if (character.Board.TryGetLichSkullObject(cellPosition, out LichSkullObject skull) && skull != null)
        {
            character.DealDamageToLichSkull(skull, damage, true, DamageSoundType.Default, this);
            return true;
        }

        if (projectile != null)
        {
            projectile.transform.DOScale(projectile.transform.localScale * Mathf.Max(0.01f, emptyImpactScale), emptyImpactScaleDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    if (projectile != null)
                    {
                        Destroy(projectile);
                    }
                });
        }

        return false;
    }

    private List<Vector2Int> GetPrimaryTargetCells(Character character, Vector2Int centerCell)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        if (character == null || character.Board == null)
        {
            return cells;
        }

        int radius = baseRadius + character.GetUpgradeStacks(AbilityUpgradeKey.RainOfBoltsCloudySky);
        for (int offsetX = -radius; offsetX <= radius; offsetX++)
        {
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                if (Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)) > radius)
                {
                    continue;
                }

                Vector2Int cellPosition = centerCell + new Vector2Int(offsetX, offsetY);
                if (!character.Board.TryGetCell(cellPosition, out BoardCell cell) || cellPosition == character.GridPosition)
                {
                    continue;
                }

                bool hasEnemyTarget = character.Board.TryGetEnemy(cellPosition, out Enemy enemy) && enemy != null;
                bool hasSkullTarget = character.Board.TryGetLichSkullObject(cellPosition, out LichSkullObject skull) && skull != null;
                if (hasEnemyTarget || hasSkullTarget || (cell.Walkable && !cell.IsOccupied))
                {
                    cells.Add(cellPosition);
                }
            }
        }

        return cells;
    }

    private List<Vector2Int> GetExtraTargetCells(Character character, HashSet<Vector2Int> excludedCells)
    {
        List<Vector2Int> extraCells = new List<Vector2Int>();
        if (character == null || character.Board == null)
        {
            return extraCells;
        }

        int extraProjectileCount = 2 * character.GetUpgradeStacks(AbilityUpgradeKey.RainOfBoltsLostBolt);
        if (extraProjectileCount <= 0)
        {
            return extraCells;
        }

        List<Vector2Int> preferredCells = new List<Vector2Int>();
        List<Vector2Int> fallbackCells = new List<Vector2Int>();
        for (int x = 0; x < character.Board.Width; x++)
        {
            for (int y = 0; y < character.Board.Height; y++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                if (cellPosition == character.GridPosition || (excludedCells != null && excludedCells.Contains(cellPosition)))
                {
                    continue;
                }

                if (!character.Board.TryGetCell(cellPosition, out BoardCell cell))
                {
                    continue;
                }

                bool isBarrel = character.Board.TryGetBarrel(cellPosition, out BarrelObstacle barrel) && barrel != null;
                if (!cell.HasBlockingTerrain || isBarrel)
                {
                    preferredCells.Add(cellPosition);
                }
                else
                {
                    fallbackCells.Add(cellPosition);
                }
            }
        }

        for (int index = 0; index < extraProjectileCount; index++)
        {
            Vector2Int chosenCell;
            if (TryPickRandomUniqueCell(preferredCells, extraCells, out chosenCell)
                || TryPickRandomUniqueCell(fallbackCells, extraCells, out chosenCell))
            {
                extraCells.Add(chosenCell);
            }
        }

        return extraCells;
    }

    private bool TryPickRandomUniqueCell(List<Vector2Int> sourceCells, List<Vector2Int> alreadyPicked, out Vector2Int chosenCell)
    {
        chosenCell = default;
        if (sourceCells == null || sourceCells.Count <= 0)
        {
            return false;
        }

        List<Vector2Int> availableCells = new List<Vector2Int>();
        for (int index = 0; index < sourceCells.Count; index++)
        {
            Vector2Int candidate = sourceCells[index];
            if (alreadyPicked == null || !alreadyPicked.Contains(candidate))
            {
                availableCells.Add(candidate);
            }
        }

        if (availableCells.Count <= 0)
        {
            return false;
        }

        chosenCell = availableCells[Random.Range(0, availableCells.Count)];
        return true;
    }
}
