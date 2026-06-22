using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Somersault Jump", fileName = "SomersaultJump")]
public class SomersaultJumpAbility : AbilityDefinition
{
    private readonly struct JumpContext
    {
        public JumpContext(Vector2Int targetCell, bool triggersVolley)
        {
            TargetCell = targetCell;
            TriggersVolley = triggersVolley;
        }

        public Vector2Int TargetCell { get; }
        public bool TriggersVolley { get; }
    }

    [Min(1)]
    [SerializeField] private int baseDamage = 1;
    [Min(1)]
    [SerializeField] private int adjacentRange = 1;
    [Min(1)]
    [SerializeField] private int volleyRange = 10;
    [SerializeField] private GameObject projectilePrefab;
    [Min(0.01f)]
    [SerializeField] private float projectileSpeed = 16f;
    [Min(0f)]
    [SerializeField] private float projectileLaunchDelay = 0.04f;
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 projectileImpactOffset = new Vector3(0f, 0.2f, 0f);
    [Header("Jump Timing")]
    [Min(0.01f)]
    [SerializeField] private float jumpDurationPerCell = 0.12f;
    [Min(0.01f)]
    [SerializeField] private float minimumJumpDuration = 0.12f;
    [Header("Landing FX")]
    [SerializeField] private GameObject landingFxPrefab;
    [SerializeField] private Vector3 landingFxOffset;
    [Min(0f)]
    [SerializeField] private float landingFxDestroyAfterSeconds = 1f;
    [Min(0f)]
    [SerializeField] private float volleyDelayAfterLanding = 0.1f;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override int GetBonusUsesPerTurn(Character character, CharacterAbilityRuntime runtime)
    {
        return character != null && character.GetUpgradeStacks(AbilityUpgradeKey.SomersaultJumpDoubleJump) > 0 ? 1 : 0;
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return TryBuildJumpContext(character, targetCell, out _);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return TryBuildJumpContext(character, targetCell, out _);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (!targetCell.HasValue || !TryBuildJumpContext(character, targetCell.Value, out JumpContext context))
        {
            return false;
        }

        character.StartCoroutine(ResolveJumpSequence(character, context));
        return true;
    }

    private IEnumerator ResolveJumpSequence(Character character, JumpContext context)
    {
        if (character == null)
        {
            yield break;
        }

        character.BeginActionLock();
        character.FaceTargetCell(context.TargetCell);

        int jumpDistance = Mathf.Max(
            Mathf.Abs(context.TargetCell.x - character.GridPosition.x),
            Mathf.Abs(context.TargetCell.y - character.GridPosition.y));
        float jumpDuration = Mathf.Max(minimumJumpDuration, jumpDistance * jumpDurationPerCell);

        bool success = character.TryTeleportTo(context.TargetCell, jumpDuration);
        if (!success)
        {
            character.EndActionLock();
            yield break;
        }

        yield return new WaitUntil(() => !character.IsMoving);
        PlayLandingFx(character);
        if (volleyDelayAfterLanding > 0f)
        {
            yield return new WaitForSeconds(volleyDelayAfterLanding);
        }

        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
        if (context.TriggersVolley)
        {
            FireVolley(character, hitEnemies);
        }

        PlayConfiguredFx(character, hitEnemies);
        character.EndActionLock();
    }

    private void FireVolley(Character character, HashSet<Enemy> hitEnemies)
    {
        if (character == null || character.Board == null)
        {
            return;
        }

        bool applyBleed = character.GetUpgradeStacks(AbilityUpgradeKey.SomersaultJumpRoseThorns) > 0;
        bool playedShotSound = false;
        Vector2Int[] directions = HectorAbilityUtils.OrthogonalAndDiagonalDirections;
        for (int index = 0; index < directions.Length; index++)
        {
            FireVolleyInDirection(character, directions[index], hitEnemies, applyBleed, ref playedShotSound);
        }
    }

    private void FireVolleyInDirection(
        Character character,
        Vector2Int direction,
        HashSet<Enemy> hitEnemies,
        bool applyBleed,
        ref bool playedShotSound)
    {
        if (character == null || character.Board == null || direction == Vector2Int.zero)
        {
            return;
        }

        if (!TryGetVolleyEndpoint(character, direction, out Vector2Int impactCell, out Enemy enemy, out LichSkullObject skull, out BarrelObstacle barrel))
        {
            return;
        }

        Vector3 targetPosition = character.Board.GridToWorldPosition(impactCell) + projectileImpactOffset;
        System.Action onImpact = null;

        if (enemy != null)
        {
            onImpact = () =>
            {
                int appliedDamage = character.DealDamageToEnemy(enemy, baseDamage, true, true, DamageSoundType.Default, this);
                if (applyBleed && appliedDamage > 0 && enemy != null && enemy.CurrentHealth > 0)
                {
                    enemy.ApplyStatusEffect(CombatStatusType.Bleeding, -1, 1);
                }
            };

            hitEnemies?.Add(enemy);
        }
        else if (skull != null)
        {
            onImpact = () => character.DealDamageToLichSkull(skull, baseDamage, true, DamageSoundType.Default, this);
        }
        else if (barrel != null)
        {
            onImpact = barrel.TakeHit;
        }

        HectorAbilityUtils.TryPlayLinearProjectile(
            character,
            projectilePrefab,
            targetPosition,
            projectileSpeed,
            projectileSpawnOffset,
            projectileLaunchDelay,
            onImpact,
            !playedShotSound);

        playedShotSound = true;
    }

    private void PlayLandingFx(Character character)
    {
        if (character == null || landingFxPrefab == null)
        {
            return;
        }

        character.PlayFeedbackFx(
            landingFxPrefab,
            positionOffset: landingFxOffset,
            destroyAfterSeconds: landingFxDestroyAfterSeconds);
    }

    private bool TryGetVolleyEndpoint(
        Character character,
        Vector2Int direction,
        out Vector2Int impactCell,
        out Enemy enemy,
        out LichSkullObject skull,
        out BarrelObstacle barrel)
    {
        impactCell = character != null ? character.GridPosition : Vector2Int.zero;
        enemy = null;
        skull = null;
        barrel = null;

        if (character == null || character.Board == null || direction == Vector2Int.zero || volleyRange <= 0)
        {
            return false;
        }

        Vector2Int lastInsideBoardCell = character.GridPosition;
        for (int step = 1; step <= volleyRange; step++)
        {
            Vector2Int scan = character.GridPosition + (direction * step);
            if (!character.Board.TryGetCell(scan, out BoardCell cell))
            {
                break;
            }

            lastInsideBoardCell = scan;

            if (character.Board.TryGetEnemy(scan, out enemy) && enemy != null)
            {
                impactCell = scan;
                return true;
            }

            if (character.Board.TryGetLichSkullObject(scan, out skull) && skull != null)
            {
                impactCell = scan;
                return true;
            }

            if (character.Board.TryGetBarrel(scan, out barrel) && barrel != null)
            {
                impactCell = scan;
                return true;
            }

            if (cell.HasBlockingTerrain || cell.IsOccupied)
            {
                impactCell = scan;
                return true;
            }
        }

        if (lastInsideBoardCell == character.GridPosition)
        {
            return false;
        }

        impactCell = lastInsideBoardCell;
        return true;
    }

    private bool TryBuildJumpContext(Character character, Vector2Int targetCell, out JumpContext context)
    {
        context = default;

        if (character == null || character.Board == null || targetCell == character.GridPosition || !character.Board.IsCellWalkable(targetCell))
        {
            return false;
        }

        if (!HectorAbilityUtils.TryResolveAlignedDirection(character.GridPosition, targetCell, true, Mathf.Max(character.Board.Width, character.Board.Height), out Vector2Int direction, out int distance))
        {
            return false;
        }

        if (distance <= adjacentRange)
        {
            return SetContextAndSucceed(targetCell, false, out context);
        }

        bool canSmallJump = character.GetUpgradeStacks(AbilityUpgradeKey.SomersaultJumpSmallJump) > 0;
        Vector2Int firstStepCell = character.GridPosition + direction;
        bool firstStepBlocked = IsJumpBlockedCell(character.Board, firstStepCell);

        if (firstStepBlocked)
        {
            for (int step = 1; step < distance; step++)
            {
                Vector2Int scan = character.GridPosition + (direction * step);
                if (!IsJumpBlockedCell(character.Board, scan))
                {
                    return false;
                }
            }

            int blockedCellsCount = distance - 1;
            if (blockedCellsCount > 1 && character.GetUpgradeStacks(AbilityUpgradeKey.SomersaultJumpHeroicCascade) <= 0)
            {
                return false;
            }

            return SetContextAndSucceed(targetCell, true, out context);
        }

        int maxFreeJumpDistance = canSmallJump ? 2 : adjacentRange;
        if (distance > maxFreeJumpDistance)
        {
            return false;
        }

        for (int step = 1; step < distance; step++)
        {
            Vector2Int scan = character.GridPosition + (direction * step);
            if (IsJumpBlockedCell(character.Board, scan))
            {
                return false;
            }
        }

        return SetContextAndSucceed(targetCell, false, out context);
    }

    private static bool IsJumpBlockedCell(BoardManager board, Vector2Int cellPosition)
    {
        if (board == null || !board.TryGetCell(cellPosition, out BoardCell cell))
        {
            return false;
        }

        return cell.HasBlockingTerrain
            || cell.IsOccupied
            || (board.TryGetBarrel(cellPosition, out BarrelObstacle barrel) && barrel != null)
            || (board.TryGetLichSkullObject(cellPosition, out LichSkullObject skull) && skull != null);
    }

    private static bool SetContextAndSucceed(Vector2Int targetCell, bool triggersVolley, out JumpContext context)
    {
        context = new JumpContext(targetCell, triggersVolley);
        return true;
    }
}
