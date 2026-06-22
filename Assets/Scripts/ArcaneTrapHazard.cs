using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

public class ArcaneTrapHazard : BoardHazard
{
    private enum TrapPhase
    {
        Warning,
        Active,
        Expired
    }

    [SerializeField] private GameObject warningFxPrefab;
    [FormerlySerializedAs("activeFxPrefab")]
    [SerializeField] private GameObject trapObjPrefab;
    [SerializeField] private float warningFxLifetime;
    [SerializeField] private float trapObjLifetime;
    [SerializeField] private float trapObjSpawnStartScale = 0.1f;
    [SerializeField] private float trapObjSpawnScaleDuration = 0.5f;
    [SerializeField] private float trapObjExpireEndScale = 0.1f;
    [SerializeField] private float trapObjExpireScaleDuration = 0.5f;
    [SerializeField] private string trapActionTriggerParameter = "Action";
    [SerializeField] private float enemyTriggerDelay = 0.15f;
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
    private GameObject trapObjInstance;
    private bool isResolving;

    public override bool IsVisibleToEnemies => phase == TrapPhase.Active;

    public void Configure(
        BoardManager targetBoard,
        Character targetOwner,
        Vector2Int targetGridPosition,
        AbilityDefinition abilityDefinition,
        GameObject warningPrefab,
        GameObject trapPrefab,
        int damage,
        int sustainStacks,
        int eruptionBonus,
        bool wave,
        bool exhaustion)
    {
        Assign(targetBoard, targetOwner, targetGridPosition);
        sourceAbility = abilityDefinition;
        warningFxPrefab = warningPrefab;
        trapObjPrefab = trapPrefab;
        baseDamage = Mathf.Max(1, damage);
        activePlayerTurnDuration = Mathf.Max(1, 3 + sustainStacks);
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
            remainingActivePlayerTurnStarts = Mathf.Max(1, activePlayerTurnDuration);
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
            BeginNaturalExpiration();
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

        enemy.BeginMovementInterrupt();
        StartCoroutine(TriggerEnemyAfterDelay(enemy));
    }

    private System.Collections.IEnumerator TriggerEnemyAfterDelay(Enemy enemy)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, enemyTriggerDelay));

        if (enemy != null)
        {
            Trigger(enemy, 0);
            enemy.EndMovementInterrupt();
        }
    }

    private void Trigger(Enemy primaryEnemy, int bonusDamage)
    {
        if (phase == TrapPhase.Expired || primaryEnemy == null || owner == null || board == null)
        {
            return;
        }

        isResolving = true;
        phase = TrapPhase.Expired;
        PlayTrapActivationAnimation();

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
        ClearVisual(ref trapObjInstance);

        switch (phase)
        {
            case TrapPhase.Warning:
                warningFxInstance = SpawnVisual(warningFxPrefab, warningFxLifetime);
                break;
            case TrapPhase.Active:
                trapObjInstance = SpawnVisual(trapObjPrefab, trapObjLifetime);
                break;
        }
    }

    private void PlayTrapActivationAnimation()
    {
        if (trapObjInstance == null || string.IsNullOrWhiteSpace(trapActionTriggerParameter))
        {
            return;
        }

        Animator trapAnimator = trapObjInstance.GetComponentInChildren<Animator>();
        if (trapAnimator == null)
        {
            return;
        }

        trapAnimator.ResetTrigger(trapActionTriggerParameter);
        trapAnimator.SetTrigger(trapActionTriggerParameter);
    }

    private GameObject SpawnVisual(GameObject prefab, float lifetime)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Instantiate(prefab, transform.position, prefab.transform.rotation, transform);
        Vector3 targetScale = prefab.transform.localScale;
        instance.transform.localScale = targetScale;

        if (prefab == trapObjPrefab)
        {
            float startScale = Mathf.Max(0f, trapObjSpawnStartScale);
            instance.transform.localScale = targetScale * startScale;
            instance.transform.DOScale(targetScale, Mathf.Max(0f, trapObjSpawnScaleDuration))
                .SetEase(Ease.OutBack);
        }

        if (lifetime > 0f && prefab != trapObjPrefab)
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
        isResolving = true;
        ClearVisual(ref warningFxInstance);
        ClearVisual(ref trapObjInstance);
        board?.UnregisterHazard(this);
        Destroy(gameObject);
    }

    private void BeginNaturalExpiration()
    {
        if (phase == TrapPhase.Expired || isResolving)
        {
            return;
        }

        phase = TrapPhase.Expired;
        isResolving = true;
        ClearVisual(ref warningFxInstance);
        board?.UnregisterHazard(this);

        if (trapObjInstance == null)
        {
            Destroy(gameObject);
            return;
        }

        Transform trapTransform = trapObjInstance.transform;
        Vector3 startScale = trapTransform.localScale;
        Vector3 endScale = startScale * Mathf.Max(0f, trapObjExpireEndScale);

        float totalDuration = Mathf.Max(0.01f, trapObjExpireScaleDuration);
        float growDuration = totalDuration * 0.35f;
        float shrinkDuration = totalDuration - growDuration;
        Vector3 overshootScale = startScale * 1.08f;

        trapTransform.DOComplete();
        DOTween.Sequence()
            .Append(trapTransform.DOScale(overshootScale, growDuration).SetEase(Ease.OutBack))
            .Append(trapTransform.DOScale(endScale, shrinkDuration).SetEase(Ease.InBack))
            .OnComplete(() =>
            {
                if (trapObjInstance != null)
                {
                    Destroy(trapObjInstance);
                    trapObjInstance = null;
                }

                Destroy(gameObject);
            })
            .OnKill(() =>
            {
                if (trapObjInstance != null)
                {
                    Destroy(trapObjInstance);
                    trapObjInstance = null;
                }

                if (this != null)
                {
                    Destroy(gameObject);
                }
            });
    }
}
