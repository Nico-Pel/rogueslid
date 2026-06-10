using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbilityButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image abilityImage;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private GameObject countRoot;
    [SerializeField] private GameObject activeIndicator;
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.5f);

    private GameTurnManager gameTurnManager;
    private Character character;
    private int abilityIndex = -1;
    private ActiveIndicator activeIndicatorEffect;

    public int AbilityIndex => abilityIndex;

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
        abilityIndex = index;
        Refresh();
    }

    public void Clear()
    {
        character = null;
        abilityIndex = -1;
        Refresh();
    }

    public void Refresh()
    {
        CacheReferences();

        CharacterAbilityRuntime runtime = character != null ? character.GetAbility(abilityIndex) : null;
        bool hasAbility = runtime != null && runtime.Definition != null;
        gameObject.SetActive(hasAbility);

        if (!hasAbility)
        {
            return;
        }

        bool isPlayerTurn = gameTurnManager != null && gameTurnManager.CurrentTurn == TurnSide.Player;
        bool isUsable = isPlayerTurn && runtime.IsUsable(character);
        bool isTargetingThis = gameTurnManager != null && gameTurnManager.PendingCellTargetAbilityIndex == abilityIndex;
        bool showUnlimitedCount = runtime.Definition.UsesPerTurn > 0 || runtime.Definition.UsesPerCombat > 0 || runtime.RemainingCooldown > 0;

        if (button != null)
        {
            button.interactable = isUsable;
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
        gameTurnManager?.RequestAbilityUse(abilityIndex);
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
}
