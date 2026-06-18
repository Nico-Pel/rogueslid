using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData_DragoonRider", menuName = "RogueSliders/Enemies/Dragoon Rider Enemy Data")]
public class DragoonRiderEnemyData : EnemyData
{
    [Header("Dragoon Rider")]
    [Min(1)]
    [SerializeField] private int meleeDamage = 3;
    [Min(1)]
    [SerializeField] private int rangedDamage = 2;
    [Min(1)]
    [SerializeField] private int rangedAttackRange = 99;
    [SerializeField] private DamageSoundType meleeDamageSoundType = DamageSoundType.Sword;
    [SerializeField] private DamageSoundType rangedDamageSoundType = DamageSoundType.MagicHit;
    [SerializeField] private AnimationClip attackSourceClip;
    [SerializeField] private AnimationClip meleeAttackAnimationClip;
    [SerializeField] private AnimationClip rangedAttackAnimationClip;
    [Min(0f)]
    [SerializeField] private float rangedImpactDamageDelay = 0.1f;
    [Min(0f)]
    [SerializeField] private float flightPreparationDuration = 0.5f;
    [Min(0.01f)]
    [SerializeField] private float flightJumpDuration = 3f;
    [Min(0f)]
    [SerializeField] private float altAttackRotateDuration = 0.25f;
    [Min(0f)]
    [SerializeField] private float fireballSpawnDelay = 0.25f;
    [Min(1)]
    [SerializeField] private int summonedFireballCount = 2;
    [Min(0f)]
    [SerializeField] private float fireballJumpPower = 1.5f;
    [Min(0.01f)]
    [SerializeField] private float fireballJumpDuration = 0.45f;
    [Min(0.01f)]
    [SerializeField] private float fireballVolleyDuration = 2f;
    [Min(0f)]
    [SerializeField] private float delayBetweenFireballs = 0.08f;
    [SerializeField] private GameObject fireImpactFxPrefab;
    [SerializeField] private GameObject fireObjectPrefab;
    [SerializeField] private GameObject fireDamageSoundParametersPrefab;
    [Min(1)]
    [SerializeField] private int fireTileDamage = 1;

    public int MeleeDamage => Mathf.Max(1, meleeDamage);
    public int RangedDamage => Mathf.Max(1, rangedDamage);
    public int RangedAttackRange => Mathf.Max(1, rangedAttackRange);
    public DamageSoundType MeleeDamageSoundType => meleeDamageSoundType;
    public DamageSoundType RangedDamageSoundType => rangedDamageSoundType;
    public AnimationClip AttackSourceClip => attackSourceClip;
    public AnimationClip MeleeAttackAnimationClip => meleeAttackAnimationClip;
    public AnimationClip RangedAttackAnimationClip => rangedAttackAnimationClip;
    public float RangedImpactDamageDelay => Mathf.Max(0f, rangedImpactDamageDelay);
    public float FlightPreparationDuration => Mathf.Max(0f, flightPreparationDuration);
    public float FlightJumpDuration => Mathf.Max(0.01f, flightJumpDuration);
    public float AltAttackRotateDuration => Mathf.Max(0f, altAttackRotateDuration);
    public float FireballSpawnDelay => Mathf.Max(0f, fireballSpawnDelay);
    public int SummonedFireballCount => Mathf.Max(1, summonedFireballCount);
    public float FireballJumpPower => Mathf.Max(0f, fireballJumpPower);
    public float FireballJumpDuration => Mathf.Max(0.01f, fireballJumpDuration);
    public float FireballVolleyDuration => Mathf.Max(0.01f, fireballVolleyDuration);
    public float DelayBetweenFireballs => Mathf.Max(0f, delayBetweenFireballs);
    public GameObject FireImpactFxPrefab => fireImpactFxPrefab;
    public GameObject FireObjectPrefab => fireObjectPrefab;
    public GameObject FireDamageSoundParametersPrefab => fireDamageSoundParametersPrefab;
    public int FireTileDamage => Mathf.Max(1, fireTileDamage);
}
