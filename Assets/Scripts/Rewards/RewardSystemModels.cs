using System.Collections.Generic;
using UnityEngine;

public enum RewardOfferKind
{
    AbilityUnlock,
    AbilityUpgrade,
    Item
}

public enum RewardPresentationIconKind
{
    BasicAttack,
    MobilitySkill,
    SpecialPower,
    Item
}

public enum AbilityUpgradeKey
{
    SpinningBladesBloodthirst,
    SpinningBladesSharpening,
    SpinningBladesLongBlades,
    SpinningBladesLightningAttack,
    GhostStepsFrenzy,
    GhostStepsHeartPiercer,
    GhostStepsAdrenaline,
    NighttimeMenaceTheatricalAppearance,
    NighttimeMenaceBloodyEscape,
    NighttimeMenaceDeadlyDuel,
    NighttimeMenaceShadowArea,
    AssassinsRushSpectralForm,
    AssassinsRushShadowPulse,
    AssassinsRushTasteOfBlood,
    AssassinsRushBloodyTorpedo,
    DemonicChainChainedStrike,
    DemonicChainGoodCatch,
    DemonicChainExecutionersChains,
    DemonicChainGhostChains,
    DemonicChainSpidersChains,
    DeathMarkStruckByFear,
    DeathMarkBleeding,
    DeathMarkCycleOfDeath,
    DeathMarkHuntingBonus,
    DeathMarkPetrify
}

public enum ItemRewardKey
{
    BloodAmulet,
    WingedBoots,
    FortuneDagger,
    HeroAegis,
    ThornArmor,
    Boomerang,
    ProteinSoup,
    SacredChalice,
    IronGreaves,
    CursedPuppet,
    DuelistsSpur,
    MoonLantern,
    WarBanner,
    SwiftAnklet,
    ThornedBracer,
    RavenFeather,
    GuardMedal,
    RunicBelt,
    FrostCharm,
    LuckyCoin,
    SandglassTalisman
}

public sealed class RewardOffer
{
    public string Id;
    public string Title;
    public string Description;
    public Sprite Artwork;
    public RewardDefinition Definition;
    public RewardOfferKind Kind;
    public RewardPresentationIconKind IconKind;
    public bool ShowPowerStroke;
    public AbilityDefinition Ability;
    public AbilityUpgradeKey UpgradeKey;
    public bool IsStackable;
    public ItemRewardKey ItemKey;
}

public sealed class PlayerRunRewardState
{
    private readonly Dictionary<AbilityCategory, AbilityDefinition> equippedAbilities = new Dictionary<AbilityCategory, AbilityDefinition>();
    private readonly List<AbilityDefinition> knownAbilities = new List<AbilityDefinition>();
    private readonly HashSet<AbilityCategory> rewardChosenCategories = new HashSet<AbilityCategory>();
    private readonly Dictionary<AbilityUpgradeKey, int> upgradeStacks = new Dictionary<AbilityUpgradeKey, int>();
    private readonly HashSet<ItemRewardKey> ownedItems = new HashSet<ItemRewardKey>();

    public bool IsInitialized { get; private set; }
    public int CurrentHealth { get; private set; } = -1;

    public void InitializeFrom(IReadOnlyList<AbilityDefinition> initialAbilities, int currentHealth)
    {
        if (IsInitialized)
        {
            return;
        }

        knownAbilities.Clear();
        equippedAbilities.Clear();

        if (initialAbilities != null)
        {
            for (int index = 0; index < initialAbilities.Count; index++)
            {
                AbilityDefinition ability = initialAbilities[index];
                if (ability == null)
                {
                    continue;
                }

                if (!knownAbilities.Contains(ability))
                {
                    knownAbilities.Add(ability);
                }

                equippedAbilities[ability.Category] = ability;
            }
        }

        CurrentHealth = Mathf.Max(1, currentHealth);
        IsInitialized = true;
    }

    public List<AbilityDefinition> GetEquippedAbilities()
    {
        List<AbilityDefinition> abilities = new List<AbilityDefinition>();
        for (int index = 0; index < knownAbilities.Count; index++)
        {
            AbilityDefinition knownAbility = knownAbilities[index];
            if (knownAbility == null)
            {
                continue;
            }

            if (equippedAbilities.TryGetValue(knownAbility.Category, out AbilityDefinition equippedAbility)
                && equippedAbility == knownAbility)
            {
                abilities.Add(knownAbility);
            }
        }

        return abilities;
    }

    public List<AbilityDefinition> GetKnownAbilities()
    {
        return new List<AbilityDefinition>(knownAbilities);
    }

    public AbilityDefinition GetEquippedAbility(AbilityCategory category)
    {
        equippedAbilities.TryGetValue(category, out AbilityDefinition ability);
        return ability;
    }

    public bool HasAbility(AbilityDefinition ability)
    {
        if (ability == null)
        {
            return false;
        }

        return equippedAbilities.TryGetValue(ability.Category, out AbilityDefinition equippedAbility)
            && equippedAbility == ability;
    }

    public bool HasChosenRewardCategory(AbilityCategory category)
    {
        return rewardChosenCategories.Contains(category);
    }

    public void UnlockAbility(AbilityDefinition ability)
    {
        if (ability == null)
        {
            return;
        }

        if (!knownAbilities.Contains(ability))
        {
            knownAbilities.Add(ability);
        }

        equippedAbilities[ability.Category] = ability;
        rewardChosenCategories.Add(ability.Category);
    }

    public void AddUpgrade(AbilityUpgradeKey upgradeKey)
    {
        if (!upgradeStacks.ContainsKey(upgradeKey))
        {
            upgradeStacks[upgradeKey] = 0;
        }

        upgradeStacks[upgradeKey]++;
    }

    public int GetUpgradeStacks(AbilityUpgradeKey upgradeKey)
    {
        return upgradeStacks.TryGetValue(upgradeKey, out int stacks) ? stacks : 0;
    }

    public void AddItem(ItemRewardKey itemKey)
    {
        ownedItems.Add(itemKey);

        if (itemKey == ItemRewardKey.ProteinSoup)
        {
            CurrentHealth += 2;
        }
    }

    public bool HasItem(ItemRewardKey itemKey)
    {
        return ownedItems.Contains(itemKey);
    }

    public List<ItemRewardKey> GetOwnedItems()
    {
        return new List<ItemRewardKey>(ownedItems);
    }

    public void SetCurrentHealth(int currentHealth)
    {
        CurrentHealth = Mathf.Max(0, currentHealth);
    }

    public int GetBonusMovementPoints()
    {
        return HasItem(ItemRewardKey.WingedBoots) ? 1 : 0;
    }

    public int GetBonusDamage()
    {
        return HasItem(ItemRewardKey.FortuneDagger) ? 1 : 0;
    }

    public int GetBonusResistance()
    {
        return HasItem(ItemRewardKey.HeroAegis) ? 1 : 0;
    }

    public int GetBonusMaxHealth()
    {
        return HasItem(ItemRewardKey.ProteinSoup) ? 2 : 0;
    }
}
