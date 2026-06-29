using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Wolf Step", fileName = "WolfStep")]
public class WolfStepAbility : AbilityDefinition
{
    private sealed class WolfStepState
    {
        public int RemainingSteps;
        public int ConsumedSteps;
        public int CachedTotalStepCount;
    }

    [Min(1)]
    [SerializeField] private int baseStepCount = 2;
    [Header("Loup Alpha FX")]
    [SerializeField] private GameObject alphaPulseFxPrefab;
    [Min(0f)]
    [SerializeField] private float alphaPulseFxDuration = 1f;
    [Header("Loup Affame FX")]
    [SerializeField] private GameObject hungryBoostFxPrefab;
    [SerializeField] private GameObject hungryAuraFxPrefab;
    [Min(0f)]
    [SerializeField] private float hungryBoostFxDuration = 1f;

    private readonly Dictionary<CharacterAbilityRuntime, WolfStepState> states = new Dictionary<CharacterAbilityRuntime, WolfStepState>();

    public override bool KeepsActiveStateBetweenTurns => false;
    public override bool RefundUseIfDeactivatedWithoutConsumption => true;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        int steps = GetTotalStepCount(character);
        return $"Activate Wolf Step to gain {steps} movement-free steps. Each step moves Hector by exactly 1 tile. Reusing Wolf Step replaces any unused steps.";
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        WolfStepState state = GetState(runtime);
        state.CachedTotalStepCount = GetTotalStepCount(character);
        return character != null
            && runtime != null
            && (character.RemainingMovementPoints > 0 || character.GetUpgradeStacks(AbilityUpgradeKey.WolfStepWolfCharge) > 0);
    }

    public override bool LimitsNextSlideToOneCell(Character character, CharacterAbilityRuntime runtime)
    {
        return runtime != null && runtime.IsActive && character != null && character.WolfMovementPoints > 0;
    }

    public override bool ConsumeSingleStepModifierAfterMovement(Character character, CharacterAbilityRuntime runtime)
    {
        return false;
    }

    public override bool PreventsSlideMovementPointConsumption(Character character, CharacterAbilityRuntime runtime)
    {
        return false;
    }

    public override string GetCounterText(CharacterAbilityRuntime runtime)
    {
        return runtime != null && runtime.RemainingCooldown > 0
            ? runtime.RemainingCooldown.ToString()
            : string.Empty;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null)
        {
            return false;
        }

        bool hadNoMovementBeforeActivation = character.RemainingMovementPoints <= 0;
        WolfStepState state = GetState(runtime);
        state.CachedTotalStepCount = GetTotalStepCount(character);
        state.RemainingSteps = state.CachedTotalStepCount;
        state.ConsumedSteps = 0;
        character.GainWolfMovementPoints(state.RemainingSteps);

        if (hadNoMovementBeforeActivation && character.GetUpgradeStacks(AbilityUpgradeKey.WolfStepWolfCharge) > 0)
        {
            character.GainMovementPoints(1);
        }

        PlayConfiguredFx(character);
        return true;
    }

    public override void OnTurnStarted(Character character, CharacterAbilityRuntime runtime)
    {
        WolfStepState state = GetState(runtime);
        state.CachedTotalStepCount = GetTotalStepCount(character);
        if (runtime == null || !runtime.IsActive)
        {
            state.RemainingSteps = 0;
            state.ConsumedSteps = 0;
        }
    }

    public override void OnAbilityDeactivated(Character character, CharacterAbilityRuntime runtime)
    {
        character?.ClearWolfMovementPoints();
        WolfStepState state = GetState(runtime);
        state.RemainingSteps = 0;
        state.ConsumedSteps = 0;
        state.CachedTotalStepCount = GetTotalStepCount(character);
        character?.RefreshAbilityState();
    }

    public override void OnCharacterMoved(
        Character character,
        CharacterAbilityRuntime runtime,
        Vector2Int previousCell,
        Vector2Int currentCell,
        bool consumedMovementPoint)
    {
        WolfStepState state = GetState(runtime);
        if (character == null
            || runtime == null
            || !runtime.IsActive
            || state.RemainingSteps <= 0
            || Mathf.Abs(currentCell.x - previousCell.x) + Mathf.Abs(currentCell.y - previousCell.y) != 1)
        {
            return;
        }

        state.RemainingSteps = character.WolfMovementPoints;
        state.ConsumedSteps++;
        if (state.ConsumedSteps == 1)
        {
            runtime.ConsumePreparedActivation();
            if (character.GetUpgradeStacks(AbilityUpgradeKey.WolfStepHungryWolf) > 0)
            {
                character.AddBonusDamageUntilNextMovement(1, hungryBoostFxPrefab, hungryAuraFxPrefab, hungryBoostFxDuration);
            }
        }

        if (character.GetUpgradeStacks(AbilityUpgradeKey.WolfStepAlphaWolf) > 0)
        {
            character.DamageEnemiesAround(character.GridPosition, 1, 1, true, this);
            character.PlayFeedbackFx(alphaPulseFxPrefab, destroyAfterSeconds: alphaPulseFxDuration);
        }

        if (state.RemainingSteps <= 0)
        {
            runtime.Deactivate(character);
        }
        else
        {
            character.RefreshAbilityState();
        }
    }

    private WolfStepState GetState(CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return new WolfStepState();
        }

        if (!states.TryGetValue(runtime, out WolfStepState state) || state == null)
        {
            state = new WolfStepState
            {
                CachedTotalStepCount = baseStepCount
            };
            states[runtime] = state;
        }

        return state;
    }

    private int GetTotalStepCount(Character character)
    {
        return baseStepCount + (character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.WolfStepQuickSteps) : 0);
    }
}
