using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class ArcaneTrapHazard : BoardHazard
{
    private enum TrapPhase
    {
        Warning,
        Active,
        Expired
    }

    [SerializeField] private GameObject warningFxPrefab;
    [SerializeField] private GameObject activeFxPrefab;
    [SerializeField] private float warningFxLifetime;
    [SerializeField] private float activeFxLifetime;
    [SerializeField] private int baseDamage = 4;
    [SerializeField] private int activePlayerTurnDuration = 1;
    [SerializeField] private int eruptionBonusDamage;
    [SerializeField] private bool waveTrigger;
    [SerializeField] private bool applyExhaustion;

    private AbilityDefinition sourceAbility;
    private TrapPhase phase = TrapPhase.Warning;
    private int playerTurnStartsUntilActivation = 1;
    private int remainingActivePlayerTurnStarts = 1;
    private GameObject warningFxInstance;
    private GameObject activeFxInstance;
    private bool isResolving;

    public override bool IsVisibleToEnemies => phase == TrapPhase.Active;

    public void Configure(
        BoardManager targetBoard,
        Character targetOwner,
        Vector2Int targetGridPosition,
        AbilityDefinition abilityDefinition,
        GameObject warningPrefab,
        GameObject activePrefab,
        int damage,
        int sustainStacks,
        int eruptionBonus,
        bool wave,
        bool exhaustion)
    {
        Assign(targetBoard, targetOwner, targetGridPosition);
        sourceAbility = abilityDefinition;
        warningFxPrefab = warningPrefab;
        activeFxPrefab = activePrefab;
        baseDamage = Mathf.Max(1, damage);
        activePlayerTurnDuration = Mathf.Max(1, 1 + sustainStacks);
        eruptionBonusDamage = Mathf.Max(0, eruptionBonus);
        waveTrigger = wave;
        applyExhaustion = exhaustion;
        playerTurnStartsUntilActivation = 1;
        remainingActivePlayerTurnStarts = activePlayerTurnDuration;
        phase = TrapPhase.Warning;

        transform.SetParent(targetBoard.transform, true);
        transform.position = targetBoard.GridToWorldPosition(targetGridPosition);
        targetBoard.RegisterHazard(this, targetGridPosition);
        RefreshVisuals();
    }

    public override void HandlePlayerTurnStarted()
    {
        if (phase == TrapPhase.Expired || isResolving)
        {
            return;
        }

        if (phase == TrapPhase.Warning)
        {
            playerTurnStartsUntilActivation = Mathf.Max(0, playerTurnStartsUntilActivation - 1);
            if (playerTurnStartsUntilActivation > 0)
            {
                return;
            }

            phase = TrapPhase.Active;
            RefreshVisuals();

            if (board != null && board.TryGetEnemy(gridPosition, out Enemy enemy) && enemy != null)
            {
                Trigger(enemy, eruptionBonusDamage);
            }

            return;
        }

        remainingActivePlayerTurnStarts = Mathf.Max(0, remainingActivePlayerTurnStarts - 1);
        if (remainingActivePlayerTurnStarts <= 0)
        {
            DestroyHazard();
        }
    }

    public override int GetEnemyPathPenalty(Enemy enemy)
    {
        return phase == TrapPhase.Active ? 5 : 0;
    }

    public override bool WouldKillEnemy(Enemy enemy)
    {
        if (phase != TrapPhase.Active || enemy == null)
        {
            return false;
        }

        int estimatedDamage = Mathf.Max(1, baseDamage - enemy.Resistance);
        return enemy.CurrentHealth <= estimatedDamage;
    }

    public override void HandleEnemyEntered(Enemy enemy)
    {
        if (phase != TrapPhase.Active || enemy == null || isResolving)
        {
            return;
        }

        Trigger(enemy, 0);
    }

    private void Trigger(Enemy primaryEnemy, int bonusDamage)
    {
        if (phase == TrapPhase.Expired || primaryEnemy == null || owner == null || board == null)
        {
            return;
        }

        isResolving = true;
        phase = TrapPhase.Expired;

        int totalDamage = baseDamage + Mathf.Max(0, bonusDamage);
        HashSet<Enemy> affectedEnemies = new HashSet<Enemy>();
        ApplyTrapEffects(primaryEnemy, totalDamage, affectedEnemies);

        if (waveTrigger)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    Vector2Int cell = gridPosition + new Vector2Int(offsetX, offsetY);
                    if (!board.TryGetEnemy(cell, out Enemy nearbyEnemy) || nearbyEnemy == null || affectedEnemies.Contains(nearbyEnemy))
                    {
                        continue;
                    }

                    ApplyTrapEffects(nearbyEnemy, totalDamage, affectedEnemies);
                }
            }
        }

        DestroyHazard();
    }

    private void ApplyTrapEffects(Enemy enemy, int damage, HashSet<Enemy> affectedEnemies)
    {
        if (enemy == null || !affectedEnemies.Add(enemy))
        {
            return;
        }

        int appliedDamage = owner.DealDamageToEnemy(enemy, damage, true, true, DamageSoundType.MagicHit, sourceAbility);
        if (appliedDamage > 0 && applyExhaustion)
        {
            enemy.ApplyForceModifierUntilEndOfCombat(-1);
        }
    }

    private void RefreshVisuals()
    {
        ClearVisual(ref warningFxInstance);
        ClearVisual(ref activeFxInstance);

        switch (phase)
        {
            case TrapPhase.Warning:
                warningFxInstance = SpawnVisual(warningFxPrefab, warningFxLifetime);
                break;
            case TrapPhase.Active:
                activeFxInstance = SpawnVisual(activeFxPrefab, activeFxLifetime);
                break;
        }
    }

    private GameObject SpawnVisual(GameObject prefab, float lifetime)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Instantiate(prefab, transform.position, prefab.transform.rotation, transform);
        instance.transform.localScale = prefab.transform.localScale;
        if (lifetime > 0f)
        {
            Destroy(instance, lifetime);
        }

        return instance;
    }

    private void ClearVisual(ref GameObject visualInstance)
    {
        if (visualInstance == null)
        {
            return;
        }

        Destroy(visualInstance);
        visualInstance = null;
    }

    private void DestroyHazard()
    {
        phase = TrapPhase.Expired;
        ClearVisual(ref warningFxInstance);
        ClearVisual(ref activeFxInstance);
        board?.UnregisterHazard(this);
        Destroy(gameObject);
    }
}
