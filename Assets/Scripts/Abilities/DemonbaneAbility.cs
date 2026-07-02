using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Demonbane", fileName = "Demonbane")]
public class DemonbaneAbility : AbilityDefinition
{
    private readonly Dictionary<Character, bool> pendingFallbackCastByCharacter = new Dictionary<Character, bool>();
    private readonly Dictionary<Character, HectorShadowAttackContext> pendingShadowAttackContextByCharacter = new Dictionary<Character, HectorShadowAttackContext>();

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

        if (!BidimensionalShadowAbility.TryResolveLineAttackContext(character, targetCell.Value, allowDiagonals, range, out HectorShadowAttackContext context))
        {
            return false;
        }

        BidimensionalShadowAbility.FaceTargetForContext(character, context, targetCell.Value);
        pendingShadowAttackContextByCharacter[character] = context;

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

            Vector3 primaryStart = BidimensionalShadowAbility.GetPrimaryProjectileStartWorldPosition(character, context, projectileSpawnOffset);
            HectorAbilityUtils.TryPlayLinearProjectileFromWorldPosition(
                character,
                projectilePrefab,
                primaryStart,
                enemy.transform.position + projectileImpactOffset,
                projectileSpeed,
                projectileLaunchDelay,
                () =>
                {
                    int appliedDamage = character.DealDamageToEnemy(enemy, damage, true, true, DamageSoundType.Default, this);
                    if (appliedDamage > 0 && enemy != null && enemy.CurrentHealth > 0)
                    {
                        enemy.ApplyStatusEffect(CombatStatusType.Bleeding, -1, 1);
                    }
                });

            if (context.CanTwinAttack)
            {
                int twinDamage = damage;
                Vector3 secondaryStart = BidimensionalShadowAbility.GetSecondaryProjectileStartWorldPosition(character, context, projectileSpawnOffset);
                HectorAbilityUtils.TryPlayLinearProjectileFromWorldPosition(
                    character,
                    projectilePrefab,
                    secondaryStart,
                    enemy.transform.position + projectileImpactOffset,
                    projectileSpeed,
                    projectileLaunchDelay + 0.1f,
                    () =>
                    {
                        if (enemy == null || enemy.CurrentHealth <= 0)
                        {
                            return;
                        }

                        int appliedDamage = character.DealDamageToEnemy(enemy, twinDamage, true, true, DamageSoundType.Default, this);
                        if (appliedDamage > 0 && enemy.CurrentHealth > 0)
                        {
                            enemy.ApplyStatusEffect(CombatStatusType.Bleeding, -1, 1);
                        }
                    },
                    false);
            }

            PlayConfiguredFx(character, new[] { enemy });
            return true;
        }

        if (character.Board.TryGetLichSkullObject(targetCell.Value, out LichSkullObject skull) && skull != null)
        {
            Vector3 primaryStart = BidimensionalShadowAbility.GetPrimaryProjectileStartWorldPosition(character, context, projectileSpawnOffset);
            HectorAbilityUtils.TryPlayLinearProjectileFromWorldPosition(
                character,
                projectilePrefab,
                primaryStart,
                skull.transform.position + projectileImpactOffset,
                projectileSpeed,
                projectileLaunchDelay,
                () => character.DealDamageToLichSkull(skull, damage, true, DamageSoundType.Default, this));
            PlayConfiguredFx(character);
            return true;
        }

        if (character.Board.TryGetBarrel(targetCell.Value, out BarrelObstacle barrel) && barrel != null)
        {
            Vector3 primaryStart = BidimensionalShadowAbility.GetPrimaryProjectileStartWorldPosition(character, context, projectileSpawnOffset);
            HectorAbilityUtils.TryPlayLinearProjectileFromWorldPosition(
                character,
                projectilePrefab,
                primaryStart,
                character.Board.GridToWorldPosition(barrel.GridPosition) + projectileImpactOffset,
                projectileSpeed,
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
            && pendingShadowAttackContextByCharacter.TryGetValue(character, out HectorShadowAttackContext context))
        {
            BidimensionalShadowAbility.PlayAttackAnimationForContext(character, context, AttackAnimationClip);
            pendingShadowAttackContextByCharacter.Remove(character);
            pendingFallbackCastByCharacter[character] = false;
            return;
        }

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

        for (int x = 0; x < character.Board.Width; x++)
        {
            for (int y = 0; y < character.Board.Height; y++)
            {
                if (CanTargetCell(character, new Vector2Int(x, y), allowDiagonals, false))
                {
                    return true;
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

        if (!BidimensionalShadowAbility.TryResolveLineAttackContext(character, targetCell, allowDiagonals, range, out _))
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
