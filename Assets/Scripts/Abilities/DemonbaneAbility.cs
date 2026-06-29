using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Demonbane", fileName = "Demonbane")]
public class DemonbaneAbility : AbilityDefinition
{
    private readonly Dictionary<Character, bool> pendingFallbackCastByCharacter = new Dictionary<Character, bool>();

    [Min(1)]
    [SerializeField] private int baseDamage = 4;
    [Min(1)]
    [SerializeField] private int range = 10;
    [SerializeField] private GameObject projectilePrefab;
    [Min(0.01f)]
    [SerializeField] private float projectileSpeed = 14f;
    [Min(0f)]
    [SerializeField] private float projectileLaunchDelay = 0.08f;
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 projectileImpactOffset = new Vector3(0f, 0.2f, 0f);
    [Header("Wherever You Are")]
    [SerializeField] private AnimationClip fallbackIncantationAnimation;
    [SerializeField] private GameObject fallbackBleedImpactFxPrefab;
    [Min(0f)]
    [SerializeField] private float fallbackBleedDelay = 0.5f;
    [Min(0f)]
    [SerializeField] private float fallbackBleedImpactFxDuration = 1f;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        bool allowDiagonals = character != null && character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneBlindSpot) > 0;
        bool allowEmptyCell = character != null
            && character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneWhereverYouAre) > 0
            && !HasAnyDirectTargetAvailable(character, allowDiagonals);
        return CanTargetCell(character, targetCell, allowDiagonals, allowEmptyCell);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        bool allowDiagonals = character != null && character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneBlindSpot) > 0;
        bool allowEmptyCell = character != null
            && character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneWhereverYouAre) > 0
            && !HasAnyDirectTargetAvailable(character, allowDiagonals);
        return CanTargetCell(character, targetCell, allowDiagonals, allowEmptyCell);
    }

    public override bool TryGetAutomaticTargetCell(Character character, CharacterAbilityRuntime runtime, out Vector2Int targetCell)
    {
        targetCell = default;
        if (character == null || character.Board == null)
        {
            return false;
        }

        bool allowDiagonals = character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneBlindSpot) > 0;
        if (character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneWhereverYouAre) <= 0
            || HasAnyDirectTargetAvailable(character, allowDiagonals))
        {
            return false;
        }

        targetCell = character.GridPosition;
        return true;
    }

    public bool HasFallbackCastOpportunity(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        bool allowDiagonals = character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneBlindSpot) > 0;
        return character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneWhereverYouAre) > 0
            && !HasAnyDirectTargetAvailable(character, allowDiagonals)
            && FindFallbackBleedTarget(character) != null;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || character.Board == null || !targetCell.HasValue)
        {
            return false;
        }

        bool allowDiagonals = character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneBlindSpot) > 0;
        bool hasDirectTargetAvailable = HasAnyDirectTargetAvailable(character, allowDiagonals);
        if (character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneWhereverYouAre) > 0
            && !hasDirectTargetAvailable
            && targetCell.Value == character.GridPosition)
        {
            if (TryApplyFallbackBleed(character))
            {
                pendingFallbackCastByCharacter[character] = true;
                PlayConfiguredFx(character);
                return true;
            }

            return false;
        }

        if (!HectorAbilityUtils.TryResolveAlignedDirection(character.GridPosition, targetCell.Value, allowDiagonals, range, out Vector2Int direction, out _)
            || !HectorAbilityUtils.IsLineClear(character.Board, character.GridPosition, targetCell.Value, direction))
        {
            return false;
        }

        character.FaceTargetCell(targetCell.Value);

        int damage = baseDamage;
        if (character.RemainingMovementPoints <= 0)
        {
            damage += character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneLastChance);
        }

        if (character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) && enemy != null)
        {
            if (enemy.HasStatusEffect(CombatStatusType.Bleeding))
            {
                damage += 2 * character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneBloodCallsBlood);
            }

            HectorAbilityUtils.TryPlayLinearProjectile(
                character,
                projectilePrefab,
                enemy.transform.position + projectileImpactOffset,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay,
                () =>
                {
                    int appliedDamage = character.DealDamageToEnemy(enemy, damage, true, true, DamageSoundType.Default, this);
                    if (appliedDamage > 0 && enemy != null && enemy.CurrentHealth > 0)
                    {
                        enemy.ApplyStatusEffect(CombatStatusType.Bleeding, -1, 1);
                    }
                });
            PlayConfiguredFx(character, new[] { enemy });
            return true;
        }

        if (character.Board.TryGetLichSkullObject(targetCell.Value, out LichSkullObject skull) && skull != null)
        {
            HectorAbilityUtils.TryPlayLinearProjectile(
                character,
                projectilePrefab,
                skull.transform.position + projectileImpactOffset,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay,
                () => character.DealDamageToLichSkull(skull, damage, true, DamageSoundType.Default, this));
            PlayConfiguredFx(character);
            return true;
        }

        if (character.Board.TryGetBarrel(targetCell.Value, out BarrelObstacle barrel) && barrel != null)
        {
            HectorAbilityUtils.TryPlayLinearProjectile(
                character,
                projectilePrefab,
                character.Board.GridToWorldPosition(barrel.GridPosition) + projectileImpactOffset,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay,
                barrel.TakeHit);
            PlayConfiguredFx(character);
            return true;
        }

        if (character.GetUpgradeStacks(AbilityUpgradeKey.DemonbaneWhereverYouAre) > 0
            && !hasDirectTargetAvailable
            && TryApplyFallbackBleed(character))
        {
            pendingFallbackCastByCharacter[character] = true;
            PlayConfiguredFx(character);
            return true;
        }

        return false;
    }

    public override void PlayActivationAnimation(Character character)
    {
        if (character != null
            && pendingFallbackCastByCharacter.TryGetValue(character, out bool useFallbackAnimation)
            && useFallbackAnimation
            && fallbackIncantationAnimation != null)
        {
            character.PlayAttackAnimation(fallbackIncantationAnimation);
            pendingFallbackCastByCharacter[character] = false;
            return;
        }

        if (character != null)
        {
            pendingFallbackCastByCharacter[character] = false;
        }

        base.PlayActivationAnimation(character);
    }

    private bool TryApplyFallbackBleed(Character character)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        Enemy bestTarget = FindFallbackBleedTarget(character);
        if (bestTarget == null)
        {
            return false;
        }

        character.StartCoroutine(ApplyFallbackBleedAfterDelay(character, bestTarget));
        return true;
    }

    private System.Collections.IEnumerator ApplyFallbackBleedAfterDelay(Character character, Enemy target)
    {
        if (fallbackBleedDelay > 0f)
        {
            yield return new WaitForSeconds(fallbackBleedDelay);
        }

        if (character == null || target == null || target.CurrentHealth <= 0)
        {
            yield break;
        }

        target.ApplyStatusEffect(CombatStatusType.Bleeding, -1, 1);
        character.PlayFeedbackFx(
            fallbackBleedImpactFxPrefab,
            target.EffectAnchor,
            destroyAfterSeconds: fallbackBleedImpactFxDuration);
    }

    private Enemy FindFallbackBleedTarget(Character character)
    {
        if (character == null || character.Board == null)
        {
            return null;
        }

        Enemy bestTarget = null;
        IReadOnlyList<Enemy> enemies = character.Board.SpawnedEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy candidate = enemies[index];
            if (candidate == null || candidate.CurrentHealth <= 0 || candidate.HasStatusEffect(CombatStatusType.Bleeding))
            {
                continue;
            }

            if (bestTarget == null
                || candidate.CurrentHealth < bestTarget.CurrentHealth
                || (candidate.CurrentHealth == bestTarget.CurrentHealth && candidate.MaxHealth < bestTarget.MaxHealth))
            {
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private bool HasAnyDirectTargetAvailable(Character character, bool allowDiagonals)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        Vector2Int[] directions = allowDiagonals
            ? HectorAbilityUtils.OrthogonalAndDiagonalDirections
            : HectorAbilityUtils.OrthogonalDirections;

        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            Vector2Int direction = directions[directionIndex];
            for (int step = 1; step <= range; step++)
            {
                Vector2Int cellPosition = character.GridPosition + (direction * step);
                if (!character.Board.TryGetCell(cellPosition, out BoardCell boardCell))
                {
                    break;
                }

                if (character.Board.TryGetEnemy(cellPosition, out Enemy enemy) && enemy != null)
                {
                    return true;
                }

                if (character.Board.TryGetLichSkullObject(cellPosition, out LichSkullObject skull) && skull != null)
                {
                    return true;
                }

                if (character.Board.TryGetBarrel(cellPosition, out BarrelObstacle barrel) && barrel != null)
                {
                    return true;
                }

                if (boardCell.HasBlockingTerrain || boardCell.IsOccupied)
                {
                    break;
                }
            }
        }

        return false;
    }

    private bool CanTargetCell(Character character, Vector2Int targetCell, bool allowDiagonals, bool allowEmptyCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        if (allowEmptyCell && targetCell == character.GridPosition)
        {
            return true;
        }

        if (!HectorAbilityUtils.TryResolveAlignedDirection(character.GridPosition, targetCell, allowDiagonals, range, out Vector2Int direction, out _)
            || !HectorAbilityUtils.IsLineClear(character.Board, character.GridPosition, targetCell, direction))
        {
            return false;
        }

        if (allowEmptyCell)
        {
            return character.Board.IsInsideBoard(targetCell);
        }

        return (character.Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null)
            || (character.Board.TryGetLichSkullObject(targetCell, out LichSkullObject skull) && skull != null)
            || (character.Board.TryGetBarrel(targetCell, out BarrelObstacle barrel) && barrel != null);
    }
}
