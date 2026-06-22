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

    private sealed class PortalState
    {
        public bool IsPlaced;
        public bool WasTeleportedThisTurn;
        public Vector2Int PortalCell;
        public GameObject VisualInstance;
    }

    [SerializeField] private Sprite activePortalIcon;
    [SerializeField] private GameObject portalVisualPrefab;
    [SerializeField] private Vector3 portalVisualOffset = new Vector3(0f, 0.08f, 0f);
    [Header("Portal FX")]
    [SerializeField] private PortalFxConfig portalPlacementFx;
    [SerializeField] private PortalFxConfig portalEntryFx;
    [SerializeField] private PortalFxConfig portalExitFx;
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
        return state.IsPlaced && activePortalIcon != null ? activePortalIcon : base.GetIcon(runtime);
    }

    public override string GetCounterText(CharacterAbilityRuntime runtime)
    {
        PortalState state = GetState(runtime);
        if (state.IsPlaced)
        {
            return "IN";
        }

        return base.GetCounterText(runtime);
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (!base.CanActivate(character, runtime))
        {
            return false;
        }

        PortalState state = GetState(runtime);
        if (!state.IsPlaced)
        {
            return true;
        }

        return character != null
            && character.Board != null
            && character.GridPosition != state.PortalCell
            && !character.Board.TryGetEnemy(state.PortalCell, out Enemy occupyingEnemy);
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null || runtime == null)
        {
            return false;
        }

        PortalState state = GetState(runtime);
        if (state.IsPlaced)
        {
            return targetCell == state.PortalCell && CanActivate(character, runtime);
        }

        int range = 1 + character.GetUpgradeStacks(AbilityUpgradeKey.TridimensionalPortalEye);
        Vector2Int delta = targetCell - character.GridPosition;
        if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) > range)
        {
            return false;
        }

        if (!character.Board.IsInsideBoard(targetCell) || character.Board.TryGetEnemy(targetCell, out Enemy enemyOnCell) && enemyOnCell != null)
        {
            return false;
        }

        return targetCell == character.GridPosition || character.Board.IsCellWalkable(targetCell);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanActivateOnCell(character, runtime, targetCell);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue)
        {
            return false;
        }

        PortalState state = GetState(runtime);
        if (!state.IsPlaced)
        {
            state.IsPlaced = true;
            state.WasTeleportedThisTurn = false;
            state.PortalCell = targetCell.Value;
            SpawnPortalVisual(character, state);
            PlayPortalFxAtCell(character, state.PortalCell, portalPlacementFx);

            if (character.GetUpgradeStacks(AbilityUpgradeKey.TridimensionalPortalDistortion) > 0)
            {
                character.DamageEnemiesAround(state.PortalCell, 1, 1, true, this);
            }

            return true;
        }

        Vector2Int originCell = character.GridPosition;
        if (character.GetUpgradeStacks(AbilityUpgradeKey.TridimensionalPortalImpact) > 0)
        {
            character.DamageEnemiesAround(originCell, 1, 1, true, this);
        }

        state.WasTeleportedThisTurn = true;
        state.IsPlaced = false;
        character.RefreshAbilityState();
        character.StartCoroutine(ResolvePortalTraversal(character, state, originCell));
        return true;
    }

    public override void OnAbilityActivated(Character character, CharacterAbilityRuntime runtime)
    {
        base.OnAbilityActivated(character, runtime);

        PortalState state = GetState(runtime);
        if (!state.IsPlaced || state.WasTeleportedThisTurn || runtime == null)
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
        if (!state.IsPlaced)
        {
            return;
        }

        ClearPortalVisual(state);
        state.IsPlaced = false;
        if (!state.WasTeleportedThisTurn)
        {
            runtime.SetRemainingCooldown(CooldownTurns);
        }

        state.WasTeleportedThisTurn = false;
    }

    public override void OnTurnStarted(Character character, CharacterAbilityRuntime runtime)
    {
        PortalState state = GetState(runtime);
        ClearPortalVisual(state);
        state.IsPlaced = false;
        state.WasTeleportedThisTurn = false;
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

    private void SpawnPortalVisual(Character character, PortalState state)
    {
        if (character == null || character.Board == null || state == null)
        {
            return;
        }

        ClearPortalVisual(state);
        if (portalVisualPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = character.Board.GridToWorldPosition(state.PortalCell) + portalVisualOffset;
        state.VisualInstance = Object.Instantiate(portalVisualPrefab, spawnPosition, portalVisualPrefab.transform.rotation);
        Vector3 targetScale = portalVisualPrefab.transform.localScale;
        state.VisualInstance.transform.localScale = targetScale * Mathf.Max(0.01f, portalVisualAppearFromScaleMultiplier);
        state.VisualInstance.transform.DOScale(targetScale, portalVisualAppearDuration)
            .SetDelay(portalVisualAppearDelay)
            .SetEase(Ease.OutBack);
    }

    private void ClearPortalVisual(PortalState state)
    {
        if (state == null || state.VisualInstance == null)
        {
            return;
        }

        Object.Destroy(state.VisualInstance);
        state.VisualInstance = null;
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

    private IEnumerator ResolvePortalTraversal(Character character, PortalState state, Vector2Int originCell)
    {
        if (character == null || state == null)
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

        bool teleported = character.TryTeleportToImmediate(state.PortalCell);
        if (!teleported)
        {
            if (bodyTransform != null)
            {
                bodyTransform.localScale = bodyBaseScale;
            }

            state.IsPlaced = true;
            state.WasTeleportedThisTurn = false;
            character.RefreshAbilityState();
            character.EndActionLock();
            yield break;
        }

        ClearPortalVisual(state);
        PlayPortalFxAtCell(character, state.PortalCell, portalExitFx);

        if (character.GetUpgradeStacks(AbilityUpgradeKey.TridimensionalPortalImpact) > 0)
        {
            character.DamageEnemiesAround(state.PortalCell, 1, 1, true, this);
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
}
