using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Zhuang Serpent Hook")]
public sealed class ZhuangSerpentHookAbility : ZhuangAbility
{
    [SerializeField, Min(1)] private int range = 10;
    public override int GetBonusUsesPerTurn(Character character, CharacterAbilityRuntime runtime) => Stacks(character, AbilityUpgradeKey.ZhuangTwinSerpents);
    public override void PlayActivationAnimation(Character character) { }

    private bool Resolve(Character character, Vector2Int target, out Vector2Int landing)
    {
        landing = default;
        if (character == null || character.Board == null || ZhuangAbilityUtils.GetManhattanDistance(character.GridPosition, target) > range || !ZhuangAbilityUtils.StraightDirection(character.GridPosition, target, out Vector2Int direction)) return false;
        if (!character.Board.TryGetCell(target, out BoardCell cell) || (!cell.HasBlockingTerrain && !character.Board.TryGetEnemy(target, out _) && !character.Board.TryGetBarrel(target, out _) && !character.Board.TryGetLichSkullObject(target, out _))) return false;
        if (!ZhuangAbilityUtils.ClearLine(character.Board, character.GridPosition, target)) return false;
        landing = target - direction;
        return landing == character.GridPosition || character.Board.IsCellWalkable(landing);
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int cell) => Resolve(character, cell, out _);
    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (!targetCell.HasValue || !Resolve(character, targetCell.Value, out Vector2Int landing)) return false;
        character.FaceTargetCell(targetCell.Value);
        Run(character, Hook(character, targetCell.Value, landing));
        return true;
    }

    private IEnumerator Hook(Character character, Vector2Int target, Vector2Int landing)
    {
        Cast(character);
        base.PlayActivationAnimation(character);
        Vector3 targetPosition = character.Board.GridToWorldPosition(target) + Vector3.up * 0.5f;
        yield return ZhuangAbilityUtils.Projectile(ProjectilePrefab, character.transform.position + Vector3.up * 0.5f, () => targetPosition, motionDuration);
        GameObject chain = ZhuangAbilityUtils.Fx(SecondaryFxPrefab, character.transform.position, lifetime: 0);
        if (chain != null)
        {
            LineRenderer line = chain.GetComponentInChildren<LineRenderer>();
            if (line != null) { line.useWorldSpace = true; line.positionCount = 2; line.SetPositions(new[] { character.transform.position + Vector3.up * 0.5f, targetPosition }); }
        }
        yield return Move(character, landing, false);
        if (chain != null) Destroy(chain);
        if (character.Board.TryGetEnemy(target, out Enemy enemy))
        {
            Hit(character, enemy, 1 + Stacks(character, AbilityUpgradeKey.ZhuangSerpentFangs));
            if (Stacks(character, AbilityUpgradeKey.ZhuangFlyingSerpent) > 0) Launch(character, enemy);
        }
        else
        {
            ZhuangAbilityUtils.Fx(ImpactFxPrefab, targetPosition);
            ZhuangAbilityUtils.Sound(ImpactSoundParametersPrefab, targetPosition);
        }
    }
}
