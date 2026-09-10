using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Zhuang Lions Roar")]
public sealed class ZhuangLionsRoarAbility : ZhuangAbility
{
    [SerializeField, Min(0)] private int minimumDistance = 3;
    [SerializeField, Min(0)] private int fearDistance = 5;
    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.Immediate;
    private bool IsTarget(Character character, Enemy enemy) => enemy != null && enemy.CurrentHealth > 0 && ZhuangAbilityUtils.GetChebyshevDistance(character.GridPosition, enemy.GridPosition) > minimumDistance - Stacks(character, AbilityUpgradeKey.ZhuangVelvetStep);

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || character.Board == null) return false;
        foreach (Enemy enemy in character.Board.SpawnedEnemies) if (IsTarget(character, enemy)) return true;
        return false;
    }

    public override Color? GetAttentionColor(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || character.Board == null || Stacks(character, AbilityUpgradeKey.ZhuangBeastlyFear) == 0 || !runtime.IsUsable(character)) return null;
        foreach (Enemy enemy in character.Board.SpawnedEnemies)
            if (IsTarget(character, enemy) && ZhuangAbilityUtils.GetChebyshevDistance(character.GridPosition, enemy.GridPosition) > fearDistance) return Color.yellow;
        return null;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (!CanActivate(character, runtime)) return false;
        Cast(character);
        foreach (Enemy enemy in new System.Collections.Generic.List<Enemy>(character.Board.SpawnedEnemies))
        {
            if (!IsTarget(character, enemy)) continue;
            int reduction = 1;
            if (Stacks(character, AbilityUpgradeKey.ZhuangBeastlyFear) > 0 && ZhuangAbilityUtils.GetChebyshevDistance(character.GridPosition, enemy.GridPosition) > fearDistance) reduction++;
            ZhuangEnemyEffects effects = ZhuangEnemyEffects.Get(enemy);
            effects.ApplyLion(character, this, reduction, false);
            int damage = Stacks(character, AbilityUpgradeKey.ZhuangResoundingRoar);
            if (damage > 0) Hit(character, enemy, damage, false);
            if (enemy.CurrentHealth > 0) effects.ApplyLion(character, this, reduction, Stacks(character, AbilityUpgradeKey.ZhuangLionStartle) > 0);
            Secondary(character, enemy.transform.position);
        }
        return true;
    }

    public override void OnTurnEnded(Character character, CharacterAbilityRuntime runtime)
    {
        foreach (Enemy enemy in character.Board.SpawnedEnemies) enemy?.GetComponent<ZhuangEnemyEffects>()?.ClearLion();
    }
}
