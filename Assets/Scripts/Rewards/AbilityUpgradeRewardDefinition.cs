using UnityEngine;

[CreateAssetMenu(fileName = "AbilityUpgradeReward", menuName = "RogueSliders/Rewards/Ability Upgrade Reward")]
public class AbilityUpgradeRewardDefinition : RewardDefinition
{
    [SerializeField] private AbilityDefinition ability;
    [SerializeField] private AbilityUpgradeKey upgradeKey;
    [SerializeField] private bool stackable;
    [SerializeField] private int maxStacks;

    public AbilityDefinition Ability => ability;
    public AbilityUpgradeKey UpgradeKey => upgradeKey;
    public bool Stackable => stackable;
    public int MaxStacks => maxStacks;

    public override RewardOfferKind Kind => RewardOfferKind.AbilityUpgrade;
    public override RewardPresentationIconKind IconKind => GetAbilityIconKind(ability != null ? ability.Category : AbilityCategory.BasicAttack);

    public override bool CanOffer(PlayerRunRewardState runRewardState)
    {
        int currentStacks = runRewardState != null ? runRewardState.GetUpgradeStacks(upgradeKey) : 0;
        if (ability == null || runRewardState == null || !runRewardState.HasAbility(ability))
        {
            return false;
        }

        bool canAddMoreStacks = !stackable
            ? currentStacks == 0
            : maxStacks <= 0 || currentStacks < maxStacks;
        if (!canAddMoreStacks)
        {
            return false;
        }

        if (currentStacks > 0)
        {
            return true;
        }

        const int maxDistinctUpgradesPerAbility = 5;
        return ability.CountOwnedDistinctUpgrades(runRewardState) < maxDistinctUpgradesPerAbility;
    }

    public override void Apply(PlayerRunRewardState runRewardState)
    {
        runRewardState?.AddUpgrade(upgradeKey);
    }

    public override RewardOffer CreateOffer()
    {
        RewardOffer rewardOffer = base.CreateOffer();
        rewardOffer.Ability = ability;
        rewardOffer.UpgradeKey = upgradeKey;
        rewardOffer.IsStackable = stackable;
        rewardOffer.Artwork = ability != null ? ability.Icon : null;

        return rewardOffer;
    }
}
