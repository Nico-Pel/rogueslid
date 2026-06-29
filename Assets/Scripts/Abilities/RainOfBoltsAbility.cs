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
    [SerializeField] private AnimationCurve projectileTravelProgressCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 2.2f, 2.2f),
        new Keyframe(0.35f, 0.46f, 0.45f, 0.45f),
        new Keyframe(0.62f, 0.6f, 1.4f, 1.4f),
        new Keyframe(1f, 1f, 3f, 3f));
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

    public HashSet<Vector2Int> GetPreviewAreaCells(Character character, Vector2Int centerCell)
    {
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        if (character == null || character.Board == null || !character.Board.IsInsideBoard(centerCell))
        {
            return cells;
        }

        int radius = baseRadius + character.GetUpgradeStacks(AbilityUpgradeKey.RainOfBoltsCloudySky);
        for (int offsetX = -radius; offsetX <= radius; offsetX++)
        {
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                if ((offsetX * offsetX) + (offsetY * offsetY) > (radius * radius))
                {
                    continue;
                }

                Vector2Int candidateCell = centerCell + new Vector2Int(offsetX, offsetY);
                if (character.Board.IsInsideBoard(candidateCell))
                {
                    cells.Add(candidateCell);
                }
            }
        }

        return cells;
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
        int successfulTargetCount = CountSuccessfulTargetCells(character, allCells);
        int damagePerHit = baseDamage + Mathf.Max(0, successfulTargetCount - 1) + character.GetUpgradeStacks(AbilityUpgradeKey.RainOfBoltsIronBolts);

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
            character.StartCoroutine(PlayRainBoltProjectileRoutine(
                character,
                cellPosition,
                targetPosition,
                travelDuration,
                playArrowShotSound,
                damagePerHit,
                () =>
                {
                    hitCount++;
                }));
        }

        PlayConfiguredFx(character);
        yield return new WaitForSeconds(maxTravelDuration + emptyImpactScaleDuration + 0.05f);

        if (hitCount >= 2 && character.GetUpgradeStacks(AbilityUpgradeKey.RainOfBoltsLuckyHunter) > 0)
        {
            character.AddBonusDamageUntilEndOfTurn(1, luckyHunterBoostFxPrefab, luckyHunterAuraFxPrefab, luckyHunterBoostFxDuration);
        }

        character.EndActionLock();
    }

    private bool ResolveProjectileImpact(Character character, Vector2Int cellPosition, GameObject projectile, int damage)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }
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

        if (character.Board.TryGetBarrel(cellPosition, out BarrelObstacle barrel) && barrel != null)
        {
            barrel.TakeHit();
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

    private IEnumerator PlayRainBoltProjectileRoutine(
        Character character,
        Vector2Int cellPosition,
        Vector3 targetPosition,
        float travelDuration,
        bool playArrowShotSound,
        int damage,
        System.Action onSuccessfulHit)
    {
        if (character == null || projectilePrefab == null)
        {
            yield break;
        }

        Vector3 startPosition = character.transform.position + projectileSpawnOffset;
        if (playArrowShotSound)
        {
            SoundManager.Instance?.PlayArrowShot(startPosition);
        }

        GameObject projectile = Instantiate(projectilePrefab, startPosition, Quaternion.identity);
        projectile.transform.localScale = projectilePrefab.transform.localScale;

        float duration = Mathf.Max(0.05f, travelDuration);
        AnimationCurve progressCurve = GetValidatedProjectileTravelCurve();
        float elapsed = 0f;
        while (elapsed < duration && projectile != null)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            ApplyRainBoltProjectilePose(projectile.transform, startPosition, targetPosition, normalizedTime, progressCurve);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (projectile != null)
        {
            ApplyRainBoltProjectilePose(projectile.transform, startPosition, targetPosition, 1f, progressCurve);
        }

        bool didHit = ResolveProjectileImpact(character, cellPosition, projectile, damage);
        if (didHit)
        {
            onSuccessfulHit?.Invoke();
        }

        if (projectile != null && didHit)
        {
            Destroy(projectile);
        }
    }

    private List<Vector2Int> GetPrimaryTargetCells(Character character, Vector2Int centerCell)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        if (character == null || character.Board == null)
        {
            return cells;
        }

        int radius = baseRadius + character.GetUpgradeStacks(AbilityUpgradeKey.RainOfBoltsCloudySky);
        HashSet<Enemy> targetedEnemies = new HashSet<Enemy>();
        HashSet<LichSkullObject> targetedSkulls = new HashSet<LichSkullObject>();
        for (int offsetX = -radius; offsetX <= radius; offsetX++)
        {
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                if ((offsetX * offsetX) + (offsetY * offsetY) > (radius * radius))
                {
                    continue;
                }

                Vector2Int cellPosition = centerCell + new Vector2Int(offsetX, offsetY);
                if (!character.Board.TryGetCell(cellPosition, out BoardCell cell) || cellPosition == character.GridPosition)
                {
                    continue;
                }

                if (character.Board.TryGetEnemy(cellPosition, out Enemy enemy) && enemy != null)
                {
                    if (targetedEnemies.Add(enemy))
                    {
                        cells.Add(cellPosition);
                    }

                    continue;
                }

                if (character.Board.TryGetLichSkullObject(cellPosition, out LichSkullObject skull) && skull != null)
                {
                    if (targetedSkulls.Add(skull))
                    {
                        cells.Add(cellPosition);
                    }

                    continue;
                }

                if (cell.Walkable && !cell.IsOccupied)
                {
                    cells.Add(cellPosition);
                }
            }
        }

        return cells;
    }

    private int CountSuccessfulTargetCells(Character character, List<Vector2Int> cells)
    {
        if (character == null || character.Board == null || cells == null)
        {
            return 0;
        }

        int count = 0;
        HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>(cells);
        HashSet<Enemy> targetedEnemies = new HashSet<Enemy>();
        HashSet<LichSkullObject> targetedSkulls = new HashSet<LichSkullObject>();
        foreach (Vector2Int cellPosition in uniqueCells)
        {
            if (character.Board.TryGetEnemy(cellPosition, out Enemy enemy) && enemy != null)
            {
                if (targetedEnemies.Add(enemy))
                {
                    count++;
                }

                continue;
            }

            if (character.Board.TryGetLichSkullObject(cellPosition, out LichSkullObject skull) && skull != null
                && targetedSkulls.Add(skull))
            {
                count++;
            }
        }

        return count;
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
        HashSet<Enemy> excludedEnemies = new HashSet<Enemy>();
        HashSet<LichSkullObject> excludedSkulls = new HashSet<LichSkullObject>();
        if (excludedCells != null)
        {
            foreach (Vector2Int excludedCell in excludedCells)
            {
                if (character.Board.TryGetEnemy(excludedCell, out Enemy excludedEnemy) && excludedEnemy != null)
                {
                    excludedEnemies.Add(excludedEnemy);
                }
                else if (character.Board.TryGetLichSkullObject(excludedCell, out LichSkullObject excludedSkull) && excludedSkull != null)
                {
                    excludedSkulls.Add(excludedSkull);
                }
            }
        }

        HashSet<Enemy> candidateEnemies = new HashSet<Enemy>();
        HashSet<LichSkullObject> candidateSkulls = new HashSet<LichSkullObject>();
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

                if (character.Board.TryGetEnemy(cellPosition, out Enemy enemy) && enemy != null)
                {
                    if (excludedEnemies.Contains(enemy) || !candidateEnemies.Add(enemy))
                    {
                        continue;
                    }
                }
                else if (character.Board.TryGetLichSkullObject(cellPosition, out LichSkullObject skull) && skull != null)
                {
                    if (excludedSkulls.Contains(skull) || !candidateSkulls.Add(skull))
                    {
                        continue;
                    }
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

    private void ApplyRainBoltProjectilePose(
        Transform projectileTransform,
        Vector3 startPosition,
        Vector3 targetPosition,
        float normalizedTime,
        AnimationCurve progressCurve)
    {
        if (projectileTransform == null)
        {
            return;
        }

        float clampedTime = Mathf.Clamp01(normalizedTime);
        float progress = Mathf.Clamp01(progressCurve.Evaluate(clampedTime));
        Vector3 flatPosition = Vector3.LerpUnclamped(startPosition, targetPosition, progress);
        float arcHeight = Mathf.Max(0.01f, projectileJumpPower);
        float arcOffset = 4f * progress * (1f - progress) * arcHeight;
        Vector3 currentPosition = flatPosition + (Vector3.up * arcOffset);
        projectileTransform.position = currentPosition;

        const float sampleStep = 0.02f;
        float nextTime = Mathf.Clamp01(clampedTime + sampleStep);
        if (nextTime <= clampedTime)
        {
            nextTime = clampedTime;
        }

        float nextProgress = Mathf.Clamp01(progressCurve.Evaluate(nextTime));
        Vector3 nextFlatPosition = Vector3.LerpUnclamped(startPosition, targetPosition, nextProgress);
        float nextArcOffset = 4f * nextProgress * (1f - nextProgress) * arcHeight;
        Vector3 nextPosition = nextFlatPosition + (Vector3.up * nextArcOffset);
        Vector3 direction = nextPosition - currentPosition;
        if (direction.sqrMagnitude > 0.0001f)
        {
            projectileTransform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private AnimationCurve GetValidatedProjectileTravelCurve()
    {
        if (projectileTravelProgressCurve == null || projectileTravelProgressCurve.length < 2)
        {
            return AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        return projectileTravelProgressCurve;
    }
}
