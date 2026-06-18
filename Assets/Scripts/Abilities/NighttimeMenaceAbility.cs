using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Nighttime Menace", fileName = "NighttimeMenace")]
public class NighttimeMenaceAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseRange = 3;
    [SerializeField] private List<UpgradedSecondaryEffectEntry> secondaryEffects = new List<UpgradedSecondaryEffectEntry>();
    [SerializeField] private float delayAfterBloodyEscapeBeforeTeleport = 0.5f;
    [SerializeField] private float delayAfterTeleportBeforeTheatricalAppearance = 0.2f;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null || !character.Board.IsCellWalkable(targetCell))
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        int radialDistance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        int maxRange = baseRange + character.GetUpgradeStacks(AbilityUpgradeKey.NighttimeMenaceShadowArea);
        return radialDistance <= maxRange;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue)
        {
            return false;
        }

        Vector2Int previousCell = character.GridPosition;
        AbilityExecutionContext context = new AbilityExecutionContext(this, runtime, previousCell, targetCell.Value);
        character.StartCoroutine(ResolveNighttimeMenaceSequence(character, runtime, context));
        return true;
    }

    private System.Collections.IEnumerator ResolveNighttimeMenaceSequence(
        Character character,
        CharacterAbilityRuntime runtime,
        AbilityExecutionContext context)
    {
        if (character == null)
        {
            yield break;
        }

        character.BeginActionLock();

        bool hasBeforeMovementEffects = HasUnlockedEffects(character, SecondaryEffectTiming.BeforeMovement);
        bool hasAfterMovementEffects = HasUnlockedEffects(character, SecondaryEffectTiming.AfterMovement);

        ExecuteUnlockedSecondaryEffects(character, runtime, context, secondaryEffects, SecondaryEffectTiming.BeforeMovement);

        if (hasBeforeMovementEffects && delayAfterBloodyEscapeBeforeTeleport > 0f)
        {
            yield return new WaitForSeconds(delayAfterBloodyEscapeBeforeTeleport);
        }

        bool success = character.TryTeleportTo(context.TargetCell);
        if (!success)
        {
            character.EndActionLock();
            yield break;
        }

        yield return new WaitUntil(() => !character.IsMoving);

        if (hasAfterMovementEffects && delayAfterTeleportBeforeTheatricalAppearance > 0f)
        {
            yield return new WaitForSeconds(delayAfterTeleportBeforeTheatricalAppearance);
        }

        ExecuteUnlockedSecondaryEffects(character, runtime, context, secondaryEffects, SecondaryEffectTiming.AfterMovement);
        PlayConfiguredFx(character);
        character.EndActionLock();
    }

    private bool HasUnlockedEffects(Character character, SecondaryEffectTiming timing)
    {
        if (character == null || secondaryEffects == null)
        {
            return false;
        }

        for (int index = 0; index < secondaryEffects.Count; index++)
        {
            UpgradedSecondaryEffectEntry entry = secondaryEffects[index];
            if (entry == null
                || entry.Timing != timing
                || !entry.IsUnlocked(character)
                || entry.Effect == null)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
