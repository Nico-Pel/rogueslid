using UnityEngine;
using DG.Tweening;

public class ContinuousRotate : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Vector3 rotationAxis = Vector3.up; // Axe de rotation
    public float rotateSpeed = 45f; // Degrés par seconde
    private Tween rotateTween;

    private void Start()
    {
        RotateObject();
    }

    private void OnDisable()
    {
        rotateTween?.Kill();
        rotateTween = null;
    }

    private void RotateObject()
    {
        rotateTween?.Kill();

        // Tween de rotation infinie relative
        float duration = 360f / Mathf.Max(0.01f, rotateSpeed); // Temps pour un tour complet

        rotateTween = transform.DORotate(rotationAxis * 360f, duration, RotateMode.LocalAxisAdd) // rotation relative
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart); // boucle infinie
    }

    public void SetRotateSpeed(float newRotateSpeed)
    {
        rotateSpeed = Mathf.Max(0.01f, newRotateSpeed);
        if (isActiveAndEnabled)
        {
            RotateObject();
        }
    }
} 
