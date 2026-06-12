using UnityEngine;

[CreateAssetMenu(fileName = "ItemReward", menuName = "RogueSliders/Rewards/Item Reward")]
public class ItemRewardDefinition : RewardDefinition
{
    [SerializeField] private ItemRewardKey itemKey;

    public ItemRewardKey ItemKey => itemKey;

    public override RewardOfferKind Kind => RewardOfferKind.Item;
    public override RewardPresentationIconKind IconKind => RewardPresentationIconKind.Item;

    public override bool CanOffer(PlayerRunRewardState runRewardState)
    {
        return runRewardState != null && !runRewardState.HasItem(itemKey);
    }

    public override void Apply(PlayerRunRewardState runRewardState)
    {
        runRewardState?.AddItem(itemKey);
    }

    public override RewardOffer CreateOffer()
    {
        RewardOffer rewardOffer = base.CreateOffer();
        rewardOffer.ItemKey = itemKey;
        return rewardOffer;
    }
}
