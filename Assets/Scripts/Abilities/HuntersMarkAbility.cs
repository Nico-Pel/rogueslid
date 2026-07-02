using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Hunter's Mark", fileName = "HuntersMark")]
public class HuntersMarkAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDashRange = 2;
    [SerializeField] private GameObject postDashFxPrefab;
    [Min(0f)]
    [SerializeField] private float postDashFxDuration = 1f;
    [SerializeField] private GameObject markFxPrefab;
    [Min(0.01f)]
    [SerializeField] private float markFxAppearDuration = 0.2f;
    [SerializeField] private GameObject markBurstFxPrefab;
    [Min(0f)]
    [SerializeField] private float markBurstFxDuration = 1f;
    [SerializeField] private AudioClip markBurstSound;
    [Min(0.1f)]
    [SerializeField] private float markBurstSoundPitch = 1.15f;

    private readonly Dictionary<Enemy, GameObject> activeMarkFxByEnemy = new Dictionary<Enemy, GameObject>();

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        int dashRange = GetDashRange(character);
        int markRadius = GetMarkRadius(character);
        bool diagonalDash = character != null && character.GetUpgradeStacks(AbilityUpgradeKey.HuntersMarkShortcut) > 0;
        bool persistentMark = character != null && character.GetUpgradeStacks(AbilityUpgradeKey.HuntersMarkRelentlessMark) > 0;
        return $"Dash up to {dashRange} tile(s) in a straight line{(diagonalDash ? " or diagonal" : string.Empty)}. After moving, enemies within {markRadius} tile(s) are marked. Until the end of the turn, the next ranged attack against a marked enemy deals +1 damage per tile separating Hector and the target, with adjacent targets counting as 1. Marks {(persistentMark ? "last until consumed" : "expire at end of turn")}.";
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        Vector2Int[] directions = GetAllowedDirections(character);
        int maxRange = GetDashRange(character);
        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            Vector2Int direction = directions[directionIndex];
            for (int distance = 1; distance <= maxRange; distance++)
            {
                if (CanActivateOnCell(character, runtime, character.GridPosition + (direction * distance)))
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

        if (!TryResolveDash(character, targetCell, out Vector2Int direction, out int distance))
        {
            return false;
        }

        if (!character.Board.TryGetCell(targetCell, out BoardCell targetBoardCell) || !targetBoardCell.Walkable || targetBoardCell.IsOccupied)
        {
            return false;
        }

        Vector2Int scan = character.GridPosition + direction;
        while (scan != targetCell)
        {
            if (!character.Board.TryGetCell(scan, out BoardCell cell) || cell.HasBlockingTerrain || cell.IsOccupied)
            {
                return false;
            }

            scan += direction;
        }

        return distance > 0 && distance <= GetDashRange(character);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return character != null
            && character.Board != null
            && TryResolveDash(character, targetCell, out _, out _);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue || !CanActivateOnCell(character, runtime, targetCell.Value))
        {
            return false;
        }

        character.FaceTargetCell(targetCell.Value);
        character.StartCoroutine(ResolveHuntersMark(character, targetCell.Value));
        return true;
    }

    public void PlayMarkBurstFeedback(Character character, Enemy enemy)
    {
        if (character == null || enemy == null)
        {
            return;
        }

        if (markBurstFxPrefab != null)
        {
            character.PlayFeedbackFx(markBurstFxPrefab, enemy.EffectAnchor, destroyAfterSeconds: markBurstFxDuration);
        }

        if (markBurstSound != null && enemy.EffectAnchor != null)
        {
            SoundManager.Instance?.PlaySound(markBurstSound, enemy.EffectAnchor.position, 1f, markBurstSoundPitch);
        }
    }

    public void OnMarkApplied(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        ClearMarkFx(enemy);
        if (markFxPrefab == null)
        {
            return;
        }

        Transform anchor = enemy.EffectAnchor != null ? enemy.EffectAnchor : enemy.transform;
        GameObject markFx = Instantiate(markFxPrefab, anchor.position, markFxPrefab.transform.rotation, anchor);
        Vector3 targetScale = markFxPrefab.transform.localScale;
        markFx.transform.localScale = Vector3.zero;
        markFx.transform.DOScale(targetScale, Mathf.Max(0.01f, markFxAppearDuration)).SetEase(Ease.OutBack);
        activeMarkFxByEnemy[enemy] = markFx;
    }

    public void OnMarkCleared(Enemy enemy)
    {
        ClearMarkFx(enemy);
    }

    private System.Collections.IEnumerator ResolveHuntersMark(Character character, Vector2Int targetCell)
    {
        if (character == null)
        {
            yield break;
        }

        bool persistentMark = character.GetUpgradeStacks(AbilityUpgradeKey.HuntersMarkRelentlessMark) > 0;
        int fearStacks = character.GetUpgradeStacks(AbilityUpgradeKey.HuntersMarkSkeetShooting);
        bool foretasteActive = character.GetUpgradeStacks(AbilityUpgradeKey.HuntersMarkForetaste) > 0;

        character.BeginActionLock();
        bool success = character.TryTeleportTo(targetCell);
        if (!success)
        {
            character.EndActionLock();
            yield break;
        }

        yield return new WaitUntil(() => !character.IsMoving);
        if (postDashFxPrefab != null)
        {
            Quaternion fxRotation = postDashFxPrefab.transform.rotation;
            GameObject postDashFx = Instantiate(postDashFxPrefab, character.transform.position, fxRotation);
            Vector3 baseScale = postDashFxPrefab.transform.localScale;
            int veteranTrackerStacks = character.GetUpgradeStacks(AbilityUpgradeKey.HuntersMarkVeteranTracker);
            postDashFx.transform.localScale = baseScale + (Vector3.one * veteranTrackerStacks);
            if (postDashFxDuration > 0f)
            {
                Destroy(postDashFx, postDashFxDuration);
            }
        }

        int markRadius = GetMarkRadius(character);
        List<(Enemy enemy, int distance)> enemiesToMark = new List<(Enemy enemy, int distance)>();
        for (int offsetX = -markRadius; offsetX <= markRadius; offsetX++)
        {
            for (int offsetY = -markRadius; offsetY <= markRadius; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                int distance = Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY));
                if (distance > markRadius)
                {
                    continue;
                }

                Vector2Int markedCell = character.GridPosition + new Vector2Int(offsetX, offsetY);
                if (!character.Board.TryGetEnemy(markedCell, out Enemy enemy) || enemy == null)
                {
                    continue;
                }

                enemiesToMark.Add((enemy, Mathf.Max(1, distance)));
            }
        }

        enemiesToMark.Sort((left, right) => left.distance.CompareTo(right.distance));
        int lastAppliedDistance = 0;
        for (int index = 0; index < enemiesToMark.Count; index++)
        {
            (Enemy enemy, int distance) markTarget = enemiesToMark[index];
            if (markTarget.enemy == null || markTarget.enemy.CurrentHealth <= 0)
            {
                continue;
            }

            int distanceDelta = Mathf.Max(0, markTarget.distance - lastAppliedDistance);
            float applicationDelay = 0.1f * distanceDelta;
            if (applicationDelay > 0f)
            {
                yield return new WaitForSeconds(applicationDelay);
            }
            lastAppliedDistance = markTarget.distance;

            if (markTarget.enemy == null || markTarget.enemy.CurrentHealth <= 0)
            {
                continue;
            }

            if (foretasteActive)
            {
                character.DealDamageToEnemy(markTarget.enemy, 2, false, true, DamageSoundType.ArrowHit, this);
                if (markTarget.enemy.CurrentHealth <= 0)
                {
                    continue;
                }
            }

            character.ApplyHuntersMark(markTarget.enemy, this, persistentMark);
            if (fearStacks > 0 && Random.value < 0.07f * fearStacks)
            {
                markTarget.enemy.ApplyFear(1);
            }
        }

        PlayConfiguredFx(character);
        character.EndActionLock();
    }

    private bool TryResolveDash(Character character, Vector2Int targetCell, out Vector2Int direction, out int distance)
    {
        direction = Vector2Int.zero;
        distance = 0;
        if (character == null)
        {
            return false;
        }

        bool allowDiagonals = character.GetUpgradeStacks(AbilityUpgradeKey.HuntersMarkShortcut) > 0;
        return HectorAbilityUtils.TryResolveAlignedDirection(character.GridPosition, targetCell, allowDiagonals, GetDashRange(character), out direction, out distance);
    }

    private int GetDashRange(Character character)
    {
        return Mathf.Max(1, baseDashRange);
    }

    private int GetMarkRadius(Character character)
    {
        return Mathf.Max(1, 1 + (character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.HuntersMarkVeteranTracker) : 0));
    }

    private Vector2Int[] GetAllowedDirections(Character character)
    {
        return character != null && character.GetUpgradeStacks(AbilityUpgradeKey.HuntersMarkShortcut) > 0
            ? HectorAbilityUtils.OrthogonalAndDiagonalDirections
            : HectorAbilityUtils.OrthogonalDirections;
    }

    private void ClearMarkFx(Enemy enemy)
    {
        if (enemy == null || !activeMarkFxByEnemy.TryGetValue(enemy, out GameObject markFx))
        {
            return;
        }

        activeMarkFxByEnemy.Remove(enemy);
        if (markFx != null)
        {
            Destroy(markFx);
        }
    }
}
