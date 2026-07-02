using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Heart Ripper", fileName = "HeartRipper")]
public class HeartRipperAbility : AbilityDefinition
{
    private readonly Dictionary<Character, HectorShadowAttackContext> pendingShadowAttackContextByCharacter = new Dictionary<Character, HectorShadowAttackContext>();

    [Min(1)]
    [SerializeField] private int baseDamage = 3;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        if (!character.Board.TryGetEnemy(targetCell, out Enemy enemy) || enemy == null)
        {
            return false;
        }

        return TryGetLineInfo(character, targetCell, out _, out _);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return TryGetLineInfo(character, targetCell, out _, out _);
    }

    public override bool ShouldAutoUseWhenOnlyOneValidTarget(Character character, CharacterAbilityRuntime runtime)
    {
        return true;
    }

    public override bool TryGetAutomaticTargetCell(Character character, CharacterAbilityRuntime runtime, out Vector2Int targetCell)
    {
        targetCell = default;
        if (character == null || character.Board == null)
        {
            return false;
        }

        Vector2Int singleValidCell = default;
        int validTargetCount = 0;
        IReadOnlyList<Enemy> enemies = character.Board.SpawnedEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy enemy = enemies[index];
            if (enemy == null || enemy.CurrentHealth <= 0 || !CanActivateOnCell(character, runtime, enemy.GridPosition))
            {
                continue;
            }

            validTargetCount++;
            if (validTargetCount > 1)
            {
                return false;
            }

            singleValidCell = enemy.GridPosition;
        }

        if (validTargetCount != 1)
        {
            return false;
        }

        targetCell = singleValidCell;
        return true;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || character.Board == null || !targetCell.HasValue || !TryGetLineInfo(character, targetCell.Value, out HectorShadowAttackContext context, out int distance))
        {
            return false;
        }

        if (!character.Board.TryGetEnemy(targetCell.Value, out Enemy primaryTarget) || primaryTarget == null)
        {
            return false;
        }

        BidimensionalShadowAbility.FaceTargetForContext(character, context, targetCell.Value);
        pendingShadowAttackContextByCharacter[character] = context;

        int damage = baseDamage + character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperHookedFist);
        if (character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperDemonicDuel) > 0 && CountLivingEnemies(character.Board) == 1)
        {
            damage += 1;
        }

        int healAmountPerKill = 2 + character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperRevigoratingHeart);
        Enemy middleEnemy = null;
        if (character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperDemonicHand) > 0 && distance == 2)
        {
            Vector2Int middleCell = context.PrimaryOriginCell + context.PrimaryDirection;
            if (character.Board.TryGetEnemy(middleCell, out Enemy foundMiddleEnemy) && foundMiddleEnemy != null)
            {
                middleEnemy = foundMiddleEnemy;
            }
        }

        Enemy secondaryMiddleEnemy = null;
        if (context.CanTwinAttack && character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperDemonicHand) > 0 && distance == 2)
        {
            Vector2Int secondaryMiddleCell = context.SecondaryOriginCell + context.SecondaryDirection;
            if (character.Board.TryGetEnemy(secondaryMiddleCell, out Enemy foundSecondaryMiddleEnemy) && foundSecondaryMiddleEnemy != null)
            {
                secondaryMiddleEnemy = foundSecondaryMiddleEnemy;
            }
        }

        character.StartCoroutine(ResolveHeartRipperSequence(character, context, primaryTarget, middleEnemy, secondaryMiddleEnemy, damage, healAmountPerKill));
        return true;
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

    public bool HasExecutableHealOpportunity(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || character.Board == null || character.CurrentHealth >= character.MaxHealth)
        {
            return false;
        }

        int damage = GetDamage(character);
        IReadOnlyList<Enemy> enemies = character.Board.SpawnedEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy primaryTarget = enemies[index];
            if (primaryTarget == null
                || primaryTarget.CurrentHealth <= 0
                || !CanActivateOnCell(character, runtime, primaryTarget.GridPosition)
                || !TryGetLineInfo(character, primaryTarget.GridPosition, out HectorShadowAttackContext context, out int distance))
            {
                continue;
            }

            if (primaryTarget.CurrentHealth <= damage)
            {
                return true;
            }

            if (character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperDemonicHand) > 0 && distance == 2)
            {
                Vector2Int middleCell = context.PrimaryOriginCell + context.PrimaryDirection;
                if (character.Board.TryGetEnemy(middleCell, out Enemy middleEnemy)
                    && middleEnemy != null
                    && middleEnemy.CurrentHealth > 0
                    && middleEnemy.CurrentHealth <= damage)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerator ResolveHeartRipperSequence(
        Character character,
        HectorShadowAttackContext context,
        Enemy primaryTarget,
        Enemy middleEnemy,
        Enemy secondaryMiddleEnemy,
        int damage,
        int healAmountPerKill)
    {
        if (character == null || primaryTarget == null)
        {
            yield break;
        }

        character.BeginActionLock();

        float damageDelay = GetDamageApplyDelay(context.PrimaryOriginCell, primaryTarget.GridPosition);
        if (damageDelay > 0f)
        {
            yield return new WaitForSeconds(damageDelay);
        }

        int totalHeal = 0;

        if (middleEnemy != null && middleEnemy.CurrentHealth > 0)
        {
            character.DealDamageToEnemy(middleEnemy, damage, true, true, DamageSoundType.Sword, this);
            if (middleEnemy.CurrentHealth <= 0)
            {
                totalHeal += healAmountPerKill;
            }
        }

        if (primaryTarget != null && primaryTarget.CurrentHealth > 0)
        {
            character.DealDamageToEnemy(primaryTarget, damage, true, true, DamageSoundType.Sword, this);
        }

        if (primaryTarget != null && primaryTarget.CurrentHealth <= 0)
        {
            totalHeal += healAmountPerKill;
        }

        if (context.CanTwinAttack)
        {
            yield return new WaitForSeconds(0.1f);

            if (secondaryMiddleEnemy != null && secondaryMiddleEnemy.CurrentHealth > 0)
            {
                character.DealDamageToEnemy(secondaryMiddleEnemy, damage, true, true, DamageSoundType.Sword, this);
                if (secondaryMiddleEnemy.CurrentHealth <= 0)
                {
                    totalHeal += healAmountPerKill;
                }
            }

            if (primaryTarget != null && primaryTarget.CurrentHealth > 0)
            {
                character.DealDamageToEnemy(primaryTarget, damage, true, true, DamageSoundType.Sword, this);
                if (primaryTarget.CurrentHealth <= 0)
                {
                    totalHeal += healAmountPerKill;
                }
            }
        }

        if (totalHeal > 0)
        {
            int previousHealth = character.CurrentHealth;
            character.Heal(totalHeal, null, false);
            if (character.CurrentHealth > previousHealth)
            {
                character.PlayHealProcFx();
            }
        }

        if (primaryTarget != null && primaryTarget.CurrentHealth > 0)
        {
            character.TakeDamage(2, null, false, DamageSoundType.Default);
        }

        PlayConfiguredFx(character, new[] { primaryTarget });
        character.EndActionLock();
    }

    private bool TryGetLineInfo(Character character, Vector2Int targetCell, out HectorShadowAttackContext context, out int distance)
    {
        context = default;
        distance = 0;
        if (character == null || character.Board == null)
        {
            return false;
        }

        int maxRange = 1 + (character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperDemonicHand) > 0 ? 1 : 0);
        if (!BidimensionalShadowAbility.TryResolveLineAttackContext(character, targetCell, false, maxRange, out context))
        {
            return false;
        }

        distance = context.PrimaryDistance;
        if (distance == 2)
        {
            Vector2Int middleCell = context.PrimaryOriginCell + context.PrimaryDirection;
            if (!character.Board.TryGetCell(middleCell, out BoardCell boardCell) || boardCell.HasBlockingTerrain)
            {
                return false;
            }
        }

        return distance == 1 || distance == 2;
    }

    private int CountLivingEnemies(BoardManager board)
    {
        if (board == null)
        {
            return 0;
        }

        int count = 0;
        IReadOnlyList<Enemy> enemies = board.SpawnedEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            if (enemies[index] != null && enemies[index].CurrentHealth > 0)
            {
                count++;
            }
        }

        return count;
    }

    private int GetDamage(Character character)
    {
        int damage = baseDamage + character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperHookedFist);
        if (character.GetUpgradeStacks(AbilityUpgradeKey.HeartRipperDemonicDuel) > 0 && CountLivingEnemies(character.Board) == 1)
        {
            damage += 1;
        }

        return damage;
    }
}
