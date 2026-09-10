using UnityEngine;

public sealed class ZhuangTurtleGuard : MonoBehaviour
{
    private Character character;
    private ZhuangBlackTurtleStepAbility ability;
    private GameObject aura;
    private bool active;
    public void Activate(Character owner, ZhuangBlackTurtleStepAbility source)
    {
        character = owner;
        ability = source;
        if (!active) character.AddResistanceUntilNextEnemyTurn(1);
        active = true;
        if (aura == null) aura = ZhuangAbilityUtils.Fx(ability.PersistentFxPrefab, character.transform.position, lifetime: 0, parent: character.transform);
    }

    public void OnHit(Enemy enemy)
    {
        if (!active) return;
        if (character.GetUpgradeStacks(AbilityUpgradeKey.ZhuangSacredCarapace) > 0) character.AddResistanceUntilNextEnemyTurn(1);
        int thorns = character.GetUpgradeStacks(AbilityUpgradeKey.ZhuangSpikedCarapace);
        if (thorns > 0 && enemy != null && enemy.CurrentHealth > 0)
        {
            character.DealDamageToEnemy(enemy, thorns, false, true, DamageSoundType.MagicHit, ability);
            ZhuangAbilityUtils.Fx(ability.ImpactFxPrefab, enemy.EffectAnchor.position);
            ZhuangAbilityUtils.Sound(ability.ImpactSoundParametersPrefab, enemy.EffectAnchor.position);
        }
    }

    public void Clear()
    {
        active = false;
        if (aura != null) Destroy(aura);
        aura = null;
    }
    private void OnDisable() => Clear();
}
