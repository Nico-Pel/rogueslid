using UnityEngine;

public class FireTileHazard : BoardHazard
{
    [SerializeField] private int damage = 1;
    [SerializeField] private GameObject fireObjectPrefab;
    [SerializeField] private GameObject impactFxPrefab;
    [SerializeField] private GameObject damageSoundParametersPrefab;
    [SerializeField] private float impactFxLifetime = 1.5f;

    private GameObject fireObjectInstance;

    public override bool IsVisibleToEnemies => true;

    public void Configure(
        BoardManager targetBoard,
        Character targetOwner,
        Vector2Int targetGridPosition,
        int fireDamage,
        GameObject fireObjectVisualPrefab,
        GameObject fireImpactPrefab,
        GameObject fireDamageSoundPrefab)
    {
        Assign(targetBoard, targetOwner, targetGridPosition);
        damage = Mathf.Max(1, fireDamage);
        fireObjectPrefab = fireObjectVisualPrefab;
        impactFxPrefab = fireImpactPrefab;
        damageSoundParametersPrefab = fireDamageSoundPrefab;

        transform.SetParent(targetBoard.transform, true);
        transform.position = targetBoard.GridToWorldPosition(targetGridPosition);
        targetBoard.RegisterHazard(this, targetGridPosition);
        SpawnImpactFx();
        EnsureFireObject();
    }

    public override int GetEnemyPathPenalty(Enemy enemy)
    {
        return enemy != null && enemy.IsImmuneToFire ? 0 : 5;
    }

    public override bool WouldKillEnemy(Enemy enemy)
    {
        if (enemy == null || enemy.IsImmuneToFire)
        {
            return false;
        }

        int estimatedDamage = Mathf.Max(1, damage - enemy.Resistance);
        return enemy.CurrentHealth <= estimatedDamage;
    }

    public override void HandleEnemyEntered(Enemy enemy)
    {
        if (enemy == null || enemy.IsImmuneToFire)
        {
            return;
        }

        PlayDamageSound();
        enemy.TakeDamage(damage, DamageSoundType.MagicHit);
    }

    public override void HandleCharacterEntered(Character character)
    {
        if (character == null)
        {
            return;
        }

        PlayDamageSound();
        character.TakeDamage(damage, null, false, DamageSoundType.MagicHit);
    }

    private void EnsureFireObject()
    {
        if (fireObjectInstance != null || fireObjectPrefab == null)
        {
            return;
        }

        fireObjectInstance = Instantiate(fireObjectPrefab, transform.position, fireObjectPrefab.transform.rotation, transform);
        fireObjectInstance.transform.localScale = fireObjectPrefab.transform.localScale;
    }

    private void SpawnImpactFx()
    {
        if (impactFxPrefab == null)
        {
            return;
        }

        GameObject impactFx = Instantiate(impactFxPrefab, transform.position, impactFxPrefab.transform.rotation);
        impactFx.transform.localScale = impactFxPrefab.transform.localScale;
        if (impactFxLifetime > 0f)
        {
            Destroy(impactFx, impactFxLifetime);
        }
    }

    private void PlayDamageSound()
    {
        if (damageSoundParametersPrefab == null)
        {
            return;
        }

        GameObject soundObject = Instantiate(damageSoundParametersPrefab, transform.position, damageSoundParametersPrefab.transform.rotation);
        soundObject.transform.localScale = damageSoundParametersPrefab.transform.localScale;

        SoundParameters soundParameters = soundObject.GetComponent<SoundParameters>();
        if (soundParameters != null)
        {
            soundParameters.PlaySound(transform.position);
        }

        Destroy(soundObject, 1f);
    }
}
