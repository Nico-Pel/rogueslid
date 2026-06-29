using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class BarrelObstacle : MonoBehaviour
{
    [SerializeField] private Transform fractures;
    [SerializeField] private float fractureForce = 4f;
    [SerializeField] private float upwardForce = 1.5f;
    [SerializeField] [Range(0f, 1f)] private float fractureCullRatio = 0.5f;
    [SerializeField] private float freezeDelay = 0.2f;
    [SerializeField] private Vector2 shrinkDelayRange = new Vector2(1f, 1.5f);
    [SerializeField] private float shrinkDuration = 0.25f;
    [SerializeField] private float finalScale = 0.1f;
    [SerializeField] private SoundParameters breakSound;

    private BoardManager board;
    private Vector2Int gridPosition;
    private bool isDestroyed;

    public Vector2Int GridPosition => gridPosition;
    public bool IsDestroyed => isDestroyed;

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
                Vector3 direction = (body.worldCenterOfMass - transform.position);
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = Random.onUnitSphere;
                }

                direction = direction.normalized;
                Vector3 force = (direction * fractureForce) + (Vector3.up * upwardForce);
                body.AddForce(force, ForceMode.Impulse);
            }
        }

        breakSound?.PlaySound(transform.position);
        board?.ClearStaticObstacle(gridPosition, gameObject);
        board?.HandleBarrelDestroyed(gridPosition);

        if (fracturesRoot != null)
        {
            BarrelFractureCleanup cleanup = fracturesRoot.gameObject.GetComponent<BarrelFractureCleanup>();
            if (cleanup == null)
            {
                cleanup = fracturesRoot.gameObject.AddComponent<BarrelFractureCleanup>();
            }

            cleanup.Begin(rigidbodies, freezeDelay, shrinkDelayRange, shrinkDuration, finalScale);
        }

        Destroy(gameObject);
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

public class BarrelFractureCleanup : MonoBehaviour
{
    public void Begin(List<Rigidbody> rigidbodies, float freezeDelay, Vector2 shrinkDelayRange, float shrinkDuration, float finalScale)
    {
        StartCoroutine(Run(rigidbodies, freezeDelay, shrinkDelayRange, shrinkDuration, finalScale));
    }

    private IEnumerator Run(List<Rigidbody> rigidbodies, float freezeDelay, Vector2 shrinkDelayRange, float shrinkDuration, float finalScale)
    {
        if (freezeDelay > 0f)
        {
            yield return new WaitForSeconds(freezeDelay);
        }

        if (rigidbodies != null)
        {
            for (int index = 0; index < rigidbodies.Count; index++)
            {
                Rigidbody body = rigidbodies[index];
                if (body == null)
                {
                    continue;
                }

                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.constraints = RigidbodyConstraints.FreezeAll;
            }
        }

        int tweenCount = 0;
        int completedTweens = 0;
        float targetScale = Mathf.Max(0.01f, finalScale);
        float minDelay = Mathf.Min(shrinkDelayRange.x, shrinkDelayRange.y);
        float maxDelay = Mathf.Max(shrinkDelayRange.x, shrinkDelayRange.y);

        for (int index = 0; index < transform.childCount; index++)
        {
            Transform child = transform.GetChild(index);
            if (child == null)
            {
                continue;
            }

            tweenCount++;
            float childDelay = Random.Range(minDelay, maxDelay);
            child.DOScale(Vector3.one * targetScale, shrinkDuration)
                .SetDelay(Mathf.Max(0f, childDelay))
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    if (child != null)
                    {
                        Destroy(child.gameObject);
                    }

                    completedTweens++;
                    if (completedTweens >= tweenCount)
                    {
                        Destroy(gameObject);
                    }
                });
        }

        if (tweenCount == 0)
        {
            Destroy(gameObject);
        }
    }
}
