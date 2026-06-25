using System.Collections.Generic;
using UnityEngine;

public class RockBoulderProjectile : MonoBehaviour
{
    [SerializeField] private Transform fractures;
    [SerializeField] private float fractureForce = 3.5f;
    [SerializeField] private float upwardForce = 1.25f;
    [SerializeField] private float freezeDelay = 0.4f;
    [SerializeField] private Vector2 shrinkDelayRange = new Vector2(1f, 1.5f);
    [SerializeField] private float shrinkDuration = 0.25f;
    [SerializeField] private float finalScale = 0.1f;

    public void Fracture(BoardManager board)
    {
        Transform fracturesRoot = fractures != null ? fractures : transform.Find("Fractures");
        if (fracturesRoot == null)
        {
            Destroy(gameObject);
            return;
        }

        fracturesRoot.SetParent(board != null ? board.transform : null, true);
        fracturesRoot.gameObject.SetActive(true);

        List<Rigidbody> rigidbodies = new List<Rigidbody>(fracturesRoot.GetComponentsInChildren<Rigidbody>(true));
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
        Destroy(gameObject);
    }
}
