using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Rogue Sliders/Abilities/Arcane Trap", fileName = "ArcaneTrap")]
public class ArcaneTrapAbility : AbilityDefinition
{
    [Min(1)]
    [SerializeField] private int baseRange = 4;
    [Min(1)]
    [SerializeField] private int baseDamage = 4;
    [SerializeField] private GameObject warningFxPrefab;
    [FormerlySerializedAs("activeFxPrefab")]
    [SerializeField] private GameObject trapObjPrefab;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    public override bool CanActivateOnCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
    {
        if (character == null || character.Board == null)
        {
            return false;
        }

        int distance = Mathf.Abs(targetCell.x - character.GridPosition.x) + Mathf.Abs(targetCell.y - character.GridPosition.y);
        if (distance <= 0 || distance > baseRange)
        {
            return false;
        }

        if (!character.Board.TryGetCell(targetCell, out BoardCell cell) || !cell.Walkable || cell.IsOccupied || cell.Hazard != null)
        {
            return false;
        }

        return true;
    }

    public override bool TryActivate(Character character, CharacterAbilityRuntime runtime, Vector2Int? targetCell)
    {
        if (character == null || runtime == null || !targetCell.HasValue || character.Board == null)
        {
            return false;
        }

        GameObject trapObject = new GameObject("ArcaneTrapHazard");
        ArcaneTrapHazard hazard = trapObject.AddComponent<ArcaneTrapHazard>();
        hazard.Configure(
            character.Board,
            character,
            targetCell.Value,
            this,
            warningFxPrefab,
            trapObjPrefab,
            baseDamage,
            character.GetUpgradeStacks(AbilityUpgradeKey.ArcaneTrapArcaneSustain),
            character.GetUpgradeStacks(AbilityUpgradeKey.ArcaneTrapArcaneEruption) > 0 ? 2 : 0,
            character.GetUpgradeStacks(AbilityUpgradeKey.ArcaneTrapArcaneWave) > 0,
            character.GetUpgradeStacks(AbilityUpgradeKey.ArcaneTrapArcaneExhaustion) > 0);
        PlayConfiguredFx(character);
        return true;
    }
}
