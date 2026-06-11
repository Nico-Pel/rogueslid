using UnityEngine;
using DG.Tweening;

public class ContinuousDoScale : MonoBehaviour
{
    [Header("Scale Settings")]
    public Vector3 scaleMultiplier = Vector3.one * 1.1f; // Multiplicateur par rapport au scale initial
    public float scaleDuration = 1f; // Durée pour aller au scale cible
    public Ease scaleEase = Ease.InOutSine;

    private Vector3 _baseScale;

    private void Start()
    {
        _baseScale = transform.localScale;
        ScaleObject();
    }

    private void ScaleObject()
    {
        Vector3 targetScale = Vector3.Scale(_baseScale, scaleMultiplier);
        transform.DOScale(targetScale, scaleDuration)
            .SetEase(scaleEase)
            .SetLoops(-1, LoopType.Yoyo);
    }
}
