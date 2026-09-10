using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Zhuang Tiger Fists")]
public sealed class ZhuangTigerFistsAbility : ZhuangAbility
{
    public sealed class State { public bool White; }
    [SerializeField, Min(1)] private int baseDamage = 5;
    [SerializeField, Min(0)] private int collisionDamage = 2;
    [SerializeField, Min(0)] private float collisionDelay = 0.35f;
    [SerializeField] private Sprite whiteTigerIcon;

    public override string GetDisplayName(Character character, CharacterAbilityRuntime runtime) => runtime.GetState<State>().White ? "White Tiger Fist" : AbilityName;
    public override Sprite GetIcon(CharacterAbilityRuntime runtime) => runtime.GetState<State>().White && whiteTigerIcon != null ? whiteTigerIcon : base.GetIcon(runtime);
    public override void OnTurnStarted(Character character, CharacterAbilityRuntime runtime) => runtime.GetState<State>().White = false;
    public override void OnTurnEnded(Character character, CharacterAbilityRuntime runtime) => runtime.GetState<State>().White = false;
    public override void PlayActivationAnimation(Character character) { }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int cell)
    {
        if (character == null || character.Board == null || runtime == null) return false;
        if (cell == character.GridPosition)
            return Stacks(character, AbilityUpgradeKey.ZhuangWhiteTiger) > 0 && !runtime.GetState<State>().White && character.CurrentHealth > 2;
        if (GetDistance(character, cell) != 1) return false;
        if (runtime.GetState<State>().White && character.CurrentHealth <= 2) return false;
        if (character.Board.TryGetEnemy(cell, out _)) return true;
        if (Stacks(character, AbilityUpgradeKey.ZhuangStoneTiger) > 0 && character.Board.TryGetRock(cell, out _)) return true;
        return Stacks(character, AbilityUpgradeKey.ZhuangTigerClaws) > 0 && character.Board.IsCellWalkable(cell) && ClawTargets(character, cell).Count > 0;
    }

    private int GetDistance(Character character, Vector2Int cell) => ZhuangAbilityUtils.GetManhattanDistance(character.GridPosition, cell);
    private List<Enemy> ClawTargets(Character character, Vector2Int cell)
    {
        List<Enemy> enemies = new List<Enemy>();
        ZhuangAbilityUtils.TryCollectAdjacentEnemies(character.Board, cell, enemies, false);
        return enemies;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (!targetCell.HasValue || !CanActivateOnCell(character, runtime, targetCell.Value)) return false;
        if (targetCell == character.GridPosition)
        {
            runtime.GetState<State>().White = true;
            runtime.GrantBonusTurnUse();
            if (secondaryAnimation != null) character.PlayAttackAnimation(secondaryAnimation);
            Cast(character);
            Secondary(character, character.transform.position);
            return true;
        }
        bool white = runtime.GetState<State>().White;
        character.FaceTargetCell(targetCell.Value);
        Run(character, Attack(character, targetCell.Value, white));
        return true;
    }

    private IEnumerator Attack(Character character, Vector2Int cell, bool white)
    {
        Cast(character);
        base.PlayActivationAnimation(character);
        Vector2Int direction = cell - character.GridPosition;
        Enemy target = null;
        if (character.Board.TryGetRock(cell, out Rock rock) && Stacks(character, AbilityUpgradeKey.ZhuangStoneTiger) > 0)
        {
            rock.TakeHit();
            Secondary(character, character.Board.GridToWorldPosition(cell));
            character.Board.TryGetEnemy(cell + direction, out target);
        }
        else if (!character.Board.TryGetEnemy(cell, out target))
        {
            target = ZhuangAbilityUtils.ChooseWeakestEnemy(ClawTargets(character, cell));
            if (target == null || !target.TryForcedMoveTo(cell)) yield break;
            yield return new WaitForSeconds(motionDuration);
        }
        if (white) character.TakeDamage(2 + character.Resistance);
        if (target == null || target.CurrentHealth <= 0) yield break;
        yield return new WaitForSeconds(Mathf.Max(0.01f, GetDamageApplyDelay(character.GridPosition, target.GridPosition)));
        Hit(character, target, baseDamage + (white ? 2 : 0));
        if (target.CurrentHealth <= 0) yield break;
        int pushDistance = white ? 2 : 1;
        bool collision = false;
        for (int i = 0; i < pushDistance; i++)
        {
            if (!target.TryForcedMoveTo(target.GridPosition + direction)) { collision = true; break; }
            yield return new WaitForSeconds(motionDuration);
        }
        if (!collision || target.CurrentHealth <= 0) yield break;
        yield return new WaitForSeconds(collisionDelay);
        if (target == null || target.CurrentHealth <= 0) yield break;
        Hit(character, target, collisionDamage + 2 * Stacks(character, AbilityUpgradeKey.ZhuangFierceFist), false);
        Secondary(character, target.transform.position);
        Launch(character, target);
    }
}
