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

    public void Bind(RewardOffer rewardOffer, RewardButtonStyle style, Sprite resolvedTypeIcon, Action<RewardOffer> onClicked)
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
            subtitleText.color = GetSubtitleTextColor(rewardOffer);
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
            outlineImage.color = rewardOffer.Kind == RewardOfferKind.Item
                ? ItemOutlineColor
                : PowerOutlineColor;
        }

        if (subtitleBackground != null)
        {
            subtitleBackground.color = GetSubtitleBackgroundColor(rewardOffer);
        }

        if (powerStroke != null)
        {
            powerStroke.SetActive(rewardOffer.ShowPowerStroke);
        }

        if (button != null)
        {
            button.onClick.AddListener(() =>
            {
                SoundManager.Instance?.PlayClick();
                onClicked?.Invoke(rewardOffer);
            });
        }
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
    }

    private static string GetSubtitleText(RewardOffer rewardOffer)
    {
        if (rewardOffer == null)
        {
            return string.Empty;
        }

        if (rewardOffer.Kind == RewardOfferKind.Item)
        {
            return "Passive";
        }

        if (rewardOffer.Kind == RewardOfferKind.AbilityUpgrade)
        {
            return "Upgrade";
        }

        switch (rewardOffer.IconKind)
        {
            case RewardPresentationIconKind.MobilitySkill:
                return "Mobility";
            case RewardPresentationIconKind.SpecialPower:
                return "Power";
            default:
                return "Weapon";
        }
    }

    private static Color GetSubtitleBackgroundColor(RewardOffer rewardOffer)
    {
        if (rewardOffer == null)
        {
            return PowerSubtitleBackgroundColor;
        }

        if (rewardOffer.Kind == RewardOfferKind.Item)
        {
            return ItemSubtitleBackgroundColor;
        }

        return rewardOffer.Kind == RewardOfferKind.AbilityUpgrade
            ? PowerSubtitleBackgroundColor
            : NewPowerSubtitleBackgroundColor;
    }

    private static Color GetSubtitleTextColor(RewardOffer rewardOffer)
    {
        if (rewardOffer == null)
        {
            return PowerSubtitleTextColor;
        }

        if (rewardOffer.Kind == RewardOfferKind.Item)
        {
            return ItemSubtitleTextColor;
        }

        return rewardOffer.Kind == RewardOfferKind.AbilityUpgrade
            ? PowerSubtitleTextColor
            : NewPowerSubtitleTextColor;
    }
}
