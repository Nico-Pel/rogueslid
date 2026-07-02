using System.Collections;
using UnityEngine;

public class PoisonTrailHazard : BoardHazard
{
    private int remainingPlayerTurnStarts = 1;
    private GameObject poisonFieldPrefab;
    private GameObject poisonFieldInstance;
    private bool isDestroying;
    private Coroutine destroyRoutine;

    public void Configure(
        BoardManager targetBoard,
        Character targetOwner,
        Vector2Int targetGridPosition,
        int lifetimeInPlayerTurnStarts,
        GameObject poisonFieldVisualPrefab)
    {
        Assign(targetBoard, targetOwner, targetGridPosition);
        remainingPlayerTurnStarts = Mathf.Max(1, lifetimeInPlayerTurnStarts);
        poisonFieldPrefab = poisonFieldVisualPrefab;

        transform.SetParent(targetBoard.transform, true);
        transform.position = targetBoard.GridToWorldPosition(targetGridPosition);
        targetBoard.RegisterHazard(this, targetGridPosition);
        EnsurePoisonFieldObject();

        if (targetBoard.TryGetEnemy(targetGridPosition, out Enemy enemy) && enemy != null)
        {
            ApplyPoison(enemy);
        }
    }

    public void Refresh(int lifetimeInPlayerTurnStarts)
    {
        remainingPlayerTurnStarts = Mathf.Max(remainingPlayerTurnStarts, Mathf.Max(1, lifetimeInPlayerTurnStarts));
    }

    public override void HandlePlayerTurnStarted()
    {
        remainingPlayerTurnStarts = Mathf.Max(0, remainingPlayerTurnStarts - 1);
        if (remainingPlayerTurnStarts <= 0)
        {
            DestroyHazard();
        }
    }

    public override void HandleEnemyEntered(Enemy enemy)
    {
        ApplyPoison(enemy);
    }

    private void ApplyPoison(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        enemy.ApplyStatusEffect(CombatStatusType.Poisoned, -1, 1, false);
    }

    private void EnsurePoisonFieldObject()
    {
        if (poisonFieldInstance != null || poisonFieldPrefab == null)
        {
            return;
        }

        poisonFieldInstance = Instantiate(poisonFieldPrefab, transform.position, poisonFieldPrefab.transform.rotation, transform);
        poisonFieldInstance.transform.localScale = poisonFieldPrefab.transform.localScale;
    }

    private void DestroyHazard()
    {
        if (isDestroying)
        {
            return;
        }

        isDestroying = true;

        if (poisonFieldInstance != null && Application.isPlaying)
        {
            destroyRoutine = StartCoroutine(StopPoisonFieldAndDestroyRoutine());
            return;
        }

        DestroyHazardImmediate();
    }

    private IEnumerator StopPoisonFieldAndDestroyRoutine()
    {
        float stopDelay = Random.Range(0.1f, 1f);
        yield return new WaitForSeconds(stopDelay);

        float longestRemainingLifetime = 0f;
        ParticleSystem[] particleSystems = poisonFieldInstance != null
            ? poisonFieldInstance.GetComponentsInChildren<ParticleSystem>(true)
            : null;
        if (particleSystems != null)
        {
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem particleSystem = particleSystems[index];
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.enabled = false;
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                ParticleSystem.MainModule main = particleSystem.main;
                longestRemainingLifetime = Mathf.Max(longestRemainingLifetime, main.startLifetime.constantMax);
            }
        }

        if (longestRemainingLifetime > 0f)
        {
            yield return new WaitForSeconds(longestRemainingLifetime);
        }

        DestroyHazardImmediate();
    }

    private void DestroyHazardImmediate()
    {
        if (destroyRoutine != null)
        {
            StopCoroutine(destroyRoutine);
            destroyRoutine = null;
        }

        if (board != null)
        {
            board.UnregisterHazard(this);
        }

        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }
}
