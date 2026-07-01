using UnityEngine;
using UnityEngine.Serialization;

public abstract class RewardDefinition : ScriptableObject
{
    protected const int GlobalShopPriceIncrease = 20;
    protected const int ConditionalShopPriceDiscount = 10;

    [SerializeField] private string rewardId;
    [SerializeField] private string rewardTitle;
    [TextArea(3, 6)]
    [SerializeField] private string rewardDescription;
    [SerializeField] private Sprite artwork;
    [FormerlySerializedAs("unlockByDefault")]
    [SerializeField] private bool lockByDefault;

    public string RewardId => string.IsNullOrWhiteSpace(rewardId) ? name : rewardId;
    public string RewardTitle => string.IsNullOrWhiteSpace(rewardTitle) ? name : rewardTitle;
    public string RewardDescription => rewardDescription;
    public Sprite Artwork => artwork;
    public bool LockByDefault => lockByDefault;
    public virtual int ShopPrice => 0;

    public abstract RewardOfferKind Kind { get; }
    public abstract RewardPresentationIconKind IconKind { get; }
    public virtual bool ShowPowerStroke => false;

    public abstract bool CanOffer(PlayerRunRewardState runRewardState);
    public abstract void Apply(PlayerRunRewardState runRewardState);

    public virtual RewardOffer CreateOffer()
    {
        return new RewardOffer
        {
            Id = RewardId,
            Title = RewardTitle,
            Description = RewardDescription,
            Artwork = Artwork,
            Kind = Kind,
            IconKind = IconKind,
            ShowPowerStroke = ShowPowerStroke,
            Definition = this,
            ShopPrice = ShopPrice
        };
    }

    protected static RewardPresentationIconKind GetAbilityIconKind(AbilityCategory abilityCategory)
    {
        switch (abilityCategory)
        {
            case AbilityCategory.MobilitySkill:
                return RewardPresentationIconKind.MobilitySkill;
            case AbilityCategory.SpecialPower:
                return RewardPresentationIconKind.SpecialPower;
            default:
                return RewardPresentationIconKind.BasicAttack;
        }
    }

    protected int ApplyShopPriceAdjustment(int basePrice, bool qualifiesForConditionalDiscount)
    {
        int adjustedPrice = Mathf.Max(0, basePrice) + GlobalShopPriceIncrease;
        if (qualifiesForConditionalDiscount)
        {
            adjustedPrice -= ConditionalShopPriceDiscount;
        }

        return Mathf.Max(0, adjustedPrice);
    }
}
