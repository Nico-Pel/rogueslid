using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ChainLineRendererFollower : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float tilingPerUnit = 3f;

    private Transform startTarget;
    private Transform endTarget;
    private Vector3 startOffset;
    private Vector3 endOffset;
    private Material runtimeMaterial;
    private Vector2 baseTextureScale = Vector2.one;

    private void Awake()
    {
        CacheReferences();
    }

    private void LateUpdate()
    {
        RefreshLine();
    }

    public void Setup(Transform start, Transform end, Vector3 startWorldOffset, Vector3 endWorldOffset)
    {
        CacheReferences();
        startTarget = start;
        endTarget = end;
        startOffset = startWorldOffset;
        endOffset = endWorldOffset;
        RefreshLine();
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    private void CacheReferences()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer == null || runtimeMaterial != null)
        {
            return;
        }

        runtimeMaterial = lineRenderer.material;
        if (runtimeMaterial != null)
        {
            baseTextureScale = runtimeMaterial.mainTextureScale;
        }
    }

    private void RefreshLine()
    {
        if (lineRenderer == null || startTarget == null || endTarget == null)
        {
            return;
        }

        Vector3 startPosition = startTarget.position + startOffset;
        Vector3 endPosition = endTarget.position + endOffset;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, endPosition);

        if (runtimeMaterial == null)
        {
            return;
        }

        float distance = Vector3.Distance(startPosition, endPosition);
        runtimeMaterial.mainTextureScale = new Vector2(distance * tilingPerUnit, baseTextureScale.y);
    }
}
