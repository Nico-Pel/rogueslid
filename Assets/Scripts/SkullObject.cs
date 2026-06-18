using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SkullObject : MonoBehaviour
{
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Transform fractures;
    [SerializeField] private Transform canvasRoot;
    [SerializeField] private TMP_Text reviveCountText;
    [SerializeField] private float fractureForce = 3.5f;
    [SerializeField] private float upwardForce = 1.25f;
    [SerializeField] private float freezeDelay = 0.2f;
    [SerializeField] private Vector2 shrinkDelayRange = new Vector2(1f, 1.5f);
    [SerializeField] private float shrinkDuration = 0.25f;
    [SerializeField] private float finalScale = 0.1f;
    [SerializeField] private float victoryResolveDelay = 0.5f;

    private BoardManager board;
    private Vector2Int gridPosition;
    private SkeletonEnemyData skeletonData;
    private bool isResolving;
    private int remainingTurns;

    public Vector2Int GridPosition => gridPosition;
    public bool IsResolving => isResolving;

    public void Assign(BoardManager ownerBoard, Vector2Int cellPosition, SkeletonEnemyData sourceData)
    {
        board = ownerBoard;
        gridPosition = cellPosition;
        skeletonData = sourceData;
        remainingTurns = sourceData != null ? sourceData.ReviveTurns : 3;

        if (modelRoot == null)
        {
            modelRoot = transform.Find("Model");
        }

        if (fractures == null)
        {
            fractures = transform.Find("Fractures");
        }

        if (canvasRoot == null)
        {
            canvasRoot = transform.Find("Canvas");
        }

        if (reviveCountText == null && canvasRoot != null)
        {
            reviveCountText = canvasRoot.GetComponentInChildren<TMP_Text>(true);
        }

        if (fractures != null)
        {
            fractures.gameObject.SetActive(false);
        }

        if (modelRoot != null)
        {
            modelRoot.gameObject.SetActive(true);
        }

        if (canvasRoot != null)
        {
            canvasRoot.gameObject.SetActive(true);
        }

        RefreshCounter();
    }

    public void HandlePlayerTurnStarted()
    {
        if (isResolving)
        {
            return;
        }

        remainingTurns = Mathf.Max(remainingTurns - 1, 0);
        RefreshCounter();
        if (remainingTurns <= 0)
        {
            ReviveSkeleton();
        }
    }

    public void ShatterForVictory()
    {
        if (isResolving)
        {
            return;
        }

        isResolving = true;
        if (modelRoot != null)
        {
            modelRoot.gameObject.SetActive(false);
        }

        if (canvasRoot != null)
        {
            canvasRoot.gameObject.SetActive(false);
        }

        Transform fracturesRoot = fractures;
        List<Rigidbody> rigidbodies = new List<Rigidbody>();
        if (fracturesRoot != null)
        {
            fracturesRoot.SetParent(board != null ? board.transform : null, true);
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

            BarrelFractureCleanup cleanup = fracturesRoot.gameObject.GetComponent<BarrelFractureCleanup>();
            if (cleanup == null)
            {
                cleanup = fracturesRoot.gameObject.AddComponent<BarrelFractureCleanup>();
            }

            cleanup.Begin(rigidbodies, freezeDelay, shrinkDelayRange, shrinkDuration, finalScale);
        }

        StartCoroutine(FinalizeVictoryResolution());
    }

    private IEnumerator FinalizeVictoryResolution()
    {
        if (victoryResolveDelay > 0f)
        {
            yield return new WaitForSeconds(victoryResolveDelay);
        }

        board?.ClearStaticObstacle(gridPosition, gameObject);
        board?.UnregisterSkullObject(this);
        Destroy(gameObject);
    }

    private void ReviveSkeleton()
    {
        if (isResolving)
        {
            return;
        }

        isResolving = true;
        board?.ClearStaticObstacle(gridPosition, gameObject);
        board?.ReviveSkeletonFromSkull(this, skeletonData);
        board?.UnregisterSkullObject(this);
        Destroy(gameObject);
    }

    private void RefreshCounter()
    {
        if (reviveCountText != null)
        {
            reviveCountText.text = remainingTurns.ToString();
        }
    }
}
