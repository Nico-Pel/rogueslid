using UnityEngine;
using UnityEngine.UI;

public class ItemIconUI : MonoBehaviour
{
    [SerializeField] private Image spriteImage;

    public Image SpriteImage => spriteImage;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void Bind(Sprite sprite)
    {
        CacheReferences();
        if (spriteImage == null)
        {
            return;
        }

        spriteImage.sprite = sprite;
        spriteImage.enabled = sprite != null;
    }

    private void CacheReferences()
    {
        if (spriteImage == null)
        {
            spriteImage = transform.Find("Sprite")?.GetComponent<Image>();
        }
    }
}
