using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct RewardButtonStyle
{
    public readonly Sprite ArtworkSprite;
    public readonly Color BackgroundColor;
    public readonly Color TitleBackgroundColor;
    public readonly Color TitleTextColor;
    public readonly Color DescriptionBackgroundColor;
    public readonly Color DescriptionTextColor;
    public readonly Color TypeContainerColor;
    public readonly Color TypeIconColor;

    public RewardButtonStyle(
        Sprite artworkSprite,
        Color backgroundColor,
        Color titleBackgroundColor,
        Color titleTextColor,
        Color descriptionBackgroundColor,
        Color descriptionTextColor,
        Color typeContainerColor,
        Color typeIconColor)
    {
        ArtworkSprite = artworkSprite;
        BackgroundColor = backgroundColor;
        TitleBackgroundColor = titleBackgroundColor;
        TitleTextColor = titleTextColor;
        DescriptionBackgroundColor = descriptionBackgroundColor;
        DescriptionTextColor = descriptionTextColor;
        TypeContainerColor = typeContainerColor;
        TypeIconColor = typeIconColor;
    }
}

public readonly struct RewardButtonTheme
{
    public readonly Color OutlineColor;
    public readonly Color SubtitleBackgroundColor;
    public readonly Color SubtitleTextColor;
    public readonly Color NewSubtitleBackgroundColor;
    public readonly Color NewSubtitleTextColor;

    public RewardButtonTheme(
        Color outlineColor,
        Color subtitleBackgroundColor,
        Color subtitleTextColor,
        Color newSubtitleBackgroundColor,
        Color newSubtitleTextColor)
    {
        OutlineColor = outlineColor;
        SubtitleBackgroundColor = subtitleBackgroundColor;
        SubtitleTextColor = subtitleTextColor;
        NewSubtitleBackgroundColor = newSubtitleBackgroundColor;
        NewSubtitleTextColor = newSubtitleTextColor;
    }
}

public class RewardButtonUI : MonoBehaviour
{
    private static readonly Color PowerOutlineColor = new Color32(0x92, 0x00, 0xBE, 0xFF);
    private static readonly Color ItemOutlineColor = new Color32(0x96, 0x7F, 0x62, 0xFF);
    private static readonly Color PowerSubtitleBackgroundColor = new Color32(0x43, 0x15, 0x47, 0xFF);
    private static readonly Color ItemSubtitleBackgroundColor = new Color32(0xCB, 0xA3, 0x72, 0xFF);
    private static readonly Color PowerSubtitleTextColor = new Color32(0x82, 0x43, 0x7F, 0xFF);
    private static readonly Color ItemSubtitleTextColor = new Color32(0xF5, 0xD6, 0xB1, 0xFF);
    private static readonly Color NewPowerSubtitleBackgroundColor = new Color32(0x17, 0x0B, 0x15, 0xFF);
    private static readonly Color NewPowerSubtitleTextColor = new Color32(0xFF, 0xBB, 0x00, 0xFF);
    private static readonly Color ShopPriceAffordableColor = Color.white;
    private static readonly Color ShopPriceUnaffordableColor = new Color32(0xFF, 0x4A, 0x4A, 0xFF);

    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image artworkImage;
    [SerializeField] private Image outlineImage;
    [SerializeField] private GameObject powerStroke;
    [SerializeField] private Image titleBackground;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image descriptionBackground;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image subtitleBackground;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private Image typeContainerBackground;
    [SerializeField] private Image typeIcon;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private GameObject boughtOverlay;
    private Action shopBuyAction;
    private bool buyButtonListenerBound;

    public Button Button => button;
    public Sprite CurrentTypeSprite => typeIcon != null ? typeIcon.sprite : null;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public static RewardButtonTheme DefaultPowerTheme => new RewardButtonTheme(
        PowerOutlineColor,
        PowerSubtitleBackgroundColor,
        PowerSubtitleTextColor,
        NewPowerSubtitleBackgroundColor,
        NewPowerSubtitleTextColor);

    public static RewardButtonTheme DefaultItemTheme => new RewardButtonTheme(
        ItemOutlineColor,
        ItemSubtitleBackgroundColor,
        ItemSubtitleTextColor,
        ItemSubtitleBackgroundColor,
        ItemSubtitleTextColor);

    public RewardButtonStyle CaptureStyle()
    {
        CacheReferences();
        return new RewardButtonStyle(
            artworkImage != null ? artworkImage.sprite : null,
            background != null ? background.color : Color.white,
            titleBackground != null ? titleBackground.color : Color.white,
            titleText != null ? titleText.color : Color.white,
            descriptionBackground != null ? descriptionBackground.color : Color.white,
            descriptionText != null ? descriptionText.color : Color.white,
            typeContainerBackground != null ? typeContainerBackground.color : Color.white,
            typeIcon != null ? typeIcon.color : Color.white);
    }

    public void Bind(RewardOffer rewardOffer, RewardButtonStyle style, RewardButtonTheme theme, Sprite resolvedTypeIcon, Action<RewardOffer> onClicked)
    {
        CacheReferences();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
        }

        if (rewardOffer == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        ApplyStyle(style);

        if (titleText != null)
        {
            titleText.text = rewardOffer.Title;
        }

        if (descriptionText != null)
        {
            descriptionText.text = rewardOffer.Description;
        }

        if (subtitleText != null)
        {
            subtitleText.text = GetSubtitleText(rewardOffer);
            subtitleText.color = GetSubtitleTextColor(rewardOffer, theme);
        }

        if (artworkImage != null)
        {
            artworkImage.sprite = rewardOffer.Artwork != null ? rewardOffer.Artwork : style.ArtworkSprite;
        }

        if (typeIcon != null && resolvedTypeIcon != null)
        {
            typeIcon.sprite = resolvedTypeIcon;
        }

        if (outlineImage != null)
        {
            outlineImage.color = theme.OutlineColor;
        }

        if (subtitleBackground != null)
        {
            subtitleBackground.color = GetSubtitleBackgroundColor(rewardOffer, theme);
        }

        if (powerStroke != null)
        {
            powerStroke.SetActive(rewardOffer.ShowPowerStroke);
        }

        if (button != null)
        {
            button.onClick.AddListener(() =>
            {
                if (IsPointerOverBuyButton())
                {
                    Debug.Log("[Pouet Shop] Root reward card click detected over BBuy area. Forwarding to buy.", this);
                    HandleBuyButtonClicked();
                    return;
                }

                SoundManager.Instance?.PlayClick();
                onClicked?.Invoke(rewardOffer);
            });
        }
    }

    public void SetShopPurchaseState(bool showShopPurchase, int price, bool isBought, bool canBuy, Action onBuyClicked)
    {
        CacheReferences();
        shopBuyAction = showShopPurchase && !isBought && canBuy ? onBuyClicked : null;

        if (buyButton != null)
        {
            buyButton.gameObject.SetActive(showShopPurchase && !isBought);
            buyButton.interactable = showShopPurchase && !isBought && canBuy;
            buyButton.transform.SetAsLastSibling();
        }

        if (priceText != null)
        {
            priceText.text = Mathf.Max(0, price).ToString();
            priceText.color = canBuy || isBought
                ? ShopPriceAffordableColor
                : ShopPriceUnaffordableColor;
        }

        if (boughtOverlay != null)
        {
            boughtOverlay.SetActive(showShopPurchase && isBought);
        }
    }

    private void HandleBuyButtonClicked()
    {
        if (shopBuyAction == null)
        {
            Debug.Log("[Pouet Shop] BBuy clicked but shopBuyAction is null.", this);
            return;
        }

        Debug.Log("[Pouet Shop] BBuy clicked. Invoking shopBuyAction.", this);
        SoundManager.Instance?.PlayClick();
        shopBuyAction.Invoke();
    }

    private bool IsPointerOverBuyButton()
    {
        if (buyButton == null || !buyButton.gameObject.activeInHierarchy)
        {
            return false;
        }

        RectTransform buyRect = buyButton.transform as RectTransform;
        if (buyRect == null)
        {
            return false;
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        Camera eventCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(buyRect, Input.mousePosition, eventCamera);
    }

    private void ApplyStyle(RewardButtonStyle style)
    {
        if (background != null)
        {
            background.color = style.BackgroundColor;
        }

        if (titleBackground != null)
        {
            titleBackground.color = style.TitleBackgroundColor;
        }

        if (titleText != null)
        {
            titleText.color = style.TitleTextColor;
        }

        if (descriptionBackground != null)
        {
            descriptionBackground.color = style.DescriptionBackgroundColor;
        }

        if (descriptionText != null)
        {
            descriptionText.color = style.DescriptionTextColor;
        }

        if (typeContainerBackground != null)
        {
            typeContainerBackground.color = style.TypeContainerColor;
        }

        if (typeIcon != null)
        {
            typeIcon.color = style.TypeIconColor;
        }
    }

    private void CacheReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (background == null)
        {
            background = GetComponent<Image>();
        }

        if (artworkImage == null)
        {
            artworkImage = transform.Find("MaskIcon/PowerSprite")?.GetComponent<Image>();
        }

        if (outlineImage == null)
        {
            outlineImage = transform.Find("MaskIcon/iOutline")?.GetComponent<Image>();
        }

        if (powerStroke == null)
        {
            Transform strokeTransform = transform.Find("PowerStroke");
            if (strokeTransform != null)
            {
                powerStroke = strokeTransform.gameObject;
            }
        }

        if (titleBackground == null)
        {
            titleBackground = transform.Find("Title")?.GetComponent<Image>();
        }

        if (titleText == null)
        {
            titleText = transform.Find("Title/Chara-Name")?.GetComponent<TMP_Text>();
        }

        if (descriptionBackground == null)
        {
            descriptionBackground = transform.Find("iDescription")?.GetComponent<Image>();
        }

        if (descriptionText == null)
        {
            descriptionText = transform.Find("iDescription/tDescription")?.GetComponent<TMP_Text>();
        }

        if (subtitleBackground == null)
        {
            subtitleBackground = transform.Find("iSubstitle")?.GetComponent<Image>();
        }

        if (subtitleText == null)
        {
            subtitleText = transform.Find("iSubstitle/tSubstitle")?.GetComponent<TMP_Text>();
        }

        if (typeContainerBackground == null)
        {
            typeContainerBackground = transform.Find("MaskIcon/iPowerPlace")?.GetComponent<Image>();
        }

        if (typeIcon == null)
        {
            typeIcon = transform.Find("MaskIcon/iPowerPlace/icon")?.GetComponent<Image>();
        }

        if (buyButton == null)
        {
            buyButton = transform.Find("BBuy")?.GetComponent<Button>();
        }

        if (buyButton != null && !buyButtonListenerBound)
        {
            buyButton.onClick.AddListener(HandleBuyButtonClicked);
            buyButtonListenerBound = true;
        }

        if (priceText == null)
        {
            priceText = transform.Find("BBuy/tPrice")?.GetComponent<TMP_Text>();
        }

        if (boughtOverlay == null)
        {
            Transform boughtTransform = transform.Find("iBought");
            if (boughtTransform != null)
            {
                boughtOverlay = boughtTransform.gameObject;
            }
        }
    }

    private static string GetSubtitleText(RewardOffer rewardOffer)
    {
        if (rewardOffer == null)
        {
            return string.Empty;
        }

        if (rewardOffer.Kind == RewardOfferKind.AbilityUpgrade)
        {
            return "Upgrade";
        }

        return rewardOffer.SubtitleKind switch
        {
            RewardSubtitleKind.Mobility => "Mobility",
            RewardSubtitleKind.Power => "Power",
            RewardSubtitleKind.Passive => "Passive",
            RewardSubtitleKind.BonusAbility => "Bonus Ability",
            RewardSubtitleKind.Potion => "Potion",
            _ => "Weapon"
        };
    }

    private static Color GetSubtitleBackgroundColor(RewardOffer rewardOffer, RewardButtonTheme theme)
    {
        if (rewardOffer == null)
        {
            return theme.SubtitleBackgroundColor;
        }

        if (rewardOffer.Kind == RewardOfferKind.Item)
        {
            return theme.SubtitleBackgroundColor;
        }

        return rewardOffer.Kind == RewardOfferKind.AbilityUpgrade
            ? theme.SubtitleBackgroundColor
            : theme.NewSubtitleBackgroundColor;
    }

    private static Color GetSubtitleTextColor(RewardOffer rewardOffer, RewardButtonTheme theme)
    {
        if (rewardOffer == null)
        {
            return theme.SubtitleTextColor;
        }

        if (rewardOffer.Kind == RewardOfferKind.Item)
        {
            return theme.SubtitleTextColor;
        }

        return rewardOffer.Kind == RewardOfferKind.AbilityUpgrade
            ? theme.SubtitleTextColor
            : theme.NewSubtitleTextColor;
    }
}
