using UnityEngine;

public class LichSkullObject : SkullObject
{
    [Header("Lich Summon")]
    [Min(1)]
    [SerializeField] private int maxHealth = 3;
    [Min(1)]
    [SerializeField] private int reviveTurns = 3;
    [SerializeField] private CanvasUnitUI canvasUnitUI;

    private int currentHealth;
    private GameObject assignedEnemyPrefab;
    private Enemy summoner;

    public override bool AllowsTraversal => false;
    public override bool CanPlayerStandOn => false;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => Mathf.Max(1, maxHealth);
    public GameObject AssignedEnemyPrefab => assignedEnemyPrefab;
    public Enemy Summoner => summoner;
    public Transform EffectAnchor => transform;

    public void Assign(BoardManager ownerBoard, Vector2Int cellPosition, GameObject enemyPrefab, Enemy owner)
    {
        assignedEnemyPrefab = enemyPrefab;
        summoner = owner;
        currentHealth = MaxHealth;
        AssignBase(ownerBoard, cellPosition, Mathf.Max(1, reviveTurns));
    }

    protected override void OnAssigned()
    {
        base.OnAssigned();
        if (canvasUnitUI == null)
        {
            canvasUnitUI = GetComponentInChildren<CanvasUnitUI>(true);
        }

        RefreshHealth();
    }

    public int TakeDamage(int incomingDamage, DamageSoundType hitSoundType = DamageSoundType.Default)
    {
        if (isResolving)
        {
            return 0;
        }

        int finalDamage = Mathf.Max(1, incomingDamage);
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        SoundManager.Instance?.PlayDamageSound(hitSoundType, transform.position);
        RefreshHealth();

        if (currentHealth <= 0)
        {
            DestroyBeforeRevive();
        }

        return finalDamage;
    }

    protected override void HandleCountdownReachedZero()
    {
        if (isResolving)
        {
            return;
        }

        BeginResolution();
        if (assignedEnemyPrefab == null || summoner == null || summoner.CurrentHealth <= 0)
        {
            Board?.ClearStaticObstacle(GridPosition, gameObject);
            Board?.UnregisterSkullObject(this);
            Destroy(gameObject);
            return;
        }

        Board?.ClearStaticObstacle(GridPosition, gameObject);
        Board?.ReviveEnemyFromLichSkull(this, assignedEnemyPrefab, summoner);
        Board?.UnregisterSkullObject(this);
        Destroy(gameObject);
    }

    private void DestroyBeforeRevive()
    {
        if (isResolving)
        {
            return;
        }

        BeginResolution();
        Board?.ClearStaticObstacle(GridPosition, gameObject);
        Board?.UnregisterSkullObject(this);
        Destroy(gameObject);
    }

    private void RefreshHealth()
    {
        if (canvasUnitUI == null)
        {
            return;
        }

        canvasUnitUI.RefreshHealth(currentHealth, MaxHealth);
    }
}
