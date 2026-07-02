using UnityEngine;

[CreateAssetMenu(fileName = "BonusAbilityReward", menuName = "RogueSliders/Rewards/Bonus Ability Reward")]
public class BonusAbilityRewardDefinition : RewardDefinition
{
    [SerializeField] private AbilityDefinition bonusAbility;
    [SerializeField] private bool consumablePotion;
    [Min(0)]
    [SerializeField] private int shopPrice = 15;

    public AbilityDefinition BonusAbility => bonusAbility;
    public bool IsConsumablePotion => consumablePotion;
    public override int ShopPrice => ApplyShopPriceAdjustment(shopPrice, false);
    public override RewardOfferKind Kind => RewardOfferKind.Item;
    public override RewardPresentationIconKind IconKind => bonusAbility != null && bonusAbility.GetButtonTypeIconKind(null, null) == AbilityButtonTypeIconKind.Potion
        ? RewardPresentationIconKind.Potion
        : GetAbilityIconKind(bonusAbility != null ? bonusAbility.Category : AbilityCategory.BasicAttack);
    public override bool ShowPowerStroke => !consumablePotion;

    public override bool CanOffer(PlayerRunRewardState runRewardState)
    {
        if (runRewardState == null || bonusAbility == null || !runRewardState.HasEmptyBonusAbilitySlot())
        {
            return false;
        }

        return consumablePotion || !runRewardState.HasBonusAbility(bonusAbility);
    }

    public override void Apply(PlayerRunRewardState runRewardState)
    {
        if (runRewardState == null || bonusAbility == null)
        {
            return;
        }

        runRewardState.TryAddBonusAbility(bonusAbility, consumablePotion);
    }

    public override RewardOffer CreateOffer()
    {
        RewardOffer rewardOffer = base.CreateOffer();
        rewardOffer.Ability = bonusAbility;
        rewardOffer.Artwork = bonusAbility != null && bonusAbility.Icon != null ? bonusAbility.Icon : Artwork;
        rewardOffer.SubtitleKind = consumablePotion ? RewardSubtitleKind.Potion : RewardSubtitleKind.BonusAbility;
        rewardOffer.VisualKind = consumablePotion ? RewardVisualKind.Potion : RewardVisualKind.BonusAbility;
        return rewardOffer;
    }
}
