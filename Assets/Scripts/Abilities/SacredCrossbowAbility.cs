using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Sacred Crossbow", fileName = "SacredCrossbow")]
public class SacredCrossbowAbility : AbilityDefinition
{
    private sealed class PullCandidate
    {
        public Enemy Enemy;
        public Vector2Int Destination;
        public int Health;
        public int DistanceToHector;
        public float TieBreaker;
    }

    [Min(1)]
    [SerializeField] private int baseDamage = 5;
    [Min(1)]
    [SerializeField] private int range = 10;
    [Min(1)]
    [SerializeField] private int secondarySplashDamage = 1;
    [Min(1)]
    [SerializeField] private int secondarySplashRange = 1;
    [SerializeField] private GameObject projectilePrefab;
    [Min(0.01f)]
    [SerializeField] private float projectileSpeed = 28f;
    [Min(0f)]
    [SerializeField] private float projectileLaunchDelay = 0.08f;
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 projectileImpactOffset = new Vector3(0f, 0.2f, 0f);
    [Header("Secondary Impact FX")]
    [SerializeField] private GameObject lightBurstImpactFxPrefab;
    [SerializeField] private GameObject sacredDemolitionImpactFxPrefab;
    [Min(0f)]
    [SerializeField] private float secondaryImpactFxDuration = 1f;
    [Header("Sacred Ray FX")]
    [SerializeField] private GameObject sacredRayAttractionFxPrefab;
    [SerializeField] private GameObject sacredRaySoundParametersPrefab;
    [Min(0f)]
    [SerializeField] private float sacredRayAttractionFxDuration = 1f;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanTargetCell(character, targetCell);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanTargetCell(character, targetCell);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || character.Board == null || !targetCell.HasValue)
        {
            return false;
        }

        if (!HectorAbilityUtils.TryResolveAlignedDirection(character.GridPosition, targetCell.Value, false, range, out Vector2Int direction, out _))
        {
            return false;
        }

        List<Vector2Int> lineCells = BuildLineCells(character.Board, character.GridPosition, direction, range);
        if (lineCells.Count <= 0)
        {
            return false;
        }

        character.StartCoroutine(ResolveSacredCrossbowSequence(character, direction, lineCells));
        return true;
    }

    private IEnumerator ResolveSacredCrossbowSequence(Character character, Vector2Int direction, List<Vector2Int> lineCells)
    {
        if (character == null || character.Board == null || lineCells == null || lineCells.Count <= 0)
        {
            yield break;
        }

        character.BeginActionLock();
        character.FaceTargetCell(lineCells[lineCells.Count - 1]);

        if (character.GetUpgradeStacks(AbilityUpgradeKey.SacredCrossbowSacredRay) > 0)
        {
            List<Enemy> movedEnemies = PullEnemiesTowardLine(character, lineCells);
            while (AnyEnemyMoving(movedEnemies))
            {
                yield return null;
            }
        }

        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
        int lightBoltStacks = character.GetUpgradeStacks(AbilityUpgradeKey.SacredCrossbowLightBolt);
        bool hasLightBurst = character.GetUpgradeStacks(AbilityUpgradeKey.SacredCrossbowLightBurst) > 0;
        bool hasSacredDemolition = character.GetUpgradeStacks(AbilityUpgradeKey.SacredCrossbowSacredDemolition) > 0;
        int traversedTargetCount = 0;
        float maxImpactDelay = 0f;

        for (int index = 0; index < lineCells.Count; index++)
        {
            Vector2Int cellPosition = lineCells[index];
            Vector3 impactPosition = character.Board.GridToWorldPosition(cellPosition) + projectileImpactOffset;
            float impactDelay = HectorAbilityUtils.EstimateLinearProjectileTravelDelay(
                character,
                impactPosition,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay);

            if (character.Board.TryGetEnemy(cellPosition, out Enemy enemy) && enemy != null)
            {
                int damage = baseDamage + (lightBoltStacks * traversedTargetCount);
                traversedTargetCount++;
                hitEnemies.Add(enemy);
                maxImpactDelay = Mathf.Max(maxImpactDelay, impactDelay);
                character.StartCoroutine(ResolveEnemyImpactAfterDelay(character, enemy, cellPosition, damage, impactDelay, hasLightBurst));
                continue;
            }

            if (character.Board.TryGetLichSkullObject(cellPosition, out LichSkullObject skull) && skull != null)
            {
                int damage = baseDamage + (lightBoltStacks * traversedTargetCount);
                traversedTargetCount++;
                maxImpactDelay = Mathf.Max(maxImpactDelay, impactDelay);
                character.StartCoroutine(ResolveSkullImpactAfterDelay(character, skull, cellPosition, damage, impactDelay, hasLightBurst));
                continue;
            }

            if (character.Board.TryGetBarrel(cellPosition, out BarrelObstacle barrel) && barrel != null)
            {
                traversedTargetCount++;
                maxImpactDelay = Mathf.Max(maxImpactDelay, impactDelay);
                bool triggerDemolitionOnBarrel = hasSacredDemolition && !hasLightBurst;
                character.StartCoroutine(ResolveBarrelImpactAfterDelay(character, barrel, cellPosition, impactDelay, hasLightBurst, triggerDemolitionOnBarrel));
                continue;
            }

            if (hasSacredDemolition && IsBlockingObstacleCell(character.Board, cellPosition))
            {
                maxImpactDelay = Mathf.Max(maxImpactDelay, impactDelay);
                character.StartCoroutine(ResolveObstacleDemolitionAfterDelay(character, cellPosition, impactDelay));
            }
        }

        HectorAbilityUtils.TryPlayLinearProjectile(
            character,
            projectilePrefab,
            character.Board.GridToWorldPosition(lineCells[lineCells.Count - 1]) + projectileImpactOffset,
            projectileSpeed,
            projectileSpawnOffset,
            projectileLaunchDelay);
        PlayConfiguredFx(character, hitEnemies);

        yield return new WaitForSeconds(maxImpactDelay + 0.05f);
        character.EndActionLock();
    }

    private IEnumerator ResolveEnemyImpactAfterDelay(
        Character character,
        Enemy enemy,
        Vector2Int cellPosition,
        int damage,
        float delay,
        bool triggerLightBurst)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (character == null || enemy == null || enemy.CurrentHealth <= 0)
        {
            yield break;
        }

        character.DealDamageToEnemy(enemy, damage, true, true, DamageSoundType.Default, this);
        if (triggerLightBurst)
        {
            PlaySecondaryImpactFx(character, lightBurstImpactFxPrefab, cellPosition);
            character.DamageEnemiesAround(cellPosition, secondarySplashRange, secondarySplashDamage, true, this);
        }
    }

    private IEnumerator ResolveSkullImpactAfterDelay(
        Character character,
        LichSkullObject skull,
        Vector2Int cellPosition,
        int damage,
        float delay,
        bool triggerLightBurst)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (character == null || skull == null || skull.IsResolving)
        {
            yield break;
        }

        character.DealDamageToLichSkull(skull, damage, true, DamageSoundType.Default, this);
        if (triggerLightBurst)
        {
            PlaySecondaryImpactFx(character, lightBurstImpactFxPrefab, cellPosition);
            character.DamageEnemiesAround(cellPosition, secondarySplashRange, secondarySplashDamage, true, this);
        }
    }

    private IEnumerator ResolveBarrelImpactAfterDelay(
        Character character,
        BarrelObstacle barrel,
        Vector2Int cellPosition,
        float delay,
        bool triggerLightBurst,
        bool triggerDemolition)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (barrel == null || barrel.IsDestroyed)
        {
            yield break;
        }

        barrel.TakeHit();
        if (triggerLightBurst || triggerDemolition)
        {
            PlaySecondaryImpactFx(character, triggerLightBurst ? lightBurstImpactFxPrefab : sacredDemolitionImpactFxPrefab, cellPosition);
            character?.DamageEnemiesAround(cellPosition, secondarySplashRange, secondarySplashDamage, true, this);
        }
    }

    private IEnumerator ResolveObstacleDemolitionAfterDelay(Character character, Vector2Int cellPosition, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        PlaySecondaryImpactFx(character, sacredDemolitionImpactFxPrefab, cellPosition);
        character?.DamageEnemiesAround(cellPosition, secondarySplashRange, secondarySplashDamage, true, this);
    }

    private void PlaySecondaryImpactFx(Character character, GameObject fxPrefab, Vector2Int cellPosition)
    {
        if (character == null || character.Board == null || fxPrefab == null)
        {
            return;
        }

        Vector3 worldPosition = character.Board.GridToWorldPosition(cellPosition);
        Vector3 offset = worldPosition - character.transform.position;
        character.PlayFeedbackFx(fxPrefab, positionOffset: offset, destroyAfterSeconds: secondaryImpactFxDuration);
    }

    private List<Vector2Int> BuildLineCells(BoardManager board, Vector2Int origin, Vector2Int direction, int maxRange)
    {
        List<Vector2Int> lineCells = new List<Vector2Int>();
        if (board == null || direction == Vector2Int.zero)
        {
            return lineCells;
        }

        for (int step = 1; step <= maxRange; step++)
        {
            Vector2Int cellPosition = origin + (direction * step);
            if (!board.TryGetCell(cellPosition, out _))
            {
                break;
            }

            lineCells.Add(cellPosition);
        }

        return lineCells;
    }

    private List<Enemy> PullEnemiesTowardLine(Character character, List<Vector2Int> lineCells)
    {
        List<Enemy> movedEnemies = new List<Enemy>();
        if (character == null || character.Board == null || lineCells == null || lineCells.Count <= 0)
        {
            return movedEnemies;
        }

        HashSet<Vector2Int> lineCellSet = new HashSet<Vector2Int>(lineCells);
        Dictionary<Vector2Int, PullCandidate> bestCandidateByDestination = new Dictionary<Vector2Int, PullCandidate>();
        IReadOnlyList<Enemy> enemies = character.Board.SpawnedEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy enemy = enemies[index];
            if (enemy == null || enemy.CurrentHealth <= 0 || lineCellSet.Contains(enemy.GridPosition))
            {
                continue;
            }

            if (!TryFindBestPullDestination(character, enemy, lineCells, out Vector2Int destination))
            {
                continue;
            }

            PullCandidate candidate = new PullCandidate
            {
                Enemy = enemy,
                Destination = destination,
                Health = enemy.CurrentHealth,
                DistanceToHector = character.Board.GetManhattanDistance(enemy.GridPosition, character.GridPosition),
                TieBreaker = Random.value
            };

            if (!bestCandidateByDestination.TryGetValue(destination, out PullCandidate currentBest)
                || IsPullCandidateBetter(candidate, currentBest))
            {
                bestCandidateByDestination[destination] = candidate;
            }
        }

        List<PullCandidate> winners = new List<PullCandidate>(bestCandidateByDestination.Values);
        winners.Sort((left, right) =>
        {
            int destinationDistanceComparison = character.Board.GetManhattanDistance(left.Destination, character.GridPosition)
                .CompareTo(character.Board.GetManhattanDistance(right.Destination, character.GridPosition));
            if (destinationDistanceComparison != 0)
            {
                return destinationDistanceComparison;
            }

            return left.TieBreaker.CompareTo(right.TieBreaker);
        });

        if (winners.Count > 0)
        {
            PlaySacredRaySound(character);
            for (int index = 0; index < winners.Count; index++)
            {
                PlaySacredRayAttractionFx(character, winners[index].Destination);
            }
        }

        for (int index = 0; index < winners.Count; index++)
        {
            PullCandidate winner = winners[index];
            if (winner.Enemy != null && winner.Enemy.TryForcedMoveTo(winner.Destination))
            {
                movedEnemies.Add(winner.Enemy);
            }
        }

        return movedEnemies;
    }

    private bool TryFindBestPullDestination(Character character, Enemy enemy, List<Vector2Int> lineCells, out Vector2Int destination)
    {
        destination = default;
        if (character == null || character.Board == null || enemy == null || lineCells == null)
        {
            return false;
        }

        bool foundDestination = false;
        int bestDistanceToHector = int.MaxValue;
        int bestDistanceToEnemy = int.MaxValue;

        for (int index = 0; index < lineCells.Count; index++)
        {
            Vector2Int candidateCell = lineCells[index];
            if (!character.Board.TryGetCell(candidateCell, out BoardCell boardCell)
                || !boardCell.Walkable
                || boardCell.IsOccupied
                || candidateCell == character.GridPosition)
            {
                continue;
            }

            int adjacencyDistance = Mathf.Abs(candidateCell.x - enemy.GridPosition.x)
                + Mathf.Abs(candidateCell.y - enemy.GridPosition.y);
            if (adjacencyDistance != 1)
            {
                continue;
            }

            int distanceToHector = character.Board.GetManhattanDistance(candidateCell, character.GridPosition);
            int distanceToEnemy = character.Board.GetManhattanDistance(candidateCell, enemy.GridPosition);
            if (!foundDestination
                || distanceToHector < bestDistanceToHector
                || (distanceToHector == bestDistanceToHector && distanceToEnemy < bestDistanceToEnemy))
            {
                foundDestination = true;
                bestDistanceToHector = distanceToHector;
                bestDistanceToEnemy = distanceToEnemy;
                destination = candidateCell;
            }
        }

        return foundDestination;
    }

    private bool IsPullCandidateBetter(PullCandidate contender, PullCandidate currentBest)
    {
        if (currentBest == null)
        {
            return true;
        }

        if (contender.Health != currentBest.Health)
        {
            return contender.Health < currentBest.Health;
        }

        if (contender.DistanceToHector != currentBest.DistanceToHector)
        {
            return contender.DistanceToHector < currentBest.DistanceToHector;
        }

        return contender.TieBreaker < currentBest.TieBreaker;
    }

    private void PlaySacredRayAttractionFx(Character character, Vector2Int cellPosition)
    {
        if (character == null || character.Board == null || sacredRayAttractionFxPrefab == null)
        {
            return;
        }

        Vector3 worldPosition = character.Board.GridToWorldPosition(cellPosition);
        Vector3 offset = worldPosition - character.transform.position;
        character.PlayFeedbackFx(sacredRayAttractionFxPrefab, positionOffset: offset, destroyAfterSeconds: sacredRayAttractionFxDuration);
    }

    private void PlaySacredRaySound(Character character)
    {
        if (character == null || sacredRaySoundParametersPrefab == null)
        {
            return;
        }

        GameObject soundObject = Object.Instantiate(
            sacredRaySoundParametersPrefab,
            character.transform.position,
            sacredRaySoundParametersPrefab.transform.rotation);
        soundObject.transform.localScale = sacredRaySoundParametersPrefab.transform.localScale;

        SoundParameters soundParameters = soundObject.GetComponent<SoundParameters>();
        if (soundParameters != null)
        {
            soundParameters.PlaySound(character.transform.position);
        }
    }

    private bool CanTargetCell(Character character, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        return HectorAbilityUtils.TryResolveAlignedDirection(character.GridPosition, targetCell, false, range, out _, out _)
            && character.Board.IsInsideBoard(targetCell);
    }

    private bool IsBlockingObstacleCell(BoardManager board, Vector2Int cellPosition)
    {
        if (board == null || !board.TryGetCell(cellPosition, out BoardCell cell) || !cell.HasBlockingTerrain)
        {
            return false;
        }

        return !board.TryGetEnemy(cellPosition, out _)
            && !board.TryGetLichSkullObject(cellPosition, out _)
            && !board.TryGetBarrel(cellPosition, out _);
    }

    private bool AnyEnemyMoving(List<Enemy> enemies)
    {
        if (enemies == null)
        {
            return false;
        }

        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy enemy = enemies[index];
            if (enemy != null && enemy.IsMoving)
            {
                return true;
            }
        }

        return false;
    }
}
