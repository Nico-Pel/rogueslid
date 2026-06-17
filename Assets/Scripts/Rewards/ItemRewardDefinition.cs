using UnityEngine;

public enum ItemYesNoEffect
{
    None,
    SpawnExtraEnemy
}

[CreateAssetMenu(fileName = "ItemReward", menuName = "RogueSliders/Rewards/Item Reward")]
public class ItemRewardDefinition : RewardDefinition
{
    [SerializeField] private ItemRewardKey itemKey;
    [Header("Optional Yes/No Prompt")]
    [SerializeField] private bool showCombatStartYesNoPrompt;
    [TextArea(1, 3)]
    [SerializeField] private string activationQuestionOverride;
    [SerializeField] private bool consumeOnYes;
    [SerializeField] private ItemYesNoEffect yesNoEffect;
    [SerializeField] private AudioClip activationSound;

    public ItemRewardKey ItemKey => itemKey;
    public bool ShowCombatStartYesNoPrompt => showCombatStartYesNoPrompt;
    public bool ConsumeOnYes => consumeOnYes;
    public ItemYesNoEffect YesNoEffect => yesNoEffect;
    public AudioClip ActivationSound => activationSound;

    public override RewardOfferKind Kind => RewardOfferKind.Item;
    public override RewardPresentationIconKind IconKind => RewardPresentationIconKind.Item;

    public string GetActivationQuestion()
    {
        return string.IsNullOrWhiteSpace(activationQuestionOverride)
            ? $"Activate {RewardTitle}?"
            : activationQuestionOverride;
    }

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
