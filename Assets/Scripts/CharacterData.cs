using System.Collections.Generic;
using UnityEngine;

public enum CharacterUIColorKey
{
    PowerRewardBackground = 0,
    PowerRewardTitleBackground = 1,
    PowerRewardTitleText = 2,
    PowerRewardDescriptionBackground = 3,
    PowerRewardDescriptionText = 4,
    PowerRewardTypeContainer = 5,
    PowerRewardTypeIcon = 6,
    PowerRewardOutline = 7,
    PowerRewardSubtitleBackground = 8,
    PowerRewardSubtitleText = 9,
    PowerRewardNewSubtitleBackground = 10,
    PowerRewardNewSubtitleText = 11,
    ItemRewardBackground = 12,
    ItemRewardTitleBackground = 13,
    ItemRewardTitleText = 14,
    ItemRewardDescriptionBackground = 15,
    ItemRewardDescriptionText = 16,
    ItemRewardTypeContainer = 17,
    ItemRewardTypeIcon = 18,
    ItemRewardOutline = 19,
    ItemRewardSubtitleBackground = 20,
    ItemRewardSubtitleText = 21,
    ItemIconBackground = 22,
    ItemIconActivation = 23,
    AbilityButtonOutline = 24,
    ToolsPrimaryButtonBackground = 25,
    ToolsPanelBackground = 26,
    ToolsSecondaryText = 27,
    ToolsDetailText = 28,
    ToolsNavigationButtonBackground = 29,
    ToolsCloseButtonBackground = 30,
    ToolsInputBackground = 31,
    ToolsInputPlaceholder = 32,
    FooterBackground = 33,
    PortraitBackground = 34,
    PortraitNameplateBackground = 35,
    MobilityBarBackground = 36,
    MobilityAvailable = 37,
    MobilityConsumed = 38,
    AbilityButtonBackground = 39,
    AbilityButtonCountBackground = 40,
    AbilityButtonTypeIcon = 41,
    AbilityButtonTypeOutline = 42
}

[System.Serializable]
public struct CharacterUIColorEntry
{
    public CharacterUIColorKey key;
    public Color color;
}

[CreateAssetMenu(fileName = "CharacterData", menuName = "RogueSliders/Characters/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string characterName;
    [TextArea(3, 6)]
    [SerializeField] private string description;
    [SerializeField] private Sprite portrait;
    [SerializeField] private Sprite portraitLose;
    [SerializeField] private Sprite pathIcon;

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

    [Header("UI Theme")]
    [SerializeField] private List<CharacterUIColorEntry> uiColors = new List<CharacterUIColorEntry>();

    public string CharacterName => string.IsNullOrWhiteSpace(characterName) ? name : characterName;
    public string Description => description;
    public Sprite Portrait => portrait;
    public Sprite PortraitLose => portraitLose != null ? portraitLose : portrait;
    public Sprite PathIcon => pathIcon;
    public int MaxHealth => Mathf.Max(1, maxHealth);
    public int BonusDamage => Mathf.Max(0, bonusDamage);
    public int Resistance => Mathf.Max(0, resistance);
    public int MovementPointsPerTurn => Mathf.Max(1, movementPointsPerTurn);
    public IReadOnlyList<AbilityDefinition> StartingAbilities => startingAbilities;
    public IReadOnlyList<AbilityRewardDefinition> UnlockableAbilityRewards => unlockableAbilityRewards;
    public IReadOnlyList<CharacterUIColorEntry> UIColors => uiColors;

    public bool TryGetUIColor(CharacterUIColorKey key, out Color color)
    {
        for (int index = 0; index < uiColors.Count; index++)
        {
            if (uiColors[index].key == key)
            {
                color = uiColors[index].color;
                return true;
            }
        }

        color = default;
        return false;
    }

    public Color GetUIColor(CharacterUIColorKey key, Color fallback)
    {
        return TryGetUIColor(key, out Color color) ? color : fallback;
    }

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
