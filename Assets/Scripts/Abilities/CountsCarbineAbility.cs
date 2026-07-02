using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Count's Carbine", fileName = "CountsCarbine")]
public class CountsCarbineAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 6;
    [Min(1)]
    [SerializeField] private int baseRange = 4;
    [SerializeField] private GameObject projectilePrefab;
    [Min(0.01f)]
    [SerializeField] private float projectileSpeed = 28f;
    [Min(0f)]
    [SerializeField] private float projectileLaunchDelay = 0.08f;
    [SerializeField] private GameObject castSoundParametersPrefab;
    [Min(0f)]
    [SerializeField] private float castSoundDelay;
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 projectileImpactOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private CountsSidearmAbility backupPistolAbilityData;

    private readonly Dictionary<CharacterAbilityRuntime, bool> backupPistolAvailableByRuntime = new Dictionary<CharacterAbilityRuntime, bool>();
    private readonly Dictionary<CharacterAbilityRuntime, bool> backupPistolVisualModeByRuntime = new Dictionary<CharacterAbilityRuntime, bool>();
    private readonly HashSet<CharacterAbilityRuntime> backupPistolAnimationPending = new HashSet<CharacterAbilityRuntime>();
    private readonly Dictionary<Character, HectorShadowAttackContext> pendingShadowAttackContextByCharacter = new Dictionary<Character, HectorShadowAttackContext>();

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;
    public override bool IsRangedAttack => true;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        if (IsBackupPistolVisualMode(runtime) && backupPistolAbilityData != null)
        {
            return backupPistolAbilityData.GetDisplayDescription(character, runtime);
        }

        int range = GetRange(character);
        int silverBulletsBonus = character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.CountsCarbineSilverBullets) : 0;
        bool pointBlankActive = character != null && character.GetUpgradeStacks(AbilityUpgradeKey.CountsCarbinePointBlank) > 0;
        bool piercingRoundActive = character != null && character.GetUpgradeStacks(AbilityUpgradeKey.CountsCarbinePiercingRound) > 0;
        return $"Deal {baseDamage + silverBulletsBonus} damage to an enemy within range {range}. Point-Blank: {(pointBlankActive ? "+2 at distance 2 or less" : "Locked")}. Piercing Round: {(piercingRoundActive ? "Hits every enemy in the shot line" : "Locked")}. Backup Pistol: {(character != null && character.GetUpgradeStacks(AbilityUpgradeKey.CountsCarbineBackupPistol) > 0 ? "When Count's Carbine runs out of uses this turn, gain Backup Pistol for 1 shot." : "Locked")}.";
    }

    public override AbilityDefinition GetPresentationDefinition(Character character, CharacterAbilityRuntime runtime)
    {
        return IsBackupPistolVisualMode(runtime) && backupPistolAbilityData != null
            ? backupPistolAbilityData
            : base.GetPresentationDefinition(character, runtime);
    }

    public override string GetDisplayName(Character character, CharacterAbilityRuntime runtime)
    {
        if (!IsBackupPistolVisualMode(runtime))
        {
            return base.GetDisplayName(character, runtime);
        }

        return backupPistolAbilityData != null
            ? backupPistolAbilityData.GetDisplayName(character, runtime)
            : "Backup Pistol";
    }

    public override Sprite GetIcon(CharacterAbilityRuntime runtime)
    {
        if (IsBackupPistolVisualMode(runtime) && backupPistolAbilityData != null)
        {
            Sprite alternateIcon = backupPistolAbilityData.GetIcon(runtime);
            if (alternateIcon != null)
            {
                return alternateIcon;
            }
        }

        return base.GetIcon(runtime);
    }

    public override string GetCounterText(CharacterAbilityRuntime runtime)
    {
        if (runtime == null)
        {
            return string.Empty;
        }

        if (IsBackupPistolAvailable(runtime))
        {
            return "1";
        }

        if (IsBackupPistolVisualMode(runtime))
        {
            return "0";
        }

        return base.GetCounterText(runtime);
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || runtime == null || character.Board == null)
        {
            return false;
        }

        if (IsBackupPistolAvailable(runtime) && backupPistolAbilityData != null)
        {
            return backupPistolAbilityData.CanActivate(character, runtime);
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
        if (IsBackupPistolAvailable(runtime) && backupPistolAbilityData != null)
        {
            return backupPistolAbilityData.CanActivateOnCell(character, runtime, targetCell);
        }

        return TryResolveShot(character, targetCell, out _, out _);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (IsBackupPistolVisualMode(runtime) && backupPistolAbilityData != null)
        {
            return backupPistolAbilityData.CanShowPotentialTargetCell(character, runtime, targetCell);
        }

        return character != null
            && character.Board != null
            && BidimensionalShadowAbility.TryResolveLineAttackContext(character, targetCell, false, GetRange(character), out _)
            && character.Board.IsInsideBoard(targetCell);
    }

    public override void OnTurnStarted(Character character, CharacterAbilityRuntime runtime)
    {
        if (runtime != null)
        {
            backupPistolAvailableByRuntime[runtime] = false;
            backupPistolVisualModeByRuntime[runtime] = false;
        }
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue)
        {
            return false;
        }

        if (IsBackupPistolAvailable(runtime) && backupPistolAbilityData != null)
        {
            backupPistolAnimationPending.Add(runtime);
            bool activatedBackupPistol = backupPistolAbilityData.TryActivate(character, runtime, targetCell);
            if (!activatedBackupPistol)
            {
                backupPistolAnimationPending.Remove(runtime);
                return false;
            }

            backupPistolAvailableByRuntime[runtime] = false;
            backupPistolVisualModeByRuntime[runtime] = true;
            character.RefreshAbilityState();
            return true;
        }

        if (!TryResolveShot(character, targetCell.Value, out Enemy primaryTarget, out HectorShadowAttackContext context))
        {
            return false;
        }

        BidimensionalShadowAbility.FaceTargetForContext(character, context, targetCell.Value);
        pendingShadowAttackContextByCharacter[character] = context;
        PlayCastSound(character);
        List<Enemy> shotTargets = GatherShotTargets(character, primaryTarget, context.PrimaryDirection, context.PrimaryOriginCell);
        Vector3 primaryStart = BidimensionalShadowAbility.GetPrimaryProjectileStartWorldPosition(character, context, projectileSpawnOffset);
        HectorAbilityUtils.TryPlayLinearProjectileFromWorldPosition(
            character,
            projectilePrefab,
            primaryStart,
            primaryTarget.transform.position + projectileImpactOffset,
            projectileSpeed,
            projectileLaunchDelay,
            () => ResolveShotImpact(character, runtime, shotTargets, context.PrimaryDirection, false));

        if (context.CanTwinAttack)
        {
            List<Enemy> secondaryShotTargets = GatherShotTargets(character, primaryTarget, context.SecondaryDirection, context.SecondaryOriginCell);
            Vector3 secondaryStart = BidimensionalShadowAbility.GetSecondaryProjectileStartWorldPosition(character, context, projectileSpawnOffset);
            HectorAbilityUtils.TryPlayLinearProjectileFromWorldPosition(
                character,
                projectilePrefab,
                secondaryStart,
                primaryTarget.transform.position + projectileImpactOffset,
                projectileSpeed,
                projectileLaunchDelay + 0.1f,
                () => ResolveShotImpact(character, runtime, secondaryShotTargets, context.SecondaryDirection, true),
                false);
        }

        PlayConfiguredFx(character, shotTargets);
        return true;
    }

    public override void PlayActivationAnimation(Character character)
    {
        CharacterAbilityRuntime pendingRuntime = FindRuntimeForCharacter(character);
        if (pendingRuntime != null
            && backupPistolAnimationPending.Remove(pendingRuntime)
            && backupPistolAbilityData != null)
        {
            backupPistolAbilityData.PlayActivationAnimation(character);
            return;
        }

        if (character != null
            && pendingShadowAttackContextByCharacter.TryGetValue(character, out HectorShadowAttackContext context))
        {
            BidimensionalShadowAbility.PlayAttackAnimationForContext(character, context, AttackAnimationClip);
            pendingShadowAttackContextByCharacter.Remove(character);
            return;
        }

        base.PlayActivationAnimation(character);
    }

    private void ResolveShotImpact(Character character, CharacterAbilityRuntime runtime, List<Enemy> shotTargets, Vector2Int direction, bool isTwinFollowUp)
    {
        if (character == null || runtime == null || shotTargets == null || shotTargets.Count == 0)
        {
            return;
        }

        int silverBulletsStacks = character.GetUpgradeStacks(AbilityUpgradeKey.CountsCarbineSilverBullets);
        bool pointBlankActive = character.GetUpgradeStacks(AbilityUpgradeKey.CountsCarbinePointBlank) > 0;
        bool shellShotActive = character.GetUpgradeStacks(AbilityUpgradeKey.CountsCarbineShellShot) > 0;
        bool backupPistolUnlocked = character.GetUpgradeStacks(AbilityUpgradeKey.CountsCarbineBackupPistol) > 0;

        for (int index = 0; index < shotTargets.Count; index++)
        {
            Enemy enemy = shotTargets[index];
            if (enemy == null || enemy.CurrentHealth <= 0)
            {
                continue;
            }

            int damage = baseDamage + silverBulletsStacks;
            int enemyDistance = Mathf.Max(
                1,
                Mathf.Max(
                    Mathf.Abs(enemy.GridPosition.x - character.GridPosition.x),
                    Mathf.Abs(enemy.GridPosition.y - character.GridPosition.y)));
            if (pointBlankActive && enemyDistance <= 2)
            {
                damage += 2;
            }

            if (isTwinFollowUp && enemy.CurrentHealth <= 0)
            {
                continue;
            }

            character.DealDamageToEnemy(enemy, damage, true, true, DamageSoundType.BulletHit, this);
            if (shellShotActive)
            {
                TryKnockbackEnemy(enemy, direction);
            }
        }

        if (backupPistolUnlocked
            && !isTwinFollowUp
            && !IsBackupPistolAvailable(runtime)
            && runtime.RemainingUsesThisTurn <= 0)
        {
            backupPistolAvailableByRuntime[runtime] = true;
            backupPistolVisualModeByRuntime[runtime] = true;
            runtime.GrantBonusTurnUse(1);
            character.RefreshAbilityState();
        }
    }

    private bool TryResolveShot(Character character, Vector2Int targetCell, out Enemy primaryTarget, out HectorShadowAttackContext context)
    {
        primaryTarget = null;
        context = default;
        if (character == null
            || character.Board == null
            || !BidimensionalShadowAbility.TryResolveLineAttackContext(character, targetCell, false, GetRange(character), out context))
        {
            return false;
        }

        bool canPierceEnemies = character.GetUpgradeStacks(AbilityUpgradeKey.CountsCarbinePiercingRound) > 0;
        Vector2Int scan = context.PrimaryOriginCell + context.PrimaryDirection;
        while (scan != targetCell)
        {
            if (!character.Board.TryGetCell(scan, out BoardCell cell) || cell.HasBlockingTerrain)
            {
                return false;
            }

            if (cell.IsOccupied)
            {
                bool occupiedByEnemy = character.Board.TryGetEnemy(scan, out Enemy occupantEnemy) && occupantEnemy != null;
                if (!canPierceEnemies || !occupiedByEnemy)
                {
                    return false;
                }
            }

            scan += context.PrimaryDirection;
        }

        return character.Board.TryGetEnemy(targetCell, out primaryTarget) && primaryTarget != null;
    }

    private List<Enemy> GatherShotTargets(Character character, Enemy primaryTarget, Vector2Int direction, Vector2Int originCell)
    {
        List<Enemy> targets = new List<Enemy>();
        if (character == null || character.Board == null || primaryTarget == null)
        {
            return targets;
        }

        bool piercingRoundActive = character.GetUpgradeStacks(AbilityUpgradeKey.CountsCarbinePiercingRound) > 0;
        if (!piercingRoundActive)
        {
            targets.Add(primaryTarget);
            return targets;
        }

        for (int step = 1; step <= GetRange(character); step++)
        {
            Vector2Int cellPosition = originCell + (direction * step);
            if (!character.Board.TryGetCell(cellPosition, out BoardCell cell) || cell.HasBlockingTerrain)
            {
                break;
            }

            if (character.Board.TryGetEnemy(cellPosition, out Enemy enemy) && enemy != null)
            {
                targets.Add(enemy);
                continue;
            }

            if (cell.IsOccupied)
            {
                break;
            }
        }

        if (targets.Count == 0)
        {
            targets.Add(primaryTarget);
        }

        return targets;
    }

    private bool TryFindFirstTargetInDirection(Character character, Vector2Int direction, out Enemy target)
    {
        target = null;
        if (character == null || character.Board == null)
        {
            return false;
        }

        for (int step = 1; step <= GetRange(character); step++)
        {
            Vector2Int cellPosition = character.GridPosition + (direction * step);
            if (!character.Board.TryGetCell(cellPosition, out BoardCell cell) || cell.HasBlockingTerrain)
            {
                break;
            }

            if (character.Board.TryGetEnemy(cellPosition, out target) && target != null)
            {
                return true;
            }

            if (cell.IsOccupied)
            {
                break;
            }
        }

        return false;
    }

    private void TryKnockbackEnemy(Enemy enemy, Vector2Int direction)
    {
        if (enemy == null || direction == Vector2Int.zero || enemy.Board == null)
        {
            return;
        }

        Vector2Int destination = enemy.GridPosition + direction;
        if (enemy.Board.IsCellWalkable(destination))
        {
            enemy.TryForcedMoveTo(destination);
        }
    }

    private int GetRange(Character character)
    {
        return Mathf.Max(1, baseRange + (character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.CountsCarbineScopedSight) : 0));
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

    private bool IsBackupPistolAvailable(CharacterAbilityRuntime runtime)
    {
        return runtime != null
            && backupPistolAvailableByRuntime.TryGetValue(runtime, out bool isAvailable)
            && isAvailable;
    }

    private bool IsBackupPistolVisualMode(CharacterAbilityRuntime runtime)
    {
        return runtime != null
            && backupPistolVisualModeByRuntime.TryGetValue(runtime, out bool isVisualMode)
            && isVisualMode;
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
