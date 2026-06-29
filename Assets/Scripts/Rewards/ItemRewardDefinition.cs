using UnityEngine;

public enum ItemYesNoEffect
{
    None,
    SpawnExtraEnemy,
    VoodooCharm,
    SpringCoil,
    BlastStaff
}

[CreateAssetMenu(fileName = "ItemReward", menuName = "RogueSliders/Rewards/Item Reward")]
public class ItemRewardDefinition : RewardDefinition
{
    [SerializeField] private ItemRewardKey itemKey;
    [Min(0)]
    [SerializeField] private int shopPrice = 15;
    [Header("Optional Yes/No Prompt")]
    [SerializeField] private bool showCombatStartYesNoPrompt;
    [TextArea(1, 3)]
    [SerializeField] private string activationQuestionOverride;
    [SerializeField] private bool consumeOnYes;
    [SerializeField] private ItemYesNoEffect yesNoEffect;
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private GameObject damageFxPrefab;
    [Min(0f)]
    [SerializeField] private float damageFxLifetime = 1f;
    [SerializeField] [Range(0f, 1f)] private float activationSoundVolume = 1f;

    public ItemRewardKey ItemKey => itemKey;
    public bool ShowCombatStartYesNoPrompt => showCombatStartYesNoPrompt;
    public bool ConsumeOnYes => consumeOnYes;
    public ItemYesNoEffect YesNoEffect => yesNoEffect;
    public AudioClip ActivationSound => activationSound;
    public GameObject DamageFxPrefab => damageFxPrefab;
    public float DamageFxLifetime => Mathf.Max(0f, damageFxLifetime);
    public float ActivationSoundVolume => Mathf.Clamp01(activationSoundVolume);
    public override int ShopPrice => Mathf.Max(0, shopPrice);

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
