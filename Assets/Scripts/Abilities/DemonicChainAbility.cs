using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Demonic Chain", fileName = "DemonicChain")]
public class DemonicChainAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 4;
    [SerializeField] private SecondaryAbilityEffectDefinition chainedStrikeEffect;
    [SerializeField] private GameObject chainLineRendererPrefab;
    [SerializeField] private Vector3 chainStartOffset = new Vector3(0f, 0.1f, 0f);
    [SerializeField] private Vector3 chainEndOffset = new Vector3(0f, 0.1f, 0f);
    [Min(0f)]
    [SerializeField] private float chainLifetime = 0.4f;
    [Min(0f)]
    [SerializeField] private float chainedStrikeBumpDuration = 0.3f;

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        List<Enemy> targets = GatherTargets(character);
        if (targets.Count == 0)
        {
            return false;
        }

        int damage = baseDamage;
        int extraTargets = Mathf.Max(0, targets.Count - 1);
        if (extraTargets > 0)
        {
            damage += extraTargets * character.GetUpgradeStacks(AbilityUpgradeKey.DemonicChainGoodCatch);
        }

        List<Enemy> hitEnemies = new List<Enemy>(targets);
        character.StartCoroutine(ResolveDemonicChainSequence(character, runtime, targets, hitEnemies, damage));
        return true;
    }

    private IEnumerator ResolveDemonicChainSequence(
        Character character,
        CharacterAbilityRuntime runtime,
        List<Enemy> targets,
        List<Enemy> hitEnemies,
        int damage)
    {
        character.BeginActionLock();

        for (int index = 0; index < targets.Count; index++)
        {
            Enemy enemy = targets[index];
            if (enemy == null)
            {
                continue;
            }

            SpawnChainLine(character, enemy);
            character.DealDamageToEnemy(enemy, damage, true, true, DamageSoundType.Default, this);
            PullEnemyTowardsCharacter(character, enemy, false);
            yield return WaitForEnemyMovement(enemy);

            if (character.GetUpgradeStacks(AbilityUpgradeKey.DemonicChainExecutionersChains) > 0
                && enemy.CurrentHealth > 0
                && enemy.CurrentHealth <= 3
                && PullEnemyTowardsCharacter(character, enemy, true))
            {
                yield return WaitForEnemyMovement(enemy);
                character.DealDamageToEnemy(enemy, enemy.CurrentHealth, false);
            }

            if (character.GetUpgradeStacks(AbilityUpgradeKey.DemonicChainChainedStrike) > 0
                && enemy.CurrentHealth > 0
                && IsAdjacentOrDiagonal(character.GridPosition, enemy.GridPosition))
            {
                enemy.PlayCharacterCollisionBump(character.transform.position, chainedStrikeBumpDuration);
                AbilityExecutionContext chainedStrikeContext = new AbilityExecutionContext(
                    this,
                    runtime,
                    character.GridPosition,
                    enemy.GridPosition,
                    enemy);

                if (chainedStrikeEffect != null)
                {
                    chainedStrikeEffect.Execute(character, chainedStrikeContext);
                }
                else
                {
                    yield return new WaitForSeconds(chainedStrikeBumpDuration);
                    if (enemy != null && enemy.CurrentHealth > 0)
                    {
                        character.DealDamageToEnemy(enemy, 2, false, true, DamageSoundType.Default, this);
                    }
                    continue;
                }

                yield return new WaitForSeconds(chainedStrikeBumpDuration);
            }
        }

        PlayConfiguredFx(character, hitEnemies);
        character.EndActionLock();
    }

    private static IEnumerator WaitForEnemyMovement(Enemy enemy)
    {
        if (enemy == null)
        {
            yield break;
        }

        yield return new WaitUntil(() => enemy == null || !enemy.IsMoving);
    }

    private List<Enemy> GatherTargets(Character character)
    {
        List<Enemy> targets = new List<Enemy>();
        HashSet<Enemy> added = new HashSet<Enemy>();
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        bool includeDiagonals = character.GetUpgradeStacks(AbilityUpgradeKey.DemonicChainSpidersChains) > 0;
        if (includeDiagonals)
        {
            directions = new[]
            {
                Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
                new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1)
            };
        }

        bool pierceEnemies = character.GetUpgradeStacks(AbilityUpgradeKey.DemonicChainGhostChains) > 0;
        for (int index = 0; index < directions.Length; index++)
        {
            Vector2Int direction = directions[index];
            Vector2Int current = character.GridPosition + direction;
            while (character.Board.TryGetCell(current, out BoardCell cell))
            {
                if (character.Board.TryGetEnemy(current, out Enemy enemy) && enemy != null)
                {
                    if (added.Add(enemy))
                    {
                        targets.Add(enemy);
                    }

                    if (!pierceEnemies)
                    {
                        break;
                    }
                }

                if (cell.HasBlockingTerrain)
                {
                    break;
                }

                current += direction;
            }
        }

        return targets;
    }

    private static bool IsAdjacentOrDiagonal(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx > 0 || dy > 0) && dx <= 1 && dy <= 1;
    }

    private bool PullEnemyTowardsCharacter(Character character, Enemy enemy, bool pullUntilAdjacent)
    {
        if (character == null || enemy == null)
        {
            return false;
        }

        if (pullUntilAdjacent && IsAdjacentOrDiagonal(character.GridPosition, enemy.GridPosition))
        {
            return true;
        }

        bool moved = false;
        while (enemy.CurrentHealth > 0)
        {
            Vector2Int delta = character.GridPosition - enemy.GridPosition;
            Vector2Int step = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
            if (step == Vector2Int.zero)
            {
                break;
            }

            Vector2Int nextCell = enemy.GridPosition + step;
            if (nextCell == character.GridPosition || !enemy.TryForcedMoveTo(nextCell))
            {
                break;
            }

            moved = true;
            if (!pullUntilAdjacent || IsAdjacentOrDiagonal(character.GridPosition, enemy.GridPosition))
            {
                break;
            }
        }

        return pullUntilAdjacent
            ? IsAdjacentOrDiagonal(character.GridPosition, enemy.GridPosition)
            : moved;
    }

    private void SpawnChainLine(Character character, Enemy enemy)
    {
        if (chainLineRendererPrefab == null || character == null || enemy == null)
        {
            return;
        }

        GameObject chainObject = Instantiate(chainLineRendererPrefab);
        chainObject.name = $"{chainLineRendererPrefab.name}_{enemy.name}";

        ChainLineRendererFollower chainFollower = chainObject.GetComponent<ChainLineRendererFollower>();
        if (chainFollower == null)
        {
            chainFollower = chainObject.AddComponent<ChainLineRendererFollower>();
        }

        chainFollower.Setup(character.transform, enemy.transform, chainStartOffset, chainEndOffset);

        if (chainLifetime > 0f)
        {
            Destroy(chainObject, chainLifetime);
        }
    }
}
