using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbilityButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image abilityImage;
    [SerializeField] private GameObject componentsRoot;
    [SerializeField] private GameObject emptyRoot;
    [SerializeField] private Image abilityTypeImage;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private GameObject countRoot;
    [SerializeField] private GameObject activeIndicator;
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Sprite basicAttackTypeSprite;
    [SerializeField] private Sprite mobilityTypeSprite;
    [SerializeField] private Sprite specialPowerTypeSprite;

    private GameTurnManager gameTurnManager;
    private Character character;
    private int abilitySlotIndex = -1;
    private ActiveIndicator activeIndicatorEffect;

    public int AbilityIndex => abilitySlotIndex;
    public AbilityDefinition BoundDefinition => character != null ? character.GetAbilityForSlot(abilitySlotIndex)?.Definition : null;
    public Sprite TypeSprite => abilityTypeImage != null ? abilityTypeImage.sprite : null;

    private void Awake()
    {
        CacheReferences();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }
    }

    public void Setup(GameTurnManager turnManager, Character boundCharacter, int index)
    {
        gameTurnManager = turnManager;
        character = boundCharacter;
        abilitySlotIndex = index;
        Refresh();
    }

    public void Clear()
    {
        character = null;
        abilitySlotIndex = -1;
        Refresh();
    }

    public void Refresh()
    {
        CacheReferences();

        CharacterAbilityRuntime runtime = character != null ? character.GetAbilityForSlot(abilitySlotIndex) : null;
        int runtimeIndex = character != null ? character.GetRuntimeIndexForSlot(abilitySlotIndex) : -1;
        bool hasAbility = runtime != null && runtime.Definition != null;
        SetEmptyState(!hasAbility);

        bool isPlayerTurn = gameTurnManager != null && gameTurnManager.CurrentTurn == TurnSide.Player;
        bool isUsable = hasAbility && isPlayerTurn && runtime.IsUsable(character);
        bool isTargetingThis = hasAbility && gameTurnManager != null && gameTurnManager.PendingCellTargetAbilityIndex == runtimeIndex;
        bool showUnlimitedCount = hasAbility
            && (runtime.Definition.UsesPerTurn > 0 || runtime.Definition.UsesPerCombat > 0 || runtime.RemainingCooldown > 0);

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

            return;
        }

        if (runtime.Definition.Icon != null)
        {
            if (abilityImage != null)
            {
                abilityImage.sprite = runtime.Definition.Icon;
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
            countRoot.SetActive(showUnlimitedCount);
        }

        if (countLabel != null)
        {
            countLabel.text = runtime.Definition.GetCounterText(runtime);
        }
    }

    private void HandleClicked()
    {
        if (character == null)
        {
            return;
        }

        SoundManager.Instance?.PlayClick();

        int runtimeIndex = character.GetRuntimeIndexForSlot(abilitySlotIndex);
        if (runtimeIndex < 0)
        {
            return;
        }

        gameTurnManager?.RequestAbilityUse(runtimeIndex);
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
