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
    }

    private void OnValidate()
    {
        CacheReferences();
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

        if (arenaCountLabel == null)
        {
            arenaCountLabel = transform.Find("iArenaCount/tArenaCount")?.GetComponent<TMP_Text>();
            if (arenaCountLabel == null)
            {
                arenaCountLabel = transform.Find("tArenaCount")?.GetComponent<TMP_Text>();
            }
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
