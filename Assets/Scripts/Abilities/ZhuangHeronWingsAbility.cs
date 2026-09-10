using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Zhuang Heron Wings")]
public sealed class ZhuangHeronWingsAbility : ZhuangAbility
{
    [SerializeField, Min(1)] private int baseRange = 2;
    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int cell)
        => character != null && character.Board != null && cell != character.GridPosition && ZhuangAbilityUtils.GetChebyshevDistance(character.GridPosition, cell) <= baseRange + Stacks(character, AbilityUpgradeKey.ZhuangGreatWings) && character.Board.IsCellWalkable(cell);

    public override void OnEnemyLaunched(Character character, CharacterAbilityRuntime runtime, Enemy enemy, bool allowMomentum)
    {
        if (Stacks(character, AbilityUpgradeKey.ZhuangHeronGlide) == 0) return;
        runtime.GrantBonusTurnUse();
        runtime.SetRemainingCooldown(0);
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (!targetCell.HasValue || !CanActivateOnCell(character, runtime, targetCell.Value)) return false;
        character.FaceTargetCell(targetCell.Value);
        Cast(character);
        Run(character, Glide(character, targetCell.Value));
        return true;
    }

    private IEnumerator Glide(Character character, Vector2Int cell)
    {
        GameObject aura = ZhuangAbilityUtils.Fx(PersistentFxPrefab, character.transform.position, lifetime: motionDuration, parent: character.transform);
        yield return Move(character, cell, true);
        Secondary(character, character.transform.position);
        var targets = Nearby(character, character.GridPosition, 1);
        if (targets.Count >= 2 && Stacks(character, AbilityUpgradeKey.ZhuangDivineFeather) > 0) character.Heal(1);
        int damage = Stacks(character, AbilityUpgradeKey.ZhuangFeatherWhirlwind);
        if (damage > 0 && targets.Count > 0)
        {
            if (secondaryAnimation != null) character.PlayAttackAnimation(secondaryAnimation);
            foreach (Enemy enemy in targets) Hit(character, enemy, damage);
        }
        if (aura != null) Destroy(aura);
    }
}
