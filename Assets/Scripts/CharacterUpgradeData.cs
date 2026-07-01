using UnityEngine;

[CreateAssetMenu(fileName = "CharacterUpgrade", menuName = "RogueSliders/Characters/Character Upgrade")]
public class CharacterUpgradeData : ScriptableObject
{
    [SerializeField] private string upgradeId;
    [SerializeField] private string upgradeName;
    [TextArea(2, 5)]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private Color color = Color.white;
    [Min(0)]
    [SerializeField] private int orbPrice = 10;
    [Min(1)]
    [SerializeField] private int maxUnlockCount = 1;
    [Header("Persistent Stat Bonuses")]
    [SerializeField] private int bonusMaxHealthPerUnlock;
    [SerializeField] private int bonusDamagePerUnlock;
    [SerializeField] private int bonusResistancePerUnlock;
    [SerializeField] private int bonusMovementPointsPerUnlock;
    [SerializeField] private int bonusRegenPerUnlock;
    [SerializeField] private int bonusWeaponAbilityDamagePerUnlock;
    [SerializeField] private int bonusMobilityAbilityDamagePerUnlock;
    [SerializeField] private int bonusSpecialAbilityDamagePerUnlock;
    [SerializeField] private AbilityDefinition replaceStartingAbility;
    [SerializeField] private AbilityDefinition replacementStartingAbility;

    public string UpgradeId => string.IsNullOrWhiteSpace(upgradeId) ? name : upgradeId.Trim();
    public string UpgradeName => string.IsNullOrWhiteSpace(upgradeName) ? name : upgradeName;
    public string Description => description;
    public Sprite Icon => icon;
    public Color Color => color;
    public int OrbPrice => Mathf.Max(0, orbPrice);
    public int MaxUnlockCount => Mathf.Max(1, maxUnlockCount);
    public int BonusMaxHealthPerUnlock => bonusMaxHealthPerUnlock;
    public int BonusDamagePerUnlock => bonusDamagePerUnlock;
    public int BonusResistancePerUnlock => bonusResistancePerUnlock;
    public int BonusMovementPointsPerUnlock => bonusMovementPointsPerUnlock;
    public int BonusRegenPerUnlock => bonusRegenPerUnlock;
    public int BonusWeaponAbilityDamagePerUnlock => bonusWeaponAbilityDamagePerUnlock;
    public int BonusMobilityAbilityDamagePerUnlock => bonusMobilityAbilityDamagePerUnlock;
    public int BonusSpecialAbilityDamagePerUnlock => bonusSpecialAbilityDamagePerUnlock;
    public AbilityDefinition ReplaceStartingAbility => replaceStartingAbility;
    public AbilityDefinition ReplacementStartingAbility => replacementStartingAbility;
    public bool HasStartingAbilityReplacement => replaceStartingAbility != null && replacementStartingAbility != null;
}
