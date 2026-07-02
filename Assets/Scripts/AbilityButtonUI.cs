using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class AbilityButtonUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private enum BoundAbilityMode
    {
        Standard,
        Bonus
    }

    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image abilityImage;
    [SerializeField] private Image outlineImage;
    [SerializeField] private Image typeOutlineImage;
    [SerializeField] private GameObject componentsRoot;
    [SerializeField] private GameObject emptyRoot;
    [SerializeField] private Image countBackgroundImage;
    [SerializeField] private Image emptyBackgroundImage;
    [SerializeField] private Image abilityTypeImage;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private GameObject countRoot;
    [SerializeField] private GameObject cooldownRoot;
    [SerializeField] private TMP_Text cooldownCountLabel;
    [SerializeField] private GameObject activeIndicator;
    [SerializeField] private GameObject fadeIndicator;
    [SerializeField] private Image fadeIndicatorImage;
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color heartRipperExecuteFadeColor = new Color(1f, 0f, 0.02745098f, 0.6666667f);
    [SerializeField] private Color whisperfangLuckyFadeColor = new Color(1f, 0.8627451f, 0.15294118f, 0.6666667f);
    [SerializeField] private float fadePulseMinAlpha = 0.1f;
    [SerializeField] private float fadePulseMaxAlpha = 0.4f;
    [SerializeField] private float fadePulseDuration = 0.5f;
    [SerializeField] private float countBounceScaleMultiplier = 1.2f;
    [SerializeField] private float countBounceDuration = 0.28f;
    [SerializeField] private Sprite basicAttackTypeSprite;
    [SerializeField] private Sprite mobilityTypeSprite;
    [SerializeField] private Sprite specialPowerTypeSprite;
    [SerializeField] private Sprite potionTypeSprite;
    [SerializeField] private Sprite occupiedBackgroundSprite;
    [SerializeField] private Sprite emptyBackgroundSprite;
    [SerializeField] private float emptyBonusBackgroundAlpha = 0.15f;
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
    private bool wasWaitingForReuseDelay;
    private Tween fadePulseTween;
    private Tween countBounceTween;
    private Color currentFadePulseColor;
    private int lastSeenBonusTurnUseGainVersion = -1;
    private BoundAbilityMode boundAbilityMode = BoundAbilityMode.Standard;
    private static Sprite cachedDefaultBonusEmptySprite;
    private static Sprite cachedDefaultBonusOccupiedSprite;
    private static Sprite cachedPotionTypeSprite;

    public int AbilityIndex => abilitySlotIndex;
    public AbilityDefinition BoundDefinition
    {
        get
        {
            CharacterAbilityRuntime runtime = GetBoundRuntime();
            if (runtime?.Definition == null)
            {
                return null;
            }

            return runtime.Definition.GetPresentationDefinition(character, runtime) ?? runtime.Definition;
        }
    }
    public Sprite TypeSprite => abilityTypeImage != null ? abilityTypeImage.sprite : null;
    public Sprite BasicAttackTypeSprite => basicAttackTypeSprite;
    public Sprite MobilityTypeSprite => mobilityTypeSprite;
    public Sprite SpecialPowerTypeSprite => specialPowerTypeSprite;
    public bool IsBonusSlot => boundAbilityMode == BoundAbilityMode.Bonus;

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
        CharacterAbilityRuntime runtime = GetBoundRuntime();
        bool isWaitingForReuseDelay = runtime != null && Time.time < runtime.NextReusableTime;
        if (wasWaitingForReuseDelay && !isWaitingForReuseDelay)
        {
            Refresh();
        }

        wasWaitingForReuseDelay = isWaitingForReuseDelay;

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
        boundAbilityMode = BoundAbilityMode.Standard;
        clickInterceptor = onPrimaryClick;
        longPressCallback = onLongPress;
        wasWaitingForReuseDelay = false;
        lastSeenBonusTurnUseGainVersion = -1;
        Refresh();
    }

    public void SetupBonus(GameTurnManager turnManager, Character boundCharacter, int index, Func<AbilityButtonUI, bool> onPrimaryClick = null, Action<AbilityButtonUI> onLongPress = null)
    {
        gameTurnManager = turnManager;
        character = boundCharacter;
        abilitySlotIndex = index;
        boundAbilityMode = BoundAbilityMode.Bonus;
        clickInterceptor = onPrimaryClick;
        longPressCallback = onLongPress;
        wasWaitingForReuseDelay = false;
        lastSeenBonusTurnUseGainVersion = -1;
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
        boundAbilityMode = BoundAbilityMode.Standard;
        wasWaitingForReuseDelay = false;
        lastSeenBonusTurnUseGainVersion = -1;
        StopCountBounce();
        Refresh();
    }

    public void Refresh()
    {
        CacheReferences();

        CharacterAbilityRuntime runtime = GetBoundRuntime();
        int runtimeIndex = character != null && boundAbilityMode == BoundAbilityMode.Standard
            ? character.GetRuntimeIndexForSlot(abilitySlotIndex)
            : -1;
        bool hasAbility = runtime != null && runtime.Definition != null;
        AbilityDefinition presentationDefinition = hasAbility
            ? (runtime.Definition.GetPresentationDefinition(character, runtime) ?? runtime.Definition)
            : null;
        SetEmptyState(!hasAbility);
        string counterText = hasAbility ? runtime.Definition.GetCounterText(runtime) : string.Empty;

        bool isPlayerTurn = gameTurnManager != null && gameTurnManager.CurrentTurn == TurnSide.Player;
        bool isUsable = hasAbility && isPlayerTurn && runtime.IsUsable(character) && hasAnyValidTarget;
        bool isTargetingThis = hasAbility && gameTurnManager != null && (
            boundAbilityMode == BoundAbilityMode.Bonus
                ? gameTurnManager.PendingBonusAbilitySlot == abilitySlotIndex
                : gameTurnManager.PendingCellTargetAbilityIndex == runtimeIndex);
        bool isOnCooldown = hasAbility && runtime.RemainingCooldown > 0;
        bool showActiveCounter = hasAbility && runtime.IsActive && !string.IsNullOrEmpty(counterText);
        bool showUsageCount = hasAbility && !string.IsNullOrEmpty(counterText) && (!isOnCooldown || showActiveCounter);
        bool showCooldown = isOnCooldown && !showActiveCounter;
        bool showHeartRipperExecuteFade = hasAbility
            && isPlayerTurn
            && runtime.IsUsable(character)
            && runtime.Definition is HeartRipperAbility heartRipper
            && heartRipper.HasExecutableHealOpportunity(character, runtime);
        bool showDemonbaneFallbackFade = hasAbility
            && isPlayerTurn
            && runtime.IsUsable(character)
            && runtime.Definition is DemonbaneAbility demonbane
            && demonbane.HasFallbackCastOpportunity(character, runtime);
        bool showWhisperfangLuckyFade = hasAbility
            && isPlayerTurn
            && runtime.IsUsable(character)
            && runtime.Definition is WhisperfangAbility whisperfang
            && whisperfang.IsLuckyBoltPrimed(character, runtime);
        bool shouldBounceCount = hasAbility
            && showUsageCount
            && runtime.BonusTurnUseGainVersion > 0
            && lastSeenBonusTurnUseGainVersion >= 0
            && runtime.BonusTurnUseGainVersion != lastSeenBonusTurnUseGainVersion;

        if (hasAbility)
        {
            if (lastSeenBonusTurnUseGainVersion < 0)
            {
                lastSeenBonusTurnUseGainVersion = runtime.BonusTurnUseGainVersion;
            }
        }
        else
        {
            lastSeenBonusTurnUseGainVersion = -1;
        }

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

            if (fadeIndicator != null)
            {
                SetFadeIndicatorVisible(false, heartRipperExecuteFadeColor);
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
            abilityTypeImage.sprite = GetTypeSprite(presentationDefinition, runtime);
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

        if (hasAbility)
        {
            lastSeenBonusTurnUseGainVersion = runtime.BonusTurnUseGainVersion;
        }

        if (cooldownRoot != null)
        {
            cooldownRoot.SetActive(showCooldown);
        }

        if (cooldownCountLabel != null)
        {
            cooldownCountLabel.text = showCooldown ? runtime.RemainingCooldown.ToString() : string.Empty;
        }

        if (showWhisperfangLuckyFade)
        {
            SetFadeIndicatorVisible(true, whisperfangLuckyFadeColor);
        }
        else
        {
            SetFadeIndicatorVisible(showHeartRipperExecuteFade || showDemonbaneFallbackFade, heartRipperExecuteFadeColor);
        }

        if (shouldBounceCount)
        {
            PlayCountBounce();
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

        if (boundAbilityMode == BoundAbilityMode.Bonus)
        {
            gameTurnManager?.RequestBonusAbilityUse(abilitySlotIndex);
            return;
        }

        int runtimeIndex = character.GetRuntimeIndexForSlot(abilitySlotIndex);
        if (runtimeIndex >= 0)
        {
            gameTurnManager?.RequestAbilityUse(runtimeIndex);
        }
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

    private void OnDisable()
    {
        StopFadePulse();
        StopCountBounce();
    }

    public void ApplyTheme(Color backgroundColor, Color outlineColor, Color countBackgroundColor, Color typeIconColor, Color typeOutlineColor)
    {
        CacheReferences();
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }

        if (outlineImage != null)
        {
            outlineImage.color = outlineColor;
        }

        if (countBackgroundImage != null)
        {
            countBackgroundImage.color = countBackgroundColor;
        }

        if (abilityTypeImage != null)
        {
            abilityTypeImage.color = typeIconColor;
        }

        if (typeOutlineImage != null)
        {
            typeOutlineImage.color = typeOutlineColor;
        }

        if (emptyBackgroundImage != null)
        {
            emptyBackgroundImage.color = backgroundColor;
        }
    }

    private void CacheReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (occupiedBackgroundSprite == null)
        {
            occupiedBackgroundSprite = GetSpriteByName("Button_Shape", ref cachedDefaultBonusOccupiedSprite);
        }

        if (emptyBackgroundSprite == null)
        {
            emptyBackgroundSprite = GetSpriteByName("BT_GType_Select", ref cachedDefaultBonusEmptySprite);
        }

        if (potionTypeSprite == null)
        {
            potionTypeSprite = GetSpriteByName("iPot", ref cachedPotionTypeSprite);
        }

        if (abilityImage == null)
        {
            abilityImage = transform.Find("Mask/iAbility")?.GetComponent<Image>();
        }

        if (outlineImage == null)
        {
            outlineImage = transform.Find("Mask/iOutline")?.GetComponent<Image>();
        }

        if (typeOutlineImage == null)
        {
            typeOutlineImage = transform.Find("Components/iPowerPlace/iOutline")?.GetComponent<Image>();
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

        if (countBackgroundImage == null)
        {
            countBackgroundImage = transform.Find("Components/iCount")?.GetComponent<Image>();
        }

        if (emptyBackgroundImage == null && emptyRoot != null)
        {
            emptyBackgroundImage = emptyRoot.GetComponent<Image>();
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

        if (fadeIndicator == null)
        {
            Transform fadeTransform = transform.Find("iFade");
            if (fadeTransform != null)
            {
                fadeIndicator = fadeTransform.gameObject;
            }
        }

        if (fadeIndicatorImage == null && fadeIndicator != null)
        {
            fadeIndicatorImage = fadeIndicator.GetComponent<Image>();
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

    private Sprite GetTypeSprite(AbilityDefinition presentationDefinition, CharacterAbilityRuntime runtime)
    {
        AbilityButtonTypeIconKind iconKind = presentationDefinition != null
            ? presentationDefinition.GetButtonTypeIconKind(character, runtime)
            : AbilityButtonTypeIconKind.Weapon;

        return iconKind switch
        {
            AbilityButtonTypeIconKind.Mobility => mobilityTypeSprite,
            AbilityButtonTypeIconKind.Power => specialPowerTypeSprite,
            AbilityButtonTypeIconKind.Potion => potionTypeSprite,
            _ => basicAttackTypeSprite
        };
    }

    public Sprite GetTypeSpriteForCategory(AbilityCategory category)
    {
        return GetCategorySprite(category);
    }

    public Sprite GetTypeSpriteForKind(AbilityButtonTypeIconKind iconKind)
    {
        return iconKind switch
        {
            AbilityButtonTypeIconKind.Mobility => mobilityTypeSprite,
            AbilityButtonTypeIconKind.Power => specialPowerTypeSprite,
            AbilityButtonTypeIconKind.Potion => potionTypeSprite,
            _ => basicAttackTypeSprite
        };
    }

    private void SetEmptyState(bool isEmpty)
    {
        if (boundAbilityMode == BoundAbilityMode.Bonus)
        {
            if (abilityImage != null)
            {
                abilityImage.enabled = !isEmpty;
            }

            if (componentsRoot != null)
            {
                componentsRoot.SetActive(!isEmpty);
            }

            if (backgroundImage != null)
            {
                backgroundImage.sprite = isEmpty && emptyBackgroundSprite != null
                    ? emptyBackgroundSprite
                    : occupiedBackgroundSprite != null ? occupiedBackgroundSprite : backgroundImage.sprite;

                Color color = backgroundImage.color;
                color.a = isEmpty ? emptyBonusBackgroundAlpha : 1f;
                backgroundImage.color = color;
            }

            if (emptyRoot != null)
            {
                emptyRoot.SetActive(false);
            }

            return;
        }

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

    private CharacterAbilityRuntime GetBoundRuntime()
    {
        if (character == null)
        {
            return null;
        }

        return boundAbilityMode == BoundAbilityMode.Bonus
            ? character.GetBonusAbility(abilitySlotIndex)
            : character.GetAbilityForSlot(abilitySlotIndex);
    }

    private static Sprite GetSpriteByName(string spriteName, ref Sprite cache)
    {
        if (cache != null || string.IsNullOrWhiteSpace(spriteName))
        {
            return cache;
        }

        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int index = 0; index < sprites.Length; index++)
        {
            Sprite sprite = sprites[index];
            if (sprite != null && sprite.name == spriteName)
            {
                cache = sprite;
                break;
            }
        }

        return cache;
    }

    private void SetFadeIndicatorVisible(bool isVisible, Color fadeColor)
    {
        if (fadeIndicatorImage != null)
        {
            Color appliedColor = fadeColor;
            appliedColor.a = isVisible ? Mathf.Clamp(fadePulseMaxAlpha, 0f, 1f) : 0f;
            fadeIndicatorImage.color = appliedColor;
        }

        if (fadeIndicator != null && fadeIndicator.activeSelf != isVisible)
        {
            fadeIndicator.SetActive(isVisible);
        }

        if (isVisible)
        {
            StartFadePulse(fadeColor);
        }
        else
        {
            StopFadePulse();
        }
    }

    private void PlayCountBounce()
    {
        if (countRoot == null)
        {
            return;
        }

        Transform bounceTarget = countRoot.transform;
        StopCountBounce();
        bounceTarget.localScale = Vector3.one;
        countBounceTween = bounceTarget
            .DOPunchScale(Vector3.one * Mathf.Max(0f, countBounceScaleMultiplier - 1f), Mathf.Max(0.05f, countBounceDuration), 1, 0.5f)
            .SetUpdate(true)
            .OnKill(() =>
            {
                if (bounceTarget != null)
                {
                    bounceTarget.localScale = Vector3.one;
                }

                countBounceTween = null;
            });
    }

    private void StopCountBounce()
    {
        if (countBounceTween != null)
        {
            countBounceTween.Kill();
            countBounceTween = null;
        }

        if (countRoot != null)
        {
            countRoot.transform.localScale = Vector3.one;
        }
    }

    private void SetFadeIndicatorVisible(bool isVisible)
    {
        SetFadeIndicatorVisible(isVisible, heartRipperExecuteFadeColor);
    }

    private void StartFadePulse(Color fadeColor)
    {
        if (fadeIndicatorImage == null)
        {
            return;
        }

        float minAlpha = Mathf.Clamp01(Mathf.Min(fadePulseMinAlpha, fadePulseMaxAlpha));
        float maxAlpha = Mathf.Clamp01(Mathf.Max(fadePulseMinAlpha, fadePulseMaxAlpha));
        float duration = Mathf.Max(0.01f, fadePulseDuration);

        StopFadePulse();
        currentFadePulseColor = fadeColor;
        Color baseColor = currentFadePulseColor;
        baseColor.a = minAlpha;
        fadeIndicatorImage.color = baseColor;
        fadePulseTween = fadeIndicatorImage
            .DOFade(maxAlpha, duration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void StopFadePulse()
    {
        fadePulseTween?.Kill();
        fadePulseTween = null;
    }
}
