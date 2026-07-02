using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Bonus Rogue Bow", fileName = "BonusRogueBow")]
public class BonusRogueBowAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 1;
    [Min(1)]
    [SerializeField] private int maxDamage = 5;
    [SerializeField] private GameObject projectilePrefab;
    [Min(0.01f)]
    [SerializeField] private float projectileSpeed = 18f;
    [Min(0f)]
    [SerializeField] private float projectileDelay = 0.08f;
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 projectileImpactOffset = new Vector3(0f, 0.2f, 0f);

    public override bool IsBonusAbility => true;
    public override bool IsRangedAttack => true;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        bool aligned = (delta.x == 0 && delta.y != 0) || (delta.y == 0 && delta.x != 0);
        if (!aligned)
        {
            return false;
        }

        Vector2Int direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        Vector2Int scan = character.GridPosition + direction;
        while (scan != targetCell)
        {
            if (!character.Board.TryGetCell(scan, out BoardCell cell) || cell.HasBlockingTerrain || cell.IsOccupied)
            {
                return false;
            }

            scan += direction;
        }

        return character.Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null;
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        return CanActivateOnCell(character, runtime, targetCell);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || !targetCell.HasValue || !character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy) || enemy == null)
        {
            return false;
        }

        character.FaceTargetCell(targetCell.Value);
        int damage = GetDamage(character);
        if (projectilePrefab != null)
        {
            HectorAbilityUtils.TryPlayLinearProjectileFromWorldPosition(
                character,
                projectilePrefab,
                character.transform.position + projectileSpawnOffset,
                enemy.transform.position + projectileImpactOffset,
                projectileSpeed,
                projectileDelay,
                () => ResolveHit(character, enemy, damage),
                true);
        }
        else
        {
            ResolveHit(character, enemy, damage);
        }

        return true;
    }

    public override string GetCounterText(CharacterAbilityRuntime runtime)
    {
        return string.Empty;
    }

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        return $"Shoot an enemy in a straight line for {GetDamage(character)} damage. Gains +1 persistent damage on kill during this run, up to {maxDamage}.";
    }

    private void ResolveHit(Character character, Enemy enemy, int damage)
    {
        if (character == null || enemy == null || enemy.CurrentHealth <= 0)
        {
            return;
        }

        int appliedDamage = character.DealDamageToEnemy(enemy, damage, true, true, DamageSoundType.ArrowHit, this);
        if (appliedDamage > 0 && enemy.CurrentHealth <= 0)
        {
            character.SetBonusAbilityPersistentValue(GetPersistentKey(), Mathf.Min(maxDamage - baseDamage, GetStoredBonus(character) + 1));
        }
    }

    private int GetDamage(Character character)
    {
        return Mathf.Clamp(baseDamage + GetStoredBonus(character), baseDamage, maxDamage);
    }

    private int GetStoredBonus(Character character)
    {
        return character != null ? character.GetBonusAbilityPersistentValue(GetPersistentKey()) : 0;
    }

    private string GetPersistentKey()
    {
        return $"{name}_damage_bonus";
    }
}
