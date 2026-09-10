using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Zhuang Crane Punishment")]
public sealed class ZhuangCranePunishmentAbility : ZhuangAbility
{
    [SerializeField, Min(0.1f)] private float momentumDuration = 2.5f;
    [SerializeField, Range(0.05f, 1f)] private float momentumTimeScale = 0.2f;
    [SerializeField, Min(1)] private int radius = 2;
    [SerializeField, Min(1)] private int damage = 4;
    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.Immediate;
    public override string GetCounterText(CharacterAbilityRuntime runtime) => string.Empty;
    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
        => character != null && character.GetComponent<CombatSlowMotion>() != null && character.GetComponent<CombatSlowMotion>().IsActive && Nearby(character, character.GridPosition, radius).Count > 0;
    public override Color? GetAttentionColor(Character character, CharacterAbilityRuntime runtime) => CanActivate(character, runtime) ? Color.cyan : (Color?)null;

    public override void OnEnemyLaunched(Character character, CharacterAbilityRuntime runtime, Enemy enemy, bool allowMomentum)
    {
        GameTurnManager turns = Object.FindObjectOfType<GameTurnManager>();
        if (!allowMomentum || (turns != null && turns.CurrentTurn != TurnSide.Player) || ZhuangAbilityUtils.GetChebyshevDistance(character.GridPosition, enemy.GridPosition) > radius || Nearby(character, character.GridPosition, radius).Count == 0) return;
        CombatSlowMotion slow = character.GetComponent<CombatSlowMotion>();
        if (slow == null) slow = character.gameObject.AddComponent<CombatSlowMotion>();
        slow.Open(momentumDuration, momentumTimeScale);
        character.PlayFeedbackFx(PersistentFxPrefab, destroyAfterSeconds: momentumDuration * momentumTimeScale);
    }

    public override void OnTurnEnded(Character character, CharacterAbilityRuntime runtime) => character.GetComponent<CombatSlowMotion>()?.Close();

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (!CanActivate(character, runtime)) return false;
        character.GetComponent<CombatSlowMotion>().Close();
        int spent = character.RemainingMovementPoints;
        for (int i = 0; i < spent; i++) character.ConsumeMovementPoint();
        if (Stacks(character, AbilityUpgradeKey.ZhuangGraceOfTheCrane) > 0) character.Heal(spent);
        Cast(character);
        Run(character, Strike(character));
        return true;
    }

    private IEnumerator Strike(Character character)
    {
        ZhuangAbilityUtils.Preview(character.Board, character.GridPosition, radius, followupDelay);
        yield return new WaitForSeconds(followupDelay);
        Secondary(character, character.transform.position);
        foreach (Enemy enemy in Nearby(character, character.GridPosition, radius))
        {
            Hit(character, enemy, damage + Stacks(character, AbilityUpgradeKey.ZhuangWingedWhirlwind));
            if (enemy != null && enemy.CurrentHealth > 0 && Stacks(character, AbilityUpgradeKey.ZhuangBrokenWings) > 0) enemy.ApplyMobilityPenaltyNextTurn(1, 1);
        }
    }
}
