using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Spinning Blades", fileName = "SpinningBlades")]
public class SpinningBladesAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 5;
    [Header("Range FX")]
    [SerializeField] private GameObject defaultSpinFxPrefab;
    [SerializeField] private GameObject largeSpinFxPrefab;

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

                character.DealDamageToEnemy(enemy, damage, true, true, DamageSoundType.Sword, this);
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
            PlayRangeAwareFx(character, hitEnemies, range);
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

    private void PlayRangeAwareFx(Character character, IEnumerable<Enemy> hitEnemies, int range)
    {
        if (character == null)
        {
            return;
        }

        GameObject fxOverride = null;
        if (range > 1 && largeSpinFxPrefab != null)
        {
            fxOverride = largeSpinFxPrefab;
        }
        else if (defaultSpinFxPrefab != null)
        {
            fxOverride = defaultSpinFxPrefab;
        }

        if (FxSpawns == null || FxSpawns.Count == 0 || fxOverride == null)
        {
            PlayConfiguredFx(character, hitEnemies);
            return;
        }

        List<AbilityFxSpawnConfig> runtimeFxConfigs = new List<AbilityFxSpawnConfig>(FxSpawns.Count);
        for (int index = 0; index < FxSpawns.Count; index++)
        {
            AbilityFxSpawnConfig sourceConfig = FxSpawns[index];
            if (sourceConfig == null)
            {
                continue;
            }

            runtimeFxConfigs.Add(sourceConfig.CreateRuntimeCopy(fxOverride));
        }

        character.PlayAbilityFx(runtimeFxConfigs, hitEnemies);
    }
}
