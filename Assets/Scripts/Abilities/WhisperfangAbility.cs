using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Whisperfang", fileName = "Whisperfang")]
public class WhisperfangAbility : AbilityDefinition
{
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

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

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
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanTargetCell(character, targetCell, true);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanTargetCell(character, targetCell, true, true);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || character.Board == null || runtime == null)
        {
            return false;
        }

        int damage = baseDamage;
        if (character.GetUpgradeStacks(AbilityUpgradeKey.WhisperfangLuckyBolt) > 0 && runtime.UsesThisTurnCount >= 4)
        {
            damage += 1;
        }

        if (character.GetUpgradeStacks(AbilityUpgradeKey.WhisperfangMultiShot) > 0)
        {
            List<Vector2Int> targets = GatherMultiShotTargets(character);
            if (targets.Count == 0)
            {
                return false;
            }

            HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
            for (int index = 0; index < targets.Count; index++)
            {
                ResolveSingleShot(character, targets[index], damage, hitEnemies);
            }

            PlayConfiguredFx(character, hitEnemies);
            return true;
        }

        if (!targetCell.HasValue)
        {
            return false;
        }

        HashSet<Enemy> singleHitEnemies = new HashSet<Enemy>();
        bool hitSomething = ResolveSingleShot(character, targetCell.Value, damage, singleHitEnemies);
        if (hitSomething)
        {
            PlayConfiguredFx(character, singleHitEnemies);
        }

        return hitSomething;
    }

    private bool ResolveSingleShot(Character character, Vector2Int targetCell, int damage, HashSet<Enemy> hitEnemies)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        if (!HectorAbilityUtils.TryResolveAlignedDirection(character.GridPosition, targetCell, true, range, out Vector2Int direction, out _)
            || !HectorAbilityUtils.IsLineClear(character.Board, character.GridPosition, targetCell, direction))
        {
            return false;
        }

        character.FaceTargetCell(targetCell);

        if (character.Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null)
        {
            HectorAbilityUtils.TryPlayLinearProjectile(
                character,
                projectilePrefab,
                enemy.transform.position + projectileImpactOffset,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay,
                () =>
                {
                    character.DealDamageToEnemy(enemy, damage, true, true, DamageSoundType.Default, this);
                    ResolveRicochet(character, enemy, damage, hitEnemies);
                });
            hitEnemies?.Add(enemy);
            return true;
        }

        if (character.Board.TryGetLichSkullObject(targetCell, out LichSkullObject skull) && skull != null)
        {
            HectorAbilityUtils.TryPlayLinearProjectile(
                character,
                projectilePrefab,
                skull.transform.position + projectileImpactOffset,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay,
                () => character.DealDamageToLichSkull(skull, damage, true, DamageSoundType.Default, this));
            return true;
        }

        if (character.Board.TryGetBarrel(targetCell, out BarrelObstacle barrel) && barrel != null)
        {
            HectorAbilityUtils.TryPlayLinearProjectile(
                character,
                projectilePrefab,
                character.Board.GridToWorldPosition(barrel.GridPosition) + projectileImpactOffset,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay,
                barrel.TakeHit);
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

    private List<Vector2Int> GatherMultiShotTargets(Character character)
    {
        List<Vector2Int> targets = new List<Vector2Int>();
        if (character == null || character.Board == null)
        {
            return targets;
        }

        Vector2Int[] directions = HectorAbilityUtils.OrthogonalAndDiagonalDirections;
        for (int index = 0; index < directions.Length; index++)
        {
            if (HectorAbilityUtils.TryGetFirstEnemyLikeTargetInDirection(
                    character.Board,
                    character.GridPosition,
                    directions[index],
                    range,
                    out Vector2Int targetCell,
                    out _,
                    out _,
                    out _))
            {
                targets.Add(targetCell);
            }
        }

        return targets;
    }

    private bool CanTargetCell(Character character, Vector2Int targetCell, bool allowDiagonals, bool allowEmptyCell = false)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        if (!HectorAbilityUtils.TryResolveAlignedDirection(character.GridPosition, targetCell, allowDiagonals, range, out Vector2Int direction, out _)
            || !HectorAbilityUtils.IsLineClear(character.Board, character.GridPosition, targetCell, direction))
        {
            return false;
        }

        if (allowEmptyCell)
        {
            return character.Board.IsInsideBoard(targetCell);
        }

        return (character.Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null)
            || (character.Board.TryGetLichSkullObject(targetCell, out LichSkullObject skull) && skull != null)
            || (character.Board.TryGetBarrel(targetCell, out BarrelObstacle barrel) && barrel != null);
    }
}
