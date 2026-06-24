using System.Collections.Generic;
using System.Collections;
using System;
using DG.Tweening;
using UnityEngine;

public static class HectorAbilityUtils
{
    public static readonly Vector2Int[] OrthogonalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public static readonly Vector2Int[] OrthogonalAndDiagonalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left,
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1)
    };

    public static bool TryResolveAlignedDirection(
        Vector2Int origin,
        Vector2Int targetCell,
        bool allowDiagonals,
        int maxRange,
        out Vector2Int direction,
        out int distance)
    {
        direction = Vector2Int.zero;
        distance = 0;

        Vector2Int delta = targetCell - origin;
        int absX = Mathf.Abs(delta.x);
        int absY = Mathf.Abs(delta.y);
        if (delta == Vector2Int.zero)
        {
            return false;
        }

        bool orthogonal = delta.x == 0 || delta.y == 0;
        bool diagonal = allowDiagonals && absX == absY && absX > 0;
        if (!orthogonal && !diagonal)
        {
            return false;
        }

        distance = orthogonal ? absX + absY : absX;
        if (distance <= 0 || distance > maxRange)
        {
            return false;
        }

        direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        return true;
    }

    public static bool IsLineClear(BoardManager board, Vector2Int origin, Vector2Int targetCell, Vector2Int direction)
    {
        if (board == null || direction == Vector2Int.zero)
        {
            return false;
        }

        Vector2Int scan = origin + direction;
        while (scan != targetCell)
        {
            if (!board.TryGetCell(scan, out BoardCell cell) || cell.HasBlockingTerrain || cell.IsOccupied)
            {
                return false;
            }

            scan += direction;
        }

        return true;
    }

    public static bool TryGetFirstEnemyLikeTargetInDirection(
        BoardManager board,
        Vector2Int origin,
        Vector2Int direction,
        int maxRange,
        out Vector2Int targetCell,
        out Enemy enemy,
        out LichSkullObject skull,
        out BarrelObstacle barrel)
    {
        targetCell = origin;
        enemy = null;
        skull = null;
        barrel = null;

        if (board == null || direction == Vector2Int.zero || maxRange <= 0)
        {
            return false;
        }

        for (int step = 1; step <= maxRange; step++)
        {
            Vector2Int cellPosition = origin + (direction * step);
            if (!board.TryGetCell(cellPosition, out BoardCell cell))
            {
                break;
            }

            if (board.TryGetEnemy(cellPosition, out enemy) && enemy != null)
            {
                targetCell = cellPosition;
                return true;
            }

            if (board.TryGetLichSkullObject(cellPosition, out skull) && skull != null)
            {
                targetCell = cellPosition;
                return true;
            }

            if (board.TryGetBarrel(cellPosition, out barrel) && barrel != null)
            {
                targetCell = cellPosition;
                return true;
            }

            if (cell.HasBlockingTerrain || cell.IsOccupied)
            {
                break;
            }
        }

        return false;
    }

    public static List<Enemy> FindEnemiesWithinRange(BoardManager board, Vector2Int centerCell, int range)
    {
        List<Enemy> enemies = new List<Enemy>();
        if (board == null || range <= 0)
        {
            return enemies;
        }

        IReadOnlyList<Enemy> spawnedEnemies = board.SpawnedEnemies;
        for (int index = 0; index < spawnedEnemies.Count; index++)
        {
            Enemy enemy = spawnedEnemies[index];
            if (enemy == null || enemy.CurrentHealth <= 0)
            {
                continue;
            }

            int distance = Mathf.Max(
                Mathf.Abs(enemy.GridPosition.x - centerCell.x),
                Mathf.Abs(enemy.GridPosition.y - centerCell.y));
            if (distance > 0 && distance <= range)
            {
                enemies.Add(enemy);
            }
        }

        return enemies;
    }

    public static void TryPlayLinearProjectile(
        Character character,
        GameObject projectilePrefab,
        Vector3 targetPosition,
        float projectileSpeed,
        Vector3 spawnOffset,
        float launchDelay = 0f,
        Action onImpact = null,
        bool playArrowShotSound = true)
    {
        if (character == null || projectilePrefab == null)
        {
            return;
        }

        character.StartCoroutine(PlayLinearProjectileRoutine(
            character,
            projectilePrefab,
            targetPosition,
            projectileSpeed,
            spawnOffset,
            launchDelay,
            onImpact,
            playArrowShotSound));
    }

    public static float EstimateLinearProjectileTravelDelay(
        Character character,
        Vector3 targetPosition,
        float projectileSpeed,
        Vector3 spawnOffset,
        float launchDelay = 0f)
    {
        if (character == null)
        {
            return Mathf.Max(0f, launchDelay);
        }

        Vector3 startPosition = character.transform.position + spawnOffset;
        float travelDuration = Mathf.Max(0.05f, Vector3.Distance(startPosition, targetPosition) / Mathf.Max(0.01f, projectileSpeed));
        return Mathf.Max(0f, launchDelay) + travelDuration;
    }

    public static void TryPlayArcProjectile(
        Character character,
        GameObject projectilePrefab,
        Vector3 targetPosition,
        float travelDuration,
        float jumpPower,
        Vector3 spawnOffset,
        float launchDelay = 0f,
        Func<GameObject, bool> onImpact = null,
        bool playArrowShotSound = true)
    {
        if (character == null || projectilePrefab == null)
        {
            return;
        }

        character.StartCoroutine(PlayArcProjectileRoutine(
            character,
            projectilePrefab,
            targetPosition,
            travelDuration,
            jumpPower,
            spawnOffset,
            launchDelay,
            onImpact,
            playArrowShotSound));
    }

    private static IEnumerator PlayLinearProjectileRoutine(
        Character character,
        GameObject projectilePrefab,
        Vector3 targetPosition,
        float projectileSpeed,
        Vector3 spawnOffset,
        float launchDelay,
        Action onImpact,
        bool playArrowShotSound)
    {
        if (character == null || projectilePrefab == null)
        {
            yield break;
        }

        if (launchDelay > 0f)
        {
            yield return new WaitForSeconds(launchDelay);
        }

        Vector3 startPosition = character.transform.position + spawnOffset;
        if (playArrowShotSound)
        {
            SoundManager.Instance?.PlayArrowShot(startPosition);
        }

        GameObject projectile = UnityEngine.Object.Instantiate(projectilePrefab, startPosition, Quaternion.identity);
        projectile.transform.localScale = projectilePrefab.transform.localScale;

        Vector3 travelDirection = targetPosition - startPosition;
        if (travelDirection.sqrMagnitude > 0.0001f)
        {
            projectile.transform.rotation = Quaternion.LookRotation(travelDirection.normalized, Vector3.up);
        }

        float duration = Mathf.Max(0.05f, travelDirection.magnitude / Mathf.Max(0.01f, projectileSpeed));
        Tween projectileTween = projectile.transform.DOMove(targetPosition, duration).SetEase(Ease.Linear);
        yield return projectileTween.WaitForCompletion();

        onImpact?.Invoke();
        UnityEngine.Object.Destroy(projectile);
    }

    private static IEnumerator PlayArcProjectileRoutine(
        Character character,
        GameObject projectilePrefab,
        Vector3 targetPosition,
        float travelDuration,
        float jumpPower,
        Vector3 spawnOffset,
        float launchDelay,
        Func<GameObject, bool> onImpact,
        bool playArrowShotSound)
    {
        if (character == null || projectilePrefab == null)
        {
            yield break;
        }

        if (launchDelay > 0f)
        {
            yield return new WaitForSeconds(launchDelay);
        }

        Vector3 startPosition = character.transform.position + spawnOffset;
        if (playArrowShotSound)
        {
            SoundManager.Instance?.PlayArrowShot(startPosition);
        }

        GameObject projectile = UnityEngine.Object.Instantiate(projectilePrefab, startPosition, Quaternion.identity);
        projectile.transform.localScale = projectilePrefab.transform.localScale;

        Vector3 travelDirection = targetPosition - startPosition;
        if (travelDirection.sqrMagnitude > 0.0001f)
        {
            projectile.transform.rotation = Quaternion.LookRotation(travelDirection.normalized, Vector3.up);
        }

        float duration = Mathf.Max(0.05f, travelDuration);
        Tween jumpTween = projectile.transform.DOJump(targetPosition, Mathf.Max(0.01f, jumpPower), 1, duration).SetEase(Ease.Linear);
        yield return jumpTween.WaitForCompletion();

        bool shouldAutoDestroy = onImpact == null || onImpact(projectile);
        if (projectile != null && shouldAutoDestroy)
        {
            UnityEngine.Object.Destroy(projectile);
        }
    }
}
