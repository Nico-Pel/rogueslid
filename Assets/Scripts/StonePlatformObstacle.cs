using UnityEngine;

public class StonePlatformObstacle : MonoBehaviour
{
    private static readonly int[] YawAngles = { 0, 90, 180, 270 };

    public void ApplySpawnPresentation()
    {
        Transform cachedTransform = transform;

        Vector3 eulerAngles = cachedTransform.eulerAngles;
        eulerAngles.y = YawAngles[Random.Range(0, YawAngles.Length)];
        cachedTransform.eulerAngles = eulerAngles;

        Vector3 localScale = cachedTransform.localScale;
        float absoluteScaleX = Mathf.Abs(localScale.x);
        if (absoluteScaleX <= 0.0001f)
        {
            absoluteScaleX = 1f;
        }

        localScale.x = Random.value < 0.5f ? absoluteScaleX : -absoluteScaleX;
        cachedTransform.localScale = localScale;
    }
}
