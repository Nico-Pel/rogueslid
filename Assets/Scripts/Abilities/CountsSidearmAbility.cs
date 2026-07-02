using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Count's Sidearm", fileName = "CountsSidearm")]
public class CountsSidearmAbility : AbilityDefinition
{
    private readonly Dictionary<Character, HectorShadowAttackContext> pendingShadowAttackContextByCharacter = new Dictionary<Character, HectorShadowAttackContext>();

    [Min(1)]
    [SerializeField] private int baseDamage = 3;
    [SerializeField] private GameObject projectilePrefab;
    [Min(0.01f)]
    [SerializeField] private float projectileSpeed = 24f;
    [Min(0f)]
    [SerializeField] private float projectileLaunchDelay = 0.05f;
    [SerializeField] private GameObject castSoundParametersPrefab;
    [Min(0f)]
    [SerializeField] private float castSoundDelay;
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 projectileImpactOffset = new Vector3(0f, 0.2f, 0f);

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;
    public override bool IsRangedAttack => true;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        return $"Fire Backup Pistol to deal {baseDamage} damage to an enemy in a straight line or diagonal.";
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        IReadOnlyList<Enemy> enemies = character.Board.SpawnedEnemies;
        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy enemy = enemies[index];
            if (enemy != null && enemy.CurrentHealth > 0 && CanActivateOnCell(character, runtime, enemy.GridPosition))
            {
                return true;
            }
        }

        return false;
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return TryResolveTarget(character, targetCell, out _, out _);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return character != null
            && character.Board != null
            && BidimensionalShadowAbility.TryResolveLineAttackContext(character, targetCell, true, GetMaxRange(character), out _)
            && character.Board.IsInsideBoard(targetCell);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (!targetCell.HasValue || !TryResolveTarget(character, targetCell.Value, out Enemy enemy, out HectorShadowAttackContext context))
        {
            return false;
        }

        BidimensionalShadowAbility.FaceTargetForContext(character, context, targetCell.Value);
        pendingShadowAttackContextByCharacter[character] = context;
        PlayCastSound(character);
        Vector3 primaryStart = BidimensionalShadowAbility.GetPrimaryProjectileStartWorldPosition(character, context, projectileSpawnOffset);
        HectorAbilityUtils.TryPlayLinearProjectileFromWorldPosition(
            character,
            projectilePrefab,
            primaryStart,
            enemy.transform.position + projectileImpactOffset,
            projectileSpeed,
            projectileLaunchDelay,
            () => character.DealDamageToEnemy(enemy, baseDamage, true, true, DamageSoundType.BulletHit, this));

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
                        character.DealDamageToEnemy(enemy, baseDamage, true, true, DamageSoundType.BulletHit, this);
                    }
                },
                false);
        }

        PlayConfiguredFx(character, new[] { enemy });
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

    private bool TryResolveTarget(Character character, Vector2Int targetCell, out Enemy enemy, out HectorShadowAttackContext context)
    {
        enemy = null;
        context = default;
        if (character == null
            || character.Board == null
            || !BidimensionalShadowAbility.TryResolveLineAttackContext(character, targetCell, true, GetMaxRange(character), out context))
        {
            return false;
        }

        return character.Board.TryGetEnemy(targetCell, out enemy) && enemy != null;
    }

    private bool TryFindFirstEnemyInDirection(Character character, Vector2Int direction, int maxRange, out Enemy enemy)
    {
        enemy = null;
        if (character == null || character.Board == null)
        {
            return false;
        }

        for (int step = 1; step <= maxRange; step++)
        {
            Vector2Int cellPosition = character.GridPosition + (direction * step);
            if (!character.Board.TryGetCell(cellPosition, out BoardCell cell))
            {
                break;
            }

            if (character.Board.TryGetEnemy(cellPosition, out enemy) && enemy != null)
            {
                return true;
            }

            if (cell.HasBlockingTerrain || cell.IsOccupied)
            {
                break;
            }
        }

        return false;
    }

    private int GetMaxRange(Character character)
    {
        return 99;
    }

    private void PlayCastSound(Character character)
    {
        if (character == null || castSoundParametersPrefab == null)
        {
            return;
        }

        character.StartCoroutine(PlayCastSoundAfterDelay(character));
    }

    private System.Collections.IEnumerator PlayCastSoundAfterDelay(Character character)
    {
        if (castSoundDelay > 0f)
        {
            yield return new WaitForSeconds(castSoundDelay);
        }

        if (character == null || castSoundParametersPrefab == null)
        {
            yield break;
        }

        GameObject soundObject = Instantiate(
            castSoundParametersPrefab,
            character.transform.position,
            castSoundParametersPrefab.transform.rotation);
        soundObject.transform.localScale = castSoundParametersPrefab.transform.localScale;
        SoundParameters soundParameters = soundObject.GetComponent<SoundParameters>();
        soundParameters?.PlaySound(character.transform.position);
        Destroy(soundObject, 2f);
    }
}
