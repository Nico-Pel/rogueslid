using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Whisperfang", fileName = "Whisperfang")]
public class WhisperfangAbility : AbilityDefinition
{
    private sealed class WhisperfangTargetCandidate
    {
        public Vector2Int Cell;
        public Enemy Enemy;
        public LichSkullObject Skull;
        public BarrelObstacle Barrel;
        public int Health;
        public int Distance;
        public bool IsStraight;
        public float TieBreaker;

        public bool IsNeutralOnly => Enemy == null && Skull == null && Barrel != null;
        public Transform TargetTransform => Enemy != null ? Enemy.transform : Skull != null ? Skull.transform : Barrel != null ? Barrel.transform : null;
    }

    [Min(1)]
    [SerializeField] private int baseDamage = 1;
    [Min(1)]
    [SerializeField] private int range = 10;
    [SerializeField] private GameObject projectilePrefab;
    [Min(0.01f)]
    [SerializeField] private float projectileSpeed = 16f;
    [Min(0f)]
    [SerializeField] private float projectileLaunchDelay = 0.06f;
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 projectileImpactOffset = new Vector3(0f, 0.2f, 0f);
    [Min(0f)]
    [SerializeField] private float autoReshootDelayAfterMove = 0.02f;
    [Min(0.1f)]
    [SerializeField] private float delayBetweenAutomaticShots = 0.1f;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        int damage = baseDamage;
        return $"Fire 1 automatic shot for {damage} damage at the best visible target in a straight line or diagonal. Moving grants 1 extra shot.";
    }

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.Immediate;
    public override bool KeepsActiveStateBetweenTurns => false;

    public override void PlayActivationAnimation(Character character)
    {
    }

    public override string GetCounterText(CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return string.Empty;
        }

        if (runtime.IsActive)
        {
            return runtime.RemainingUsesThisTurn.ToString();
        }

        return base.GetCounterText(runtime);
    }

    public override int GetBonusUsesPerTurn(Character character, CharacterAbilityRuntime runtime)
    {
        return character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.WhisperfangOneMoreForTheRoad) : 0;
    }

    public override void OnCharacterMoved(
        Character character,
        CharacterAbilityRuntime runtime,
        Vector2Int previousCell,
        Vector2Int currentCell,
        bool consumedMovementPoint)
    {
        if (character == null || runtime == null || previousCell == currentCell)
        {
            return;
        }

        runtime.GrantBonusTurnUse(1);
        character.RefreshAbilityState();

        if (!runtime.IsActive)
        {
            return;
        }

        character.StartCoroutine(ResolveAutoReshootAfterMove(character, runtime));
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        return character != null && runtime != null;
    }

    public bool IsLuckyBoltPrimed(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null
            || runtime == null
            || !runtime.IsActive
            || character.GetUpgradeStacks(AbilityUpgradeKey.WhisperfangLuckyBolt) <= 0
            || runtime.RemainingUsesThisTurn <= 0)
        {
            return false;
        }

        int nextShotOrdinal = runtime.UsesThisTurnCount + 1;
        return nextShotOrdinal == 5 || nextShotOrdinal == 7;
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return false;
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return false;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || character.Board == null || runtime == null)
        {
            return false;
        }

        int availableShots = runtime.RemainingUsesThisTurn;
        if (availableShots <= 0)
        {
            return false;
        }

        if (GatherTargetCandidates(character, true).Count <= 0)
        {
            return false;
        }

        character.BeginActionLock();
        character.StartCoroutine(ResolveAutomaticVolleySequence(character, runtime, availableShots, true, true));
        return true;
    }

    private System.Collections.IEnumerator ResolveAutoReshootAfterMove(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || runtime == null)
        {
            yield break;
        }

        yield return new WaitUntil(() => !character.IsMoving);
        if (autoReshootDelayAfterMove > 0f)
        {
            yield return new WaitForSeconds(autoReshootDelayAfterMove);
        }

        if (!runtime.IsActive || runtime.RemainingUsesThisTurn <= 0)
        {
            yield break;
        }

        if (GatherTargetCandidates(character, false).Count <= 0)
        {
            yield break;
        }

        character.BeginActionLock();
        yield return ResolveAutomaticVolleySequence(character, runtime, runtime.RemainingUsesThisTurn, false, false);
    }

    private System.Collections.IEnumerator ResolveAutomaticVolleySequence(
        Character character,
        CharacterAbilityRuntime runtime,
        int maxShots,
        bool allowNeutralFallbackOnFirstShot,
        bool firstShotAlreadyConsumed)
    {
        if (character == null || runtime == null || maxShots <= 0)
        {
            yield break;
        }

        yield return null;

        float minimumShotDelay = Mathf.Max(0.1f, delayBetweenAutomaticShots);
        bool canFallbackToNeutral = allowNeutralFallbackOnFirstShot;
        bool activationUseConsumed = firstShotAlreadyConsumed;
        HashSet<Vector2Int> touchedCells = new HashSet<Vector2Int>();
        bool hasMultiShot = character.GetUpgradeStacks(AbilityUpgradeKey.WhisperfangMultiShot) > 0;

        for (int shotIndex = 0; shotIndex < maxShots; shotIndex++)
        {
            List<WhisperfangTargetCandidate> candidates = GatherTargetCandidates(character, canFallbackToNeutral);
            if (candidates.Count <= 0)
            {
                break;
            }

            if (!activationUseConsumed)
            {
                if (!runtime.TryConsumeAutomaticUse())
                {
                    break;
                }
            }

            activationUseConsumed = false;
            canFallbackToNeutral = false;
            base.PlayActivationAnimation(character);
            HashSet<Enemy> shotHitEnemies = new HashSet<Enemy>();
            float waveImpactDelay;
            if (hasMultiShot)
            {
                waveImpactDelay = FireMultiShotWave(character, runtime.UsesThisTurnCount, candidates, shotHitEnemies);
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    WhisperfangTargetCandidate candidate = candidates[candidateIndex];
                    if (candidate != null)
                    {
                        touchedCells.Add(candidate.Cell);
                    }
                }
            }
            else
            {
                WhisperfangTargetCandidate candidate = SelectBestCandidate(candidates, touchedCells, false);
                if (candidate == null)
                {
                    break;
                }

                touchedCells.Add(candidate.Cell);
                int damage = GetShotDamage(character, runtime.UsesThisTurnCount, candidate);
                FireShotAtCandidate(character, candidate, damage, shotHitEnemies);
                waveImpactDelay = GetEstimatedImpactDelay(character, candidate);
            }

            PlayConfiguredFx(character, shotHitEnemies);
            character.RefreshAbilityState();

            if (shotIndex < maxShots - 1)
            {
                yield return new WaitForSeconds(Mathf.Max(minimumShotDelay, waveImpactDelay));
            }
        }

        character.EndActionLock();
        character.RefreshAbilityState();
    }

    private float FireMultiShotWave(
        Character character,
        int shotOrdinal,
        List<WhisperfangTargetCandidate> candidates,
        HashSet<Enemy> hitEnemies)
    {
        if (character == null || candidates == null || candidates.Count <= 0)
        {
            return 0f;
        }

        float longestImpactDelay = 0f;
        WhisperfangTargetCandidate facingCandidate = SelectBestCandidate(candidates, null, false);
        if (facingCandidate != null)
        {
            character.FaceTargetCell(facingCandidate.Cell);
        }

        for (int index = 0; index < candidates.Count; index++)
        {
            WhisperfangTargetCandidate candidate = candidates[index];
            if (candidate == null)
            {
                continue;
            }

            int damage = GetShotDamage(character, shotOrdinal, candidate);
            FireShotAtCandidate(character, candidate, damage, hitEnemies, faceTarget: false);
            longestImpactDelay = Mathf.Max(longestImpactDelay, GetEstimatedImpactDelay(character, candidate));
        }

        return longestImpactDelay;
    }

    private int GetShotDamage(Character character, int shotOrdinal, WhisperfangTargetCandidate candidate)
    {
        int damage = baseDamage;
        if (character != null
            && character.GetUpgradeStacks(AbilityUpgradeKey.WhisperfangLuckyBolt) > 0
            && (shotOrdinal == 5 || shotOrdinal == 7))
        {
            damage += 1;
        }

        return damage;
    }

    private float GetEstimatedImpactDelay(Character character, WhisperfangTargetCandidate candidate)
    {
        if (character == null || candidate == null)
        {
            return projectileLaunchDelay;
        }

        Vector3 startPosition = character.transform.position + projectileSpawnOffset;
        Vector3 targetPosition = candidate.TargetTransform != null
            ? candidate.TargetTransform.position + projectileImpactOffset
            : character.Board.GridToWorldPosition(candidate.Cell) + projectileImpactOffset;
        float travelDuration = Mathf.Max(0.05f, Vector3.Distance(startPosition, targetPosition) / Mathf.Max(0.01f, projectileSpeed));
        return Mathf.Max(0f, projectileLaunchDelay) + travelDuration;
    }

    private bool FireShotAtCandidate(Character character, WhisperfangTargetCandidate candidate, int damage, HashSet<Enemy> hitEnemies, bool faceTarget = true)
    {
        if (character == null || character.Board == null || candidate == null)
        {
            return false;
        }

        if (faceTarget)
        {
            character.FaceTargetCell(candidate.Cell);
        }

        Vector3 impactPosition = candidate.TargetTransform != null
            ? candidate.TargetTransform.position + projectileImpactOffset
            : character.Board.GridToWorldPosition(candidate.Cell) + projectileImpactOffset;

        if (candidate.Enemy != null)
        {
            HectorAbilityUtils.TryPlayLinearProjectile(
                character,
                projectilePrefab,
                impactPosition,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay,
                () =>
                {
                    character.DealDamageToEnemy(candidate.Enemy, damage, true, true, DamageSoundType.Default, this);
                    ResolveRicochet(character, candidate.Enemy, damage, hitEnemies);
                });
            hitEnemies?.Add(candidate.Enemy);
            return true;
        }

        if (candidate.Skull != null)
        {
            HectorAbilityUtils.TryPlayLinearProjectile(
                character,
                projectilePrefab,
                impactPosition,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay,
                () => character.DealDamageToLichSkull(candidate.Skull, damage, true, DamageSoundType.Default, this));
            return true;
        }

        if (candidate.Barrel != null)
        {
            HectorAbilityUtils.TryPlayLinearProjectile(
                character,
                projectilePrefab,
                impactPosition,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay,
                candidate.Barrel.TakeHit);
            return true;
        }

        return false;
    }

    private void ResolveRicochet(Character character, Enemy startingEnemy, int damage, HashSet<Enemy> hitEnemies)
    {
        int ricochetCount = character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.WhisperfangRicochet) : 0;
        if (character == null || character.Board == null || startingEnemy == null || ricochetCount <= 0)
        {
            return;
        }

        Enemy currentEnemy = startingEnemy;
        for (int bounceIndex = 0; bounceIndex < ricochetCount; bounceIndex++)
        {
            Enemy nextEnemy = FindRicochetTarget(character, currentEnemy, hitEnemies);
            if (nextEnemy == null)
            {
                break;
            }

            character.DealDamageToEnemy(nextEnemy, damage, true, true, DamageSoundType.Default, this);
            hitEnemies?.Add(nextEnemy);
            currentEnemy = nextEnemy;
        }
    }

    private Enemy FindRicochetTarget(Character character, Enemy currentEnemy, HashSet<Enemy> hitEnemies)
    {
        if (character == null || character.Board == null || currentEnemy == null)
        {
            return null;
        }

        Enemy untouchedCandidate = null;
        Enemy fallbackCandidate = null;
        IReadOnlyList<Enemy> enemies = character.Board.SpawnedEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy candidate = enemies[index];
            if (candidate == null || candidate.CurrentHealth <= 0 || candidate == currentEnemy)
            {
                continue;
            }

            int distance = Mathf.Max(
                Mathf.Abs(candidate.GridPosition.x - currentEnemy.GridPosition.x),
                Mathf.Abs(candidate.GridPosition.y - currentEnemy.GridPosition.y));
            if (distance <= 0 || distance > 2)
            {
                continue;
            }

            if (hitEnemies == null || !hitEnemies.Contains(candidate))
            {
                if (untouchedCandidate == null || candidate.CurrentHealth < untouchedCandidate.CurrentHealth)
                {
                    untouchedCandidate = candidate;
                }
            }
            else if (fallbackCandidate == null || candidate.CurrentHealth < fallbackCandidate.CurrentHealth)
            {
                fallbackCandidate = candidate;
            }
        }

        return untouchedCandidate ?? fallbackCandidate;
    }

    private WhisperfangTargetCandidate SelectBestCandidate(
        List<WhisperfangTargetCandidate> candidates,
        HashSet<Vector2Int> touchedCells,
        bool preferUntouchedTargets)
    {
        WhisperfangTargetCandidate bestCandidate = null;
        bool foundUntouchedCandidate = false;
        int bestHealthScore = int.MaxValue;
        int bestDistance = int.MaxValue;
        int bestLinePriority = int.MaxValue;
        float bestTieBreaker = float.MaxValue;

        for (int index = 0; index < candidates.Count; index++)
        {
            WhisperfangTargetCandidate candidate = candidates[index];
            if (candidate == null)
            {
                continue;
            }

            bool isUntouched = touchedCells == null || !touchedCells.Contains(candidate.Cell);
            if (preferUntouchedTargets)
            {
                if (foundUntouchedCandidate && !isUntouched)
                {
                    continue;
                }

                if (!foundUntouchedCandidate && isUntouched)
                {
                    bestCandidate = null;
                    bestHealthScore = int.MaxValue;
                    bestDistance = int.MaxValue;
                    bestLinePriority = int.MaxValue;
                    bestTieBreaker = float.MaxValue;
                    foundUntouchedCandidate = true;
                }
            }

            int healthScore = candidate.IsNeutralOnly ? int.MaxValue - 1 : candidate.Health;
            int linePriority = candidate.IsStraight ? 0 : 1;

            bool isBetter = bestCandidate == null
                || healthScore < bestHealthScore
                || (healthScore == bestHealthScore && candidate.Distance < bestDistance)
                || (healthScore == bestHealthScore && candidate.Distance == bestDistance && linePriority < bestLinePriority)
                || (healthScore == bestHealthScore && candidate.Distance == bestDistance && linePriority == bestLinePriority && candidate.TieBreaker < bestTieBreaker);
            if (!isBetter)
            {
                continue;
            }

            bestCandidate = candidate;
            bestHealthScore = healthScore;
            bestDistance = candidate.Distance;
            bestLinePriority = linePriority;
            bestTieBreaker = candidate.TieBreaker;
        }

        return bestCandidate;
    }

    private List<WhisperfangTargetCandidate> GatherTargetCandidates(Character character, bool allowNeutralFallback)
    {
        List<WhisperfangTargetCandidate> enemyLikeCandidates = new List<WhisperfangTargetCandidate>();
        List<WhisperfangTargetCandidate> neutralCandidates = new List<WhisperfangTargetCandidate>();
        if (character == null || character.Board == null)
        {
            return enemyLikeCandidates;
        }

        Vector2Int[] directions = HectorAbilityUtils.OrthogonalAndDiagonalDirections;
        for (int index = 0; index < directions.Length; index++)
        {
            Vector2Int direction = directions[index];
            Vector2Int scan = character.GridPosition + direction;
            int distance = 1;
            while (character.Board.IsInsideBoard(scan) && distance <= range)
            {
                if (character.Board.TryGetEnemy(scan, out Enemy enemy) && enemy != null)
                {
                    enemyLikeCandidates.Add(new WhisperfangTargetCandidate
                    {
                        Cell = scan,
                        Enemy = enemy,
                        Health = enemy.CurrentHealth,
                        Distance = distance,
                        IsStraight = direction.x == 0 || direction.y == 0,
                        TieBreaker = Random.value
                    });
                    break;
                }

                if (character.Board.TryGetLichSkullObject(scan, out LichSkullObject skull) && skull != null)
                {
                    enemyLikeCandidates.Add(new WhisperfangTargetCandidate
                    {
                        Cell = scan,
                        Skull = skull,
                        Health = skull.CurrentHealth,
                        Distance = distance,
                        IsStraight = direction.x == 0 || direction.y == 0,
                        TieBreaker = Random.value
                    });
                    break;
                }

                if (character.Board.TryGetBarrel(scan, out BarrelObstacle barrel) && barrel != null)
                {
                    neutralCandidates.Add(new WhisperfangTargetCandidate
                    {
                        Cell = scan,
                        Barrel = barrel,
                        Distance = distance,
                        IsStraight = direction.x == 0 || direction.y == 0,
                        TieBreaker = Random.value
                    });
                    break;
                }

                if (character.Board.TryGetCell(scan, out BoardCell cell) && cell.HasBlockingTerrain)
                {
                    break;
                }

                scan += direction;
                distance++;
            }
        }

        if (enemyLikeCandidates.Count > 0)
        {
            return enemyLikeCandidates;
        }

        return allowNeutralFallback ? neutralCandidates : enemyLikeCandidates;
    }
}
