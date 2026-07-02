using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct HectorShadowAttackContext
{
    public bool IsValid;
    public bool PrimaryFromShadow;
    public bool CanTwinAttack;
    public Vector2Int PrimaryOriginCell;
    public Vector2Int PrimaryDirection;
    public int PrimaryDistance;
    public Vector2Int SecondaryOriginCell;
    public Vector2Int SecondaryDirection;
    public int SecondaryDistance;
    public HectorShadowProxy ShadowProxy;
}

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Bidimensional Shadow", fileName = "BidimensionalShadow")]
public class BidimensionalShadowAbility : AbilityDefinition
{
    private sealed class ShadowState
    {
        public Character Owner;
        public CharacterAbilityRuntime Runtime;
        public HectorShadowProxy ActiveShadow;
        public Vector2Int ShadowCell;
        public bool SwapAvailable;
        public bool SwapConsumed;
    }

    [Min(1)]
    [SerializeField] private int radialCastRangeWithUpgrade = 5;
    [SerializeField] private Sprite swapAvailableIcon;
    [SerializeField] private HectorShadowProxy shadowPrefab;
    [SerializeField] private GameObject shadowSpawnFxPrefab;
    [SerializeField] private GameObject shadowDespawnFxPrefab;
    [SerializeField] private GameObject bidimensionalProjectilePrefab;
    [Min(0.01f)]
    [SerializeField] private float bidimensionalProjectileSpeed = 18f;
    [Min(0f)]
    [SerializeField] private float bidimensionalProjectileLaunchDelay = 0.08f;
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 projectileImpactOffset = new Vector3(0f, 0.2f, 0f);
    [Min(0f)]
    [SerializeField] private float openingProjectileVolleyDelay = 0.25f;

    private readonly Dictionary<CharacterAbilityRuntime, ShadowState> statesByRuntime = new Dictionary<CharacterAbilityRuntime, ShadowState>();
    private static readonly Dictionary<Character, ShadowState> activeStatesByCharacter = new Dictionary<Character, ShadowState>();

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        ShadowState state = GetState(runtime);
        bool canSwap = CanSwap(character, state);
        if (state.ActiveShadow != null)
        {
            return canSwap
                ? "Recast to swap places with Bidimensional Shadow. Hector's targeted attacks use the Shadow's sight first while it remains active."
                : "Bidimensional Shadow remains active until the end of the turn. Hector's targeted attacks use the Shadow's sight first while it remains active.";
        }

        bool hasRayUpgrade = character != null && character.GetUpgradeStacks(AbilityUpgradeKey.BidimensionalShadowBidimensionalRay) > 0;
        return hasRayUpgrade
            ? $"Place Bidimensional Shadow in a straight line with no range limit, or on any cell within {radialCastRangeWithUpgrade} tiles. Until end of turn, Hector's targeted attacks use the Shadow's sight first."
            : "Place Bidimensional Shadow on a cell in a straight line with no range limit. Until end of turn, Hector's targeted attacks use the Shadow's sight first.";
    }

    public override string GetCounterText(CharacterAbilityRuntime runtime)
    {
        ShadowState state = GetState(runtime);
        if (state.ActiveShadow == null)
        {
            return base.GetCounterText(runtime);
        }

        if (state.SwapAvailable && !state.SwapConsumed)
        {
            return "1";
        }

        return "ON";
    }

    public override Sprite GetIcon(CharacterAbilityRuntime runtime)
    {
        ShadowState state = GetState(runtime);
        if (state.ActiveShadow != null && state.SwapAvailable && !state.SwapConsumed && swapAvailableIcon != null)
        {
            return swapAvailableIcon;
        }

        return base.GetIcon(runtime);
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (!base.CanActivate(character, runtime) || character == null || character.Board == null)
        {
            return false;
        }

        ShadowState state = GetState(runtime);
        if (CanSwap(character, state))
        {
            return true;
        }

        return HasAnyValidPlacement(character);
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        ShadowState state = GetState(runtime);
        if (CanSwap(character, state))
        {
            return targetCell == state.ShadowCell;
        }

        return CanPlaceShadowAtCell(character, targetCell);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        ShadowState state = GetState(runtime);
        if (CanSwap(character, state))
        {
            return targetCell == state.ShadowCell;
        }

        if (!character.Board.IsInsideBoard(targetCell))
        {
            return false;
        }

        return IsStraightLinePlacement(character, targetCell) || IsRadialPlacementUnlockedAndValid(character, targetCell);
    }

    public override bool TryGetAutomaticTargetCell(Character character, CharacterAbilityRuntime runtime, out Vector2Int targetCell)
    {
        targetCell = default;
        ShadowState state = GetState(runtime);
        if (!CanSwap(character, state))
        {
            return false;
        }

        targetCell = state.ShadowCell;
        return true;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || character.Board == null || !targetCell.HasValue)
        {
            return false;
        }

        ShadowState state = GetState(runtime);
        if (CanSwap(character, state))
        {
            if (targetCell.Value != state.ShadowCell)
            {
                return false;
            }

            Vector2Int previousCharacterCell = character.GridPosition;
            if (!character.TryTeleportTo(state.ShadowCell))
            {
                return false;
            }

            state.ShadowCell = previousCharacterCell;
            UpdateShadowTransform(character, state);
            state.SwapConsumed = true;
            character.RefreshAbilityState();
            return true;
        }

        if (!CanPlaceShadowAtCell(character, targetCell.Value))
        {
            return false;
        }

        if (shadowPrefab == null)
        {
            return false;
        }

        ClearShadow(state, playDespawnFx: false);

        HectorShadowProxy shadowInstance = Instantiate(
            shadowPrefab,
            character.Board.GridToWorldPosition(targetCell.Value),
            shadowPrefab.transform.rotation);
        shadowInstance.transform.localScale = shadowPrefab.transform.localScale;
        shadowInstance.BindCharacter(character);

        state.Owner = character;
        state.Runtime = runtime;
        state.ActiveShadow = shadowInstance;
        state.ShadowCell = targetCell.Value;
        state.SwapAvailable = character.GetUpgradeStacks(AbilityUpgradeKey.BidimensionalShadowBidimensionalSwitch) > 0;
        state.SwapConsumed = false;
        activeStatesByCharacter[character] = state;

        PlayFxAtCell(character, targetCell.Value, shadowSpawnFxPrefab);

        if (character.GetUpgradeStacks(AbilityUpgradeKey.BidimensionalShadowBidimensionalProjectiles) > 0)
        {
            character.StartCoroutine(ResolveOpeningProjectileVolley(character, state));
        }

        return true;
    }

    public override void OnAbilityActivated(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null)
        {
            return;
        }

        ApplyTrailColorEffect(character, runtime);

        ShadowState state = GetState(runtime);
        if (state != null && state.SwapConsumed)
        {
            ApplyTrailReplacementEffect(character, runtime);
        }

        if (state.ActiveShadow == null || runtime == null || !state.SwapAvailable || state.SwapConsumed)
        {
            return;
        }

        runtime.ResetAvailability();
        runtime.SetRemainingCooldown(0);
        character?.RefreshAbilityState();
    }

    public override void OnTurnEnded(Character character, CharacterAbilityRuntime runtime)
    {
        ShadowState state = GetState(runtime);
        if (state.ActiveShadow == null)
        {
            return;
        }

        ClearShadow(state, playDespawnFx: true);
        if (runtime != null && CooldownTurns > 0 && runtime.RemainingCooldown <= 0)
        {
            runtime.SetRemainingCooldown(CooldownTurns + 1);
        }

        character?.RefreshAbilityState();
    }

    public static bool TryResolveLineAttackContext(
        Character character,
        Vector2Int targetCell,
        bool allowDiagonals,
        int maxRange,
        out HectorShadowAttackContext context)
    {
        context = default;
        if (character == null || character.Board == null)
        {
            return false;
        }

        Vector2Int? shadowCellForCharacter = TryGetActiveShadowCell(character, out Vector2Int resolvedShadowCell)
            ? resolvedShadowCell
            : (Vector2Int?)null;
        bool characterHasLine = TryResolveActorLine(character, character.GridPosition, shadowCellForCharacter, targetCell, allowDiagonals, maxRange, out Vector2Int characterDirection, out int characterDistance);

        bool shadowHasLine = false;
        HectorShadowProxy shadowProxy = GetActiveShadowProxy(character);
        if (shadowProxy != null && TryGetActiveShadowCell(character, out Vector2Int shadowCell))
        {
            shadowHasLine = TryResolveActorLine(character, shadowCell, character.GridPosition, targetCell, allowDiagonals, maxRange, out Vector2Int shadowDirection, out int shadowDistance);
            if (shadowHasLine)
            {
                context.PrimaryFromShadow = true;
                context.PrimaryOriginCell = shadowCell;
                context.PrimaryDirection = shadowDirection;
                context.PrimaryDistance = shadowDistance;
                context.ShadowProxy = shadowProxy;
            }
        }

        if (!shadowHasLine && characterHasLine)
        {
            context.PrimaryFromShadow = false;
            context.PrimaryOriginCell = character.GridPosition;
            context.PrimaryDirection = characterDirection;
            context.PrimaryDistance = characterDistance;
        }

        if (!shadowHasLine && !characterHasLine)
        {
            return false;
        }

        bool twinsUnlocked = character.GetUpgradeStacks(AbilityUpgradeKey.BidimensionalShadowBidimensionalTwins) > 0;
        if (twinsUnlocked
            && shadowHasLine
            && characterHasLine
            && TryGetActiveShadowCell(character, out Vector2Int activeShadowCell)
            && activeShadowCell != character.GridPosition)
        {
            context.CanTwinAttack = true;
            context.SecondaryOriginCell = context.PrimaryFromShadow ? character.GridPosition : activeShadowCell;
            context.SecondaryDirection = context.PrimaryFromShadow ? characterDirection : context.PrimaryDirection;
            context.SecondaryDistance = context.PrimaryFromShadow ? characterDistance : context.PrimaryDistance;
        }

        context.IsValid = true;
        return true;
    }

    public static bool TryResolveRadialAttackOrigin(
        Character character,
        Vector2Int targetCell,
        int maxRange,
        out bool fromShadow,
        out Vector2Int originCell,
        out HectorShadowProxy shadowProxy)
    {
        fromShadow = false;
        originCell = default;
        shadowProxy = GetActiveShadowProxy(character);
        if (character == null || character.Board == null)
        {
            return false;
        }

        if (shadowProxy != null
            && TryGetActiveShadowCell(character, out Vector2Int shadowCell)
            && IsWithinRadialRange(shadowCell, targetCell, maxRange))
        {
            fromShadow = true;
            originCell = shadowCell;
            return true;
        }

        if (IsWithinRadialRange(character.GridPosition, targetCell, maxRange))
        {
            fromShadow = false;
            originCell = character.GridPosition;
            return true;
        }

        return false;
    }

    public static void FaceTargetForContext(Character character, HectorShadowAttackContext context, Vector2Int targetCell)
    {
        if (character == null || !context.IsValid)
        {
            return;
        }

        if (context.PrimaryFromShadow && context.ShadowProxy != null)
        {
            context.ShadowProxy.FaceTargetCell(context.PrimaryOriginCell, targetCell);
            if (context.CanTwinAttack)
            {
                character.FaceTargetCell(targetCell);
            }

            return;
        }

        character.FaceTargetCell(targetCell);
        if (context.CanTwinAttack && context.ShadowProxy != null && TryGetActiveShadowCell(character, out Vector2Int shadowCell))
        {
            context.ShadowProxy.FaceTargetCell(shadowCell, targetCell);
        }
    }

    public static void PlayAttackAnimationForContext(Character character, HectorShadowAttackContext context, AnimationClip attackAnimationClip)
    {
        if (!context.IsValid || attackAnimationClip == null || character == null)
        {
            return;
        }

        if (context.PrimaryFromShadow)
        {
            context.ShadowProxy?.PlayAttackAnimation(attackAnimationClip);
            if (context.CanTwinAttack)
            {
                character.PlayAttackAnimation(attackAnimationClip);
            }

            return;
        }

        character.PlayAttackAnimation(attackAnimationClip);
        if (context.CanTwinAttack)
        {
            context.ShadowProxy?.PlayAttackAnimation(attackAnimationClip);
        }
    }

    public static Vector3 GetPrimaryProjectileStartWorldPosition(Character character, HectorShadowAttackContext context, Vector3 spawnOffset)
    {
        if (context.PrimaryFromShadow && context.ShadowProxy != null)
        {
            return context.ShadowProxy.LaunchAnchor.position + spawnOffset;
        }

        return character.transform.position + spawnOffset;
    }

    public static Vector3 GetSecondaryProjectileStartWorldPosition(Character character, HectorShadowAttackContext context, Vector3 spawnOffset)
    {
        if (!context.CanTwinAttack)
        {
            return character.transform.position + spawnOffset;
        }

        if (context.PrimaryFromShadow)
        {
            return character.transform.position + spawnOffset;
        }

        return context.ShadowProxy != null
            ? context.ShadowProxy.LaunchAnchor.position + spawnOffset
            : character.transform.position + spawnOffset;
    }

    public static bool HasActiveShadow(Character character)
    {
        return GetActiveShadowProxy(character) != null;
    }

    public static HectorShadowProxy GetActiveShadowProxy(Character character)
    {
        if (character == null
            || !activeStatesByCharacter.TryGetValue(character, out ShadowState state)
            || state == null
            || state.ActiveShadow == null)
        {
            return null;
        }

        return state.ActiveShadow;
    }

    public static bool TryGetActiveShadowCell(Character character, out Vector2Int shadowCell)
    {
        shadowCell = default;
        if (character == null
            || !activeStatesByCharacter.TryGetValue(character, out ShadowState state)
            || state == null
            || state.ActiveShadow == null)
        {
            return false;
        }

        shadowCell = state.ShadowCell;
        return true;
    }

    private ShadowState GetState(CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return new ShadowState();
        }

        if (!statesByRuntime.TryGetValue(runtime, out ShadowState state) || state == null)
        {
            state = new ShadowState();
            statesByRuntime[runtime] = state;
        }

        return state;
    }

    private bool CanPlaceShadowAtCell(Character character, Vector2Int targetCell)
    {
        if (character == null || character.Board == null || !character.Board.IsInsideBoard(targetCell))
        {
            return false;
        }

        if (!character.Board.TryGetCell(targetCell, out BoardCell cell)
            || !cell.Walkable
            || cell.HasBlockingTerrain
            || (cell.IsOccupied && cell.Occupant != character.gameObject))
        {
            return false;
        }

        return IsStraightLinePlacement(character, targetCell) || IsRadialPlacementUnlockedAndValid(character, targetCell);
    }

    private bool IsStraightLinePlacement(Character character, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        if (!HectorAbilityUtils.TryResolveAlignedDirection(
                character.GridPosition,
                targetCell,
                false,
                Mathf.Max(character.Board.Width, character.Board.Height),
                out Vector2Int direction,
                out int distance))
        {
            return false;
        }

        Vector2Int scan = character.GridPosition + direction;
        for (int step = 1; step < distance; step++, scan += direction)
        {
            if (!character.Board.TryGetCell(scan, out BoardCell cell)
                || cell.HasBlockingTerrain
                || cell.IsOccupied)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsRadialPlacementUnlockedAndValid(Character character, Vector2Int targetCell)
    {
        return character != null
            && character.GetUpgradeStacks(AbilityUpgradeKey.BidimensionalShadowBidimensionalRay) > 0
            && IsWithinRadialRange(character.GridPosition, targetCell, radialCastRangeWithUpgrade);
    }

    private bool HasAnyValidPlacement(Character character)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        for (int x = 0; x < character.Board.Width; x++)
        {
            for (int y = 0; y < character.Board.Height; y++)
            {
                if (CanPlaceShadowAtCell(character, new Vector2Int(x, y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool CanSwap(Character character, ShadowState state)
    {
        return character != null
            && state != null
            && state.ActiveShadow != null
            && state.SwapAvailable
            && !state.SwapConsumed;
    }

    private IEnumerator ResolveOpeningProjectileVolley(Character character, ShadowState state)
    {
        if (character == null || state == null || state.ActiveShadow == null || character.Board == null)
        {
            yield break;
        }

        if (openingProjectileVolleyDelay > 0f)
        {
            yield return new WaitForSeconds(openingProjectileVolleyDelay);
        }

        if (state.ActiveShadow == null)
        {
            yield break;
        }

        int damage = character.GetUpgradeStacks(AbilityUpgradeKey.BidimensionalShadowBidimensionalProjectiles);
        if (damage <= 0 || bidimensionalProjectilePrefab == null)
        {
            yield break;
        }

        Vector2Int shadowCell = state.ShadowCell;
        Vector2Int[] directions = HectorAbilityUtils.OrthogonalAndDiagonalDirections;
        for (int index = 0; index < directions.Length; index++)
        {
            Vector2Int direction = directions[index];
            if (!TryFindFirstEnemyVisibleFromShadow(character, shadowCell, direction, out Enemy enemy))
            {
                continue;
            }

            Vector3 startPosition = state.ActiveShadow.LaunchAnchor.position + projectileSpawnOffset;
            Vector3 targetPosition = enemy.transform.position + projectileImpactOffset;
            HectorAbilityUtils.TryPlayLinearProjectileFromWorldPosition(
                character,
                bidimensionalProjectilePrefab,
                startPosition,
                targetPosition,
                bidimensionalProjectileSpeed,
                bidimensionalProjectileLaunchDelay,
                () =>
                {
                    if (enemy != null && enemy.CurrentHealth > 0)
                    {
                        character.DealDamageToEnemy(enemy, damage, true, true, DamageSoundType.Default, this);
                    }
                },
                index == 0);
        }
    }

    private bool TryFindFirstEnemyVisibleFromShadow(Character character, Vector2Int shadowCell, Vector2Int direction, out Enemy enemy)
    {
        enemy = null;
        if (character == null || character.Board == null)
        {
            return false;
        }

        int maxRange = Mathf.Max(character.Board.Width, character.Board.Height);
        for (int step = 1; step <= maxRange; step++)
        {
            Vector2Int scan = shadowCell + (direction * step);
            if (!character.Board.TryGetCell(scan, out BoardCell cell))
            {
                break;
            }

            if (scan == character.GridPosition)
            {
                break;
            }

            if (character.Board.TryGetEnemy(scan, out enemy) && enemy != null)
            {
                return true;
            }

            if (cell.HasBlockingTerrain || cell.IsOccupied)
            {
                break;
            }
        }

        return false;
    }

    private static bool TryResolveActorLine(
        Character character,
        Vector2Int originCell,
        Vector2Int? otherActorCell,
        Vector2Int targetCell,
        bool allowDiagonals,
        int maxRange,
        out Vector2Int direction,
        out int distance)
    {
        direction = Vector2Int.zero;
        distance = 0;
        if (character == null || character.Board == null)
        {
            return false;
        }

        if (!HectorAbilityUtils.TryResolveAlignedDirection(originCell, targetCell, allowDiagonals, maxRange, out direction, out distance))
        {
            return false;
        }

        Vector2Int scan = originCell + direction;
        while (scan != targetCell)
        {
            if (!character.Board.TryGetCell(scan, out BoardCell cell) || cell.HasBlockingTerrain || cell.IsOccupied)
            {
                return false;
            }

            if (otherActorCell.HasValue && scan == otherActorCell.Value)
            {
                return false;
            }

            scan += direction;
        }

        return true;
    }

    private void UpdateShadowTransform(Character character, ShadowState state)
    {
        if (character == null || state == null || state.ActiveShadow == null || character.Board == null)
        {
            return;
        }

        state.ActiveShadow.transform.position = character.Board.GridToWorldPosition(state.ShadowCell);
    }

    private void ClearShadow(ShadowState state, bool playDespawnFx)
    {
        if (state == null)
        {
            return;
        }

        if (playDespawnFx && state.Owner != null && state.Owner.Board != null && state.ActiveShadow != null)
        {
            PlayFxAtCell(state.Owner, state.ShadowCell, shadowDespawnFxPrefab);
        }

        if (state.Owner != null)
        {
            activeStatesByCharacter.Remove(state.Owner);
        }

        if (state.ActiveShadow != null)
        {
            state.ActiveShadow.BindCharacter(null);
            Destroy(state.ActiveShadow.gameObject);
        }

        state.ActiveShadow = null;
        state.SwapAvailable = false;
        state.SwapConsumed = false;
    }

    private void PlayFxAtCell(Character character, Vector2Int cell, GameObject fxPrefab)
    {
        if (character == null || character.Board == null || fxPrefab == null)
        {
            return;
        }

        GameObject fxInstance = Instantiate(
            fxPrefab,
            character.Board.GridToWorldPosition(cell),
            fxPrefab.transform.rotation);
        fxInstance.transform.localScale = fxPrefab.transform.localScale;
        Destroy(fxInstance, 2f);
    }

    private static bool IsWithinRadialRange(Vector2Int from, Vector2Int to, int range)
    {
        int deltaX = from.x - to.x;
        int deltaY = from.y - to.y;
        return (deltaX * deltaX) + (deltaY * deltaY) <= (range * range);
    }
}
