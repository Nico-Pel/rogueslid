using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Hector Crossbow", fileName = "HectorCrossbow")]
public class HectorCrossbowAbility : AbilityDefinition
{
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

        if (!HectorAbilityUtils.TryResolveAlignedDirection(character.GridPosition, targetCell.Value, false, range, out Vector2Int direction, out _)
            || !HectorAbilityUtils.IsLineClear(character.Board, character.GridPosition, targetCell.Value, direction))
        {
            return false;
        }

        character.FaceTargetCell(targetCell.Value);

        if (character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) && enemy != null)
        {
            HectorAbilityUtils.TryPlayLinearProjectile(
                character,
                projectilePrefab,
                enemy.transform.position + projectileImpactOffset,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay,
                () => character.DealDamageToEnemy(enemy, baseDamage, true, true, DamageSoundType.Default, this));
            PlayConfiguredFx(character, new[] { enemy });
            return true;
        }

        if (character.Board.TryGetLichSkullObject(targetCell.Value, out LichSkullObject skull) && skull != null)
        {
            HectorAbilityUtils.TryPlayLinearProjectile(
                character,
                projectilePrefab,
                skull.transform.position + projectileImpactOffset,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay,
                () => character.DealDamageToLichSkull(skull, baseDamage, true, DamageSoundType.Default, this));
            PlayConfiguredFx(character);
            return true;
        }

        if (character.Board.TryGetBarrel(targetCell.Value, out BarrelObstacle barrel) && barrel != null)
        {
            HectorAbilityUtils.TryPlayLinearProjectile(
                character,
                projectilePrefab,
                character.Board.GridToWorldPosition(barrel.GridPosition) + projectileImpactOffset,
                projectileSpeed,
                projectileSpawnOffset,
                projectileLaunchDelay,
                barrel.TakeHit);
            PlayConfiguredFx(character);
            return true;
        }

        return false;
    }

    private bool CanTargetCell(Character character, Vector2Int targetCell, bool allowEmptyCell, bool allowDiagonals)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        if (!HectorAbilityUtils.TryResolveAlignedDirection(character.GridPosition, targetCell, allowDiagonals, range, out Vector2Int direction, out _)
            || !HectorAbilityUtils.IsLineClear(character.Board, character.GridPosition, targetCell, direction))
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
