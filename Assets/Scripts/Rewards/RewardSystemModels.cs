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
    Item,
    Potion
}

public enum RewardSubtitleKind
{
    Weapon,
    Mobility,
    Power,
    Passive,
    BonusAbility,
    Potion
}

public enum RewardVisualKind
{
    Power,
    Item,
    BonusAbility,
    Potion
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
    DeathMarkPetrify,
    RoyalDaggerLightStrike,
    RoyalDaggerHolyBlade,
    RoyalDaggerRoyalPunishment,
    RoyalDaggerRoyalBlessing,
    RoyalDaggerBlessedBlade,
    LamellarStepExtendedStride,
    LamellarStepVenomDaggers,
    LamellarStepTacticalRetreat,
    LamellarStepSpectralDaggers,
    SacrificialLeapSacrificialWind,
    SacrificialLeapBloodthirst,
    SacrificialLeapSacrificialBlade,
    SacrificialLeapSacrificialRite,
    ArcaneTrapArcaneSustain,
    ArcaneTrapArcaneEruption,
    ArcaneTrapArcaneWave,
    ArcaneTrapArcaneExhaustion,
    DemonbaneBlindSpot,
    DemonbaneBloodCallsBlood,
    DemonbaneWhereverYouAre,
    DemonbaneLastChance,
    WhisperfangOneMoreForTheRoad,
    WhisperfangRicochet,
    WhisperfangMultiShot,
    WhisperfangLuckyBolt,
    TridimensionalPortalEnergy,
    TridimensionalPortalEye,
    TridimensionalPortalImpact,
    TridimensionalPortalDistortion,
    HeartRipperHookedFist,
    HeartRipperRevigoratingHeart,
    HeartRipperDemonicDuel,
    HeartRipperDemonicHand,
    MistySpiritUnsteadySteps,
    MistySpiritBrokenHeart,
    MistySpiritParanoia,
    MistySpiritMistDispersion,
    SomersaultJumpRoseThorns,
    SomersaultJumpHeroicCascade,
    SomersaultJumpDoubleJump,
    SomersaultJumpSmallJump,
    TridimensionalPortalMultidimensionalPortals,
    SacredCrossbowLightBolt,
    SacredCrossbowSacredRay,
    SacredCrossbowLightBurst,
    SacredCrossbowSacredDemolition,
    RainOfBoltsIronBolts,
    RainOfBoltsCloudySky,
    RainOfBoltsLuckyHunter,
    RainOfBoltsLostBolt,
    WolfStepAlphaWolf,
    WolfStepHungryWolf,
    WolfStepQuickSteps,
    WolfStepWolfCharge,
    LamellarStepSharpenedBlades,
    SpectralClawsSpectralFracture,
    SpectralClawsSpectralStep,
    SpectralClawsSpectralTakeoff,
    SpectralClawsSpectralCrossing,
    PoisonTrailPoisonedSoul,
    PoisonTrailVenomousMomentum,
    PoisonTrailCorrosivePerfume,
    PoisonTrailToxicPath,
    PoisonTrailUnholyAura,
    CircleOfTheDamnedCursedIncantation,
    CircleOfTheDamnedDamnedWave,
    CircleOfTheDamnedDamnedRitual,
    CircleOfTheDamnedDamnedBlast,
    CircleOfTheDamnedProfaneReach,
    CountsCarbineScopedSight,
    CountsCarbinePointBlank,
    CountsCarbineShellShot,
    CountsCarbineBackupPistol,
    CountsCarbineSilverBullets,
    CountsCarbinePiercingRound,
    HuntersMarkSkeetShooting,
    HuntersMarkForetaste,
    HuntersMarkVeteranTracker,
    HuntersMarkShortcut,
    HuntersMarkRelentlessMark,
    BidimensionalShadowBidimensionalProjectiles,
    BidimensionalShadowBidimensionalSwitch,
    BidimensionalShadowBidimensionalRay,
    BidimensionalShadowBidimensionalTwins
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
    SandglassTalisman,
    Sakura,
    PumpkinHead,
    ScholarsCape,
    BarbarianHorn,
    WhiteFlag,
    SamuraiMask,
    ScopeGlasses,
    IronSpikedSandals,
    Makibishi,
    MouseTrap,
    CowardsScarf,
    CorruptedBlouse,
    VoodooCharm,
    SpringCoil,
    Scarecrow,
    LoadedDie,
    ShopBell,
    BlastStaff,
    Hood,
    ScrapBomb,
    CoinPurse,
    IronGauntlets
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
    public int ShopPrice;
    public RewardSubtitleKind SubtitleKind;
    public RewardVisualKind VisualKind;
}

public sealed class PlayerRunRewardState
{
    public const int MaxBonusAbilitySlots = 3;
    private readonly Dictionary<AbilityCategory, AbilityDefinition> equippedAbilities = new Dictionary<AbilityCategory, AbilityDefinition>();
    private readonly List<AbilityDefinition> knownAbilities = new List<AbilityDefinition>();
    private readonly HashSet<AbilityCategory> rewardChosenCategories = new HashSet<AbilityCategory>();
    private readonly Dictionary<AbilityUpgradeKey, int> upgradeStacks = new Dictionary<AbilityUpgradeKey, int>();
    private readonly HashSet<ItemRewardKey> ownedItems = new HashSet<ItemRewardKey>();
    private readonly List<AbilityDefinition> bonusAbilitySlots = new List<AbilityDefinition>(MaxBonusAbilitySlots);
    private readonly Dictionary<string, int> bonusAbilityPersistentValues = new Dictionary<string, int>();

    public bool IsInitialized { get; private set; }
    public int CurrentHealth { get; private set; } = -1;
    public int CurrentGold { get; private set; }

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
        EnsureBonusAbilitySlots();
        IsInitialized = true;
    }

    private void EnsureBonusAbilitySlots()
    {
        while (bonusAbilitySlots.Count < MaxBonusAbilitySlots)
        {
            bonusAbilitySlots.Add(null);
        }
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

    public bool KnowsAbility(AbilityDefinition ability)
    {
        return ability != null && knownAbilities.Contains(ability);
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

    public Dictionary<AbilityUpgradeKey, int> GetAllUpgradeStacks()
    {
        return new Dictionary<AbilityUpgradeKey, int>(upgradeStacks);
    }

    public void AddItem(ItemRewardKey itemKey)
    {
        ownedItems.Add(itemKey);

        if (itemKey == ItemRewardKey.ProteinSoup)
        {
            CurrentHealth += 2;
        }

        if (itemKey == ItemRewardKey.CoinPurse)
        {
            CurrentGold += 30;
        }
    }

    public bool HasItem(ItemRewardKey itemKey)
    {
        return ownedItems.Contains(itemKey);
    }

    public void RemoveItem(ItemRewardKey itemKey)
    {
        ownedItems.Remove(itemKey);
    }

    public List<ItemRewardKey> GetOwnedItems()
    {
        return new List<ItemRewardKey>(ownedItems);
    }

    public IReadOnlyList<AbilityDefinition> GetBonusAbilitySlots()
    {
        EnsureBonusAbilitySlots();
        return bonusAbilitySlots;
    }

    public AbilityDefinition GetBonusAbilityAt(int slotIndex)
    {
        EnsureBonusAbilitySlots();
        return slotIndex >= 0 && slotIndex < bonusAbilitySlots.Count
            ? bonusAbilitySlots[slotIndex]
            : null;
    }

    public bool HasEmptyBonusAbilitySlot()
    {
        EnsureBonusAbilitySlots();
        for (int index = 0; index < bonusAbilitySlots.Count; index++)
        {
            if (bonusAbilitySlots[index] == null)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasBonusAbility(AbilityDefinition ability)
    {
        if (ability == null)
        {
            return false;
        }

        EnsureBonusAbilitySlots();
        for (int index = 0; index < bonusAbilitySlots.Count; index++)
        {
            if (bonusAbilitySlots[index] == ability)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryAddBonusAbility(AbilityDefinition ability, bool allowDuplicate)
    {
        if (ability == null)
        {
            return false;
        }

        EnsureBonusAbilitySlots();
        if (!allowDuplicate && HasBonusAbility(ability))
        {
            return false;
        }

        for (int index = 0; index < bonusAbilitySlots.Count; index++)
        {
            if (bonusAbilitySlots[index] != null)
            {
                continue;
            }

            bonusAbilitySlots[index] = ability;
            return true;
        }

        return false;
    }

    public bool ClearBonusAbilityAt(int slotIndex)
    {
        EnsureBonusAbilitySlots();
        if (slotIndex < 0 || slotIndex >= bonusAbilitySlots.Count || bonusAbilitySlots[slotIndex] == null)
        {
            return false;
        }

        bonusAbilitySlots[slotIndex] = null;
        return true;
    }

    public void CompactBonusAbilitiesLeft()
    {
        EnsureBonusAbilitySlots();
        int writeIndex = 0;
        for (int readIndex = 0; readIndex < bonusAbilitySlots.Count; readIndex++)
        {
            AbilityDefinition ability = bonusAbilitySlots[readIndex];
            if (ability == null)
            {
                continue;
            }

            if (writeIndex != readIndex)
            {
                bonusAbilitySlots[writeIndex] = ability;
                bonusAbilitySlots[readIndex] = null;
            }

            writeIndex++;
        }
    }

    public int GetBonusAbilityPersistentValue(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && bonusAbilityPersistentValues.TryGetValue(key, out int value)
            ? value
            : 0;
    }

    public void SetBonusAbilityPersistentValue(string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        bonusAbilityPersistentValues[key] = value;
    }

    public int AddBonusAbilityPersistentValue(string key, int delta)
    {
        if (string.IsNullOrWhiteSpace(key) || delta == 0)
        {
            return GetBonusAbilityPersistentValue(key);
        }

        int nextValue = GetBonusAbilityPersistentValue(key) + delta;
        bonusAbilityPersistentValues[key] = nextValue;
        return nextValue;
    }

    public void SetCurrentHealth(int currentHealth)
    {
        CurrentHealth = Mathf.Max(0, currentHealth);
    }

    public void AddGold(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        CurrentGold += amount;
    }

    public bool CanSpendGold(int amount)
    {
        return amount <= 0 || CurrentGold >= amount;
    }

    public bool TrySpendGold(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (CurrentGold < amount)
        {
            return false;
        }

        CurrentGold -= amount;
        return true;
    }

    public void SetCurrentGold(int currentGold)
    {
        CurrentGold = Mathf.Max(0, currentGold);
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
