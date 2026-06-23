using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

public readonly struct ItemIconTheme
{
    public readonly Color BackgroundColor;
    public readonly Color ActivationColor;

    public ItemIconTheme(Color backgroundColor, Color activationColor)
    {
        BackgroundColor = backgroundColor;
        ActivationColor = activationColor;
    }
}

public class ItemIconUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image spriteImage;
    [SerializeField] private GameObject activationCTA;
    private ItemRewardDefinition itemDefinition;
    private Coroutine activationPulseCoroutine;

    public static ItemIconTheme DefaultTheme => new ItemIconTheme(
        new Color(0.53409004f, 0f, 1f, 0.08627451f),
        new Color(1f, 0.69796455f, 0f, 1f));

    public Image SpriteImage => spriteImage;
    public Button Button => button;
    public ItemRewardDefinition ItemDefinition => itemDefinition;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void Bind(ItemRewardDefinition definition, ItemIconTheme theme, Action<ItemRewardDefinition> onClicked)
    {
        CacheReferences();
        itemDefinition = definition;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onClicked != null)
            {
                button.onClick.AddListener(() =>
                {
                    SoundManager.Instance?.PlayClick();
                    onClicked.Invoke(itemDefinition);
                });
            }
        }

        if (spriteImage == null)
        {
            return;
        }

        Sprite sprite = definition != null ? definition.Artwork : null;
        spriteImage.sprite = sprite;
        spriteImage.enabled = sprite != null;
        ApplyTheme(theme);
        SetActivationVisible(false);
    }

    public void PulseActivation(float duration)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (activationPulseCoroutine != null)
        {
            StopCoroutine(activationPulseCoroutine);
        }

        activationPulseCoroutine = StartCoroutine(PulseActivationRoutine(duration));
    }

    public void SetActivationVisible(bool isVisible)
    {
        if (activationCTA != null && activationCTA.activeSelf != isVisible)
        {
            activationCTA.SetActive(isVisible);
        }
    }

    private IEnumerator PulseActivationRoutine(float duration)
    {
        SetActivationVisible(true);
        yield return new WaitForSeconds(Mathf.Max(0.01f, duration));
        activationPulseCoroutine = null;
        SetActivationVisible(false);
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

        if (spriteImage == null)
        {
            spriteImage = transform.Find("Sprite")?.GetComponent<Image>();
        }

        if (activationCTA == null)
        {
            activationCTA = transform.Find("ActivationCTA")?.gameObject;
        }
    }

    private void ApplyTheme(ItemIconTheme theme)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = theme.BackgroundColor;
        }

        Image activationImage = activationCTA != null ? activationCTA.GetComponent<Image>() : null;
        if (activationImage != null)
        {
            activationImage.color = theme.ActivationColor;
        }
    }
}
