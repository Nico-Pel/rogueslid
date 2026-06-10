using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Character : MonoBehaviour
{
    [Header("Core Stats")]
    [SerializeField] private int maxHealth = 10;
    [SerializeField] [ReadOnly] private int currentHealth = 10;
    [SerializeField] private int bonusDamage;
    [SerializeField] private int resistance;
    [SerializeField] private int movementPointsPerTurn = 2;
    [SerializeField] private List<AbilityDefinition> startingAbilities = new List<AbilityDefinition>();

    [Header("Board")]
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private float moveDuration = 0.18f;
    [SerializeField] private float spawnHeight = 0.08f;
    [SerializeField] private Image hpFillBar;

    private Renderer[] renderers;
    private Color[] baseColors;
    private Tween moveTween;
    private int remainingMovementPoints;
    private readonly List<CharacterAbilityRuntime> abilities = new List<CharacterAbilityRuntime>();
    private readonly List<Enemy> traversedEnemiesBuffer = new List<Enemy>();
    private RendererBlinkFeedback blinkFeedback;

    public Vector2Int GridPosition => gridPosition;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public int BonusDamage => bonusDamage;
    public int Resistance => resistance;
    public int BaseMovementPoints => movementPointsPerTurn;
    public int RemainingMovementPoints => remainingMovementPoints;
    public bool CanAct => remainingMovementPoints > 0 && !IsMoving;
    public bool IsMoving { get; private set; }
    public Player Owner { get; private set; }
    public BoardManager Board { get; private set; }
    public event Action<Character> MovementPointsChanged;
    public event Action<Character> AbilitiesChanged;
    public IReadOnlyList<CharacterAbilityRuntime> Abilities => abilities;

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
    }

    public void Assign(Player owner, Vector2Int spawnGridPosition, BoardManager board)
    {
        Owner = owner;
        Board = board;
        gridPosition = spawnGridPosition;
        currentHealth = maxHealth;
        CacheRenderers();
        blinkFeedback = GetComponent<RendererBlinkFeedback>();
        CacheHpBar();
        InitializeAbilities();
        SnapToGrid();
        ResetTurn();
        RefreshHpBar();
        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
    }

    public void ResetTurn()
    {
        remainingMovementPoints = movementPointsPerTurn;
        for (int index = 0; index < abilities.Count; index++)
        {
            abilities[index].BeginTurn();
        }

        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
    }

    public bool TrySlide(Vector2Int direction)
    {
        if (Board == null || remainingMovementPoints <= 0 || IsMoving || direction == Vector2Int.zero)
        {
            return false;
        }

        traversedEnemiesBuffer.Clear();
        Vector2Int destination = Board.GetSlideDestination(
            gridPosition,
            direction,
            CanTraverseUnits(),
            traversedEnemiesBuffer);

        if (destination == gridPosition)
        {
            return false;
        }

        if (!Board.MoveOccupant(gridPosition, destination, BoardOccupantKind.PlayerCharacter))
        {
            return false;
        }

        gridPosition = destination;
        remainingMovementPoints--;
        NotifyMovementPointsChanged();
        ApplyTraversalEffects();
        AnimateToGrid();
        return true;
    }

    public CharacterAbilityRuntime GetAbility(int abilityIndex)
    {
        return abilityIndex >= 0 && abilityIndex < abilities.Count ? abilities[abilityIndex] : null;
    }

    public bool TryUseAbility(int abilityIndex, Vector2Int? targetCell = null)
    {
        CharacterAbilityRuntime runtime = GetAbility(abilityIndex);
        if (runtime == null || IsMoving)
        {
            return false;
        }

        bool success = runtime.TryUse(this, targetCell);
        if (!success)
        {
            return false;
        }

        NotifyMovementPointsChanged();
        NotifyAbilitiesChanged();
        return true;
    }

    public bool TryTeleportTo(Vector2Int targetCell)
    {
        if (Board == null || targetCell == gridPosition)
        {
            return false;
        }

        if (!Board.IsCellWalkable(targetCell))
        {
            return false;
        }

        if (!Board.MoveOccupant(gridPosition, targetCell, BoardOccupantKind.PlayerCharacter))
        {
            return false;
        }

        gridPosition = targetCell;
        AnimateToGrid();
        return true;
    }

    public void ConsumeMovementPoint()
    {
        if (remainingMovementPoints <= 0)
        {
            return;
        }

        remainingMovementPoints--;
        NotifyMovementPointsChanged();
    }

    public int DealDamageToEnemy(Enemy enemy, int baseDamage, bool addBonusDamage)
    {
        if (enemy == null)
        {
            return 0;
        }

        int totalDamage = Mathf.Max(1, baseDamage + (addBonusDamage ? bonusDamage : 0));
        return enemy.TakeDamage(totalDamage);
    }

    public int TakeDamage(int incomingDamage)
    {
        int finalDamage = Mathf.Max(1, incomingDamage - resistance);
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);

        blinkFeedback?.Blink(Color.red, 0.5f, 0.12f);
        cam.Instance?.CamShake(finalDamage);
        RefreshHpBar();

        return finalDamage;
    }

    public void SetSelected(bool isSelected)
    {
        CacheRenderers();
        if (renderers == null)
        {
            return;
        }

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer targetRenderer = renderers[index];
            if (targetRenderer == null || targetRenderer.material == null)
            {
                continue;
            }

            targetRenderer.material.color = isSelected
                ? Color.Lerp(baseColors[index], Color.white, 0.35f)
                : baseColors[index];
        }
    }

    private void SnapToGrid()
    {
        if (Board == null)
        {
            return;
        }

        transform.position = Board.GridToWorldPosition(gridPosition) + Vector3.up * spawnHeight;
    }

    private void AnimateToGrid()
    {
        if (Board == null)
        {
            return;
        }

        moveTween?.Kill();
        IsMoving = true;
        Vector3 targetPosition = Board.GridToWorldPosition(gridPosition) + Vector3.up * spawnHeight;
        moveTween = transform.DOMove(targetPosition, moveDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                IsMoving = false;
                moveTween = null;
            });
    }

    private void CacheRenderers()
    {
        if (renderers != null)
        {
            return;
        }

        renderers = GetComponentsInChildren<Renderer>(true);
        baseColors = new Color[renderers.Length];
        for (int index = 0; index < renderers.Length; index++)
        {
            baseColors[index] = renderers[index] != null && renderers[index].material != null
                ? renderers[index].material.color
                : Color.white;
        }
    }

    private void OnDisable()
    {
        moveTween?.Kill();
        IsMoving = false;
    }

    private void CacheHpBar()
    {
        if (hpFillBar != null)
        {
            return;
        }

        Transform hpFillTransform = transform.Find("Canvas/hpBar/hpFillBar");
        if (hpFillTransform != null)
        {
            hpFillBar = hpFillTransform.GetComponent<Image>();
        }
    }

    private void RefreshHpBar()
    {
        if (hpFillBar == null)
        {
            return;
        }

        float fillRatio = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        hpFillBar.fillAmount = Mathf.Clamp01(fillRatio);
    }

    private void InitializeAbilities()
    {
        abilities.Clear();

        for (int index = 0; index < startingAbilities.Count; index++)
        {
            AbilityDefinition definition = startingAbilities[index];
            if (definition == null)
            {
                continue;
            }

            CharacterAbilityRuntime runtime = new CharacterAbilityRuntime(definition);
            runtime.ResetCombat();
            abilities.Add(runtime);
        }
    }

    private bool CanTraverseUnits()
    {
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            if (runtime.Definition != null && runtime.Definition.AllowsUnitTraversal(this, runtime))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyTraversalEffects()
    {
        if (traversedEnemiesBuffer.Count == 0)
        {
            return;
        }

        int traversalDamage = 0;
        for (int index = 0; index < abilities.Count; index++)
        {
            CharacterAbilityRuntime runtime = abilities[index];
            if (runtime.Definition == null)
            {
                continue;
            }

            traversalDamage = Mathf.Max(traversalDamage, runtime.Definition.GetTraversalDamage(this, runtime));
        }

        if (traversalDamage <= 0)
        {
            return;
        }

        HashSet<Enemy> damagedEnemies = new HashSet<Enemy>();
        for (int index = 0; index < traversedEnemiesBuffer.Count; index++)
        {
            Enemy enemy = traversedEnemiesBuffer[index];
            if (enemy == null || !damagedEnemies.Add(enemy))
            {
                continue;
            }

            enemy.TakeDamage(traversalDamage);
        }
    }

    private void NotifyMovementPointsChanged()
    {
        MovementPointsChanged?.Invoke(this);
    }

    private void NotifyAbilitiesChanged()
    {
        AbilitiesChanged?.Invoke(this);
    }
}
