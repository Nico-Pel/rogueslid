using UnityEngine;

public enum EnemySpecialBehavior
{
    None,
    SnakePoisonOpener,
    WolfPounce,
    BoarCharge,
    GiantWormTunnelBoss,
    TrollShockwaveBoss,
    RagnarWarboss,
    RagnarOgreMinion,
    DragoonTwinBoss
}

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
    [SerializeField] private bool fleeAfterAttacking;
    [Range(0f, 100f)]
    [SerializeField] private float fleeThresholdPercent;
    [SerializeField] private GameObject fearFxPrefab;
    [Min(0f)]
    [SerializeField] private float fearBodyWiggleStrength;
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
    [Min(0f)]
    [SerializeField] private float projectileSpawnDelay;
    [SerializeField] private bool useFlyAnimationOnObstacle;
    [SerializeField] private bool immuneToFire;

    [Header("Special Behaviour")]
    [SerializeField] private EnemySpecialBehavior specialBehavior;
    [Min(0)]
    [SerializeField] private int specialDamage = 1;
    [Min(0f)]
    [SerializeField] private float specialWindupDuration = 0.35f;
    [Min(0f)]
    [SerializeField] private float specialStartDelay = 0f;
    [Min(0f)]
    [SerializeField] private float specialJumpDuration = 0.35f;
    [Min(0f)]
    [SerializeField] private float specialJumpPower = 0.75f;
    [Min(0f)]
    [SerializeField] private float specialRecoveryDelay = 0.25f;
    [Min(0)]
    [SerializeField] private int specialMinimumDistance = 3;
    [Min(1)]
    [SerializeField] private int specialLandingDistance = 2;
    [Min(0f)]
    [SerializeField] private float specialImpactShakeRatio = 0.2f;
    [Min(0f)]
    [SerializeField] private float specialPerDistanceDelay = 0.05f;
    [Min(0f)]
    [SerializeField] private float specialBumpHeight = 0.12f;
    [Min(0f)]
    [SerializeField] private float specialBumpDurationPerDistance = 0.08f;
    [SerializeField] private GameObject specialSelfBuffFxPrefab;
    [SerializeField] private GameObject specialCompanionPrefab;

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
    public bool FleeAfterAttacking => fleeAfterAttacking;
    public float FleeThresholdPercent => Mathf.Clamp(fleeThresholdPercent, 0f, 100f);
    public GameObject FearFxPrefab => fearFxPrefab;
    public float FearBodyWiggleStrength => Mathf.Max(0f, fearBodyWiggleStrength);
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
    public float ProjectileSpawnDelay => Mathf.Max(0f, projectileSpawnDelay);
    public bool UseFlyAnimationOnObstacle => useFlyAnimationOnObstacle;
    public bool ImmuneToFire => immuneToFire;
    public EnemySpecialBehavior SpecialBehavior => specialBehavior;
    public int SpecialDamage => Mathf.Max(0, specialDamage);
    public float SpecialWindupDuration => Mathf.Max(0f, specialWindupDuration);
    public float SpecialStartDelay => Mathf.Max(0f, specialStartDelay);
    public float SpecialJumpDuration => Mathf.Max(0f, specialJumpDuration);
    public float SpecialJumpPower => Mathf.Max(0f, specialJumpPower);
    public float SpecialRecoveryDelay => Mathf.Max(0f, specialRecoveryDelay);
    public int SpecialMinimumDistance => Mathf.Max(0, specialMinimumDistance);
    public int SpecialLandingDistance => Mathf.Max(1, specialLandingDistance);
    public float SpecialImpactShakeRatio => Mathf.Max(0f, specialImpactShakeRatio);
    public float SpecialPerDistanceDelay => Mathf.Max(0f, specialPerDistanceDelay);
    public float SpecialBumpHeight => Mathf.Max(0f, specialBumpHeight);
    public float SpecialBumpDurationPerDistance => Mathf.Max(0f, specialBumpDurationPerDistance);
    public GameObject SpecialSelfBuffFxPrefab => specialSelfBuffFxPrefab;
    public GameObject SpecialCompanionPrefab => specialCompanionPrefab;
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
