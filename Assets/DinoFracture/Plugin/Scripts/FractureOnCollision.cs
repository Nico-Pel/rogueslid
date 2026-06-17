using System;
using System.Collections;
using UnityEngine;

namespace DinoFracture
{
    /// <summary>
    /// This component will cause a fracture to happen at the point of impact.
    /// </summary>
    [RequireComponent(typeof(FractureGeometry))]
    public class FractureOnCollision : MonoBehaviour
    {
        /// <summary>
        /// The minimum amount of force required to fracture this object.
        /// Set to 0 to have any amount of force cause the fracture.
        /// </summary>
        public float ForceThreshold;

        /// <summary>
        /// Falloff radius for transferring the force of the impact
        /// to the resulting pieces.  Any piece outside of this falloff
        /// from the point of impact will have no additional impulse
        /// set on it.
        /// </summary>
        public float ForceFalloffRadius = 1.0f;

        /// <summary>
        /// If true and this is a kinematic body, an impulse will be
        /// applied to the colliding body to counter the effects of'
        /// hitting a kinematic body.  If false and this is a kinematic
        /// body, the colliding body will bounce off as if this were an
        /// unmovable wall.
        /// </summary>
        public bool AdjustForKinematic = true;

        private Vector3 _impactImpulse;
        private float _impactMass;
        private Vector3 _impactPoint;
        private Rigidbody _impactBody;

        private FractureGeometry _fractureGeometry;
        private Rigidbody _thisBody;

        private void Awake()
        {
            _fractureGeometry = GetComponent<FractureGeometry>();
            _thisBody = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision col)
        {
            if (!_fractureGeometry.IsProcessingFracture && col.contactCount > 0)
            {
                _impactBody = col.rigidbody;
                _impactMass = (col.rigidbody != null) ? col.rigidbody.mass : 0.0f;

                _impactPoint = Vector3.zero;

                Vector3 avgNormal = Vector3.zero;
                for (int i = 0; i < col.contactCount; i++)
                {
                    var contact = col.GetContact(i);

                    _impactPoint += contact.point;
                    avgNormal += contact.normal;
                }
                _impactPoint *= 1.0f / col.contactCount;

                _impactImpulse = avgNormal.normalized * col.impulse.magnitude;

                float forceMag = 0.5f * _impactImpulse.sqrMagnitude;
                if (forceMag >= ForceThreshold)
                {
                    Vector3 localPoint = transform.worldToLocalMatrix.MultiplyPoint(_impactPoint);
                    _fractureGeometry.Fracture(localPoint);
                }
            }
        }

        private void OnFracture(OnFractureEventArgs args)
        {
            if (args.IsValid && args.OriginalObject.gameObject == gameObject && _impactMass > 0.0f)
            {
                float originalMass = (_thisBody != null) ? _thisBody.mass : 0.0f;

                for (int i = 0; i < args.FracturePiecesRootObject.transform.childCount; i++)
                {
                    Transform piece = args.FracturePiecesRootObject.transform.GetChild(i);

                    Rigidbody rb = piece.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        float percentForce = (originalMass > 0.0f) ? (rb.mass / originalMass) : 1.0f;

                        if (ForceFalloffRadius > 0.0f)
                        {
                            float dist = (piece.position - _impactPoint).magnitude;
                            percentForce *= Mathf.Clamp01(1.0f - (dist / ForceFalloffRadius));
                        }

                        rb.AddForce(_impactImpulse * percentForce, ForceMode.Impulse);
                    }
                }

                if (AdjustForKinematic)
                {
                    // If the fractured body is kinematic, the collision for the colliding body will
                    // be as if it hit an unmovable wall.  Try to correct for that by adding the same
                    // force to colliding body.
                    if (_thisBody != null && _thisBody.isKinematic && _impactBody != null)
                    {
                        _impactBody.AddForceAtPosition(_impactImpulse, _impactPoint, ForceMode.Impulse);
                    }
                }
            }
        }
    }
}
