using UnityEngine;
using DG.Tweening;

public class ContinuousDoScale : MonoBehaviour
{
    [Header("Scale Settings")]
    public Vector3 scaleMultiplier = Vector3.one * 1.1f; // Multiplicateur par rapport au scale initial
    public float scaleDuration = 1f; // Durée pour aller au scale cible
    public Ease scaleEase = Ease.InOutSine;

    private Vector3 _baseScale;
    private Tween _scaleTween;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        if (_baseScale == Vector3.zero)
        {
            _baseScale = transform.localScale;
        }

        ScaleObject();
    }

    private void OnDisable()
    {
        _scaleTween?.Kill();
        _scaleTween = null;
        transform.localScale = _baseScale;
    }

    private void ScaleObject()
    {
        _scaleTween?.Kill();
        Vector3 targetScale = Vector3.Scale(_baseScale, scaleMultiplier);
        _scaleTween = transform.DOScale(targetScale, scaleDuration)
            .SetEase(scaleEase)
            .SetLoops(-1, LoopType.Yoyo);
    }
}
