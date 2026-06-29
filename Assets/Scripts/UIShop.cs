using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIShop : MonoBehaviour
{
    private enum ShopTab
    {
        AbilityUnlocks,
        Upgrades,
        Items
    }

    private sealed class ShopEntry
    {
        public RewardOffer Offer;
        public bool IsPurchased;
    }

    [SerializeField] private Button leaveButton;
    [SerializeField] private Button tab1Button;
    [SerializeField] private Button tab2Button;
    [SerializeField] private Button tab3Button;
    [SerializeField] private GameObject leaveConfirmationPopup;
    [SerializeField] private Button leaveConfirmationYesButton;
    [SerializeField] private Button leaveConfirmationNoButton;
    [SerializeField] private TMP_Text leaveConfirmationDescriptionText;
    [SerializeField] private float leaveRevealDelay = 5f;
    [TextArea]
    [SerializeField] private string leaveConfirmationMessage = "Are you sur you want to leave Shop?";

    private readonly List<RewardButtonUI> rewardCards = new List<RewardButtonUI>();
    private readonly Dictionary<ShopTab, ShopEntry[]> entriesByTab = new Dictionary<ShopTab, ShopEntry[]>();
    private UIGame ownerUI;
    private BoardManager board;
    private SoundManager soundManager;
    private Action onLeaveRequested;
    private ShopTab activeTab = ShopTab.AbilityUnlocks;
    private float leaveRevealRemainingTime = -1f;
    private Image tab1Image;
    private Image tab2Image;
    private Image tab3Image;
    private GameObject tab1Selector;
    private GameObject tab2Selector;
    private GameObject tab3Selector;
    private Color selectedTabColor;
    private Color unselectedTabColor;
    private RewardButtonStyle shopCardStyle;
    private RewardButtonStyle shopItemCardStyle;
    private RewardButtonTheme powerTheme = RewardButtonUI.DefaultPowerTheme;
    private RewardButtonTheme itemTheme = RewardButtonUI.DefaultItemTheme;

    private void Awake()
    {
        CacheReferences();
        HideLeaveConfirmationPopup();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (leaveRevealRemainingTime < 0f)
        {
            return;
        }

        leaveRevealRemainingTime -= Time.unscaledDeltaTime;
        if (leaveRevealRemainingTime > 0f)
        {
            return;
        }

        leaveRevealRemainingTime = -1f;
        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (board != null)
        {
            board.GoldChanged -= HandleGoldChanged;
        }
    }

    public void Show(UIGame uiOwner, BoardManager currentBoard, SoundManager currentSoundManager, Action leaveCallback)
    {
        ownerUI = uiOwner;
        soundManager = currentSoundManager;
        onLeaveRequested = leaveCallback;

        if (board != null)
        {
            board.GoldChanged -= HandleGoldChanged;
        }

        board = currentBoard;
        CacheReferences();
        CaptureShopStyle();
        BuildEntries();

        if (board != null)
        {
            board.GoldChanged += HandleGoldChanged;
        }

        gameObject.SetActive(true);
        HideLeaveConfirmationPopup();
        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(false);
        }

        leaveRevealRemainingTime = Mathf.Max(0f, leaveRevealDelay);
        SetActiveTab(ShopTab.AbilityUnlocks);
        ownerUI?.RefreshMoneyDisplay();
    }

    public void Hide()
    {
        if (board != null)
        {
            board.GoldChanged -= HandleGoldChanged;
        }

        board = null;
        onLeaveRequested = null;
        leaveRevealRemainingTime = -1f;
        HideLeaveConfirmationPopup();
        gameObject.SetActive(false);
    }

    private void CacheReferences()
    {
        if (leaveButton == null)
        {
            leaveButton = transform.Find("BLeaveShop")?.GetComponent<Button>();
        }

        Transform tabsRoot = transform.Find("Tabs");
        if (tab1Button == null)
        {
            tab1Button = tabsRoot != null ? tabsRoot.Find("BTab1")?.GetComponent<Button>() : null;
        }

        if (tab2Button == null)
        {
            tab2Button = tabsRoot != null ? tabsRoot.Find("BTab2")?.GetComponent<Button>() : null;
        }

        if (tab3Button == null)
        {
            tab3Button = tabsRoot != null ? tabsRoot.Find("BTab3")?.GetComponent<Button>() : null;
        }

        tab1Image = tab1Button != null ? tab1Button.GetComponent<Image>() : null;
        tab2Image = tab2Button != null ? tab2Button.GetComponent<Image>() : null;
        tab3Image = tab3Button != null ? tab3Button.GetComponent<Image>() : null;
        tab1Selector = tab1Button != null ? tab1Button.transform.Find("iSelector")?.gameObject : null;
        tab2Selector = tab2Button != null ? tab2Button.transform.Find("iSelector")?.gameObject : null;
        tab3Selector = tab3Button != null ? tab3Button.transform.Find("iSelector")?.gameObject : null;

        if (rewardCards.Count == 0)
        {
            RewardButtonUI[] foundCards = GetComponentsInChildren<RewardButtonUI>(true);
            List<RewardButtonUI> candidateCards = new List<RewardButtonUI>();
            for (int index = 0; index < foundCards.Length; index++)
            {
                RewardButtonUI rewardCard = foundCards[index];
                if (rewardCard == null)
                {
                    continue;
                }

                if (rewardCard.transform.Find("BBuy") == null)
                {
                    continue;
                }

                candidateCards.Add(rewardCard);
            }

            candidateCards.Sort((left, right) =>
            {
                RectTransform leftRect = left != null ? left.transform as RectTransform : null;
                RectTransform rightRect = right != null ? right.transform as RectTransform : null;
                float leftY = leftRect != null ? leftRect.anchoredPosition.y : 0f;
                float rightY = rightRect != null ? rightRect.anchoredPosition.y : 0f;
                int verticalCompare = rightY.CompareTo(leftY);
                if (verticalCompare != 0)
                {
                    return verticalCompare;
                }

                float leftX = leftRect != null ? leftRect.anchoredPosition.x : 0f;
                float rightX = rightRect != null ? rightRect.anchoredPosition.x : 0f;
                return leftX.CompareTo(rightX);
            });

            for (int index = 0; index < candidateCards.Count && rewardCards.Count < 3; index++)
            {
                RewardButtonUI rewardCard = candidateCards[index];
                rewardCards.Add(rewardCard);
                Debug.Log($"[Pouet Shop] Bound shop reward card slot={rewardCards.Count - 1} name={rewardCard.name}", rewardCard);
            }
        }

        if (tab1Image != null)
        {
            selectedTabColor = tab1Image.color;
        }

        if (tab2Image != null)
        {
            unselectedTabColor = tab2Image.color;
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(HandleLeaveClicked);
            leaveButton.onClick.AddListener(HandleLeaveClicked);
        }

        if (leaveConfirmationYesButton != null)
        {
            leaveConfirmationYesButton.onClick.RemoveListener(HandleLeaveConfirmationYesClicked);
            leaveConfirmationYesButton.onClick.AddListener(HandleLeaveConfirmationYesClicked);
        }

        if (leaveConfirmationNoButton != null)
        {
            leaveConfirmationNoButton.onClick.RemoveListener(HandleLeaveConfirmationNoClicked);
            leaveConfirmationNoButton.onClick.AddListener(HandleLeaveConfirmationNoClicked);
        }

        if (tab1Button != null)
        {
            tab1Button.onClick.RemoveListener(HandleTab1Clicked);
            tab1Button.onClick.AddListener(HandleTab1Clicked);
        }

        if (tab2Button != null)
        {
            tab2Button.onClick.RemoveListener(HandleTab2Clicked);
            tab2Button.onClick.AddListener(HandleTab2Clicked);
        }

        if (tab3Button != null)
        {
            tab3Button.onClick.RemoveListener(HandleTab3Clicked);
            tab3Button.onClick.AddListener(HandleTab3Clicked);
        }
    }

    private void CaptureShopStyle()
    {
        if (rewardCards.Count > 0 && rewardCards[0] != null)
        {
            shopCardStyle = rewardCards[0].CaptureStyle();
        }

        shopItemCardStyle = ownerUI != null ? ownerUI.GetItemRewardStyle() : shopCardStyle;
    }

    private void BuildEntries()
    {
        entriesByTab.Clear();
        entriesByTab[ShopTab.AbilityUnlocks] = BuildEntries(board != null ? board.GenerateShopAbilityUnlockOffers() : null);
        entriesByTab[ShopTab.Upgrades] = BuildEntries(board != null ? board.GenerateShopUpgradeOffers() : null);
        entriesByTab[ShopTab.Items] = BuildEntries(board != null ? board.GenerateShopItemOffers() : null);
    }

    private static ShopEntry[] BuildEntries(IReadOnlyList<RewardOffer> offers)
    {
        ShopEntry[] entries = new ShopEntry[3];
        for (int index = 0; index < entries.Length; index++)
        {
            RewardOffer rewardOffer = offers != null && index < offers.Count ? offers[index] : null;
            entries[index] = new ShopEntry
            {
                Offer = rewardOffer,
                IsPurchased = false
            };
        }

        return entries;
    }

    private void HandleLeaveClicked()
    {
        SoundManager.Instance?.PlayClick();
        ShowLeaveConfirmationPopup();
    }

    private void HandleLeaveConfirmationYesClicked()
    {
        SoundManager.Instance?.PlayClick();
        HideLeaveConfirmationPopup();
        onLeaveRequested?.Invoke();
    }

    private void HandleLeaveConfirmationNoClicked()
    {
        SoundManager.Instance?.PlayClick();
        HideLeaveConfirmationPopup();
    }

    private void HandleGoldChanged(int currentGold)
    {
        ownerUI?.RefreshMoneyDisplay();
        RefreshVisibleTab();
    }

    private void HandleTab1Clicked()
    {
        SoundManager.Instance?.PlayClick();
        SetActiveTab(ShopTab.AbilityUnlocks);
    }

    private void HandleTab2Clicked()
    {
        SoundManager.Instance?.PlayClick();
        SetActiveTab(ShopTab.Upgrades);
    }

    private void HandleTab3Clicked()
    {
        SoundManager.Instance?.PlayClick();
        SetActiveTab(ShopTab.Items);
    }

    private void ShowLeaveConfirmationPopup()
    {
        if (leaveConfirmationDescriptionText != null)
        {
            leaveConfirmationDescriptionText.text = leaveConfirmationMessage;
        }

        if (leaveConfirmationPopup != null)
        {
            leaveConfirmationPopup.SetActive(true);
        }
    }

    private void HideLeaveConfirmationPopup()
    {
        if (leaveConfirmationPopup != null)
        {
            leaveConfirmationPopup.SetActive(false);
        }
    }

    private void SetActiveTab(ShopTab tab)
    {
        activeTab = tab;
        RefreshTabVisuals();
        RefreshVisibleTab();
    }

    private void RefreshTabVisuals()
    {
        if (tab1Image != null)
        {
            tab1Image.color = activeTab == ShopTab.AbilityUnlocks ? selectedTabColor : unselectedTabColor;
        }

        if (tab2Image != null)
        {
            tab2Image.color = activeTab == ShopTab.Upgrades ? selectedTabColor : unselectedTabColor;
        }

        if (tab3Image != null)
        {
            tab3Image.color = activeTab == ShopTab.Items ? selectedTabColor : unselectedTabColor;
        }

        if (tab1Selector != null)
        {
            tab1Selector.SetActive(activeTab == ShopTab.AbilityUnlocks);
        }

        if (tab2Selector != null)
        {
            tab2Selector.SetActive(activeTab == ShopTab.Upgrades);
        }

        if (tab3Selector != null)
        {
            tab3Selector.SetActive(activeTab == ShopTab.Items);
        }
    }

    private void RefreshVisibleTab()
    {
        ShopEntry[] entries = entriesByTab.TryGetValue(activeTab, out ShopEntry[] foundEntries) ? foundEntries : null;
        for (int index = 0; index < rewardCards.Count; index++)
        {
            RewardButtonUI rewardCard = rewardCards[index];
            ShopEntry entry = entries != null && index < entries.Length ? entries[index] : null;
            RewardOffer rewardOffer = entry != null ? entry.Offer : null;
            int capturedIndex = index;
            RewardButtonStyle style = rewardOffer != null && rewardOffer.Kind == RewardOfferKind.Item
                ? shopItemCardStyle
                : shopCardStyle;
            RewardButtonTheme theme = rewardOffer != null && rewardOffer.Kind == RewardOfferKind.Item ? itemTheme : powerTheme;
            Sprite typeIcon = rewardOffer != null ? ownerUI?.GetRewardTypeIconSprite(rewardOffer.IconKind) : null;
            rewardCard.Bind(rewardOffer, style, theme, typeIcon, ownerUI != null ? ownerUI.ShowShopRewardPreview : null);
            bool canAfford = board != null && rewardOffer != null && board.CurrentGold >= rewardOffer.ShopPrice;
            rewardCard.SetShopPurchaseState(
                rewardOffer != null,
                rewardOffer != null ? rewardOffer.ShopPrice : 0,
                entry != null && entry.IsPurchased,
                rewardOffer != null && !entry.IsPurchased && canAfford,
                () => HandleBuyRequested(capturedIndex));
        }
    }

    private void HandleBuyRequested(int index)
    {
        ShopEntry[] entries = entriesByTab.TryGetValue(activeTab, out ShopEntry[] foundEntries) ? foundEntries : null;
        if (entries == null || index < 0 || index >= entries.Length)
        {
            Debug.Log($"[Pouet Shop] HandleBuyRequested aborted. Invalid entries/index. tab={activeTab} index={index}", this);
            return;
        }

        ShopEntry entry = entries[index];
        if (entry == null || entry.Offer == null || entry.IsPurchased || board == null)
        {
            Debug.Log(
                $"[Pouet Shop] HandleBuyRequested aborted. entryNull={entry == null} offerNull={entry?.Offer == null} isPurchased={entry?.IsPurchased ?? false} boardNull={board == null}",
                this);
            return;
        }

        Debug.Log(
            $"[Pouet Shop] HandleBuyRequested start. index={index} title={entry.Offer.Title} price={entry.Offer.ShopPrice} gold={board.CurrentGold} tab={activeTab}",
            this);

        entry.IsPurchased = true;
        if (!board.TryPurchaseReward(entry.Offer))
        {
            Debug.Log($"[Pouet Shop] TryPurchaseReward returned false for {entry.Offer.Title}.", this);
            entry.IsPurchased = false;
            return;
        }

        Debug.Log($"[Pouet Shop] Purchase success for {entry.Offer.Title}. Gold after={board.CurrentGold}", this);

        if (soundManager != null)
        {
            soundManager.PlayUiSound(soundManager.MoneySound, 1f, 1f);
        }

        ownerUI?.RefreshMoneyDisplay();
        RefreshVisibleTab();
    }
}
