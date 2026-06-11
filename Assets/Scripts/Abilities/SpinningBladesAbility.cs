using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Spinning Blades", fileName = "SpinningBlades")]
public class SpinningBladesAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 5;

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        bool hitAtLeastOneEnemy = false;
        List<Enemy> hitEnemies = new List<Enemy>();

        for (int offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
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

                character.DealDamageToEnemy(enemy, baseDamage, true);
                hitAtLeastOneEnemy = true;
                hitEnemies.Add(enemy);
            }
        }

        if (hitAtLeastOneEnemy)
        {
            PlayConfiguredFx(character, hitEnemies);
        }

        return hitAtLeastOneEnemy;
    }
}
