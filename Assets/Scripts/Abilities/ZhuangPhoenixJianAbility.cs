using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Zhuang Phoenix Jian")]
public sealed class ZhuangPhoenixJianAbility : ZhuangAbility
{
    public sealed class State { public int BurnKills; }
    [SerializeField, Min(1)] private int baseDamage = 4;
    [SerializeField, Min(1)] private int burnDamage = 2;
    public void AddBurnKill(CharacterAbilityRuntime runtime) => runtime.GetState<State>().BurnKills++;

    public override void OnTurnStarted(Character character, CharacterAbilityRuntime runtime)
    {
        int charges = runtime.GetState<State>().BurnKills;
        runtime.GetState<State>().BurnKills = 0;
        if (charges > 0) { runtime.SetRemainingCooldown(0); runtime.GrantBonusTurnUse(charges); }
        foreach (Enemy enemy in character.Board.SpawnedEnemies) enemy?.GetComponent<ZhuangEnemyEffects>()?.ClearBurn();
    }

    private List<Enemy> Targets(Character character, Vector2Int direction)
    {
        List<Enemy> targets = new List<Enemy>();
        Vector2Int side = new Vector2Int(-direction.y, direction.x);
        for (int i = -1; i <= 1; i++)
            if (character.Board.TryGetEnemy(character.GridPosition + direction + side * i, out Enemy enemy)) targets.Add(enemy);
        if (Stacks(character, AbilityUpgradeKey.ZhuangPhoenixWing) > 0)
            for (int i = -1; i <= 1; i += 2)
                if (character.Board.TryGetEnemy(character.GridPosition + side * i, out Enemy enemy)) targets.Add(enemy);
        return targets;
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int cell)
        => character != null && character.Board != null && ZhuangAbilityUtils.GetManhattanDistance(character.GridPosition, cell) == 1 && Targets(character, cell - character.GridPosition).Count > 0;

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (!targetCell.HasValue || !CanActivateOnCell(character, runtime, targetCell.Value)) return false;
        character.FaceTargetCell(targetCell.Value);
        Cast(character);
        Run(character, Strike(character, runtime, targetCell.Value));
        return true;
    }

    private IEnumerator Strike(Character character, CharacterAbilityRuntime runtime, Vector2Int cell)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, GetDamageApplyDelay(character.GridPosition, cell)));
        List<Enemy> targets = Targets(character, cell - character.GridPosition);
        int damage = baseDamage + targets.Count * Stacks(character, AbilityUpgradeKey.ZhuangBlazingStrike);
        foreach (Enemy enemy in targets)
        {
            Hit(character, enemy, damage);
            if (enemy == null || enemy.CurrentHealth <= 0) continue;
            if (Stacks(character, AbilityUpgradeKey.ZhuangTongueOfFire) > 0) ZhuangEnemyEffects.Get(enemy).ApplyBurn(character, this, runtime, burnDamage);
            if (targets.Count > 1 && Stacks(character, AbilityUpgradeKey.ZhuangPhoenixFlight) > 0)
            {
                enemy.ApplyMobilityPenaltyNextTurn(1);
                Launch(character, enemy);
            }
        }
    }
}
