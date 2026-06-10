using TMPro;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIGame : MonoBehaviour
{
    [SerializeField] private GameTurnManager gameTurnManager;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TMP_Text turnLabel;
    [SerializeField] private RectTransform mobilityBar;
    [SerializeField] private GameObject mobilityIconPrefab;
    [SerializeField] private RectTransform abilitiesBar;
    [SerializeField] private Color mobilityAvailableColor = Color.white;
    [SerializeField] private Color mobilityConsumedColor = Color.black;

    private readonly List<GameObject> mobilityIcons = new List<GameObject>();
    private readonly List<AbilityButtonUI> abilityButtons = new List<AbilityButtonUI>();
    private Character observedCharacter;

    private void Awake()
    {
        if (gameTurnManager == null)
        {
            gameTurnManager = FindFirstObjectByType<GameTurnManager>();
        }

        if (endTurnButton == null)
        {
            Transform buttonTransform = transform.Find("BEndTurn");
            if (buttonTransform != null)
            {
                endTurnButton = buttonTransform.GetComponent<Button>();
            }
        }

        if (turnLabel == null && endTurnButton != null)
        {
            turnLabel = endTurnButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (mobilityBar == null)
        {
            Transform mobilityBarTransform = transform.Find("MobilityBar");
            if (mobilityBarTransform != null)
            {
                mobilityBar = mobilityBarTransform as RectTransform;
            }
        }

        if (abilitiesBar == null)
        {
            Transform abilitiesTransform = transform.Find("Footer/Abilities");
            if (abilitiesTransform == null)
            {
                abilitiesTransform = transform.Find("Abilities");
            }

            if (abilitiesTransform != null)
            {
                abilitiesBar = abilitiesTransform as RectTransform;
            }
        }

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(HandleEndTurnClicked);
            endTurnButton.onClick.AddListener(HandleEndTurnClicked);
        }

        CacheAbilityButtons();
    }

    private void OnEnable()
    {
        if (gameTurnManager != null)
        {
            gameTurnManager.TurnChanged += HandleTurnChanged;
            gameTurnManager.PendingAbilityChanged += HandlePendingAbilityChanged;
            gameTurnManager.EndTurnAvailabilityChanged += HandleEndTurnAvailabilityChanged;
            HandleTurnChanged(gameTurnManager.CurrentTurn);
        }

        BindToCurrentCharacter();
    }

    private void OnDisable()
    {
        if (gameTurnManager != null)
        {
            gameTurnManager.TurnChanged -= HandleTurnChanged;
            gameTurnManager.PendingAbilityChanged -= HandlePendingAbilityChanged;
            gameTurnManager.EndTurnAvailabilityChanged -= HandleEndTurnAvailabilityChanged;
        }

        UnbindCharacter();
    }

    private void HandleEndTurnClicked()
    {
        gameTurnManager?.RequestEndTurn();
    }

    private void HandleTurnChanged(TurnSide turnSide)
    {
        if (endTurnButton != null)
        {
            endTurnButton.interactable = turnSide == TurnSide.Player && gameTurnManager != null && gameTurnManager.CanEndTurn;
        }

        if (turnLabel != null)
        {
            turnLabel.text = turnSide == TurnSide.Player ? "END TURN" : "ENEMY TURN";
        }

        BindToCurrentCharacter();
        RefreshMobilityBar();
        RefreshAbilityButtons();
    }

    private void BindToCurrentCharacter()
    {
        Character currentCharacter = null;
        if (gameTurnManager != null)
        {
            BoardManager board = gameTurnManager.Board;
            if (board != null && board.Player != null)
            {
                currentCharacter = board.Player.ControlledCharacter;
            }
        }

        if (observedCharacter == currentCharacter)
        {
            return;
        }

        UnbindCharacter();
        observedCharacter = currentCharacter;

        if (observedCharacter != null)
        {
            observedCharacter.MovementPointsChanged += HandleMovementPointsChanged;
            observedCharacter.AbilitiesChanged += HandleAbilitiesChanged;
        }

        RebuildMobilityBar();
        RefreshAbilityButtons();
    }

    private void UnbindCharacter()
    {
        if (observedCharacter != null)
        {
            observedCharacter.MovementPointsChanged -= HandleMovementPointsChanged;
            observedCharacter.AbilitiesChanged -= HandleAbilitiesChanged;
            observedCharacter = null;
        }
    }

    private void HandleMovementPointsChanged(Character character)
    {
        RefreshMobilityBar();
    }

    private void HandleAbilitiesChanged(Character character)
    {
        RefreshAbilityButtons();
    }

    private void HandlePendingAbilityChanged(int abilityIndex)
    {
        RefreshAbilityButtons();
    }

    private void HandleEndTurnAvailabilityChanged(bool isAvailable)
    {
        if (endTurnButton != null && gameTurnManager != null)
        {
            endTurnButton.interactable = gameTurnManager.CurrentTurn == TurnSide.Player && isAvailable;
        }
    }

    private void RebuildMobilityBar()
    {
        if (mobilityBar == null)
        {
            return;
        }

        for (int index = mobilityBar.childCount - 1; index >= 0; index--)
        {
            Destroy(mobilityBar.GetChild(index).gameObject);
        }

        mobilityIcons.Clear();

        if (observedCharacter == null || mobilityIconPrefab == null)
        {
            return;
        }

        for (int index = 0; index < observedCharacter.BaseMovementPoints; index++)
        {
            GameObject icon = Instantiate(mobilityIconPrefab, mobilityBar);
            icon.name = $"iMobility_{index + 1}";
            mobilityIcons.Add(icon);
        }

        RefreshMobilityBar();
    }

    private void RefreshMobilityBar()
    {
        if (observedCharacter == null)
        {
            return;
        }

        for (int index = 0; index < mobilityIcons.Count; index++)
        {
            bool isAvailable = index < observedCharacter.RemainingMovementPoints;
            SetMobilityIconColor(mobilityIcons[index], isAvailable ? mobilityAvailableColor : mobilityConsumedColor);
        }
    }

    private void CacheAbilityButtons()
    {
        abilityButtons.Clear();
        if (abilitiesBar == null)
        {
            return;
        }

        AbilityButtonUI[] buttons = abilitiesBar
            .GetComponentsInChildren<AbilityButtonUI>(true)
            .Where(button => button.name.StartsWith("BAbility", System.StringComparison.Ordinal))
            .OrderBy(button => button.name)
            .ToArray();

        for (int index = 0; index < buttons.Length; index++)
        {
            abilityButtons.Add(buttons[index]);
        }
    }

    private void RefreshAbilityButtons()
    {
        if (abilityButtons.Count == 0)
        {
            CacheAbilityButtons();
        }

        for (int index = 0; index < abilityButtons.Count; index++)
        {
            AbilityButtonUI button = abilityButtons[index];
            if (button == null)
            {
                continue;
            }

            button.Setup(gameTurnManager, observedCharacter, index);
        }
    }

    private static void SetMobilityIconColor(GameObject icon, Color color)
    {
        if (icon == null)
        {
            return;
        }

        Graphic graphic = icon.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.color = color;
            return;
        }

        SpriteRenderer spriteRenderer = icon.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }
}
