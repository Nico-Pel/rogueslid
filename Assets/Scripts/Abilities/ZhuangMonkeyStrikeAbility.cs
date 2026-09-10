using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Zhuang Monkey Strike")]
public sealed class ZhuangMonkeyStrikeAbility : ZhuangAbility
{
    [SerializeField, Min(1)] private int goldenDamage = 2;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int cell)
    {
        if (character == null || character.Board == null || cell == character.GridPosition || !character.Board.IsCellWalkable(cell)) return false;
        bool diagonals = Stacks(character, AbilityUpgradeKey.ZhuangMonkeyAgility) > 0;
        foreach (Enemy enemy in character.Board.SpawnedEnemies)
            if (enemy != null && enemy.CurrentHealth > 0 && IsAdjacent(enemy.GridPosition, cell, diagonals)) return true;
        return Stacks(character, AbilityUpgradeKey.ZhuangPranksterMonkey) > 0 && IsAdjacent(character.GridPosition, cell, diagonals);
    }

    private bool IsAdjacent(Vector2Int a, Vector2Int b, bool diagonals) => (diagonals ? ZhuangAbilityUtils.GetChebyshevDistance(a, b) : ZhuangAbilityUtils.GetManhattanDistance(a, b)) == 1;
    public override void PlayActivationAnimation(Character character) { }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (!targetCell.HasValue || !CanActivateOnCell(character, runtime, targetCell.Value)) return false;
        character.FaceTargetCell(targetCell.Value);
        Run(character, Leap(character, runtime, targetCell.Value));
        return true;
    }

    private IEnumerator Leap(Character character, CharacterAbilityRuntime runtime, Vector2Int cell)
    {
        Cast(character);
        int silver = Stacks(character, AbilityUpgradeKey.ZhuangSilverMonkeyStrike);
        if (silver > 0)
        {
            if (secondaryAnimation != null) character.PlayAttackAnimation(secondaryAnimation);
            Secondary(character, character.transform.position);
            foreach (Enemy enemy in Nearby(character, character.GridPosition, 1)) Hit(character, enemy, silver);
            yield return new WaitForSeconds(followupDelay);
        }
        base.PlayActivationAnimation(character);
        yield return Move(character, cell, true);
        Secondary(character, character.transform.position);
        if (Stacks(character, AbilityUpgradeKey.ZhuangGoldenMonkeyStrike) == 0) yield break;
        var targets = Nearby(character, character.GridPosition, 1);
        bool lethal = targets.Exists(enemy => character.PreviewDamageToEnemy(enemy, goldenDamage, true, this) >= enemy.CurrentHealth);
        if (!lethal) yield break;
        yield return new WaitForSeconds(followupDelay);
        if (landingAnimation != null) character.PlayAttackAnimation(landingAnimation);
        ZhuangAbilityUtils.Fx(MarkBurstFxPrefab, character.transform.position);
        ZhuangAbilityUtils.Sound(SecondarySoundParametersPrefab, character.transform.position);
        foreach (Enemy enemy in targets) Hit(character, enemy, goldenDamage);
        runtime.SetRemainingCooldown(0);
        runtime.GrantBonusTurnUse();
    }
}
