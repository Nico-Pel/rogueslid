using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Circle Of The Damned", fileName = "CircleOfTheDamned")]
public class CircleOfTheDamnedAbility : AbilityDefinition
{
    private sealed class CircleState
    {
        public DamnedCircleHazard ActiveCircle;
        public bool RitualHoldAvailable;
    }

    [Min(1)]
    [SerializeField] private int castRange = 5;
    [Min(1)]
    [SerializeField] private int baseStartingCharges = 1;
    [SerializeField] private Sprite activeCircleIcon;
    [SerializeField] private GameObject circleVisualPrefab;
    [SerializeField] private GameObject chargeGainFxPrefab;
    [Min(0f)]
    [SerializeField] private float chargeGainFxDestroyAfterSeconds = 1.5f;
    [SerializeField] private GameObject chargeGainSoundParametersPrefab;
    [SerializeField] private GameObject explosionFxPrefab;
    [Min(0f)]
    [SerializeField] private float explosionFxDestroyAfterSeconds = 2f;
    [SerializeField] private GameObject damnedBlastShockwaveFxPrefab;
    [Min(0f)]
    [SerializeField] private float damnedBlastShockwaveFxDestroyAfterSeconds = 2f;

    private readonly Dictionary<CharacterAbilityRuntime, CircleState> circleStates = new Dictionary<CharacterAbilityRuntime, CircleState>();

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        CircleState state = GetState(runtime);
        int currentCastRange = GetCastRange(character);
        if (state.ActiveCircle != null)
        {
            return $"Recast to detonate Circle of the Damned. Current magical charges: {state.ActiveCircle.Charges}.";
        }

        int startingCharges = GetStartingCharges(character);
        int blastRange = GetBlastRange(character);
        return $"Place a cursed circle within range {currentCastRange}. It starts with {startingCharges} magical charge(s). Each time Pandora passes over it, it gains 1 charge. Recast or end your turn to detonate it for damage equal to its charges in range {blastRange}.";
    }

    public override string GetCounterText(CharacterAbilityRuntime runtime)
    {
        CircleState state = GetState(runtime);
        return state.ActiveCircle != null ? state.ActiveCircle.Charges.ToString() : base.GetCounterText(runtime);
    }

    public override Sprite GetIcon(CharacterAbilityRuntime runtime)
    {
        CircleState state = GetState(runtime);
        if (state.ActiveCircle != null && activeCircleIcon != null)
        {
            return activeCircleIcon;
        }

        return base.GetIcon(runtime);
    }

    public override bool TryGetAutomaticTargetCell(Character character, CharacterAbilityRuntime runtime, out Vector2Int targetCell)
    {
        targetCell = default;
        CircleState state = GetState(runtime);
        if (state.ActiveCircle == null)
        {
            return false;
        }

        targetCell = state.ActiveCircle.GridPosition;
        return true;
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (!base.CanActivate(character, runtime) || character == null || character.Board == null)
        {
            return false;
        }

        CircleState state = GetState(runtime);
        return state.ActiveCircle != null || HasAnyValidPlacement(character);
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        CircleState state = GetState(runtime);
        if (state.ActiveCircle != null)
        {
            return targetCell == state.ActiveCircle.GridPosition;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        int radialDistance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (radialDistance > GetCastRange(character))
        {
            return false;
        }

        if (!character.Board.TryGetCell(targetCell, out BoardCell cell) || !cell.Walkable || cell.IsOccupied)
        {
            return false;
        }

        return !character.Board.TryGetHazard(targetCell, out BoardHazard existingHazard) || existingHazard == null;
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        CircleState state = GetState(runtime);
        if (state.ActiveCircle != null)
        {
            return targetCell == state.ActiveCircle.GridPosition;
        }

        return character.Board.IsInsideBoard(targetCell)
            && Mathf.Abs(targetCell.x - character.GridPosition.x) + Mathf.Abs(targetCell.y - character.GridPosition.y) <= GetCastRange(character);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue || character.Board == null)
        {
            return false;
        }

        CircleState state = GetState(runtime);
        if (state.ActiveCircle != null)
        {
            if (targetCell.Value != state.ActiveCircle.GridPosition)
            {
                return false;
            }

            state.ActiveCircle.Explode();
            state.ActiveCircle = null;
            state.RitualHoldAvailable = false;
            return true;
        }

        if (!CanActivateOnCell(character, runtime, targetCell.Value))
        {
            return false;
        }

        GameObject circleObject = new GameObject("DamnedCircleHazard");
        DamnedCircleHazard circleHazard = circleObject.AddComponent<DamnedCircleHazard>();
        circleHazard.Configure(
            character.Board,
            character,
            runtime,
            targetCell.Value,
            this,
            circleVisualPrefab,
            chargeGainFxPrefab,
            chargeGainFxDestroyAfterSeconds,
            chargeGainSoundParametersPrefab,
            explosionFxPrefab,
            explosionFxDestroyAfterSeconds,
            damnedBlastShockwaveFxPrefab,
            damnedBlastShockwaveFxDestroyAfterSeconds,
            GetStartingCharges(character),
            GetBlastRange(character),
            character.GetUpgradeStacks(AbilityUpgradeKey.CircleOfTheDamnedDamnedWave) > 0);

        state.ActiveCircle = circleHazard;
        state.RitualHoldAvailable = character.GetUpgradeStacks(AbilityUpgradeKey.CircleOfTheDamnedDamnedRitual) > 0;
        return true;
    }

    public override void OnAbilityActivated(Character character, CharacterAbilityRuntime runtime)
    {
        base.OnAbilityActivated(character, runtime);

        CircleState state = GetState(runtime);
        if (state.ActiveCircle == null || runtime == null)
        {
            return;
        }

        runtime.ResetAvailability();
        runtime.SetRemainingCooldown(0);
        character?.RefreshAbilityState();
    }

    public override void OnTurnEnded(Character character, CharacterAbilityRuntime runtime)
    {
        CircleState state = GetState(runtime);
        if (state.ActiveCircle == null)
        {
            return;
        }

        if (state.RitualHoldAvailable)
        {
            state.RitualHoldAvailable = false;
            character?.RefreshAbilityState();
            return;
        }

        state.ActiveCircle.Explode();
        state.ActiveCircle = null;
        if (runtime != null && CooldownTurns > 0)
        {
            runtime.SetRemainingCooldown(CooldownTurns + 1);
        }

        character?.RefreshAbilityState();
    }

    public void HandleCircleChargesChanged(CharacterAbilityRuntime runtime)
    {
        CircleState state = GetState(runtime);
        if (state.ActiveCircle == null)
        {
            return;
        }

        state.ActiveCircle.Owner?.RefreshAbilityState();
    }

    public void HandleCircleDestroyed(CharacterAbilityRuntime runtime, DamnedCircleHazard hazard)
    {
        CircleState state = GetState(runtime);
        if (state.ActiveCircle != hazard)
        {
            return;
        }

        state.ActiveCircle = null;
        state.RitualHoldAvailable = false;
        hazard?.Owner?.RefreshAbilityState();
    }

    private CircleState GetState(CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return new CircleState();
        }

        if (!circleStates.TryGetValue(runtime, out CircleState state) || state == null)
        {
            state = new CircleState();
            circleStates[runtime] = state;
        }

        return state;
    }

    private int GetStartingCharges(Character character)
    {
        return Mathf.Max(1, baseStartingCharges + (character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.CircleOfTheDamnedCursedIncantation) : 0));
    }

    private int GetBlastRange(Character character)
    {
        return 1 + (character != null && character.GetUpgradeStacks(AbilityUpgradeKey.CircleOfTheDamnedDamnedBlast) > 0 ? 1 : 0);
    }

    private int GetCastRange(Character character)
    {
        int bonusRange = character != null ? 2 * character.GetUpgradeStacks(AbilityUpgradeKey.CircleOfTheDamnedProfaneReach) : 0;
        return Mathf.Max(1, castRange + bonusRange);
    }

    private bool HasAnyValidPlacement(Character character)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        int currentCastRange = GetCastRange(character);
        for (int offsetX = -currentCastRange; offsetX <= currentCastRange; offsetX++)
        {
            for (int offsetY = -currentCastRange; offsetY <= currentCastRange; offsetY++)
            {
                Vector2Int targetCell = character.GridPosition + new Vector2Int(offsetX, offsetY);
                if (Mathf.Abs(offsetX) + Mathf.Abs(offsetY) > currentCastRange)
                {
                    continue;
                }

                if (CanActivateOnCell(character, null, targetCell))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
