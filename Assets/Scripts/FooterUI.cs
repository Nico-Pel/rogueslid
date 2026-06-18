using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FooterUI : MonoBehaviour
{
    [SerializeField] private RectTransform abilitiesBar;
    [SerializeField] private AbilityButtonUI abilityButton1;
    [SerializeField] private AbilityButtonUI abilityButton2;
    [SerializeField] private AbilityButtonUI abilityButton3;
    [SerializeField] private RectTransform itemsList;
    [SerializeField] private TMP_Text characterNameLabel;
    [SerializeField] private Image characterPortraitImage;
    [SerializeField] private Button portraitButton;
    [SerializeField] private TMP_Text arenaCountLabel;
    [SerializeField] private float footerSidePadding = 24f;
    [SerializeField] private float portraitToAbilitiesSpacing = 24f;
    [SerializeField] private float minimumAbilityBarScale = 0.72f;

    private RectTransform rectTransform;
    private RectTransform portraitButtonRect;
    private Vector2 portraitOriginalAnchoredPosition;
    private Vector3 abilitiesOriginalScale = Vector3.one;
    private bool responsiveLayoutCached;

    public RectTransform AbilitiesBar => abilitiesBar;
    public AbilityButtonUI AbilityButton1 => abilityButton1;
    public AbilityButtonUI AbilityButton2 => abilityButton2;
    public AbilityButtonUI AbilityButton3 => abilityButton3;
    public RectTransform ItemsList => itemsList;
    public TMP_Text CharacterNameLabel => characterNameLabel;
    public Image CharacterPortraitImage => characterPortraitImage;
    public Button PortraitButton => portraitButton;
    public TMP_Text ArenaCountLabel => arenaCountLabel;

    private void Awake()
    {
        CacheReferences();
        ApplyResponsiveLayout();
    }

    private void OnValidate()
    {
        CacheReferences();
        ApplyResponsiveLayout();
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyResponsiveLayout();
    }

    public void RefreshCharacter(Character character)
    {
        CacheReferences();

        if (characterNameLabel != null)
        {
            characterNameLabel.text = character != null ? character.CharacterName : string.Empty;
        }

        if (characterPortraitImage != null)
        {
            Sprite portrait = character != null ? character.CharacterPortrait : null;
            characterPortraitImage.sprite = portrait;
            characterPortraitImage.enabled = portrait != null;
        }
    }

    public void RefreshArenaCount(int arenaCount)
    {
        CacheReferences();
        if (arenaCountLabel != null)
        {
            arenaCountLabel.text = $"ARENA {Mathf.Max(1, arenaCount)}";
        }
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (abilitiesBar == null)
        {
            Transform abilitiesTransform = transform.Find("Abilities");
            if (abilitiesTransform != null)
            {
                abilitiesBar = abilitiesTransform as RectTransform;
            }
        }

        if (abilityButton1 == null && abilitiesBar != null)
        {
            abilityButton1 = GetOrAddAbilityButton("BAbility (1)")
                ?? GetOrAddAbilityButton("BAbility1")
                ?? GetOrAddAbilityButton("BAbility");
        }

        if (abilityButton2 == null && abilitiesBar != null)
        {
            abilityButton2 = GetOrAddAbilityButton("BAbility (2)")
                ?? GetOrAddAbilityButton("BAbility2");
        }

        if (abilityButton3 == null && abilitiesBar != null)
        {
            abilityButton3 = GetOrAddAbilityButton("BAbility (3)")
                ?? GetOrAddAbilityButton("BAbility3");
        }

        if (itemsList == null)
        {
            Transform itemsTransform = transform.Find("ItemsList");
            if (itemsTransform != null)
            {
                itemsList = itemsTransform as RectTransform;
            }
        }

        if (characterNameLabel == null)
        {
            characterNameLabel = transform.Find("Chara-Name")?.GetComponent<TMP_Text>();
        }

        if (characterPortraitImage == null)
        {
            characterPortraitImage = transform.Find("iChara")?.GetComponent<Image>();
        }

        if (portraitButton == null)
        {
            portraitButton = transform.Find("BPortrait")?.GetComponent<Button>();
        }

        if (portraitButtonRect == null && portraitButton != null)
        {
            portraitButtonRect = portraitButton.transform as RectTransform;
        }

        if (arenaCountLabel == null)
        {
            arenaCountLabel = transform.Find("iArenaCount/tArenaCount")?.GetComponent<TMP_Text>();
            if (arenaCountLabel == null)
            {
                arenaCountLabel = transform.Find("tArenaCount")?.GetComponent<TMP_Text>();
            }
        }

        CacheResponsiveLayoutState();
    }

    private void CacheResponsiveLayoutState()
    {
        if (responsiveLayoutCached)
        {
            return;
        }

        if (portraitButtonRect != null)
        {
            portraitOriginalAnchoredPosition = portraitButtonRect.anchoredPosition;
        }

        if (abilitiesBar != null)
        {
            abilitiesOriginalScale = abilitiesBar.localScale;
        }

        responsiveLayoutCached = true;
    }

    private void ApplyResponsiveLayout()
    {
        CacheReferences();

        if (rectTransform == null || portraitButtonRect == null || abilitiesBar == null)
        {
            return;
        }

        GridLayoutGroup abilitiesLayout = abilitiesBar.GetComponent<GridLayoutGroup>();
        if (abilitiesLayout == null)
        {
            return;
        }

        float footerWidth = rectTransform.rect.width;
        if (footerWidth <= 0f)
        {
            return;
        }

        float portraitWidth = portraitButtonRect.rect.width;
        float portraitHeight = portraitButtonRect.rect.height;
        float portraitLeft = footerSidePadding;
        float portraitCenterX = portraitLeft + (portraitWidth * 0.5f);

        portraitButtonRect.anchorMin = new Vector2(0f, 0.5f);
        portraitButtonRect.anchorMax = new Vector2(0f, 0.5f);
        portraitButtonRect.pivot = new Vector2(0.5f, 0.5f);
        portraitButtonRect.anchoredPosition = new Vector2(portraitCenterX, portraitOriginalAnchoredPosition.y);

        int activeAbilitySlots = 0;
        if (abilityButton1 != null && abilityButton1.gameObject.activeSelf) activeAbilitySlots++;
        if (abilityButton2 != null && abilityButton2.gameObject.activeSelf) activeAbilitySlots++;
        if (abilityButton3 != null && abilityButton3.gameObject.activeSelf) activeAbilitySlots++;
        activeAbilitySlots = Mathf.Max(1, activeAbilitySlots);

        float desiredAbilitiesWidth = (abilitiesLayout.cellSize.x * activeAbilitySlots) + (abilitiesLayout.spacing.x * Mathf.Max(0, activeAbilitySlots - 1));
        float availableAbilitiesWidth = footerWidth - (portraitLeft + portraitWidth + portraitToAbilitiesSpacing + footerSidePadding);
        float abilityScale = desiredAbilitiesWidth > 0f ? Mathf.Clamp(availableAbilitiesWidth / desiredAbilitiesWidth, minimumAbilityBarScale, 1f) : 1f;

        abilitiesBar.localScale = abilitiesOriginalScale * abilityScale;
        abilitiesBar.anchorMin = new Vector2(0f, 0.5f);
        abilitiesBar.anchorMax = new Vector2(0f, 0.5f);
        abilitiesBar.pivot = new Vector2(0.5f, 0.5f);

        float scaledAbilitiesWidth = desiredAbilitiesWidth * abilityScale;
        float abilitiesLeft = portraitLeft + portraitWidth + portraitToAbilitiesSpacing;
        float abilitiesCenterX = abilitiesLeft + (scaledAbilitiesWidth * 0.5f);
        abilitiesBar.anchoredPosition = new Vector2(abilitiesCenterX, abilitiesBar.anchoredPosition.y);

        if (itemsList != null)
        {
            itemsList.anchorMin = new Vector2(0f, 0f);
            itemsList.anchorMax = new Vector2(0f, 0f);
            itemsList.pivot = new Vector2(0f, 0f);
            itemsList.anchoredPosition = new Vector2(footerSidePadding, itemsList.anchoredPosition.y);
        }
    }

    private AbilityButtonUI GetOrAddAbilityButton(string childName)
    {
        if (abilitiesBar == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform buttonTransform = abilitiesBar.Find(childName);
        if (buttonTransform == null)
        {
            return null;
        }

        AbilityButtonUI abilityButton = buttonTransform.GetComponent<AbilityButtonUI>();
        if (abilityButton == null)
        {
            abilityButton = buttonTransform.gameObject.AddComponent<AbilityButtonUI>();
        }

        return abilityButton;
    }
}
