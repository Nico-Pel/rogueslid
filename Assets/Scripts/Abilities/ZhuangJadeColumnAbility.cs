using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Zhuang Jade Column")]
public sealed class ZhuangJadeColumnAbility : ZhuangAbility
{
    [SerializeField] private GameObject columnPrefab;
    [SerializeField, Min(1)] private int minimumRange = 2;
    [SerializeField, Min(1)] private int maximumRange = 2;
    [SerializeField, Min(0)] private float projectionCollapseDelay = 0.25f;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int target)
    {
        if (character == null || character.Board == null || columnPrefab == null) return false;
        int eye = Stacks(character, AbilityUpgradeKey.ZhuangJadeEye);
        int distance = ZhuangAbilityUtils.GetChebyshevDistance(character.GridPosition, target);
        if (distance < (eye > 0 ? 1 : minimumRange) || distance > maximumRange + eye || !character.Board.TryGetCell(target, out BoardCell cell) || cell.HasBlockingTerrain) return false;
        if (character.Board.IsCellWalkable(target)) return true;
        return Stacks(character, AbilityUpgradeKey.ZhuangImperialProjection) > 0 && character.Board.TryGetEnemy(target, out _);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (!targetCell.HasValue || !CanActivateOnCell(character, runtime, targetCell.Value)) return false;
        character.FaceTargetCell(targetCell.Value);
        Cast(character);
        Run(character, Summon(character, targetCell.Value));
        return true;
    }

    private IEnumerator Summon(Character character, Vector2Int cell)
    {
        BoardManager board = character.Board;
        Vector3 position = board.GridToWorldPosition(cell);
        ZhuangAbilityUtils.Fx(CastFxPrefab, position);
        GameObject instance = Instantiate(columnPrefab, position - Vector3.up * 2f, columnPrefab.transform.rotation, board.ObstaclesRoot != null ? board.ObstaclesRoot : board.transform);
        Rock rock = instance.GetComponent<Rock>();
        if (rock == null) rock = instance.AddComponent<Rock>();
        rock.Assign(board, cell);
        board.TryGetCell(cell, out BoardCell data);
        data.SetStaticObstacle(instance);
        ZhuangJadeColumn column = instance.AddComponent<ZhuangJadeColumn>();
        column.Initialize(character, this, rock, cell);
        for (float t = 0; t < motionDuration; t += Time.deltaTime)
        {
            if (instance == null) yield break;
            instance.transform.position = Vector3.Lerp(position - Vector3.up * 2f, position, Mathf.Clamp01(t / motionDuration));
            yield return null;
        }
        instance.transform.position = position;
        Secondary(character, position);
        if (board.TryGetEnemy(cell, out Enemy enemy))
        {
            Launch(character, enemy);
            // The landing must not keep Zhuang locked throughout the Momentum window.
            character.StartCoroutine(ResolveProjection(character, rock, enemy));
        }
    }

    private IEnumerator ResolveProjection(Character character, Rock rock, Enemy enemy)
    {
        yield return new WaitForSeconds(projectionCollapseDelay);
        if (rock != null) rock.TakeHit();
        yield return new WaitForSeconds(Mathf.Max(0.01f, 0.55f - projectionCollapseDelay));
        if (character != null && character.CurrentHealth > 0) Hit(character, enemy, 1, false);
    }

    public void Collapse(Character character, Vector2Int cell)
    {
        Vector3 position = character.Board.GridToWorldPosition(cell);
        Secondary(character, position);
        if (Stacks(character, AbilityUpgradeKey.ZhuangJadeBurst) == 0) return;
        ZhuangAbilityUtils.Fx(ImpactFxPrefab, position);
        ZhuangAbilityUtils.Sound(ImpactSoundParametersPrefab, position);
        foreach (Enemy enemy in Nearby(character, cell, 1)) Hit(character, enemy, 2, false);
        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        {
            Vector2Int adjacent = cell + new Vector2Int(x, y);
            if (adjacent == cell) continue;
            if (character.Board.TryGetRock(adjacent, out Rock rock)) rock.TakeHit();
            if (character.Board.TryGetBarrel(adjacent, out BarrelObstacle barrel)) character.DealDamageToBarrelWithAbilityTiming(this, barrel);
            if (character.Board.TryGetLichSkullObject(adjacent, out LichSkullObject skull)) character.DealDamageToLichSkull(skull, 2, false, DamageSoundType.MagicHit, this);
        }
    }
}
