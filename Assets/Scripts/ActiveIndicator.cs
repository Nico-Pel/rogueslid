using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ActiveIndicator : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Color primaryColor = new Color32(0xFF, 0xB0, 0x00, 0xFF);
    [SerializeField] private Color secondaryColor = new Color32(0xC6, 0x89, 0x00, 0xFF);
    [SerializeField] private float blinkDuration = 0.45f;

    private Tween blinkTween;

    private void Awake()
    {
        CacheReferences();
        ApplyStaticColor(primaryColor);
    }

    private void OnEnable()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        StopBlink();
    }

    public void SetBlinking(bool isBlinking)
    {
        CacheReferences();

        if (targetImage == null)
        {
            return;
        }

        if (!isBlinking)
        {
            StopBlink();
            ApplyStaticColor(primaryColor);
            return;
        }

        if (blinkTween != null && blinkTween.IsActive())
        {
            return;
        }

        targetImage.color = primaryColor;
        blinkTween = targetImage.DOColor(secondaryColor, blinkDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void CacheReferences()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
    }

    private void StopBlink()
    {
        if (blinkTween == null)
        {
            return;
        }

        blinkTween.Kill();
        blinkTween = null;
    }

    private void ApplyStaticColor(Color color)
    {
        if (targetImage != null)
        {
            targetImage.color = color;
        }
    }
}
