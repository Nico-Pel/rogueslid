using UnityEngine;

public sealed class ZhuangEnemyEffects : MonoBehaviour
{
    private Enemy enemy;
    private Character lionOwner;
    private ZhuangLionsRoarAbility lionAbility;
    private int resistanceReduction;
    private bool marked;
    private GameObject markFx;
    private Character burnOwner;
    private ZhuangPhoenixJianAbility burnAbility;
    private CharacterAbilityRuntime burnRuntime;
    private GameObject burnFx;
    private int burnDamage;

    public int ResistanceReduction => resistanceReduction;
    public int BonusDamage(Character source, AbilityDefinition ability) => marked && source == lionOwner && ability != null ? 1 : 0;

    public static ZhuangEnemyEffects Get(Enemy target)
    {
        ZhuangEnemyEffects effects = target.GetComponent<ZhuangEnemyEffects>();
        return effects != null ? effects : target.gameObject.AddComponent<ZhuangEnemyEffects>();
    }

    private void Awake() => enemy = GetComponent<Enemy>();

    public void ApplyLion(Character owner, ZhuangLionsRoarAbility ability, int reduction, bool applyMark)
    {
        lionOwner = owner;
        lionAbility = ability;
        resistanceReduction = Mathf.Max(resistanceReduction, reduction);
        marked |= applyMark;
        if (marked && markFx == null) markFx = ZhuangAbilityUtils.Fx(ability.MarkFxPrefab, enemy.EffectAnchor.position, lifetime: 0, parent: enemy.EffectAnchor);
    }

    public void OnAbilityHit(Character source, AbilityDefinition ability)
    {
        if (!marked || source != lionOwner || ability == null || enemy.CurrentHealth <= 0) return;
        ZhuangAbilityUtils.Fx(lionAbility.MarkBurstFxPrefab, enemy.EffectAnchor.position);
        if (lionAbility.MarkBurstSound != null) SoundManager.Instance?.Play2DSound(lionAbility.MarkBurstSound);
        ZhuangAbilityUtils.Launch(source, enemy, sound: lionAbility.SecondarySoundParametersPrefab);
    }

    public void ClearLion()
    {
        resistanceReduction = 0;
        marked = false;
        lionOwner = null;
        lionAbility = null;
        if (markFx != null) Destroy(markFx);
        markFx = null;
    }

    public void ApplyBurn(Character owner, ZhuangPhoenixJianAbility ability, CharacterAbilityRuntime runtime, int damage)
    {
        if (enemy.IsImmuneToFire) return;
        burnOwner = owner;
        burnAbility = ability;
        burnRuntime = runtime;
        burnDamage = damage;
        if (burnFx == null) burnFx = ZhuangAbilityUtils.Fx(ability.PersistentFxPrefab, enemy.GetBodyTransform().position, lifetime: 0, parent: enemy.GetBodyTransform());
    }

    public void EndEnemyTurn()
    {
        if (burnOwner == null || burnAbility == null || enemy.CurrentHealth <= 0) { ClearBurn(); return; }
        ZhuangAbilityUtils.Fx(burnAbility.SecondaryFxPrefab, enemy.transform.position);
        ZhuangAbilityUtils.Sound(burnAbility.SecondarySoundParametersPrefab, enemy.transform.position);
        burnOwner.DealDamageToEnemy(enemy, burnDamage, false, true, DamageSoundType.MagicHit, burnAbility);
        if (enemy.CurrentHealth <= 0) burnAbility.AddBurnKill(burnRuntime);
        ClearBurn();
    }

    public void ClearBurn()
    {
        burnOwner = null;
        burnAbility = null;
        burnRuntime = null;
        if (burnFx != null) Destroy(burnFx);
        burnFx = null;
    }

    private void OnDestroy() { ClearLion(); ClearBurn(); }
}
