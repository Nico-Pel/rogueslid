using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ZhuangAbility : AbilityDefinition
{
    [Header("Zhuang Setup References")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GameObject castFxPrefab;
    [SerializeField] private GameObject secondaryFxPrefab;
    [SerializeField] private GameObject impactFxPrefab;
    [SerializeField] private GameObject persistentFxPrefab;
    [SerializeField] private GameObject markFxPrefab;
    [SerializeField] private GameObject markBurstFxPrefab;
    [SerializeField] private GameObject castSoundParametersPrefab;
    [SerializeField] private GameObject impactSoundParametersPrefab;
    [SerializeField] private GameObject secondarySoundParametersPrefab;
    [SerializeField] private AudioClip markBurstSound;

    public GameObject ProjectilePrefab => projectilePrefab;
    public GameObject CastFxPrefab => castFxPrefab;
    public GameObject SecondaryFxPrefab => secondaryFxPrefab;
    public GameObject ImpactFxPrefab => impactFxPrefab;
    public GameObject PersistentFxPrefab => persistentFxPrefab;
    public GameObject MarkFxPrefab => markFxPrefab;
    public GameObject MarkBurstFxPrefab => markBurstFxPrefab;
    public GameObject CastSoundParametersPrefab => castSoundParametersPrefab;
    public GameObject ImpactSoundParametersPrefab => impactSoundParametersPrefab;
    public GameObject SecondarySoundParametersPrefab => secondarySoundParametersPrefab;
    public AudioClip MarkBurstSound => markBurstSound;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] protected float motionDuration = 0.25f;
    [SerializeField, Min(0f)] protected float followupDelay = 0.2f;
    [SerializeField] protected AnimationClip secondaryAnimation;
    [SerializeField] protected AnimationClip landingAnimation;

    public override AbilityTargetingMode TargetingMode => AbilityTargetingMode.FreeCell;

    protected static int Stacks(Character character, AbilityUpgradeKey key) => character != null ? character.GetUpgradeStacks(key) : 0;

    public virtual void OnEnemyLaunched(Character character, CharacterAbilityRuntime runtime, Enemy enemy, bool allowMomentum) { }

    protected void Cast(Character character)
    {
        ZhuangAbilityUtils.Fx(castFxPrefab, character.transform.position, character.transform.rotation);
        ZhuangAbilityUtils.Sound(castSoundParametersPrefab, character.transform.position);
        PlayConfiguredFx(character);
    }

    protected int Hit(Character character, Enemy enemy, int damage, bool bonus = true)
    {
        if (enemy == null || enemy.CurrentHealth <= 0) return 0;
        Vector3 position = enemy.EffectAnchor.position;
        int dealt = character.DealDamageToEnemy(enemy, damage, bonus, true, DamageSoundType.MagicHit, this);
        ZhuangAbilityUtils.Fx(impactFxPrefab, position);
        ZhuangAbilityUtils.Sound(impactSoundParametersPrefab, position);
        return dealt;
    }

    protected void Secondary(Character character, Vector3 position)
    {
        ZhuangAbilityUtils.Fx(secondaryFxPrefab, position);
        ZhuangAbilityUtils.Sound(secondarySoundParametersPrefab, position);
    }

    protected void Launch(Character character, Enemy enemy, bool momentum = true)
    {
        ZhuangAbilityUtils.Launch(character, enemy, persistentFxPrefab, secondarySoundParametersPrefab, momentum);
    }

    protected void Run(Character character, IEnumerator action) => character.StartCoroutine(ZhuangAbilityUtils.LockedAction(character, action));

    protected IEnumerator Move(Character character, Vector2Int destination, bool leap)
    {
        if (character.GridPosition == destination)
        {
            character.PlayImpactBump(character.transform.position - character.transform.forward, motionDuration);
            yield return new WaitForSeconds(motionDuration);
            yield break;
        }
        if (!character.TryTeleportTo(destination, motionDuration, leap ? 0.8f : 0f)) yield break;
        yield return new WaitForSeconds(motionDuration);
    }

    protected List<Enemy> Nearby(Character character, Vector2Int center, int radius) => ZhuangAbilityUtils.EnemiesInRadius(character.Board, center, radius);

    public override bool CanActivate(Character character, CharacterAbilityRuntime runtime)
    {
        if (character == null || runtime == null || character.Board == null) return false;
        for (int x = 0; x < character.Board.Width; x++)
        for (int y = 0; y < character.Board.Height; y++)
            if (CanActivateOnCell(character, runtime, new Vector2Int(x, y))) return true;
        return false;
    }

    public override bool CanShowPotentialTargetCell(Character character, CharacterAbilityRuntime runtime, Vector2Int targetCell)
        => CanActivateOnCell(character, runtime, targetCell);
}
