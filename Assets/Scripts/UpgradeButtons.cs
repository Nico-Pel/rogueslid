using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeButtons : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image selectorImage;
    [SerializeField] private List<Image> arrowImages = new List<Image>();

    public Button Button => button;
    public Image BackgroundImage => backgroundImage;
    public Image IconImage => iconImage;
    public TMP_Text CountText => countText;
    public Image SelectorImage => selectorImage;
    public List<Image> ArrowImages => arrowImages;
    public RectTransform RectTransform => transform as RectTransform;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheReferences();
        }
    }

    public void CacheReferences()
    {
        button ??= GetComponent<Button>();
        backgroundImage ??= GetComponent<Image>();
        iconImage ??= FindComponentByName<Image>(transform, "iIcon");
        countText ??= FindComponentByName<TMP_Text>(transform, "tCount");
        selectorImage ??= FindComponentByName<Image>(transform, "iSelector");
    }

    private static T FindComponentByName<T>(Transform root, string objectName) where T : Component
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        T[] components = root.GetComponentsInChildren<T>(true);
        for (int index = 0; index < components.Length; index++)
        {
            T component = components[index];
            if (component != null && string.Equals(component.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return component;
            }
        }

        return null;
    }
}
