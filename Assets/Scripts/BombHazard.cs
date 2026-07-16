using TMPro;
using UnityEngine;

public class BombHazard : BoardHazard
{
    [SerializeField] private TMP_Text explosionCountText;
    [SerializeField] private GameObject explosionFxPrefab;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private float explosionFxLifetime = 1.5f;

    private int damage = 1;
    private int blastRange = 1;
    private int turnsBeforeExplosion = 1;
    private bool hasExploded;

    public void Configure(BoardManager targetBoard, Vector2Int targetGridPosition, int explosionDamage, int explosionRange, int turnDelay = 1)
    {
        Assign(targetBoard, null, targetGridPosition);
        damage = Mathf.Max(1, explosionDamage);
        blastRange = Mathf.Max(0, explosionRange);
        turnsBeforeExplosion = Mathf.Max(1, turnDelay);
        hasExploded = false;

        transform.SetParent(targetBoard.transform, true);
        transform.position = targetBoard.GridToWorldPosition(targetGridPosition);
        targetBoard.RegisterHazard(this, targetGridPosition);
        RefreshCountLabel();
    }

    public override void HandlePlayerTurnStarted()
    {
        TickFuse();
    }

    public override void HandleEnemyTurnStarted()
    {
        TickFuse();
    }

    private void TickFuse()
    {
        if (hasExploded || board == null)
        {
            return;
        }

        turnsBeforeExplosion--;
        if (turnsBeforeExplosion > 0)
        {
            RefreshCountLabel();
            return;
        }

        Explode();
    }

    private void Explode()
    {
        if (hasExploded)
        {
            return;
        }

        hasExploded = true;
        PlayExplosionFeedback();
        ApplyExplosionDamage();
        DestroyHazard();
    }

    private void ApplyExplosionDamage()
    {
        if (board == null)
        {
            return;
        }

        for (int offsetX = -blastRange; offsetX <= blastRange; offsetX++)
        {
            for (int offsetY = -blastRange; offsetY <= blastRange; offsetY++)
            {
                if (Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)) > blastRange)
                {
                    continue;
                }

                Vector2Int targetCell = gridPosition + new Vector2Int(offsetX, offsetY);
                if (board.Player != null
                    && board.Player.ControlledCharacter != null
                    && board.Player.ControlledCharacter.GridPosition == targetCell)
                {
                    board.Player.ControlledCharacter.TakeDamage(damage, null, false, DamageSoundType.Default);
                }

                if (board.TryGetEnemy(targetCell, out Enemy enemy) && enemy != null)
                {
                    enemy.TakeDamage(damage, DamageSoundType.Default);
                }

                if (board.TryGetLichSkullObject(targetCell, out LichSkullObject skullObject) && skullObject != null)
                {
                    skullObject.TakeDamage(damage, DamageSoundType.Default);
                }

                if (board.TryGetBarrel(targetCell, out BarrelObstacle barrel) && barrel != null)
                {
                    barrel.TakeHit();
                }
            }
        }
    }

    private void PlayExplosionFeedback()
    {
        if (explosionFxPrefab != null)
        {
            GameObject spawnedFx = Instantiate(explosionFxPrefab, transform.position, explosionFxPrefab.transform.rotation);
            spawnedFx.transform.localScale = explosionFxPrefab.transform.localScale;
            if (explosionFxLifetime > 0f)
            {
                Destroy(spawnedFx, explosionFxLifetime);
            }
        }

        if (explosionSound != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySound(explosionSound, transform.position);
        }
    }

    private void RefreshCountLabel()
    {
        if (explosionCountText == null)
        {
            explosionCountText = GetComponentInChildren<TMP_Text>(true);
        }

        if (explosionCountText != null)
        {
            explosionCountText.text = Mathf.Max(0, turnsBeforeExplosion).ToString();
        }
    }

    private void DestroyHazard()
    {
        board?.UnregisterHazard(this);
        Destroy(gameObject);
    }
}
