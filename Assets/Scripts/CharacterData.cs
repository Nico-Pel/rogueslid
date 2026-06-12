using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "RogueSliders/Characters/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string characterName;
    [TextArea(3, 6)]
    [SerializeField] private string description;
    [SerializeField] private Sprite portrait;

    [Header("Base Stats")]
    [Min(1)]
    [SerializeField] private int maxHealth = 10;
    [Min(0)]
    [SerializeField] private int bonusDamage;
    [Min(0)]
    [SerializeField] private int resistance;
    [Min(1)]
    [SerializeField] private int movementPointsPerTurn = 2;

    [Header("Abilities")]
    [SerializeField] private List<AbilityDefinition> startingAbilities = new List<AbilityDefinition>();
    [SerializeField] private List<AbilityRewardDefinition> unlockableAbilityRewards = new List<AbilityRewardDefinition>();

    public string CharacterName => string.IsNullOrWhiteSpace(characterName) ? name : characterName;
    public string Description => description;
    public Sprite Portrait => portrait;
    public int MaxHealth => Mathf.Max(1, maxHealth);
    public int BonusDamage => Mathf.Max(0, bonusDamage);
    public int Resistance => Mathf.Max(0, resistance);
    public int MovementPointsPerTurn => Mathf.Max(1, movementPointsPerTurn);
    public IReadOnlyList<AbilityDefinition> StartingAbilities => startingAbilities;
    public IReadOnlyList<AbilityRewardDefinition> UnlockableAbilityRewards => unlockableAbilityRewards;

    public List<AbilityDefinition> GetAllPotentialAbilities()
    {
        List<AbilityDefinition> abilities = new List<AbilityDefinition>();
        AddUniqueAbilities(abilities, startingAbilities);

        for (int index = 0; index < unlockableAbilityRewards.Count; index++)
        {
            AbilityDefinition ability = unlockableAbilityRewards[index] != null
                ? unlockableAbilityRewards[index].Ability
                : null;
            if (ability != null && !abilities.Contains(ability))
            {
                abilities.Add(ability);
            }
        }

        return abilities;
    }

    private static void AddUniqueAbilities(List<AbilityDefinition> target, List<AbilityDefinition> source)
    {
        if (target == null || source == null)
        {
            return;
        }

        for (int index = 0; index < source.Count; index++)
        {
            AbilityDefinition ability = source[index];
            if (ability != null && !target.Contains(ability))
            {
                target.Add(ability);
            }
        }
    }
}
