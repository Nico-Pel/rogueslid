using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Zhuang Celestial Rings")]
public sealed class ZhuangCelestialRingsAbility : ZhuangAbility
{
    public sealed class State { public int Charges; public bool Divine; public bool Initialized; public float PromotedAt = -10f; }
    [SerializeField] private Sprite divineIcon;
    [SerializeField, Min(1)] private int damage = 2;
    public override bool IsRangedAttack => true;
    public override string GetCounterText(CharacterAbilityRuntime runtime) => runtime.GetState<State>().Charges.ToString();
    public override string GetDisplayName(Character character, CharacterAbilityRuntime runtime) => runtime.GetState<State>().Divine ? "Divine Rings" : AbilityName;
    public override Sprite GetIcon(CharacterAbilityRuntime runtime) => runtime.GetState<State>().Divine && divineIcon != null ? divineIcon : base.GetIcon(runtime);
    public override Color? GetAttentionColor(Character character, CharacterAbilityRuntime runtime) => Time.unscaledTime - runtime.GetState<State>().PromotedAt < 2f ? Color.yellow : (Color?)null;

    private State Get(Character character, CharacterAbilityRuntime runtime)
    {
        State state = runtime.GetState<State>();
        if (!state.Initialized)
        {
            state.Initialized = true;
            state.Charges = Stacks(character, AbilityUpgradeKey.ZhuangFetishRing) > 0 ? 1 : 0;
        }
        if (!state.Divine && state.Charges >= 3 && Stacks(character, AbilityUpgradeKey.ZhuangDivineRings) > 0)
        {
            state.Divine = true;
            state.PromotedAt = Time.unscaledTime;
            Secondary(character, character.transform.position);
        }
        return state;
    }

    public override void OnTurnStarted(Character character, CharacterAbilityRuntime runtime) => Get(character, runtime);
    public override void OnEnemyLaunched(Character character, CharacterAbilityRuntime runtime, Enemy enemy, bool allowMomentum)
    {
        Get(character, runtime).Charges++;
        Get(character, runtime);
    }
    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int cell)
        => character != null && character.Board != null && runtime != null && Get(character, runtime).Charges > 0 && character.Board.TryGetEnemy(cell, out Enemy enemy) && enemy.CurrentHealth > 0 && ZhuangAbilityUtils.ClearLine(character.Board, character.GridPosition, cell);

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (!targetCell.HasValue || !CanActivateOnCell(character, runtime, targetCell.Value)) return false;
        State state = Get(character, runtime);
        state.Charges--;
        bool divine = state.Divine;
        character.Board.TryGetEnemy(targetCell.Value, out Enemy enemy);
        character.FaceTargetCell(targetCell.Value);
        Cast(character);
        Run(character, Throw(character, runtime, enemy, targetCell.Value, divine));
        return true;
    }

    private IEnumerator Throw(Character character, CharacterAbilityRuntime runtime, Enemy target, Vector2Int targetCell, bool divine)
    {
        Vector3 impact = character.Board.GridToWorldPosition(targetCell) + Vector3.up * 0.5f;
        yield return ZhuangAbilityUtils.Projectile(ProjectilePrefab, character.transform.position + Vector3.up * 0.5f, () => target != null ? target.EffectAnchor.position : impact, motionDuration);
        bool aliveBefore = target != null && target.CurrentHealth > 0;
        Hit(character, target, damage + (divine ? 1 : 0));
        bool killed = aliveBefore && (target == null || target.CurrentHealth <= 0);
        int kills = killed ? 1 : 0;
        if (divine)
        {
            Secondary(character, impact);
            foreach (Enemy other in Nearby(character, targetCell, 1))
            {
                if (other == target) continue;
                Hit(character, other, damage + 1);
                if (other == null || other.CurrentHealth <= 0) kills++;
            }
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int cell = targetCell + new Vector2Int(x, y);
                if (character.Board.TryGetRock(cell, out Rock rock)) rock.TakeHit();
                if (character.Board.TryGetBarrel(cell, out BarrelObstacle barrel)) character.DealDamageToBarrelWithAbilityTiming(this, barrel);
                if (character.Board.TryGetLichSkullObject(cell, out LichSkullObject skull)) character.DealDamageToLichSkull(skull, damage + 1, true, DamageSoundType.MagicHit, this);
            }
        }
        if (kills > 0) character.Heal(kills * Stacks(character, AbilityUpgradeKey.ZhuangGiftOfTheSky));
        if (killed && Stacks(character, AbilityUpgradeKey.ZhuangCelestialRebound) > 0)
        {
            yield return ZhuangAbilityUtils.Projectile(ProjectilePrefab, impact, () => character.transform.position + Vector3.up * 0.5f, motionDuration);
            runtime.GetState<State>().Charges++;
            Secondary(character, character.transform.position);
        }
        if (runtime.GetState<State>().Charges <= 0) runtime.GetState<State>().Divine = false;
        Get(character, runtime);
    }
}
