using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Spinning Blades", fileName = "SpinningBlades")]
public class SpinningBladesAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 5;

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || character.Board == null || runtime == null)
        {
            return false;
        }

        bool hitAtLeastOneEnemy = false;
        List<Enemy> hitEnemies = new List<Enemy>();
        int kills = 0;
        int range = 1 + character.GetUpgradeStacks(AbilityUpgradeKey.SpinningBladesLongBlades);
        int damage = baseDamage + character.GetUpgradeStacks(AbilityUpgradeKey.SpinningBladesSharpening);

        for (int offsetX = -range; offsetX <= range; offsetX++)
        {
            for (int offsetY = -range; offsetY <= range; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                Vector2Int cell = character.GridPosition + new Vector2Int(offsetX, offsetY);
                if (!character.Board.TryGetEnemy(cell, out Enemy enemy) || enemy == null)
                {
                    continue;
                }

                character.DealDamageToEnemy(enemy, damage, true, true);
                hitAtLeastOneEnemy = true;
                hitEnemies.Add(enemy);
                if (enemy.CurrentHealth <= 0)
                {
                    kills++;
                }
            }
        }

        if (hitAtLeastOneEnemy)
        {
            PlayConfiguredFx(character, hitEnemies);
        }

        if (kills > 0 && character.GetUpgradeStacks(AbilityUpgradeKey.SpinningBladesBloodthirst) > 0)
        {
            character.GrantAbilityBonusTurnUse(this, 1);
        }

        if (hitEnemies.Count > 1 && character.GetUpgradeStacks(AbilityUpgradeKey.SpinningBladesLightningAttack) > 0)
        {
            character.GainMovementPoints(1);
        }

        return hitAtLeastOneEnemy;
    }
}
