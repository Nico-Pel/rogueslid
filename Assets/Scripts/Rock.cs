using System.Collections.Generic;
using UnityEngine;

public enum RockBreakMode
{
    Collapse,
    Explosion
}

public class Rock : MonoBehaviour
{
    [Header("Break")]
    [SerializeField] private Transform fractures;
    [SerializeField] private float fractureForce = 3f;
    [SerializeField] private float upwardForce = 1.1f;
    [SerializeField] [Range(0f, 1f)] private float fractureCullRatio = 0.35f;
    [SerializeField] private float freezeDelay = 0.2f;
    [SerializeField] private Vector2 shrinkDelayRange = new Vector2(1f, 1.4f);
    [SerializeField] private float shrinkDuration = 0.25f;
    [SerializeField] private float finalScale = 0.1f;
    [SerializeField] private SoundParameters breakSound;

    [Header("Special Break")]
    [SerializeField] private RockBreakMode breakMode = RockBreakMode.Collapse;
    [SerializeField] [Min(1)] private int explosionDamage = 2;
    [SerializeField] [Min(1)] private int explosionRadius = 1;
    [SerializeField] private GameObject explosionFxPrefab;
    [SerializeField] private float explosionFxLifetime = 1.5f;

    private BoardManager board;
    private Vector2Int gridPosition;
    private bool isDestroyed;

    public Vector2Int GridPosition => gridPosition;
    public bool IsDestroyed => isDestroyed;
    public event System.Action Broken;

    public void Assign(BoardManager ownerBoard, Vector2Int cellPosition)
    {
        board = ownerBoard;
        gridPosition = cellPosition;

        if (fractures == null)
        {
            fractures = transform.Find("Fractures");
        }

        if (fractures != null)
        {
            fractures.gameObject.SetActive(false);
        }
    }

    public void TakeHit()
    {
        if (isDestroyed)
        {
            return;
        }

        isDestroyed = true;
        Break();
    }

    private void Break()
    {
        Transform fracturesRoot = fractures;
        List<Rigidbody> rigidbodies = new List<Rigidbody>();

        if (fracturesRoot != null)
        {
            fracturesRoot.SetParent(board != null ? board.transform : null, true);
            CullFractures(fracturesRoot);
            fracturesRoot.gameObject.SetActive(true);
            rigidbodies.AddRange(fracturesRoot.GetComponentsInChildren<Rigidbody>(true));

            for (int index = 0; index < rigidbodies.Count; index++)
            {
                Rigidbody body = rigidbodies[index];
                if (body == null)
                {
                    continue;
                }

                body.isKinematic = false;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                Vector3 direction = body.worldCenterOfMass - transform.position;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = Random.onUnitSphere;
                }

                Vector3 force = (direction.normalized * fractureForce) + (Vector3.up * upwardForce);
                body.AddForce(force, ForceMode.Impulse);
            }
        }

        breakSound?.PlaySound(transform.position);
        board?.ClearStaticObstacle(gridPosition, gameObject);
        Broken?.Invoke();
        ExplodeIfNeeded();

        if (fracturesRoot != null)
        {
            BarrelFractureCleanup cleanup = fracturesRoot.gameObject.GetComponent<BarrelFractureCleanup>();
            if (cleanup == null)
            {
                cleanup = fracturesRoot.gameObject.AddComponent<BarrelFractureCleanup>();
            }

            cleanup.Begin(rigidbodies, freezeDelay, shrinkDelayRange, shrinkDuration, finalScale);
        }

        if (fracturesRoot == null && breakMode == RockBreakMode.Collapse)
        {
            StartCoroutine(CollapseVisual());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private System.Collections.IEnumerator CollapseVisual()
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        Vector3 start = transform.position;
        Vector3 scale = transform.localScale;
        for (float t = 0; t < 0.25f; t += Time.deltaTime)
        {
            transform.position = start - Vector3.up * (t / 0.25f);
            transform.localScale = Vector3.Lerp(scale, Vector3.zero, t / 0.25f);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void ExplodeIfNeeded()
    {
        if (breakMode != RockBreakMode.Explosion || board == null)
        {
            return;
        }

        if (explosionFxPrefab != null)
        {
            GameObject spawnedFx = Instantiate(explosionFxPrefab, transform.position, explosionFxPrefab.transform.rotation);
            spawnedFx.transform.localScale = explosionFxPrefab.transform.localScale;
            if (explosionFxLifetime > 0f)
            {
                Destroy(spawnedFx, explosionFxLifetime);
            }
        }

        for (int index = 0; index < board.SpawnedEnemies.Count; index++)
        {
            Enemy enemy = board.SpawnedEnemies[index];
            if (enemy == null || enemy.CurrentHealth <= 0)
            {
                continue;
            }

            if (GetChebyshevDistance(gridPosition, enemy.GridPosition) <= explosionRadius)
            {
                enemy.TakeDamage(explosionDamage, DamageSoundType.Default);
            }
        }

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                if (GetChebyshevDistance(gridPosition, cellPosition) > explosionRadius
                    || !board.TryGetRock(cellPosition, out Rock rock)
                    || rock == null
                    || rock == this)
                {
                    continue;
                }

                rock.TakeHit();
            }
        }
    }

    private static int GetChebyshevDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    private void CullFractures(Transform fracturesRoot)
    {
        if (fracturesRoot == null || fractureCullRatio <= 0f)
        {
            return;
        }

        List<Transform> fractureChildren = new List<Transform>();
        for (int index = 0; index < fracturesRoot.childCount; index++)
        {
            Transform child = fracturesRoot.GetChild(index);
            if (child != null)
            {
                fractureChildren.Add(child);
            }
        }

        if (fractureChildren.Count == 0)
        {
            return;
        }

        int destroyCount = Mathf.Clamp(Mathf.RoundToInt(fractureChildren.Count * fractureCullRatio), 0, fractureChildren.Count - 1);
        for (int index = fractureChildren.Count - 1; index > 0; index--)
        {
            int swapIndex = Random.Range(0, index + 1);
            (fractureChildren[index], fractureChildren[swapIndex]) = (fractureChildren[swapIndex], fractureChildren[index]);
        }

        for (int index = 0; index < destroyCount; index++)
        {
            if (fractureChildren[index] != null)
            {
                Destroy(fractureChildren[index].gameObject);
            }
        }
    }
}
