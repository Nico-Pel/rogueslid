using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Hector Crossbow", fileName = "HectorCrossbow")]
public class HectorCrossbowAbility : AbilityDefinition
{
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

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        return $"Deal {baseDamage} damage to an enemy in a straight line within range {range}.";
    }

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;
    public override bool IsRangedAttack => true;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanTargetCell(character, targetCell, false, false);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanTargetCell(character, targetCell, true, false);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || character.Board == null || !targetCell.HasValue)
        {
            return false;
        }

        if (!BidimensionalShadowAbility.TryResolveLineAttackContext(character, targetCell.Value, false, range, out HectorShadowAttackContext context))
        {
            return false;
        }

        BidimensionalShadowAbility.FaceTargetForContext(character, context, targetCell.Value);
        pendingShadowAttackContextByCharacter[character] = context;

        if (character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) && enemy != null)
        {
            Vector3 primaryStart = BidimensionalShadowAbility.GetPrimaryProjectileStartWorldPosition(character, context, projectileSpawnOffset);
            HectorAbilityUtils.TryPlayLinearProjectileFromWorldPosition(
                character,
                projectilePrefab,
                primaryStart,
                enemy.transform.position + projectileImpactOffset,
                projectileSpeed,
                projectileLaunchDelay,
                () => character.DealDamageToEnemy(enemy, baseDamage, true, true, DamageSoundType.Default, this));

            if (context.CanTwinAttack)
            {
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
                        if (enemy != null && enemy.CurrentHealth > 0)
                        {
                            character.DealDamageToEnemy(enemy, baseDamage, true, true, DamageSoundType.Default, this);
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
                () => character.DealDamageToLichSkull(skull, baseDamage, true, DamageSoundType.Default, this));
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

        return false;
    }

    public override void PlayActivationAnimation(Character character)
    {
        if (character != null
            && pendingShadowAttackContextByCharacter.TryGetValue(character, out HectorShadowAttackContext context))
        {
            BidimensionalShadowAbility.PlayAttackAnimationForContext(character, context, AttackAnimationClip);
            pendingShadowAttackContextByCharacter.Remove(character);
            return;
        }

        base.PlayActivationAnimation(character);
    }

    private bool CanTargetCell(Character character, Vector2Int targetCell, bool allowEmptyCell, bool allowDiagonals)
    {
        if (character == null || character.Board == null)
        {
            return false;
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
