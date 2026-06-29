using UnityEngine;

[CreateAssetMenu(fileName = "AbilityReward", menuName = "RogueSliders/Rewards/Ability Reward")]
public class AbilityRewardDefinition : RewardDefinition
{
    [SerializeField] private AbilityDefinition ability;
    [Min(0)]
    [SerializeField] private int shopPrice = 30;

    public AbilityDefinition Ability => ability;
    public override int ShopPrice => ApplyShopPriceAdjustment(shopPrice, false);
    public override RewardOfferKind Kind => RewardOfferKind.AbilityUnlock;
    public override RewardPresentationIconKind IconKind => GetAbilityIconKind(ability != null ? ability.Category : AbilityCategory.BasicAttack);
    public override bool ShowPowerStroke => true;

    public override bool CanOffer(PlayerRunRewardState runRewardState)
    {
        return ability != null
            && runRewardState != null
            && !runRewardState.KnowsAbility(ability)
            && !runRewardState.HasChosenRewardCategory(ability.Category);
    }

    public override void Apply(PlayerRunRewardState runRewardState)
    {
        runRewardState?.UnlockAbility(ability);
    }

    public override RewardOffer CreateOffer()
    {
        RewardOffer rewardOffer = base.CreateOffer();
        rewardOffer.Ability = ability;
        rewardOffer.Artwork = ability != null ? ability.Icon : null;

        return rewardOffer;
    }
}
