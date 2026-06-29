using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Tridimensional Portal", fileName = "TridimensionalPortal")]
public class TridimensionalPortalAbility : AbilityDefinition
{
    [System.Serializable]
    private sealed class PortalFxConfig
    {
        [SerializeField] private GameObject fxPrefab;
        [SerializeField] private Vector3 positionOffset;
        [Min(0f)]
        [SerializeField] private float destroyAfterSeconds = 1f;

        public GameObject FxPrefab => fxPrefab;
        public Vector3 PositionOffset => positionOffset;
        public float DestroyAfterSeconds => destroyAfterSeconds;
    }

    private sealed class PlacedPortal
    {
        public Vector2Int PortalCell;
        public GameObject VisualInstance;
        public int RemainingExtraTurns;
    }

    private sealed class PortalState
    {
        public bool WasTeleportedThisTurn;
        public readonly List<PlacedPortal> ActivePortals = new List<PlacedPortal>();
    }

    [SerializeField] private Sprite activePortalIcon;
    [SerializeField] private GameObject portalVisualPrefab;
    [SerializeField] private Vector3 portalVisualOffset = new Vector3(0f, 0.08f, 0f);
    [Header("Portal FX")]
    [SerializeField] private PortalFxConfig portalPlacementFx;
    [SerializeField] private PortalFxConfig portalEntryFx;
    [SerializeField] private PortalFxConfig portalExitFx;
    [SerializeField] private PortalFxConfig portalImpactDepartureFx;
    [SerializeField] private PortalFxConfig portalImpactArrivalFx;
    [SerializeField] private PortalFxConfig portalDistortionSpawnFx;
    [Header("Portal Travel")]
    [Min(0f)]
    [SerializeField] private float disappearDuration = 0.25f;
    [Min(0f)]
    [SerializeField] private float reappearDuration = 0.25f;
    [Min(0.01f)]
    [SerializeField] private float vanishedScaleMultiplier = 0.1f;
    [Header("Portal Visual Animation")]
    [Min(0f)]
    [SerializeField] private float portalVisualAppearDelay = 0.25f;
    [Min(0f)]
    [SerializeField] private float portalVisualAppearDuration = 0.25f;
    [Min(0.01f)]
    [SerializeField] private float portalVisualAppearFromScaleMultiplier = 0.1f;

    private readonly Dictionary<CharacterAbilityRuntime, PortalState> portalStates = new Dictionary<CharacterAbilityRuntime, PortalState>();

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override Sprite GetIcon(CharacterAbilityRuntime runtime)
    {
        PortalState state = GetState(runtime);
        return state.ActivePortals.Count > 0 && activePortalIcon != null ? activePortalIcon : base.GetIcon(runtime);
    }

    public override string GetCounterText(CharacterAbilityRuntime runtime)
    {
        PortalState state = GetState(runtime);
        if (state.ActivePortals.Count > 0)
        {
            return "ON";
        }

        return base.GetCounterText(runtime);
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (!base.CanActivate(character, runtime) || character == null || character.Board == null)
        {
            return false;
        }

        PortalState state = GetState(runtime);
        return HasAnyTeleportDestination(character, state) || CanPlaceAdditionalPortal(character, state);
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null || runtime == null)
        {
            return false;
        }

        PortalState state = GetState(runtime);
        if (TryFindPortalAtCell(state, targetCell, out PlacedPortal portal))
        {
            return character.GridPosition != portal.PortalCell
                && !character.Board.TryGetEnemy(portal.PortalCell, out Enemy occupyingEnemy);
        }

        int range = 1 + character.GetUpgradeStacks(AbilityUpgradeKey.TridimensionalPortalEye);
        Vector2Int delta = targetCell - character.GridPosition;
        int radialDistance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (radialDistance > range)
        {
            return false;
        }

        if (!character.Board.IsInsideBoard(targetCell)
            || character.Board.TryGetEnemy(targetCell, out Enemy enemyOnCell) && enemyOnCell != null)
        {
            return false;
        }

        return CanPlaceAdditionalPortal(character, state)
            && (targetCell == character.GridPosition || character.Board.IsCellWalkable(targetCell));
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanActivateOnCell(character, runtime, targetCell);
    }

    public override bool TryGetAutomaticTargetCell(Character character, CharacterAbilityRuntime runtime, out Vector2Int targetCell)
    {
        targetCell = default;
        PortalState state = GetState(runtime);
        if (character == null || state.ActivePortals.Count != 1)
        {
            return false;
        }

        PlacedPortal onlyPortal = state.ActivePortals[0];
        if (onlyPortal == null || onlyPortal.PortalCell == character.GridPosition)
        {
            return false;
        }

        targetCell = onlyPortal.PortalCell;
        return true;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue)
        {
            return false;
        }

        PortalState state = GetState(runtime);
        if (TryFindPortalAtCell(state, targetCell.Value, out PlacedPortal destinationPortal))
        {
            Vector2Int originCell = character.GridPosition;
            if (character.GetUpgradeStacks(AbilityUpgradeKey.TridimensionalPortalImpact) > 0)
            {
                DamageEnemiesAroundWithPortalImpact(character, originCell, 1);
                PlayPortalFxAtCell(character, originCell, portalImpactDepartureFx);
            }

            state.WasTeleportedThisTurn = true;
            character.RefreshAbilityState();
            character.StartCoroutine(ResolvePortalTraversal(character, state, destinationPortal, originCell));
            return true;
        }

        if (!CanActivateOnCell(character, runtime, targetCell.Value))
        {
            return false;
        }

        PlacedPortal newPortal = new PlacedPortal
        {
            PortalCell = targetCell.Value,
            RemainingExtraTurns = character.GetUpgradeStacks(AbilityUpgradeKey.TridimensionalPortalMultidimensionalPortals)
        };
        state.ActivePortals.Add(newPortal);
        state.WasTeleportedThisTurn = false;
        SpawnPortalVisual(character, newPortal);
        PlayPortalFxAtCell(character, newPortal.PortalCell, portalPlacementFx);

        if (character.GetUpgradeStacks(AbilityUpgradeKey.TridimensionalPortalDistortion) > 0)
        {
            character.DamageEnemiesAround(newPortal.PortalCell, 1, 2, true, this);
            PlayPortalFxAtCell(character, newPortal.PortalCell, portalDistortionSpawnFx);
        }

        TrimPortalCountToLimit(state, GetMaxPortalCount(character));
        return true;
    }

    public override void OnAbilityActivated(Character character, CharacterAbilityRuntime runtime)
    {
        base.OnAbilityActivated(character, runtime);

        PortalState state = GetState(runtime);
        if (state.ActivePortals.Count <= 0 || state.WasTeleportedThisTurn || runtime == null)
        {
            return;
        }

        runtime.ResetAvailability();
        runtime.SetRemainingCooldown(0);
        character?.RefreshAbilityState();
    }

    public override void OnTurnEnded(Character character, CharacterAbilityRuntime runtime)
    {
        PortalState state = GetState(runtime);
        if (state.ActivePortals.Count <= 0)
        {
            return;
        }

        for (int index = state.ActivePortals.Count - 1; index >= 0; index--)
        {
            PlacedPortal portal = state.ActivePortals[index];
            if (portal == null)
            {
                state.ActivePortals.RemoveAt(index);
                continue;
            }

            if (portal.RemainingExtraTurns > 0)
            {
                portal.RemainingExtraTurns--;
                continue;
            }

            ClearPortalVisual(portal);
            state.ActivePortals.RemoveAt(index);
        }

        if (state.ActivePortals.Count <= 0 && !state.WasTeleportedThisTurn)
        {
            runtime.SetRemainingCooldown(CooldownTurns);
        }

        state.WasTeleportedThisTurn = false;
    }

    public override void OnTurnStarted(Character character, CharacterAbilityRuntime runtime)
    {
        PortalState state = GetState(runtime);
        state.WasTeleportedThisTurn = false;
        if (state.ActivePortals.Count > 0)
        {
            runtime.ResetAvailability();
            runtime.SetRemainingCooldown(0);
        }
    }

    private PortalState GetState(CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return new PortalState();
        }

        if (!portalStates.TryGetValue(runtime, out PortalState state) || state == null)
        {
            state = new PortalState();
            portalStates[runtime] = state;
        }

        return state;
    }

    private bool HasAnyTeleportDestination(Character character, PortalState state)
    {
        if (character == null || state == null)
        {
            return false;
        }

        for (int index = 0; index < state.ActivePortals.Count; index++)
        {
            PlacedPortal portal = state.ActivePortals[index];
            if (portal == null || portal.PortalCell == character.GridPosition)
            {
                continue;
            }

            if (!character.Board.TryGetEnemy(portal.PortalCell, out Enemy occupyingEnemy))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanPlaceAdditionalPortal(Character character, PortalState state)
    {
        return character != null && state != null && state.ActivePortals.Count < GetMaxPortalCount(character);
    }

    private int GetMaxPortalCount(Character character)
    {
        return 1 + (character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.TridimensionalPortalMultidimensionalPortals) : 0);
    }

    private bool TryFindPortalAtCell(PortalState state, Vector2Int cell, out PlacedPortal portal)
    {
        portal = null;
        if (state == null)
        {
            return false;
        }

        for (int index = 0; index < state.ActivePortals.Count; index++)
        {
            PlacedPortal candidate = state.ActivePortals[index];
            if (candidate != null && candidate.PortalCell == cell)
            {
                portal = candidate;
                return true;
            }
        }

        return false;
    }

    private void TrimPortalCountToLimit(PortalState state, int maxPortalCount)
    {
        if (state == null)
        {
            return;
        }

        int targetCount = Mathf.Max(1, maxPortalCount);
        while (state.ActivePortals.Count > targetCount)
        {
            PlacedPortal removedPortal = state.ActivePortals[0];
            ClearPortalVisual(removedPortal);
            state.ActivePortals.RemoveAt(0);
        }
    }

    private void SpawnPortalVisual(Character character, PlacedPortal portal)
    {
        if (character == null || character.Board == null || portal == null)
        {
            return;
        }

        ClearPortalVisual(portal);
        if (portalVisualPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = character.Board.GridToWorldPosition(portal.PortalCell) + portalVisualOffset;
        portal.VisualInstance = Object.Instantiate(portalVisualPrefab, spawnPosition, portalVisualPrefab.transform.rotation);
        Vector3 targetScale = portalVisualPrefab.transform.localScale;
        portal.VisualInstance.transform.localScale = targetScale * Mathf.Max(0.01f, portalVisualAppearFromScaleMultiplier);
        portal.VisualInstance.transform.DOScale(targetScale, portalVisualAppearDuration)
            .SetDelay(portalVisualAppearDelay)
            .SetEase(Ease.OutBack);
    }

    private void ClearPortalVisual(PlacedPortal portal)
    {
        if (portal == null || portal.VisualInstance == null)
        {
            return;
        }

        Object.Destroy(portal.VisualInstance);
        portal.VisualInstance = null;
    }

    private void PlayPortalFxAtCell(Character character, Vector2Int cell, PortalFxConfig fxConfig)
    {
        if (character == null || character.Board == null || fxConfig == null || fxConfig.FxPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = character.Board.GridToWorldPosition(cell) + fxConfig.PositionOffset;
        GameObject spawnedFx = Object.Instantiate(fxConfig.FxPrefab, spawnPosition, fxConfig.FxPrefab.transform.rotation);
        spawnedFx.transform.localScale = fxConfig.FxPrefab.transform.localScale;
        if (fxConfig.DestroyAfterSeconds > 0f)
        {
            Object.Destroy(spawnedFx, fxConfig.DestroyAfterSeconds);
        }
    }

    private IEnumerator ResolvePortalTraversal(Character character, PortalState state, PlacedPortal destinationPortal, Vector2Int originCell)
    {
        if (character == null || state == null || destinationPortal == null)
        {
            yield break;
        }

        character.BeginActionLock();

        Transform bodyTransform = character.GetBodyTransform();
        Vector3 bodyBaseScale = bodyTransform != null ? bodyTransform.localScale : Vector3.one;
        Vector3 vanishScale = bodyBaseScale * Mathf.Max(0.01f, vanishedScaleMultiplier);

        PlayPortalFxAtCell(character, originCell, portalEntryFx);

        bool vanishCompleted = bodyTransform == null || disappearDuration <= 0f;
        Tween vanishTween = null;
        if (bodyTransform != null && disappearDuration > 0f)
        {
            vanishTween = bodyTransform.DOScale(vanishScale, disappearDuration)
                .SetEase(Ease.Linear)
                .OnComplete(() => vanishCompleted = true);
        }
        else if (bodyTransform != null)
        {
            bodyTransform.localScale = vanishScale;
        }

        while (!vanishCompleted)
        {
            yield return null;
        }

        vanishTween?.Kill();

        bool teleported = character.TryTeleportToImmediate(destinationPortal.PortalCell);
        if (!teleported)
        {
            if (bodyTransform != null)
            {
                bodyTransform.localScale = bodyBaseScale;
            }

            state.WasTeleportedThisTurn = false;
            character.RefreshAbilityState();
            character.EndActionLock();
            yield break;
        }

        ClearPortalVisual(destinationPortal);
        state.ActivePortals.Remove(destinationPortal);
        PlayPortalFxAtCell(character, destinationPortal.PortalCell, portalExitFx);

        if (character.GetUpgradeStacks(AbilityUpgradeKey.TridimensionalPortalImpact) > 0)
        {
            DamageEnemiesAroundWithPortalImpact(character, destinationPortal.PortalCell, 1);
            PlayPortalFxAtCell(character, destinationPortal.PortalCell, portalImpactArrivalFx);
        }

        if (character.GetUpgradeStacks(AbilityUpgradeKey.TridimensionalPortalEnergy) > 0)
        {
            character.GainMovementPoints(1);
        }

        bool reappearCompleted = bodyTransform == null || reappearDuration <= 0f;
        Tween reappearTween = null;
        if (bodyTransform != null && reappearDuration > 0f)
        {
            reappearTween = bodyTransform.DOScale(bodyBaseScale, reappearDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() => reappearCompleted = true);
        }
        else if (bodyTransform != null)
        {
            bodyTransform.localScale = bodyBaseScale;
        }

        while (!reappearCompleted)
        {
            yield return null;
        }

        reappearTween?.Kill();
        if (bodyTransform != null)
        {
            bodyTransform.localScale = bodyBaseScale;
        }

        character.EndActionLock();
    }

    private void DamageEnemiesAroundWithPortalImpact(Character character, Vector2Int centerCell, int range)
    {
        if (character == null || character.Board == null)
        {
            return;
        }

        for (int offsetX = -range; offsetX <= range; offsetX++)
        {
            for (int offsetY = -range; offsetY <= range; offsetY++)
            {
                if (Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)) > range)
                {
                    continue;
                }

                Vector2Int targetCell = centerCell + new Vector2Int(offsetX, offsetY);
                if (character.Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null)
                {
                    int appliedDamage = character.DealDamageToEnemy(enemy, 1, false, true, DamageSoundType.Default, this);
                    if (appliedDamage > 0)
                    {
                        enemy.ApplyStatusEffect(CombatStatusType.Bleeding, -1, 1);
                    }
                }
                else if (character.Board.TryGetLichSkullObject(targetCell, out LichSkullObject skull) && skull != null)
                {
                    character.DealDamageToLichSkull(skull, 1, false, DamageSoundType.Default, this);
                }
                else if (character.Board.TryGetBarrel(targetCell, out BarrelObstacle barrel) && barrel != null)
                {
                    barrel.TakeHit();
                }
            }
        }
    }
}
