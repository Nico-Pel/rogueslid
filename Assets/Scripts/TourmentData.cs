using UnityEngine;

public enum TourmentBossUnlockTier
{
    Boss1 = 1,
    Boss2 = 2,
    Boss3 = 3
}

[System.Serializable]
public class TourmentRewardUnlockDefinition
{
    [Min(1)]
    [SerializeField] private int requiredTourmentLevel = 1;
    [SerializeField] private TourmentBossUnlockTier requiredBoss = TourmentBossUnlockTier.Boss1;
    [SerializeField] private RewardDefinition rewardDefinition;

    public int RequiredTourmentLevel => Mathf.Max(1, requiredTourmentLevel);
    public TourmentBossUnlockTier RequiredBoss => requiredBoss;
    public RewardDefinition RewardDefinition => rewardDefinition;
}

[CreateAssetMenu(fileName = "TourmentData", menuName = "RogueSliders/Tourments/Tourment Data")]
public class TourmentData : ScriptableObject
{
    [Min(1)]
    [SerializeField] private int level = 1;
    [SerializeField] private string displayName = "TORMENT I";
    [SerializeField] private Sprite icon;
    [Range(0f, 5f)]
    [SerializeField] private float enemyHealthMultiplier = 1f;
    [Min(0)]
    [SerializeField] private int enemyForceBonus;
    [Min(0)]
    [SerializeField] private int enemyResistanceBonus;
    [Min(0)]
    [SerializeField] private int enemyRegenBonus;

    public int Level => Mathf.Max(1, level);
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? $"TORMENT {Level}" : displayName;
    public Sprite Icon => icon;
    public float EnemyHealthMultiplier => Mathf.Max(1f, enemyHealthMultiplier);
    public int EnemyForceBonus => Mathf.Max(0, enemyForceBonus);
    public int EnemyResistanceBonus => Mathf.Max(0, enemyResistanceBonus);
    public int EnemyRegenBonus => Mathf.Max(0, enemyRegenBonus);
}
