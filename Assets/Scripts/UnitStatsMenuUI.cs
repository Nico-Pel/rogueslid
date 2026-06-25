using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class UnitStatRowUI
{
    [SerializeField] private GameObject root;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text valueLabel;
    private Color defaultValueColor = Color.white;
    private bool hasCachedDefaultValueColor;

    public void CacheFrom(Transform parent, string rowName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(rowName))
        {
            return;
        }

        if (root == null || !root.transform.IsChildOf(parent))
        {
            Transform rowTransform = FindDescendantByName(parent, rowName);
            root = rowTransform != null ? rowTransform.gameObject : null;
        }

        if (root == null)
        {
            return;
        }

        if (icon == null || !icon.transform.IsChildOf(root.transform))
        {
            Transform iconTransform = FindDescendantByName(root.transform, "iIcon");
            icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        }

        if (valueLabel == null || !valueLabel.transform.IsChildOf(root.transform))
        {
            Transform valueLabelTransform = FindDescendantByName(root.transform, "tStats");
            valueLabel = valueLabelTransform != null ? valueLabelTransform.GetComponent<TMP_Text>() : null;
        }

        if (valueLabel != null && !hasCachedDefaultValueColor)
        {
            defaultValueColor = valueLabel.color;
            hasCachedDefaultValueColor = true;
        }
    }

    public void Bind(bool isVisible, string value, Sprite spriteOverride = null, Color? valueColorOverride = null)
    {
        if (root == null)
        {
            return;
        }

        root.SetActive(isVisible);
        if (!isVisible)
        {
            return;
        }

        if (valueLabel != null)
        {
            valueLabel.text = value;
            valueLabel.color = valueColorOverride ?? defaultValueColor;
        }

        if (icon != null && spriteOverride != null)
        {
            icon.sprite = spriteOverride;
            icon.enabled = true;
        }
    }

    private static Transform FindDescendantByName(Transform parent, string targetName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == targetName)
            {
                return child;
            }

            Transform nestedMatch = FindDescendantByName(child, targetName);
            if (nestedMatch != null)
            {
                return nestedMatch;
            }
        }

        return null;
    }
}

public class UnitStatsMenuUI : MonoBehaviour
{
    private static readonly Color BuffedStatColor = new Color32(0x71, 0xF0, 0x6A, 0xFF);
    private static readonly Color DebuffedStatColor = new Color32(0xFF, 0x63, 0x63, 0xFF);

    [Header("Common")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text specialInfoLabel;
    [SerializeField] private RectTransform titleBackground;
    [SerializeField] private float titleHorizontalPadding = 70f;
    [SerializeField] private float titleMinWidth = 240f;

    [Header("Stats")]
    [SerializeField] private UnitStatRowUI attackStat = new UnitStatRowUI();
    [SerializeField] private UnitStatRowUI forceStat = new UnitStatRowUI();
    [SerializeField] private UnitStatRowUI hpStat = new UnitStatRowUI();
    [SerializeField] private UnitStatRowUI resistanceStat = new UnitStatRowUI();
    [SerializeField] private UnitStatRowUI mobilityStat = new UnitStatRowUI();
    [SerializeField] private UnitStatRowUI regenStat = new UnitStatRowUI();
    [SerializeField] private UnitStatRowUI specialStat = new UnitStatRowUI();

    public Button CloseButton => closeButton;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void ShowEnemy(Enemy enemy)
    {
        CacheReferences();
        if (enemy == null)
        {
            Hide();
            return;
        }

        EnemyData data = enemy.Data;
        BindCommon(enemy.EnemyName, enemy.EnemyPortrait, enemy.EnemySpecialInfo);

        int naturalAttack = enemy.Force;
        int currentAttack = enemy.EffectiveForce;
        attackStat.Bind(
            data == null || data.ShowAttack,
            currentAttack.ToString(),
            data != null ? data.AttackSprite : null,
            GetStatColor(currentAttack, naturalAttack));
        forceStat.Bind(false, string.Empty);
        hpStat.Bind(data == null || data.ShowHealth, $"{enemy.CurrentHealth}/{enemy.MaxHealth}");
        resistanceStat.Bind(data != null && data.ShowResistance, enemy.Resistance.ToString());
        mobilityStat.Bind(data == null || data.ShowMobility, enemy.Mobility.ToString());
        regenStat.Bind(data != null && data.ShowRegen, data != null ? data.RegenPerTurn.ToString() : "0");
        specialStat.Bind(data != null && data.ShowSpecial, data != null ? data.SpecialStatValue : string.Empty, data != null ? data.SpecialStatSprite : null);

        gameObject.SetActive(true);
    }

    public void ShowCharacter(Character character)
    {
        CacheReferences();
        if (character == null)
        {
            Hide();
            return;
        }

        BindCommon(character.CharacterName, character.CharacterPortrait, character.CharacterDescription);

        int naturalAttack = character.GetDisplayedBasicAttackDamage(false);
        int currentAttack = character.GetDisplayedBasicAttackDamage(true);
        int naturalForce = character.GetNaturalBonusDamage();
        int currentForce = character.BonusDamage;
        int naturalHp = character.GetNaturalMaxHealth();
        int currentHp = character.MaxHealth;
        int naturalResistance = character.GetNaturalResistance();
        int currentResistance = character.Resistance;
        int naturalMobility = character.GetNaturalMovementPointsPerTurn();
        int currentMobility = character.BaseMovementPoints;

        attackStat.Bind(true, currentAttack.ToString(), null, GetStatColor(currentAttack, naturalAttack));
        forceStat.Bind(true, currentForce.ToString(), null, GetStatColor(currentForce, naturalForce));
        hpStat.Bind(true, $"{character.CurrentHealth}/{currentHp}", null, GetStatColor(currentHp, naturalHp));
        resistanceStat.Bind(true, currentResistance.ToString(), null, GetStatColor(currentResistance, naturalResistance));
        mobilityStat.Bind(true, currentMobility.ToString(), null, GetStatColor(currentMobility, naturalMobility));
        regenStat.Bind(false, string.Empty);
        specialStat.Bind(false, string.Empty);

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void BindCommon(string displayName, Sprite portrait, string specialInfo)
    {
        if (nameLabel != null)
        {
            nameLabel.text = displayName ?? string.Empty;
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        if (specialInfoLabel != null)
        {
            specialInfoLabel.text = string.IsNullOrWhiteSpace(specialInfo) ? string.Empty : specialInfo;
        }
    }

    private void CacheReferences()
    {
        if (closeButton == null)
        {
            closeButton = transform.Find("BClose")?.GetComponent<Button>();
        }

        if (portraitImage == null)
        {
            portraitImage = transform.Find("iPortraits")?.GetComponent<Image>();
            if (portraitImage == null)
            {
                portraitImage = transform.Find("iChara")?.GetComponent<Image>();
            }
        }

        if (nameLabel == null)
        {
            nameLabel = transform.Find("enemy-Name")?.GetComponent<TMP_Text>();
            if (nameLabel == null)
            {
                nameLabel = transform.Find("Chara-Name")?.GetComponent<TMP_Text>();
            }
        }

        if (specialInfoLabel == null)
        {
            specialInfoLabel = transform.Find("tSpecialInfo")?.GetComponent<TMP_Text>();
        }

        if (titleBackground == null)
        {
            titleBackground = transform.Find("iTitle") as RectTransform;
            if (titleBackground == null)
            {
                titleBackground = transform.Find("CharaTitle") as RectTransform;
            }
        }

        attackStat.CacheFrom(transform, "StatAttack");
        forceStat.CacheFrom(transform, "StatForce");
        hpStat.CacheFrom(transform, "StatHP");
        resistanceStat.CacheFrom(transform, "StatResistance");
        mobilityStat.CacheFrom(transform, "StatMobility");
        regenStat.CacheFrom(transform, "StatRegen");
        specialStat.CacheFrom(transform, "StatSpecial");
    }

    private static Color? GetStatColor(int currentValue, int naturalValue)
    {
        if (currentValue > naturalValue)
        {
            return BuffedStatColor;
        }

        if (currentValue < naturalValue)
        {
            return DebuffedStatColor;
        }

        return null;
    }
}
