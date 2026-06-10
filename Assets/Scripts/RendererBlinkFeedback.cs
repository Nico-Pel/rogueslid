using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class RendererBlinkFeedback : MonoBehaviour
{
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    [SerializeField] private float defaultBlinkDuration = 0.12f;
    [SerializeField] [Range(0f, 1f)] private float defaultBlendStrength = 0.5f;

    private readonly List<Renderer> cachedRenderers = new List<Renderer>();
    private readonly Dictionary<Renderer, MaterialPropertyBlock> propertyBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();
    private Sequence blinkSequence;

    private void Start()
    {
        CacheRenderers();
    }

    public void Blink(Color targetColor)
    {
        Blink(targetColor, defaultBlendStrength, defaultBlinkDuration);
    }

    public void Blink(Color targetColor, float blendStrength)
    {
        Blink(targetColor, blendStrength, defaultBlinkDuration);
    }

    public void Blink(Color targetColor, float blendStrength, float duration)
    {
        CacheRenderers();
        if (cachedRenderers.Count == 0)
        {
            return;
        }

        blinkSequence?.Kill();

        float halfDuration = Mathf.Max(0.01f, duration * 0.5f);
        float clampedStrength = Mathf.Clamp01(blendStrength);

        blinkSequence = DOTween.Sequence();
        blinkSequence.Append(DOVirtual.Float(0f, 1f, halfDuration, value =>
        {
            ApplyBlinkProperties(targetColor, clampedStrength * value);
        }));
        blinkSequence.Append(DOVirtual.Float(1f, 0f, halfDuration, value =>
        {
            ApplyBlinkProperties(targetColor, clampedStrength * value);
        }));
        blinkSequence.OnKill(ClearBlinkProperties);
        blinkSequence.OnComplete(() =>
        {
            ClearBlinkProperties();
            blinkSequence = null;
        });
    }

    private void CacheRenderers()
    {
        if (cachedRenderers.Count > 0)
        {
            return;
        }

        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int index = 0; index < meshRenderers.Length; index++)
        {
            if (meshRenderers[index] != null)
            {
                cachedRenderers.Add(meshRenderers[index]);
                propertyBlocks[meshRenderers[index]] = new MaterialPropertyBlock();
            }
        }

        SkinnedMeshRenderer[] skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int index = 0; index < skinnedMeshRenderers.Length; index++)
        {
            if (skinnedMeshRenderers[index] != null)
            {
                cachedRenderers.Add(skinnedMeshRenderers[index]);
                propertyBlocks[skinnedMeshRenderers[index]] = new MaterialPropertyBlock();
            }
        }
    }

    private void ApplyBlinkProperties(Color targetColor, float flashAmount)
    {
        for (int index = 0; index < cachedRenderers.Count; index++)
        {
            Renderer targetRenderer = cachedRenderers[index];
            if (targetRenderer == null)
            {
                continue;
            }

            MaterialPropertyBlock block = propertyBlocks[targetRenderer];
            targetRenderer.GetPropertyBlock(block);
            block.SetColor(FlashColorId, targetColor);
            block.SetFloat(FlashAmountId, flashAmount);
            targetRenderer.SetPropertyBlock(block);
        }
    }

    private void ClearBlinkProperties()
    {
        for (int index = 0; index < cachedRenderers.Count; index++)
        {
            Renderer targetRenderer = cachedRenderers[index];
            if (targetRenderer == null)
            {
                continue;
            }

            MaterialPropertyBlock block = propertyBlocks[targetRenderer];
            targetRenderer.GetPropertyBlock(block);
            block.SetFloat(FlashAmountId, 0f);
            targetRenderer.SetPropertyBlock(block);
        }
    }

    private void OnDisable()
    {
        blinkSequence?.Kill();
        blinkSequence = null;
    }
}
