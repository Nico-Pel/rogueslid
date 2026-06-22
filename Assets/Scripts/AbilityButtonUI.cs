using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class AbilityButtonUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image abilityImage;
    [SerializeField] private GameObject componentsRoot;
    [SerializeField] private GameObject emptyRoot;
    [SerializeField] private Image abilityTypeImage;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private GameObject countRoot;
    [SerializeField] private GameObject cooldownRoot;
    [SerializeField] private TMP_Text cooldownCountLabel;
    [SerializeField] private GameObject activeIndicator;
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Sprite basicAttackTypeSprite;
    [SerializeField] private Sprite mobilityTypeSprite;
    [SerializeField] private Sprite specialPowerTypeSprite;
    private float abilityCheckLongPressDuration = 0.5f;

    private GameTurnManager gameTurnManager;
    private Character character;
    private int abilitySlotIndex = -1;
    private ActiveIndicator activeIndicatorEffect;
    private Func<AbilityButtonUI, bool> clickInterceptor;
    private Action<AbilityButtonUI> longPressCallback;
    private bool hasAnyValidTarget = true;
    private bool isPointerDown;
    private bool longPressTriggered;
    private bool suppressNextClick;
    private float pointerDownStartTime;

    public int AbilityIndex => abilitySlotIndex;
    public AbilityDefinition BoundDefinition => character != null ? character.GetAbilityForSlot(abilitySlotIndex)?.Definition : null;
    public Sprite TypeSprite => abilityTypeImage != null ? abilityTypeImage.sprite : null;
    public Sprite BasicAttackTypeSprite => basicAttackTypeSprite;
    public Sprite MobilityTypeSprite => mobilityTypeSprite;
    public Sprite SpecialPowerTypeSprite => specialPowerTypeSprite;

    private void Awake()
    {
        CacheReferences();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }
    }

    private void Update()
    {
        if (!isPointerDown || longPressTriggered || abilityCheckLongPressDuration <= 0f)
        {
            return;
        }

        if (Time.unscaledTime - pointerDownStartTime < abilityCheckLongPressDuration)
        {
            return;
        }

        longPressTriggered = true;
        suppressNextClick = true;
        longPressCallback?.Invoke(this);
    }

    public void Setup(GameTurnManager turnManager, Character boundCharacter, int index, Func<AbilityButtonUI, bool> onPrimaryClick = null, Action<AbilityButtonUI> onLongPress = null)
    {
        gameTurnManager = turnManager;
        character = boundCharacter;
        abilitySlotIndex = index;
        clickInterceptor = onPrimaryClick;
        longPressCallback = onLongPress;
        Refresh();
    }

    public void SetHasAnyValidTarget(bool value)
    {
        hasAnyValidTarget = value;
        Refresh();
    }

    public void Clear()
    {
        character = null;
        abilitySlotIndex = -1;
        clickInterceptor = null;
        longPressCallback = null;
        isPointerDown = false;
        longPressTriggered = false;
        suppressNextClick = false;
        Refresh();
    }

    public void Refresh()
    {
        CacheReferences();

        CharacterAbilityRuntime runtime = character != null ? character.GetAbilityForSlot(abilitySlotIndex) : null;
        int runtimeIndex = character != null ? character.GetRuntimeIndexForSlot(abilitySlotIndex) : -1;
        bool hasAbility = runtime != null && runtime.Definition != null;
        SetEmptyState(!hasAbility);
        string counterText = hasAbility ? runtime.Definition.GetCounterText(runtime) : string.Empty;

        bool isPlayerTurn = gameTurnManager != null && gameTurnManager.CurrentTurn == TurnSide.Player;
        bool isUsable = hasAbility && isPlayerTurn && runtime.IsUsable(character) && hasAnyValidTarget;
        bool isTargetingThis = hasAbility && gameTurnManager != null && gameTurnManager.PendingCellTargetAbilityIndex == runtimeIndex;
        bool isOnCooldown = hasAbility && runtime.RemainingCooldown > 0;
        bool showUsageCount = hasAbility && !isOnCooldown && !string.IsNullOrEmpty(counterText);

        if (button != null)
        {
            button.interactable = isUsable;
        }

        if (!hasAbility)
        {
            if (activeIndicator != null)
            {
                activeIndicator.SetActive(false);
                activeIndicatorEffect?.SetBlinking(false);
            }

            if (countRoot != null)
            {
                countRoot.SetActive(false);
            }

            if (cooldownRoot != null)
            {
                cooldownRoot.SetActive(false);
            }

            return;
        }

        Sprite runtimeIcon = runtime.Definition.GetIcon(runtime);
        if (runtimeIcon != null)
        {
            if (abilityImage != null)
            {
                abilityImage.sprite = runtimeIcon;
            }
        }

        Color targetColor = isUsable ? availableColor : disabledColor;
        if (abilityImage != null)
        {
            abilityImage.color = targetColor;
        }

        if (abilityTypeImage != null)
        {
            abilityTypeImage.sprite = GetCategorySprite(runtime.Definition.Category);
            abilityTypeImage.enabled = abilityTypeImage.sprite != null;
        }

        if (activeIndicator != null)
        {
            bool isActive = runtime.IsActive || isTargetingThis;
            activeIndicator.SetActive(isActive);
            activeIndicatorEffect?.SetBlinking(isActive);
        }

        if (countRoot != null)
        {
            countRoot.SetActive(showUsageCount);
        }

        if (countLabel != null)
        {
            countLabel.text = showUsageCount ? counterText : string.Empty;
        }

        if (cooldownRoot != null)
        {
            cooldownRoot.SetActive(isOnCooldown);
        }

        if (cooldownCountLabel != null)
        {
            cooldownCountLabel.text = isOnCooldown ? $"-{runtime.RemainingCooldown}" : string.Empty;
        }
    }

    private void HandleClicked()
    {
        if (character == null)
        {
            return;
        }

        if (suppressNextClick)
        {
            suppressNextClick = false;
            return;
        }

        if (clickInterceptor != null && clickInterceptor.Invoke(this))
        {
            return;
        }

        int runtimeIndex = character.GetRuntimeIndexForSlot(abilitySlotIndex);
        if (runtimeIndex < 0)
        {
            return;
        }

        gameTurnManager?.RequestAbilityUse(runtimeIndex);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        isPointerDown = true;
        longPressTriggered = false;
        pointerDownStartTime = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerDown = false;
    }

    private void CacheReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (abilityImage == null)
        {
            abilityImage = transform.Find("Mask/iAbility")?.GetComponent<Image>();
        }

        if (componentsRoot == null)
        {
            Transform componentsTransform = transform.Find("Components");
            if (componentsTransform != null)
            {
                componentsRoot = componentsTransform.gameObject;
            }
        }

        if (emptyRoot == null)
        {
            Transform emptyTransform = transform.Find("Empty");
            if (emptyTransform != null)
            {
                emptyRoot = emptyTransform.gameObject;
            }
        }

        if (abilityTypeImage == null)
        {
            abilityTypeImage = transform.Find("iType")?.GetComponent<Image>();
        }

        if (countRoot == null)
        {
            Transform countTransform = transform.Find("iCount");
            if (countTransform != null)
            {
                countRoot = countTransform.gameObject;
            }
        }

        if (countLabel == null)
        {
            countLabel = transform.Find("iCount/tCount")?.GetComponent<TMP_Text>();
        }

        if (cooldownRoot == null)
        {
            Transform cooldownTransform = transform.Find("iCooldown");
            if (cooldownTransform != null)
            {
                cooldownRoot = cooldownTransform.gameObject;
            }
        }

        if (cooldownCountLabel == null)
        {
            cooldownCountLabel = transform.Find("iCooldown/tCooldownCount")?.GetComponent<TMP_Text>();
        }

        if (activeIndicator == null)
        {
            Transform activeTransform = transform.Find("iActive");
            if (activeTransform == null)
            {
                activeTransform = transform.Find("ActiveIndicatior");
            }

            if (activeTransform != null)
            {
                activeIndicator = activeTransform.gameObject;
            }
        }

        if (activeIndicator != null && activeIndicatorEffect == null)
        {
            activeIndicatorEffect = activeIndicator.GetComponent<ActiveIndicator>();
            if (activeIndicatorEffect == null)
            {
                activeIndicatorEffect = activeIndicator.AddComponent<ActiveIndicator>();
            }
        }
    }

    private Sprite GetCategorySprite(AbilityCategory category)
    {
        switch (category)
        {
            case AbilityCategory.MobilitySkill:
                return mobilityTypeSprite;
            case AbilityCategory.SpecialPower:
                return specialPowerTypeSprite;
            default:
                return basicAttackTypeSprite;
        }
    }

    public Sprite GetTypeSpriteForCategory(AbilityCategory category)
    {
        return GetCategorySprite(category);
    }

    private void SetEmptyState(bool isEmpty)
    {
        if (abilityImage != null)
        {
            abilityImage.enabled = !isEmpty;
        }

        if (componentsRoot != null)
        {
            componentsRoot.SetActive(!isEmpty);
        }

        if (emptyRoot != null)
        {
            emptyRoot.SetActive(isEmpty);
        }
    }
}
