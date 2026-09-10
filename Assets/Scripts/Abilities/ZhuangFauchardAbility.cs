using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Zhuang Fauchard", fileName = "ZhuangFauchard")]
public class ZhuangFauchardAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseDamage = 5;
    [SerializeField] private GameObject swingFxPrefab;
    [SerializeField] private GameObject enemyImpactFxPrefab;
    [SerializeField] private GameObject obstacleImpactFxPrefab;
    [SerializeField] private GameObject swingSoundParametersPrefab;
    [SerializeField] private GameObject obstacleImpactSoundParametersPrefab;

    public GameObject SwingFxPrefab => swingFxPrefab;
    public GameObject EnemyImpactFxPrefab => enemyImpactFxPrefab;
    public GameObject ObstacleImpactFxPrefab => obstacleImpactFxPrefab;
    public GameObject SwingSoundParametersPrefab => swingSoundParametersPrefab;
    public GameObject ObstacleImpactSoundParametersPrefab => obstacleImpactSoundParametersPrefab;

    public override string GetDisplayDescription(Character character, CharacterAbilityRuntime runtime)
    {
        return $"Strike an adjacent enemy or obstacle for {baseDamage} damage.";
    }

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        for (int index = 0; index < ZhuangAbilityUtils.OrthogonalDirections.Length; index++)
        {
            Vector2Int targetCell = character.GridPosition + ZhuangAbilityUtils.OrthogonalDirections[index];
            if (CanActivateOnCell(character, runtime, targetCell))
            {
                return true;
            }
        }

        return false;
    }

    public override bool ShouldAutoUseWhenOnlyOneValidTarget(Character character, CharacterAbilityRuntime runtime)
    {
        return true;
    }

    public override bool TryGetAutomaticTargetCell(Character character, CharacterAbilityRuntime runtime, out Vector2Int targetCell)
    {
        targetCell = default;
        if (character == null || character.Board == null)
        {
            return false;
        }

        // An adjacent obstacle should not prevent the attack from being
        // auto-targeted when there is only one living enemy in range.
        Vector2Int singleEnemyCell = default;
        int livingEnemyCount = 0;
        for (int index = 0; index < ZhuangAbilityUtils.OrthogonalDirections.Length; index++)
        {
            Vector2Int candidateCell = character.GridPosition + ZhuangAbilityUtils.OrthogonalDirections[index];
            if (!character.Board.TryGetEnemy(candidateCell, out Enemy enemy)
                || enemy == null
                || enemy.CurrentHealth <= 0)
            {
                continue;
            }

            livingEnemyCount++;
            singleEnemyCell = candidateCell;
            if (livingEnemyCount > 1)
            {
                return false;
            }
        }

        if (livingEnemyCount == 1)
        {
            targetCell = singleEnemyCell;
            return true;
        }

        int validTargetCount = 0;
        for (int index = 0; index < ZhuangAbilityUtils.OrthogonalDirections.Length; index++)
        {
            Vector2Int candidateCell = character.GridPosition + ZhuangAbilityUtils.OrthogonalDirections[index];
            if (!CanActivateOnCell(character, runtime, candidateCell))
            {
                continue;
            }

            validTargetCount++;
            if (validTargetCount > 1)
            {
                return false;
            }

            targetCell = candidateCell;
        }

        return validTargetCount == 1;
    }

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
        {
            return false;
        }

        return (character.Board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null)
            || (character.Board.TryGetBarrel(targetCell, out BarrelObstacle barrel) && barrel != null)
            || (character.Board.TryGetRock(targetCell, out Rock rock) && rock != null)
            || (character.Board.TryGetLichSkullObject(targetCell, out LichSkullObject skull) && skull != null);
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null)
        {
            return false;
        }

        Vector2Int delta = targetCell - character.GridPosition;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue || !CanActivateOnCell(character, runtime, targetCell.Value))
        {
            return false;
        }

        character.FaceTargetCell(targetCell.Value);
        ZhuangAbilityUtils.Fx(swingFxPrefab, character.transform.position, character.transform.rotation);
        ZhuangAbilityUtils.Sound(swingSoundParametersPrefab, character.transform.position);
        character.StartCoroutine(ZhuangAbilityUtils.LockedAction(character, Strike(character, targetCell.Value)));
        return true;
    }

    private IEnumerator Strike(Character character, Vector2Int cell)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, GetDamageApplyDelay(character.GridPosition, cell)));
        Vector3 position = character.Board.GridToWorldPosition(cell);

        if (character.Board.TryGetEnemy(cell, out Enemy enemy) && enemy != null)
        {
            character.DealDamageToEnemy(enemy, baseDamage, true, true, DamageSoundType.Sword, this);
            ZhuangAbilityUtils.Fx(enemyImpactFxPrefab, position);
            PlayConfiguredFx(character, new[] { enemy });
            yield break;
        }

        ZhuangAbilityUtils.Fx(obstacleImpactFxPrefab, position);
        ZhuangAbilityUtils.Sound(obstacleImpactSoundParametersPrefab, position);
        if (character.Board.TryGetBarrel(cell, out BarrelObstacle barrel) && barrel != null)
        {
            barrel.TakeHit();
            PlayConfiguredFx(character);
            yield break;
        }

        if (character.Board.TryGetRock(cell, out Rock rock) && rock != null)
        {
            rock.TakeHit();
            PlayConfiguredFx(character);
            yield break;
        }

        if (character.Board.TryGetLichSkullObject(cell, out LichSkullObject skull) && skull != null)
        {
            character.DealDamageToLichSkull(skull, baseDamage, true, DamageSoundType.Sword, this);
            PlayConfiguredFx(character);
            yield break;
        }
    }
}
