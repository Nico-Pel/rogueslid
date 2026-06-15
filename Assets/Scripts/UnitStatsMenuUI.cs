using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class UnitStatRowUI
{
    [SerializeField] private GameObject root;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text valueLabel;

    public void CacheFrom(Transform parent, string rowName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(rowName))
        {
            return;
        }

        if (root == null)
        {
            root = parent.Find(rowName)?.gameObject;
        }

        if (root == null)
        {
            return;
        }

        if (icon == null)
        {
            icon = root.transform.Find("iIcon")?.GetComponent<Image>();
        }

        if (valueLabel == null)
        {
            valueLabel = root.transform.Find("tStats")?.GetComponent<TMP_Text>();
        }
    }

    public void Bind(bool isVisible, string value, Sprite spriteOverride = null)
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
        }

        if (icon != null && spriteOverride != null)
        {
            icon.sprite = spriteOverride;
            icon.enabled = true;
        }
    }
}

public class UnitStatsMenuUI : MonoBehaviour
{
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

        attackStat.Bind(data == null || data.ShowAttack, enemy.Force.ToString(), data != null ? data.AttackSprite : null);
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

        attackStat.Bind(true, character.BonusDamage.ToString());
        hpStat.Bind(true, $"{character.CurrentHealth}/{character.MaxHealth}");
        resistanceStat.Bind(true, character.Resistance.ToString());
        mobilityStat.Bind(true, $"{character.RemainingMovementPoints}/{character.BaseMovementPoints}");
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

        UpdateTitleSize();
    }

    private void UpdateTitleSize()
    {
        if (titleBackground == null || nameLabel == null)
        {
            return;
        }

        nameLabel.ForceMeshUpdate();
        float targetWidth = Mathf.Max(titleMinWidth, nameLabel.preferredWidth + titleHorizontalPadding);
        Vector2 sizeDelta = titleBackground.sizeDelta;
        sizeDelta.x = targetWidth;
        titleBackground.sizeDelta = sizeDelta;
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
        hpStat.CacheFrom(transform, "StatHP");
        resistanceStat.CacheFrom(transform, "StatResistance");
        mobilityStat.CacheFrom(transform, "StatMobility");
        regenStat.CacheFrom(transform, "StatRegen");
        specialStat.CacheFrom(transform, "StatSpecial");
    }
}
