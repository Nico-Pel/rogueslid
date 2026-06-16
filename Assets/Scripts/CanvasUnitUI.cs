using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasUnitUI : MonoBehaviour
{
    [Header("Health UI")]
    [SerializeField] private Image hpFillBar;
    [SerializeField] private List<Image> hpFillBars = new List<Image>();
    [SerializeField] private List<GameObject> hpSplitBars = new List<GameObject>();
    [SerializeField] private RectTransform hpSplitBarsContainer;
    [SerializeField] private int healthPerLayer = 12;
    [SerializeField] private float splitBarsMinInset = 2f;
    [SerializeField] private float splitBarsMaxInset = 6f;

    public RectTransform RootTransform => transform as RectTransform;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void RefreshHealth(int currentHealth, int maxHealth)
    {
        CacheReferences();
        if (hpFillBars.Count == 0)
        {
            return;
        }

        int activeSplitCount = Mathf.Clamp(maxHealth - 1, 0, hpSplitBars.Count);
        for (int index = 0; index < hpSplitBars.Count; index++)
        {
            if (hpSplitBars[index] != null)
            {
                hpSplitBars[index].SetActive(index < activeSplitCount);
            }
        }

        RefreshHpSplitBarsLayout(activeSplitCount);

        for (int index = 0; index < hpFillBars.Count; index++)
        {
            Image fillBar = hpFillBars[index];
            if (fillBar == null)
            {
                continue;
            }

            int layerStartHealth = index * healthPerLayer;
            int layerMaxHealth = Mathf.Clamp(maxHealth - layerStartHealth, 0, healthPerLayer);
            bool isLayerActive = layerMaxHealth > 0;
            fillBar.gameObject.SetActive(isLayerActive);

            if (!isLayerActive)
            {
                fillBar.fillAmount = 0f;
                continue;
            }

            int layerCurrentHealth = Mathf.Clamp(currentHealth - layerStartHealth, 0, healthPerLayer);
            int layerDisplayCapacity = index == 0
                ? Mathf.Clamp(maxHealth, 1, healthPerLayer)
                : healthPerLayer;
            fillBar.fillAmount = layerCurrentHealth / (float)layerDisplayCapacity;
        }
    }

    private void CacheReferences()
    {
        Transform hpBarTransform = transform.Find("hpBar");
        if (hpBarTransform == null)
        {
            return;
        }

        if (hpFillBars.Count == 0)
        {
            if (hpFillBar != null)
            {
                hpFillBars.Add(hpFillBar);
            }
            else
            {
                Image legacyFillBar = hpBarTransform.Find("hpFillBar")?.GetComponent<Image>();
                if (legacyFillBar != null)
                {
                    hpFillBar = legacyFillBar;
                    hpFillBars.Add(legacyFillBar);
                }
            }

            for (int index = 1; index <= 12; index++)
            {
                Transform fillTransform = hpBarTransform.Find($"hpFillBar{index}");
                if (fillTransform == null)
                {
                    continue;
                }

                Image fillImage = fillTransform.GetComponent<Image>();
                if (fillImage != null && !hpFillBars.Contains(fillImage))
                {
                    hpFillBars.Add(fillImage);
                }
            }
        }

        if (hpFillBar == null && hpFillBars.Count > 0)
        {
            hpFillBar = hpFillBars[0];
        }

        if (hpSplitBars.Count == 0)
        {
            Transform splitBarsTransform = hpBarTransform.Find("SplitBars");
            if (splitBarsTransform != null)
            {
                hpSplitBarsContainer = splitBarsTransform as RectTransform;
                for (int index = 0; index < splitBarsTransform.childCount; index++)
                {
                    hpSplitBars.Add(splitBarsTransform.GetChild(index).gameObject);
                }
            }
        }
    }

    private void RefreshHpSplitBarsLayout(int activeSplitCount)
    {
        if (hpSplitBarsContainer == null)
        {
            return;
        }

        if (activeSplitCount <= 0)
        {
            hpSplitBarsContainer.offsetMin = new Vector2(splitBarsMinInset, hpSplitBarsContainer.offsetMin.y);
            hpSplitBarsContainer.offsetMax = new Vector2(-splitBarsMinInset, hpSplitBarsContainer.offsetMax.y);
            return;
        }

        const int minReferenceBars = 2;
        int clampedCount = Mathf.Clamp(activeSplitCount, minReferenceBars, hpSplitBars.Count);
        float t = hpSplitBars.Count <= minReferenceBars
            ? 1f
            : (clampedCount - minReferenceBars) / (float)(hpSplitBars.Count - minReferenceBars);
        float inset = Mathf.Lerp(splitBarsMaxInset, splitBarsMinInset, t);
        hpSplitBarsContainer.offsetMin = new Vector2(inset, hpSplitBarsContainer.offsetMin.y);
        hpSplitBarsContainer.offsetMax = new Vector2(-inset, hpSplitBarsContainer.offsetMax.y);
    }
}
