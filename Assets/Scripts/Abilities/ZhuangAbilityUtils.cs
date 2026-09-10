using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public static class ZhuangAbilityUtils
{
    public static readonly Vector2Int[] OrthogonalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public static readonly Vector2Int[] DiagonalDirections =
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1)
    };

    public static int GetChebyshevDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    public static int GetManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    public static Enemy ChooseWeakestEnemy(IReadOnlyList<Enemy> enemies)
    {
        Enemy bestEnemy = null;
        int bestHealth = int.MaxValue;
        int tieCount = 0;

        if (enemies == null)
        {
            return null;
        }

        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy enemy = enemies[index];
            if (enemy == null || enemy.CurrentHealth <= 0)
            {
                continue;
            }

            if (bestEnemy == null || enemy.CurrentHealth < bestHealth)
            {
                bestEnemy = enemy;
                bestHealth = enemy.CurrentHealth;
                tieCount = 1;
            }
            else if (enemy.CurrentHealth == bestHealth && Random.Range(0, ++tieCount) == 0)
            {
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }

    public static bool TryCollectAdjacentEnemies(BoardManager board, Vector2Int centerCell, List<Enemy> results, bool includeDiagonals = true)
    {
        if (board == null || results == null)
        {
            return false;
        }

        results.Clear();
        AddEnemiesInDirections(board, centerCell, OrthogonalDirections, results);
        if (includeDiagonals)
        {
            AddEnemiesInDirections(board, centerCell, DiagonalDirections, results);
        }

        return results.Count > 0;
    }

    private static void AddEnemiesInDirections(BoardManager board, Vector2Int centerCell, IReadOnlyList<Vector2Int> directions, List<Enemy> results)
    {
        for (int index = 0; index < directions.Count; index++)
        {
            Vector2Int candidateCell = centerCell + directions[index];
            if (board.TryGetEnemy(candidateCell, out Enemy enemy) && enemy != null)
            {
                results.Add(enemy);
            }
        }
    }

    public static List<Enemy> EnemiesInRadius(BoardManager board, Vector2Int center, int radius)
    {
        List<Enemy> enemies = new List<Enemy>();
        if (board == null) return enemies;
        foreach (Enemy enemy in board.SpawnedEnemies)
            if (enemy != null && enemy.CurrentHealth > 0 && GetChebyshevDistance(center, enemy.GridPosition) <= radius)
                enemies.Add(enemy);
        return enemies;
    }

    public static GameObject Fx(GameObject prefab, Vector3 position, Quaternion? rotation = null, float lifetime = 1.5f, Transform parent = null)
    {
        if (prefab == null) return null;
        GameObject instance = Object.Instantiate(prefab, position, rotation ?? prefab.transform.rotation, parent);
        if (lifetime > 0) Object.Destroy(instance, lifetime);
        return instance;
    }

    public static void Sound(GameObject prefab, Vector3 position)
    {
        if (prefab != null) prefab.GetComponentInChildren<SoundParameters>(true)?.PlaySound(position);
    }

    public static IEnumerator LockedAction(Character character, IEnumerator action)
    {
        character.BeginActionLock();
        try { yield return action; }
        finally { if (character != null) character.EndActionLock(); }
        if (character != null) character.RefreshAbilityState();
    }

    public static bool Launch(Character character, Enemy enemy, GameObject fx = null, GameObject sound = null, bool allowMomentum = true,
        float duration = 0.55f, float height = 0.55f, float fxLifetime = 1f)
    {
        if (character == null || enemy == null || !enemy.TryLaunchAirborne(duration, height, fx, fxLifetime)) return false;
        Sound(sound, enemy.transform.position);
        foreach (CharacterAbilityRuntime runtime in character.Abilities)
            if (runtime.Definition is ZhuangAbility ability)
                ability.OnEnemyLaunched(character, runtime, enemy, allowMomentum);
        character.RefreshAbilityState();
        return true;
    }

    public static bool StraightDirection(Vector2Int from, Vector2Int to, out Vector2Int direction)
    {
        Vector2Int delta = to - from;
        direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        return delta != Vector2Int.zero && (delta.x == 0 || delta.y == 0);
    }

    public static bool ClearLine(BoardManager board, Vector2Int from, Vector2Int to, bool unitsBlock = true)
    {
        if (!StraightDirection(from, to, out Vector2Int direction)) return false;
        for (Vector2Int cell = from + direction; cell != to; cell += direction)
            if (!board.TryGetCell(cell, out BoardCell data) || data.HasBlockingTerrain || (unitsBlock && data.IsOccupied)) return false;
        return board.IsInsideBoard(to);
    }

    public static IEnumerator Projectile(GameObject prefab, Vector3 from, System.Func<Vector3> destination, float duration)
    {
        GameObject projectile = Fx(prefab, from, lifetime: 0);
        try
        {
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (projectile != null)
                {
                    Vector3 target = destination();
                    projectile.transform.position = Vector3.Lerp(from, target, elapsed / duration);
                    if ((target - from).sqrMagnitude > 0.001f) projectile.transform.rotation = Quaternion.LookRotation(target - from);
                }
                yield return null;
            }
        }
        finally { if (projectile != null) Object.Destroy(projectile); }
    }

    public static void Preview(BoardManager board, Vector2Int center, int radius, float duration)
    {
        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
        {
            Vector2Int cell = center + new Vector2Int(x, y);
            if (!board.IsInsideBoard(cell)) continue;
            GameObject marker = new GameObject("Zhuang area preview");
            LineRenderer line = marker.AddComponent<LineRenderer>();
            line.sharedMaterial = PreviewMaterial;
            line.startColor = line.endColor = Color.cyan;
            line.startWidth = line.endWidth = 0.035f;
            line.positionCount = 4;
            line.loop = true;
            Vector3 p = board.GridToWorldPosition(cell) + Vector3.up * 0.08f;
            line.SetPositions(new[] { p + new Vector3(-0.4f, 0, -0.4f), p + new Vector3(0.4f, 0, -0.4f), p + new Vector3(0.4f, 0, 0.4f), p + new Vector3(-0.4f, 0, 0.4f) });
            Object.Destroy(marker, duration);
        }
    }

    private static Material previewMaterial;
    private static Material PreviewMaterial => previewMaterial != null ? previewMaterial : (previewMaterial = new Material(Shader.Find("Sprites/Default")));
}
