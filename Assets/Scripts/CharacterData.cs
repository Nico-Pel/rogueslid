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
    [SerializeField] private string characterId;
    [SerializeField] private string characterName;
    [TextArea(3, 6)]
    [SerializeField] private string description;
    [SerializeField] private Sprite portrait;
    [SerializeField] private Sprite portraitLose;
    [SerializeField] private Sprite portraitWin;
    [SerializeField] private Sprite pathIcon;
    [SerializeField] private Sprite orbIcon;

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
    [SerializeField] private List<TourmentRewardUnlockDefinition> tourmentRewardUnlocks = new List<TourmentRewardUnlockDefinition>();

    [Header("Persistent Upgrades")]
    [SerializeField] private List<CharacterUpgradeData> persistentUpgrades = new List<CharacterUpgradeData>();

    [Header("UI Theme")]
    [SerializeField] private List<CharacterUIColorEntry> uiColors = new List<CharacterUIColorEntry>();

    public string CharacterId => string.IsNullOrWhiteSpace(characterId) ? name : characterId.Trim();
    public string CharacterName => string.IsNullOrWhiteSpace(characterName) ? name : characterName;
    public string Description => description;
    public Sprite Portrait => portrait;
    public Sprite PortraitLose => portraitLose != null ? portraitLose : portrait;
    public Sprite PortraitWin => portraitWin != null ? portraitWin : portrait;
    public Sprite PathIcon => pathIcon;
    public Sprite OrbIcon => orbIcon;
    public int MaxHealth => Mathf.Max(1, maxHealth);
    public int BonusDamage => Mathf.Max(0, bonusDamage);
    public int Resistance => Mathf.Max(0, resistance);
    public int MovementPointsPerTurn => Mathf.Max(1, movementPointsPerTurn);
    public IReadOnlyList<AbilityDefinition> StartingAbilities => startingAbilities;
    public IReadOnlyList<AbilityRewardDefinition> UnlockableAbilityRewards => unlockableAbilityRewards;
    public IReadOnlyList<TourmentRewardUnlockDefinition> TourmentRewardUnlocks => tourmentRewardUnlocks;
    public IReadOnlyList<CharacterUpgradeData> PersistentUpgrades => persistentUpgrades;
    public IReadOnlyList<CharacterUIColorEntry> UIColors => uiColors;

    public List<AbilityDefinition> GetStartingAbilitiesWithPersistentUpgrades()
    {
        List<AbilityDefinition> abilities = new List<AbilityDefinition>(startingAbilities);
        if (abilities.Count == 0 || persistentUpgrades == null || persistentUpgrades.Count == 0)
        {
            return abilities;
        }

        string currentCharacterId = CharacterId;
        for (int index = 0; index < persistentUpgrades.Count; index++)
        {
            CharacterUpgradeData upgrade = persistentUpgrades[index];
            if (upgrade == null
                || !upgrade.HasStartingAbilityReplacement
                || CharacterProgressionSaveManager.GetUpgradeUnlockCount(currentCharacterId, upgrade.UpgradeId) <= 0)
            {
                continue;
            }

            for (int abilityIndex = 0; abilityIndex < abilities.Count; abilityIndex++)
            {
                if (abilities[abilityIndex] == upgrade.ReplaceStartingAbility)
                {
                    abilities[abilityIndex] = upgrade.ReplacementStartingAbility;
                }
            }
        }

        return abilities;
    }

    public int GetPersistentPreviewMaxHealth()
    {
        return MaxHealth + GetPersistentUpgradeTotalValue(upgrade => upgrade.BonusMaxHealthPerUnlock);
    }

    public int GetPersistentPreviewBonusDamage()
    {
        return BonusDamage + GetPersistentUpgradeTotalValue(upgrade => upgrade.BonusDamagePerUnlock);
    }

    public int GetPersistentPreviewResistance()
    {
        return Resistance + GetPersistentUpgradeTotalValue(upgrade => upgrade.BonusResistancePerUnlock);
    }

    public int GetPersistentPreviewMovementPoints()
    {
        return MovementPointsPerTurn + GetPersistentUpgradeTotalValue(upgrade => upgrade.BonusMovementPointsPerUnlock);
    }

    public int GetPersistentPreviewRegen()
    {
        return GetPersistentUpgradeTotalValue(upgrade => upgrade.BonusRegenPerUnlock);
    }

    public int GetPersistentPreviewBasicAttackDamage()
    {
        AbilityDefinition basicAttack = null;
        List<AbilityDefinition> resolvedStartingAbilities = GetStartingAbilitiesWithPersistentUpgrades();
        for (int index = 0; index < resolvedStartingAbilities.Count; index++)
        {
            AbilityDefinition candidate = resolvedStartingAbilities[index];
            if (candidate != null && candidate.Category == AbilityCategory.BasicAttack)
            {
                basicAttack = candidate;
                break;
            }
        }

        int baseDamage = basicAttack switch
        {
            SpinningBladesAbility => 5,
            DemonicChainAbility => 4,
            ThiefsDaggerAbility => 5,
            HectorCrossbowAbility => 4,
            DemonbaneAbility => 4,
            SacredCrossbowAbility => 5,
            WhisperfangAbility => 1,
            _ => 0
        };

        int categoryBonus = 0;
        if (basicAttack != null)
        {
            categoryBonus = basicAttack.Category switch
            {
                AbilityCategory.BasicAttack => GetPersistentUpgradeTotalValue(upgrade => upgrade.BonusWeaponAbilityDamagePerUnlock),
                AbilityCategory.MobilitySkill => GetPersistentUpgradeTotalValue(upgrade => upgrade.BonusMobilityAbilityDamagePerUnlock),
                AbilityCategory.SpecialPower => GetPersistentUpgradeTotalValue(upgrade => upgrade.BonusSpecialAbilityDamagePerUnlock),
                _ => 0
            };
        }

        return Mathf.Max(0, baseDamage + GetPersistentPreviewBonusDamage() + categoryBonus);
    }

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

        for (int index = 0; index < persistentUpgrades.Count; index++)
        {
            CharacterUpgradeData upgrade = persistentUpgrades[index];
            if (upgrade != null && upgrade.ReplacementStartingAbility != null && !abilities.Contains(upgrade.ReplacementStartingAbility))
            {
                abilities.Add(upgrade.ReplacementStartingAbility);
            }
        }

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

    private int GetPersistentUpgradeTotalValue(System.Func<CharacterUpgradeData, int> selector)
    {
        if (selector == null || persistentUpgrades == null || persistentUpgrades.Count == 0)
        {
            return 0;
        }

        int total = 0;
        string currentCharacterId = CharacterId;
        for (int index = 0; index < persistentUpgrades.Count; index++)
        {
            CharacterUpgradeData upgrade = persistentUpgrades[index];
            if (upgrade == null)
            {
                continue;
            }

            int perUnlockValue = selector(upgrade);
            if (perUnlockValue == 0)
            {
                continue;
            }

            int unlockCount = CharacterProgressionSaveManager.GetUpgradeUnlockCount(currentCharacterId, upgrade.UpgradeId);
            if (unlockCount <= 0)
            {
                continue;
            }

            total += perUnlockValue * unlockCount;
        }

        return total;
    }
}
