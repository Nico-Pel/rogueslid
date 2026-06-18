using UnityEngine;

public class RockBiomeColorTarget : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private int materialIndex;

    private static readonly int ColorShaderProperty = Shader.PropertyToID("_Color");

    public void ApplyColor(Color color)
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (targetRenderer == null)
        {
            return;
        }

        int clampedMaterialIndex = Mathf.Clamp(materialIndex, 0, Mathf.Max(0, targetRenderer.sharedMaterials.Length - 1));
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock, clampedMaterialIndex);
        propertyBlock.SetColor(ColorShaderProperty, color);
        targetRenderer.SetPropertyBlock(propertyBlock, clampedMaterialIndex);
    }
}
