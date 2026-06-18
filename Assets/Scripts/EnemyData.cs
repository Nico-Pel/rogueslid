using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "RogueSliders/Enemies/Enemy Data")]
public class EnemyData : ScriptableObject
{
    private const string DefaultSpecialInfoMessage = "The goblin is a classic enemy.";

    [Header("Identity")]
    [SerializeField] private string enemyName;
    [TextArea(2, 5)]
    [SerializeField] private string specialInfo = DefaultSpecialInfoMessage;
    [SerializeField] private Sprite portrait;

    [Header("Gameplay")]
    [Min(1)]
    [SerializeField] private int maxHealth = 8;
    [Min(0)]
    [SerializeField] private int force = 2;
    [Min(0)]
    [SerializeField] private int resistance;
    [Min(0)]
    [SerializeField] private int mobility = 2;
    [SerializeField] private EnemyAttackPattern attackPattern = EnemyAttackPattern.AdjacentOrthogonal;
    [Min(1)]
    [SerializeField] private int attackRange = 1;
    [SerializeField] private DamageSoundType damageSoundType;
    [SerializeField] private bool directVision = true;
    [SerializeField] private bool requireAlignedShot;
    [SerializeField] private bool allowPerfectDiagonalShot;
    [SerializeField] private bool hasMaxRange = true;
    [SerializeField] private bool ignoreObstacles;
    [SerializeField] private bool attackAlways;
    [SerializeField] private bool flee;
    [Min(0)]
    [SerializeField] private int maxFleeTurns = 2;
    [SerializeField] private bool attackFirst;
    [Range(0f, 100f)]
    [SerializeField] private float fleeThresholdPercent;
    [SerializeField] private bool ignoreObstaclesForMovement;
    [SerializeField] private bool canEndTurnOnObstacle;
    [SerializeField] private bool advanceTowardsCharacterWhenAlreadyInRange;
    [Min(0f)]
    [SerializeField] private float attackDamageDelay = 0.2f;
    [SerializeField] private bool multiplyAttackDamageDelayByDistance;
    [SerializeField] private bool lookAtTargetWhenAttacking;
    [SerializeField] private EnemyOptionalActionType optionalActionType;
    [Min(0)]
    [SerializeField] private int optionalHealAmount = 4;
    [Min(0f)]
    [SerializeField] private float moveDuration = 0.16f;
    [Min(0f)]
    [SerializeField] private float projectileTravelHeight = 0.5f;
    [Min(0f)]
    [SerializeField] private float projectileTravelSpeed = 10f;
    [SerializeField] private bool useFlyAnimationOnObstacle;
    [SerializeField] private bool immuneToFire;

    [Header("Displayed Stats")]
    [SerializeField] private bool showAttack = true;
    [SerializeField] private Sprite attackSprite;
    [SerializeField] private bool showHealth = true;
    [SerializeField] private bool showResistance;
    [SerializeField] private bool showMobility = true;
    [SerializeField] private bool showRegen;
    [Min(0)]
    [SerializeField] private int regenPerTurn;
    [SerializeField] private bool showSpecial;
    [SerializeField] private Sprite specialStatSprite;
    [SerializeField] private string specialStatValue;

    public string EnemyName => string.IsNullOrWhiteSpace(enemyName) ? name : enemyName;
    public string SpecialInfo => string.IsNullOrWhiteSpace(specialInfo) ? DefaultSpecialInfoMessage : specialInfo;
    public Sprite Portrait => portrait;
    public int MaxHealth => Mathf.Max(1, maxHealth);
    public int Force => Mathf.Max(0, force);
    public int Resistance => Mathf.Max(0, resistance);
    public int Mobility => Mathf.Max(0, mobility);
    public EnemyAttackPattern AttackPattern => attackPattern;
    public int AttackRange => Mathf.Max(1, attackRange);
    public DamageSoundType DamageSoundType => damageSoundType;
    public bool DirectVision => directVision;
    public bool RequireAlignedShot => requireAlignedShot;
    public bool AllowPerfectDiagonalShot => allowPerfectDiagonalShot;
    public bool HasMaxRange => hasMaxRange;
    public bool IgnoreObstacles => ignoreObstacles;
    public bool AttackAlways => attackAlways;
    public bool Flee => flee;
    public int MaxFleeTurns => Mathf.Max(0, maxFleeTurns);
    public bool AttackFirst => attackFirst;
    public float FleeThresholdPercent => Mathf.Clamp(fleeThresholdPercent, 0f, 100f);
    public bool IgnoreObstaclesForMovement => ignoreObstaclesForMovement;
    public bool CanEndTurnOnObstacle => canEndTurnOnObstacle;
    public bool AdvanceTowardsCharacterWhenAlreadyInRange => advanceTowardsCharacterWhenAlreadyInRange;
    public float AttackDamageDelay => Mathf.Max(0f, attackDamageDelay);
    public bool MultiplyAttackDamageDelayByDistance => multiplyAttackDamageDelayByDistance;
    public bool LookAtTargetWhenAttacking => lookAtTargetWhenAttacking;
    public EnemyOptionalActionType OptionalActionType => optionalActionType;
    public int OptionalHealAmount => Mathf.Max(0, optionalHealAmount);
    public float MoveDuration => Mathf.Max(0f, moveDuration);
    public float ProjectileTravelHeight => Mathf.Max(0f, projectileTravelHeight);
    public float ProjectileTravelSpeed => Mathf.Max(0f, projectileTravelSpeed);
    public bool UseFlyAnimationOnObstacle => useFlyAnimationOnObstacle;
    public bool ImmuneToFire => immuneToFire;
    public bool ShowAttack => showAttack;
    public Sprite AttackSprite => attackSprite;
    public bool ShowHealth => showHealth;
    public bool ShowResistance => showResistance;
    public bool ShowMobility => showMobility;
    public bool ShowRegen => showRegen;
    public int RegenPerTurn => Mathf.Max(0, regenPerTurn);
    public bool ShowSpecial => showSpecial;
    public Sprite SpecialStatSprite => specialStatSprite;
    public string SpecialStatValue => specialStatValue;
}
