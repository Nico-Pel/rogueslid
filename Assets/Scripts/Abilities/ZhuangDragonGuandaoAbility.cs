using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Zhuang Dragon Guandao", fileName = "ZhuangDragonGuandao")]
public class ZhuangDragonGuandaoAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseMeleeDamage = 5;
    [Min(1)]
    [SerializeField] private int baseExtendedDamage = 7;
    [Min(1)]
    [SerializeField] private int whirlwindDamage = 2;
    [Min(0f)]
    [SerializeField] private float airborneDuration = 0.55f;
    [Min(0f)]
    [SerializeField] private float airbornePeakHeight = 0.55f;
    [Min(0f)]
    [SerializeField] private float whirlwindFollowupDelay = 0.5f;
    [Min(0f)]
    [SerializeField] private float dragonCloudExecuteDelay = 0.2f;
    [SerializeField] private AnimationClip whirlwindAnimation;
    [SerializeField] private AnimationClip dragonCloudExecuteAnimation;
    [SerializeField] private GameObject primarySlashFxPrefab;
    [SerializeField] private GameObject extendedSlashFxPrefab;
    [SerializeField] private GameObject whirlwindFxPrefab;
    [SerializeField] private GameObject dragonCloudExecuteFxPrefab;
    [SerializeField] private GameObject airborneFxPrefab;
    [Min(0f)]
    [SerializeField] private float airborneFxLifetime = 1f;
    [SerializeField] private GameObject slashSoundParametersPrefab;
    [SerializeField] private GameObject extendedHitSoundParametersPrefab;
    [SerializeField] private GameObject executeSoundParametersPrefab;
    [SerializeField] private SoundParameters airborneSound;

    public GameObject PrimarySlashFxPrefab => primarySlashFxPrefab;
    public GameObject ExtendedSlashFxPrefab => extendedSlashFxPrefab;
    public GameObject WhirlwindFxPrefab => whirlwindFxPrefab;
    public GameObject DragonCloudExecuteFxPrefab => dragonCloudExecuteFxPrefab;
    public GameObject SlashSoundParametersPrefab => slashSoundParametersPrefab;
    public GameObject ExtendedHitSoundParametersPrefab => extendedHitSoundParametersPrefab;
    public GameObject ExecuteSoundParametersPrefab => executeSoundParametersPrefab;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;
    public override void PlayActivationAnimation(Character character) { }

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        int damageBonus = character != null ? character.GetUpgradeStacks(AbilityUpgradeKey.ZhuangBlazingDragon) : 0;
        bool canHitDiagonally = character != null && character.GetUpgradeStacks(AbilityUpgradeKey.ZhuangLegendaryDragon) > 0;
        bool hasCloud = character != null && character.GetUpgradeStacks(AbilityUpgradeKey.ZhuangDragonCloud) > 0;
        bool hasWhirlwind = character != null && character.GetUpgradeStacks(AbilityUpgradeKey.ZhuangSwiftDragon) > 0;
        int meleeDamage = baseMeleeDamage + damageBonus;
        int extendedDamage = baseExtendedDamage + damageBonus;
        return $"Strike an adjacent enemy for {meleeDamage} damage, or a target 2 tiles away in a straight line for {extendedDamage} damage. Targets hit from 2 tiles away are knocked airborne on the spot. Diagonal reach: {(canHitDiagonally ? "Enabled" : "Locked")}. Dragon Cloud: {(hasCloud ? "Execute airborne targets left at 2 HP after the bonus strike" : "Locked")}. Swift Dragon: {(hasWhirlwind ? $"If Zhuang has no movement left, he first deals {whirlwindDamage} damage around himself" : "Locked")}.";
    }

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        foreach (Vector2Int candidateCell in EnumerateCandidateCells(character))
        {
            if (CanActivateOnCell(character, runtime, candidateCell))
            {
                return true;
            }
        }

        return false;
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        if (!TryResolveTarget(character, targetCell, out _, out _))
        {
            return false;
        }

        return character.Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null;
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return character != null && TryResolveTarget(character, targetCell, out _, out _);
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

        int validCount = 0;
        foreach (Vector2Int candidateCell in EnumerateCandidateCells(character))
        {
            if (!CanActivateOnCell(character, runtime, candidateCell))
            {
                continue;
            }

            validCount++;
            if (validCount > 1)
            {
                return false;
            }

            targetCell = candidateCell;
        }

        return validCount == 1;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue)
        {
            return false;
        }

        if (!TryResolveTarget(character, targetCell.Value, out _, out int distance)
            || !character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy)
            || enemy == null)
        {
            return false;
        }

        character.FaceTargetCell(targetCell.Value);
        character.StartCoroutine(ZhuangAbilityUtils.LockedAction(character, ResolveAttack(character, enemy, targetCell.Value, distance)));
        return true;
    }

    private IEnumerator ResolveAttack(Character character, Enemy enemy, Vector2Int targetCell, int distance)
    {
        if (character == null || enemy == null)
        {
            yield break;
        }

        bool hasWhirlwind = character.GetUpgradeStacks(AbilityUpgradeKey.ZhuangSwiftDragon) > 0
            && character.RemainingMovementPoints <= 0;
        if (hasWhirlwind)
        {
            if (whirlwindAnimation != null)
            {
                character.PlayAttackAnimation(whirlwindAnimation);
            }

            ZhuangAbilityUtils.Fx(whirlwindFxPrefab, character.transform.position);
            ZhuangAbilityUtils.Sound(slashSoundParametersPrefab, character.transform.position);
            DealWhirlwindDamage(character);
            if (whirlwindFollowupDelay > 0f)
            {
                yield return new WaitForSeconds(whirlwindFollowupDelay);
            }
        }

        if (enemy == null || enemy.CurrentHealth <= 0) yield break;
        base.PlayActivationAnimation(character);
        ZhuangAbilityUtils.Fx(distance >= 2 ? extendedSlashFxPrefab : primarySlashFxPrefab, character.transform.position, character.transform.rotation);
        ZhuangAbilityUtils.Sound(slashSoundParametersPrefab, character.transform.position);
        yield return new WaitForSeconds(Mathf.Max(0.01f, GetDamageApplyDelay(character.GridPosition, targetCell)));
        if (enemy == null || enemy.CurrentHealth <= 0) yield break;
        int damageBonus = character.GetUpgradeStacks(AbilityUpgradeKey.ZhuangBlazingDragon);
        int damage = (distance >= 2 ? baseExtendedDamage : baseMeleeDamage) + damageBonus;
        int appliedDamage = character.DealDamageToEnemy(enemy, damage, true, true, DamageSoundType.Sword, this);
        PlayConfiguredFx(character, new[] { enemy });

        if (distance >= 2 && appliedDamage > 0 && enemy != null && enemy.CurrentHealth > 0)
        {
            bool willExecute = enemy.CurrentHealth == 2 && character.GetUpgradeStacks(AbilityUpgradeKey.ZhuangDragonCloud) > 0;
            ZhuangAbilityUtils.Sound(extendedHitSoundParametersPrefab, enemy.transform.position);
            ZhuangAbilityUtils.Launch(character, enemy, airborneFxPrefab, airborneSound != null ? airborneSound.gameObject : null, !willExecute, Mathf.Max(airborneDuration, dragonCloudExecuteDelay + 0.15f), airbornePeakHeight, airborneFxLifetime);
        }

        if (distance >= 2
            && appliedDamage > 0
            && enemy != null
            && enemy.CurrentHealth == 2
            && enemy.IsAirborne
            && character.GetUpgradeStacks(AbilityUpgradeKey.ZhuangDragonCloud) > 0)
        {
            if (dragonCloudExecuteDelay > 0f)
            {
                yield return new WaitForSeconds(dragonCloudExecuteDelay);
            }

            if (character != null && enemy != null && enemy.CurrentHealth > 0 && enemy.IsAirborne)
            {
                if (dragonCloudExecuteAnimation != null)
                {
                    character.PlayAttackAnimation(dragonCloudExecuteAnimation);
                }

                ZhuangAbilityUtils.Fx(dragonCloudExecuteFxPrefab, enemy.EffectAnchor.position);
                ZhuangAbilityUtils.Sound(executeSoundParametersPrefab, enemy.transform.position);
                character.DealDamageToEnemy(enemy, enemy.CurrentHealth + enemy.Resistance, false, true, DamageSoundType.Sword, this);
            }
        }
    }

    private void DealWhirlwindDamage(Character character)
    {
        if (character == null || character.Board == null)
        {
            return;
        }

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                Vector2Int cell = character.GridPosition + new Vector2Int(x, y);
                if (character.Board.TryGetEnemy(cell, out Enemy enemy) && enemy != null)
                {
                    character.DealDamageToEnemy(enemy, whirlwindDamage, false, true, DamageSoundType.Sword, this);
                }
            }
        }
    }

    private bool TryResolveTarget(Character character, Vector2Int targetCell, out Vector2Int direction, out int distance)
    {
        direction = Vector2Int.zero;
        distance = 0;
        if (character == null || character.Board == null)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        bool allowDiagonals = character.GetUpgradeStacks(AbilityUpgradeKey.ZhuangLegendaryDragon) > 0;
        bool isOrthogonal = (delta.x == 0 && delta.y != 0) || (delta.y == 0 && delta.x != 0);
        bool isDiagonal = allowDiagonals && Mathf.Abs(delta.x) == Mathf.Abs(delta.y) && delta.x != 0;
        if (!isOrthogonal && !isDiagonal)
        {
            return false;
        }

        distance = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
        if (distance <= 0 || distance > 2)
        {
            return false;
        }

        direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        Vector2Int scan = character.GridPosition + direction;
        while (scan != targetCell)
        {
            if (!character.Board.TryGetCell(scan, out BoardCell cell) || cell.HasBlockingTerrain)
            {
                return false;
            }

            scan += direction;
        }

        return character.Board.IsInsideBoard(targetCell);
    }

    private System.Collections.Generic.IEnumerable<Vector2Int> EnumerateCandidateCells(Character character)
    {
        foreach (Vector2Int direction in ZhuangAbilityUtils.OrthogonalDirections)
        {
            yield return character.GridPosition + direction;
            yield return character.GridPosition + (direction * 2);
        }

        if (character.GetUpgradeStacks(AbilityUpgradeKey.ZhuangLegendaryDragon) > 0)
        {
            foreach (Vector2Int direction in ZhuangAbilityUtils.DiagonalDirections)
            {
                yield return character.GridPosition + direction;
                yield return character.GridPosition + (direction * 2);
            }
        }
    }
}
