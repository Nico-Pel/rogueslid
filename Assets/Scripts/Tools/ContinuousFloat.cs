using UnityEngine;
using DG.Tweening;

public class ContinuousFloat : MonoBehaviour
{
    [Header("Float Settings")]
    public float floatHeight = 0.15f;
    public float floatDuration = 1.2f;
    public Ease floatEase = Ease.InOutSine;
    public bool useLocalPosition = true;

    private Tween _floatTween;
    private Vector3 _basePosition;

    private void OnEnable()
    {
        _basePosition = useLocalPosition ? transform.localPosition : transform.position;
        StartFloat();
    }

    private void OnDisable()
    {
        _floatTween?.Kill();

        if (useLocalPosition)
        {
            transform.localPosition = _basePosition;
        }
        else
        {
            transform.position = _basePosition;
        }
    }

    private void StartFloat()
    {
        Vector3 targetPosition = _basePosition + Vector3.up * floatHeight;

        _floatTween = useLocalPosition
            ? transform.DOLocalMove(targetPosition, floatDuration)
            : transform.DOMove(targetPosition, floatDuration);

        _floatTween
            .SetEase(floatEase)
            .SetLoops(-1, LoopType.Yoyo);
    }
}
