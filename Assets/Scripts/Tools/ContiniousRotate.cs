using UnityEngine;
using DG.Tweening;

public class ContinuousRotate : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Vector3 rotationAxis = Vector3.up; // Axe de rotation
    public float rotateSpeed = 45f; // Degrés par seconde

    private void Start()
    {
        RotateObject();
    }

    private void RotateObject()
    {
        // Tween de rotation infinie relative
        float duration = 360f / rotateSpeed; // Temps pour un tour complet

        transform.DORotate(rotationAxis * 360f, duration, RotateMode.LocalAxisAdd) // rotation relative
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart); // boucle infinie
    }
}