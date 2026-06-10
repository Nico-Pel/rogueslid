using DG.Tweening;
using UnityEngine;

public class cam : MonoBehaviour
{
    [Header("Shake")]
    [SerializeField] private float globalShakeRatio = 1f;
    [SerializeField] private float minShakePower = 0.08f;
    [SerializeField] private float maxShakePower = 0.35f;
    [SerializeField] private float shakeDuration = 0.16f;
    [SerializeField] private float maxDamageForMaxShake = 10f;

    private Tween shakeTween;
    private Vector3 baseLocalPosition;

    public static cam Instance { get; private set; }
    public float GlobalShakeRatio
    {
        get => globalShakeRatio;
        set => globalShakeRatio = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        Instance = this;
        baseLocalPosition = transform.localPosition;
    }

    public void CamShake(float damage)
    {
        float normalizedDamage = Mathf.InverseLerp(1f, Mathf.Max(1f, maxDamageForMaxShake), damage);
        float strength = Mathf.Lerp(minShakePower, maxShakePower, normalizedDamage) * globalShakeRatio;
        Shake(strength);
    }

    public void Shake(float strength)
    {
        if (strength <= 0f)
        {
            return;
        }

        shakeTween?.Kill();
        transform.localPosition = baseLocalPosition;
        shakeTween = transform.DOShakePosition(shakeDuration, strength, 18, 90f, false, true)
            .SetUpdate(true)
            .OnKill(() =>
            {
                transform.localPosition = baseLocalPosition;
            })
            .OnComplete(() =>
            {
                transform.localPosition = baseLocalPosition;
                shakeTween = null;
            });
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        shakeTween?.Kill();
        shakeTween = null;
        transform.localPosition = baseLocalPosition;
    }
}
