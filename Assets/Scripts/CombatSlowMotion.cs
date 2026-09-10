using UnityEngine;
using UnityEngine.UI;

// Owns a real-time window and restores the previous clock even when its owner disappears.
public sealed class CombatSlowMotion : MonoBehaviour
{
    private static CombatSlowMotion activeOwner;
    private float previousScale;
    private float previousFixedDelta;
    private float appliedScale;
    private float expiresAt;
    private GameObject overlay;
    public bool IsActive => activeOwner == this && Time.unscaledTime < expiresAt;

    public void Open(float realSeconds, float scale)
    {
        if (Time.timeScale <= 0) return;
        if (activeOwner != this)
        {
            if (activeOwner != null) activeOwner.Close();
            previousScale = Time.timeScale;
            previousFixedDelta = Time.fixedDeltaTime;
            activeOwner = this;
        }
        expiresAt = Time.unscaledTime + realSeconds;
        appliedScale = previousScale * Mathf.Clamp(scale, 0.05f, 1f);
        Time.timeScale = appliedScale;
        Time.fixedDeltaTime = previousFixedDelta * Mathf.Clamp(scale, 0.05f, 1f);
        if (overlay == null)
        {
            overlay = new GameObject("Momentum frame", typeof(Canvas));
            overlay.transform.SetParent(transform);
            Canvas canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            for (int i = 0; i < 4; i++)
            {
                Image edge = new GameObject("Edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                edge.transform.SetParent(overlay.transform, false);
                edge.raycastTarget = false;
                edge.color = new Color(0.1f, 0.9f, 1f, 0.22f);
                RectTransform rect = edge.rectTransform;
                rect.anchorMin = i == 1 ? new Vector2(1, 0) : i == 3 ? new Vector2(0, 1) : Vector2.zero;
                rect.anchorMax = i == 0 ? new Vector2(0, 1) : i == 2 ? new Vector2(1, 0) : Vector2.one;
                rect.pivot = i < 2 ? new Vector2(i, 0.5f) : new Vector2(0.5f, i - 2);
                rect.sizeDelta = i < 2 ? new Vector2(12, 0) : new Vector2(0, 12);
            }
        }
        overlay.SetActive(true);
    }

    private void Update()
    {
        if (activeOwner == this && Time.unscaledTime >= expiresAt) Close();
    }

    public void Close()
    {
        if (activeOwner == this)
        {
            // Do not unpause a clock paused by another system.
            if (Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = previousScale;
            Time.fixedDeltaTime = previousFixedDelta;
            activeOwner = null;
        }
        if (overlay != null) overlay.SetActive(false);
    }

    private void OnDisable() => Close();
    private void OnDestroy() { Close(); if (overlay != null) Destroy(overlay); }
}
