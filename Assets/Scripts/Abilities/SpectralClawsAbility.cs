using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Spectral Claws", fileName = "SpectralClaws")]
public class SpectralClawsAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 3;
    [Min(1)]
    [SerializeField] private int maxCharges = 2;
    [Min(0)]
    [SerializeField] private int spectralStepHeal = 1;
    [SerializeField] private SpectralStepAbility spectralStepAbilityData;

    private readonly Dictionary<CharacterAbilityRuntime, int> remainingChargesByRuntime = new Dictionary<CharacterAbilityRuntime, int>();
    private readonly Dictionary<CharacterAbilityRuntime, int> remainingSpectralStepChargesByRuntime = new Dictionary<CharacterAbilityRuntime, int>();
    private readonly Dictionary<CharacterAbilityRuntime, bool> spectralStepUnlockedByRuntime = new Dictionary<CharacterAbilityRuntime, bool>();
    private readonly HashSet<CharacterAbilityRuntime> spectralStepAnimationPending = new HashSet<CharacterAbilityRuntime>();

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        if (IsSpectralStepMode(character, runtime))
        {
            return spectralStepAbilityData != null
                ? spectralStepAbilityData.GetDisplayDescription(character, runtime)
                : $"Pandora passes through an adjacent enemy and recovers {spectralStepHeal} HP.";
        }

        int bonusDamage = character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.SpectralClawsSpectralFracture) : 0;
        bool hasDiagonal = character != null && character.GetUpgradeStacks(AbilityUpgradeKey.SpectralClawsSpectralCrossing) > 0;
        return $"Pandora deals {baseDamage} damage to an adjacent enemy and passes through it if possible. Charges: {GetRemainingCharges(runtime)}/{Mathf.Max(1, maxCharges)}. Spectral Fracture bonus: +{bonusDamage} while passing through. Diagonal strike: {(hasDiagonal ? "Enabled" : "Locked")}.";
    }

    public override string GetDisplayName(Character character, CharacterAbilityRuntime runtime)
    {
        if (!IsSpectralStepMode(character, runtime))
        {
            return base.GetDisplayName(character, runtime);
        }

        return spectralStepAbilityData != null
            ? spectralStepAbilityData.GetDisplayName(character, runtime)
            : "Spectral Step";
    }

    public override Sprite GetIcon(CharacterAbilityRuntime runtime)
    {
        if (IsSpectralStepMode(null, runtime) && spectralStepAbilityData != null)
        {
            Sprite alternateIcon = spectralStepAbilityData.GetIcon(runtime);
            if (alternateIcon != null)
            {
                return alternateIcon;
            }
        }

        return base.GetIcon(runtime);
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        SyncRuntimeState(character, runtime);
        bool canUseSpectralStep = character.GetUpgradeStacks(AbilityUpgradeKey.SpectralClawsSpectralStep) > 0;
        if (GetRemainingCharges(runtime) <= 0 && (!canUseSpectralStep || GetRemainingSpectralStepCharges(runtime) <= 0))
        {
            return false;
        }

        foreach (Vector2Int direction in EnumerateDirections(character))
        {
            Vector2Int targetCell = character.GridPosition + direction;
            if (CanActivateOnCell(character, runtime, targetCell))
            {
                return true;
            }
        }

        return false;
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        SyncRuntimeState(character, runtime);
        if (!TryGetTargetDirection(character, targetCell, out Vector2Int direction))
        {
            return false;
        }

        if (!character.Board.TryGetEnemy(targetCell, out Enemy enemy) || enemy == null)
        {
            return false;
        }

        if (GetRemainingCharges(runtime) > 0)
        {
            return true;
        }

        if (character.GetUpgradeStacks(AbilityUpgradeKey.SpectralClawsSpectralStep) <= 0)
        {
            return false;
        }

        if (GetRemainingSpectralStepCharges(runtime) <= 0)
        {
            return false;
        }

        return TryGetPassThroughDestination(character, enemy.GridPosition, direction, out _);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        return TryGetTargetDirection(character, targetCell, out _)
            && character.Board.IsInsideBoard(targetCell);
    }

    public override string GetCounterText(CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return string.Empty;
        }

        return IsSpectralStepMode(null, runtime)
            ? GetRemainingSpectralStepCharges(runtime).ToString()
            : GetRemainingCharges(runtime).ToString();
    }

    public override void OnTurnStarted(Character character, CharacterAbilityRuntime runtime)
    {
        SyncRuntimeState(character, runtime);
        SetRemainingCharges(runtime, maxCharges);
        SetRemainingSpectralStepCharges(runtime, GetSpectralStepMaxCharges());
    }

    public override void PlayActivationAnimation(Character character)
    {
        CharacterAbilityRuntime pendingRuntime = FindRuntimeForCharacter(character);
        if (pendingRuntime != null
            && spectralStepAnimationPending.Remove(pendingRuntime)
            && spectralStepAbilityData != null)
        {
            spectralStepAbilityData.PlayActivationAnimation(character);
            return;
        }

        base.PlayActivationAnimation(character);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue || character.Board == null)
        {
            return false;
        }

        SyncRuntimeState(character, runtime);
        if (!TryGetTargetDirection(character, targetCell.Value, out Vector2Int direction))
        {
            return false;
        }

        if (!character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) || enemy == null)
        {
            return false;
        }

        int remainingCharges = GetRemainingCharges(runtime);
        bool useSpectralStep = remainingCharges <= 0;
        if (useSpectralStep && character.GetUpgradeStacks(AbilityUpgradeKey.SpectralClawsSpectralStep) <= 0)
        {
            return false;
        }

        if (useSpectralStep && spectralStepAbilityData != null)
        {
            if (GetRemainingSpectralStepCharges(runtime) <= 0)
            {
                return false;
            }

            spectralStepAnimationPending.Add(runtime);
            bool activatedSpectralStep = spectralStepAbilityData.TryActivate(character, runtime, targetCell);
            if (!activatedSpectralStep)
            {
                spectralStepAnimationPending.Remove(runtime);
                return false;
            }

            SetRemainingSpectralStepCharges(runtime, GetRemainingSpectralStepCharges(runtime) - 1);
            return true;
        }

        bool canPassThrough = TryGetPassThroughDestination(character, enemy.GridPosition, direction, out Vector2Int passThroughCell);
        if (useSpectralStep && !canPassThrough)
        {
            return false;
        }

        if (!useSpectralStep)
        {
            SetRemainingCharges(runtime, remainingCharges - 1);
        }

        character.FaceTargetCell(targetCell.Value);
        character.StartCoroutine(ResolveSpectralClaws(character, runtime, enemy, canPassThrough, passThroughCell, useSpectralStep));
        return true;
    }

    private IEnumerator ResolveSpectralClaws(
        Character character,
        CharacterAbilityRuntime runtime,
        Enemy enemy,
        bool canPassThrough,
        Vector2Int passThroughCell,
        bool useSpectralStep)
    {
        if (character == null || runtime == null)
        {
            yield break;
        }

        character.BeginActionLock();

        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
        if (!useSpectralStep && enemy != null)
        {
            hitEnemies.Add(enemy);
            bool damageResolved = false;
            int damage = baseDamage;
            if (canPassThrough)
            {
                damage += character.GetUpgradeStacks(AbilityUpgradeKey.SpectralClawsSpectralFracture);
            }

            character.DealDamageToEnemyWithAbilityTiming(
                this,
                enemy,
                damage,
                true,
                true,
                DamageSoundType.Sword,
                null,
                this,
                (_, __) => damageResolved = true);

            while (!damageResolved)
            {
                yield return null;
            }
        }

        if (canPassThrough)
        {
            character.TryTeleportTo(passThroughCell);
            yield return new WaitUntil(() => !character.IsMoving);
        }

        if (useSpectralStep && spectralStepHeal > 0)
        {
            character.Heal(spectralStepHeal);
        }

        if (hitEnemies.Count > 0)
        {
            PlayConfiguredFx(character, hitEnemies);
        }
        else
        {
            PlayConfiguredFx(character);
        }

        character.EndActionLock();
    }

    private IEnumerable<Vector2Int> EnumerateDirections(Character character)
    {
        yield return Vector2Int.up;
        yield return Vector2Int.right;
        yield return Vector2Int.down;
        yield return Vector2Int.left;

        if (character != null && character.GetUpgradeStacks(AbilityUpgradeKey.SpectralClawsSpectralCrossing) > 0)
        {
            yield return new Vector2Int(1, 1);
            yield return new Vector2Int(1, -1);
            yield return new Vector2Int(-1, -1);
            yield return new Vector2Int(-1, 1);
        }
    }

    private bool TryGetTargetDirection(Character character, Vector2Int targetCell, out Vector2Int direction)
    {
        direction = Vector2Int.zero;
        if (character == null)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        int absX = Mathf.Abs(delta.x);
        int absY = Mathf.Abs(delta.y);

        bool isOrthogonalAdjacent = (absX == 1 && absY == 0) || (absX == 0 && absY == 1);
        bool isDiagonalAdjacent = absX == 1 && absY == 1
            && character.GetUpgradeStacks(AbilityUpgradeKey.SpectralClawsSpectralCrossing) > 0;
        if (!isOrthogonalAdjacent && !isDiagonalAdjacent)
        {
            return false;
        }

        direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        return direction != Vector2Int.zero;
    }

    private bool TryGetPassThroughDestination(Character character, Vector2Int enemyCell, Vector2Int direction, out Vector2Int destination)
    {
        destination = enemyCell + direction;
        return character != null
            && character.Board != null
            && character.Board.IsCellWalkable(destination);
    }

    private int GetRemainingCharges(CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return 0;
        }

        if (!remainingChargesByRuntime.TryGetValue(runtime, out int remainingCharges))
        {
            remainingCharges = Mathf.Max(1, maxCharges);
            remainingChargesByRuntime[runtime] = remainingCharges;
        }

        return Mathf.Max(0, remainingCharges);
    }

    private int GetRemainingSpectralStepCharges(CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return 0;
        }

        if (!remainingSpectralStepChargesByRuntime.TryGetValue(runtime, out int remainingCharges))
        {
            remainingCharges = GetSpectralStepMaxCharges();
            remainingSpectralStepChargesByRuntime[runtime] = remainingCharges;
        }

        return Mathf.Max(0, remainingCharges);
    }

    private void SetRemainingCharges(CharacterAbilityRuntime runtime, int remainingCharges)
    {
        if (runtime == null)
        {
            return;
        }

        remainingChargesByRuntime[runtime] = Mathf.Max(0, remainingCharges);
    }

    private void SetRemainingSpectralStepCharges(CharacterAbilityRuntime runtime, int remainingCharges)
    {
        if (runtime == null)
        {
            return;
        }

        remainingSpectralStepChargesByRuntime[runtime] = Mathf.Max(0, remainingCharges);
    }

    private bool IsSpectralStepMode(Character character, CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return false;
        }

        if (GetRemainingCharges(runtime) > 0)
        {
            return false;
        }

        if (character != null)
        {
            return character.GetUpgradeStacks(AbilityUpgradeKey.SpectralClawsSpectralStep) > 0;
        }

        return spectralStepUnlockedByRuntime.TryGetValue(runtime, out bool isUnlocked) && isUnlocked;
    }

    private void SyncRuntimeState(Character character, CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return;
        }

        spectralStepUnlockedByRuntime[runtime] = character != null
            && character.GetUpgradeStacks(AbilityUpgradeKey.SpectralClawsSpectralStep) > 0;
    }

    private int GetSpectralStepMaxCharges()
    {
        return spectralStepAbilityData != null ? spectralStepAbilityData.MaxCharges : 1;
    }

    private CharacterAbilityRuntime FindRuntimeForCharacter(Character character)
    {
        if (character == null)
        {
            return null;
        }

        IReadOnlyList<CharacterAbilityRuntime> runtimes = character.Abilities;
        for (int index = 0; index < runtimes.Count; index++)
        {
            CharacterAbilityRuntime runtime = runtimes[index];
            if (runtime != null && runtime.Definition == this)
            {
                return runtime;
            }
        }

        return null;
    }
}
