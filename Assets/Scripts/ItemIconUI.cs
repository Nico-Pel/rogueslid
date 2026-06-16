using UnityEngine;
using UnityEngine.UI;
using System;

public class ItemIconUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image spriteImage;
    private ItemRewardDefinition itemDefinition;

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

    public void Bind(ItemRewardDefinition definition, Action<ItemRewardDefinition> onClicked)
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
    }

    private void CacheReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (spriteImage == null)
        {
            spriteImage = transform.Find("Sprite")?.GetComponent<Image>();
        }
    }
}
