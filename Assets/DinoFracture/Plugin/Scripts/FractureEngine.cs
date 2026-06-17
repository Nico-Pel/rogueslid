using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DinoFracture
{
    /// <summary>
    /// Argument passed to OnFracture message
    /// </summary>
    public sealed class OnFractureEventArgs
    {
        public OnFractureEventArgs(FractureGeometry orig, Bounds origBounds, GameObject root)
        {
            OriginalObject = orig;
            OriginalMeshBounds = origBounds;
            FracturePiecesRootObject = root;
        }

        public bool IsValid => (FracturePiecesRootObject != null);

        /// <summary>
        /// The object that fractured.
        /// </summary>
        public FractureGeometry OriginalObject;

        /// <summary>
        /// The bounds of the original mesh
        /// </summary>
        public Bounds OriginalMeshBounds;

        /// <summary>
        /// The root of the pieces of the resulting fracture.
        /// </summary>
        public GameObject FracturePiecesRootObject;

        /// <summary>
        /// Returns an enumerable of just the generated Unity meshes
        /// </summary>
        /// <returns></returns>
        public IEnumerable<UnityEngine.Mesh> GetMeshes()
        {
            if (FracturePiecesRootObject != null)
            {
                for (int i = 0; i < FracturePiecesRootObject.transform.childCount; i++)
                {
                    var child = FracturePiecesRootObject.transform.GetChild(i);
                    var mesh = child.GetComponent<MeshFilter>();
                    if (mesh != null)
                    {
                        yield return mesh.sharedMesh;
                    }
                    else
                    {
                        yield return null;
                    }
                }
            }
        }
    }

    public delegate void OnFractureEventHandler(OnFractureEventArgs args);

    /// <summary>
    /// The result of a fracture.
    /// </summary>
    public sealed class AsyncFractureResult
    {
        private GameObject _callbackObj;
        private OnFractureEventHandler _callbackFunc;

        private OnFractureEventArgs _args;

        public event OnFractureEventHandler OnFractureComplete
        {
            add
            {
                if (IsComplete)
                {
                    value(_args);
                }
                else
                {
                    _callbackFunc += value;
                }
            }
            remove { _callbackFunc -= value; }
        }

        /// <summary>
        /// Returns true if the operation has finished; false otherwise.
        /// This value will always be true for synchronous fractures.
        /// </summary>
        public bool IsComplete { get; private set; }

        /// <summary>
        /// Returns true if the operation has finished and returned valid results.
        /// </summary>
        public bool IsSuccessful
        {
            get { return IsComplete && PiecesRoot != null; }
        }

        /// <summary>
        /// The original script that initiated the fracture
        /// </summary>
        public FractureGeometry FractureGeometry { get { return _args?.OriginalObject; } }

        /// <summary>
        /// The root of the pieces of the resulting fracture
        /// </summary>
        public GameObject PiecesRoot { get { return _args?.FracturePiecesRootObject; } }

        /// <summary>
        /// The bounds of the original mesh
        /// </summary>
        public Bounds EntireMeshBounds { get { return (_args != null) ? _args.OriginalMeshBounds : new Bounds(); } }

        internal bool StopRequested { get; private set; }

        internal void SetResult(OnFractureEventArgs args)
        {
            if (IsComplete)
            {
                Debug.LogWarning("DinoFracture: Setting AsyncFractureResult's results twice.");
            }
            else
            {
                _args = args;
                IsComplete = true;

                FireCallbacks();
            }
        }

        public void StopFracture()
        {
            StopRequested = true;
        }

        public void SetCallbackObject(GameObject obj)
        {
            _callbackObj = obj;

            if (IsComplete)
            {
                FireCallbackOnCallbackObj();
            }
        }

        public void SetCallbackObject(Component obj)
        {
            _callbackObj = obj?.gameObject;

            if (IsComplete)
            {
                FireCallbackOnCallbackObj();
            }
        }

        private void FireCallbacks()
        {
            FirePieceCallbacks();

            FireCallbackOnCallbackObj();
            FireCallbackOnCallbackFunc();
        }

        private void FirePieceCallbacks()
        {
            if (_args.OriginalObject != null)
            {
                if (Application.isPlaying)
                {
                    _args.OriginalObject.gameObject.SendMessage("OnFracture", _args, SendMessageOptions.DontRequireReceiver);
                }
                else
                {
                    _args.OriginalObject.OnFracture(_args);
                }
            }

            if (_args.FracturePiecesRootObject != null)
            {
                if (Application.isPlaying)
                {
                    Transform trans = _args.FracturePiecesRootObject.transform;
                    for (int i = 0; i < trans.childCount; i++)
                    {
                        trans.GetChild(i).gameObject.SendMessage("OnFracture", _args, SendMessageOptions.DontRequireReceiver);
                    }
                }
            }
        }

        private void FireCallbackOnCallbackObj()
        {
            if (_callbackObj != null && _callbackObj != _args.OriginalObject.gameObject)
            {
                _callbackObj.SendMessage("OnFracture", _args, SendMessageOptions.DontRequireReceiver);
            }
        }

        private void FireCallbackOnCallbackFunc()
        {
            if (_callbackFunc != null)
            {
                _callbackFunc.Invoke(_args);
            }
        }
    }

    /// <summary>
    /// This component is created on demand to manage the fracture coroutines.
    /// It is not intended to be added by the user.
    /// </summary>
    public sealed class FractureEngine : FractureEngineBase
    {
        private struct FractureInstance
        {
            public AsyncFractureResult Result;
            public IEnumerator Enumerator;

            public FractureInstance(AsyncFractureResult result, IEnumerator enumerator)
            {
                Result = result;
                Enumerator = enumerator;
            }
        }

        private static FractureEngine _instance;

        [SerializeField] private bool _suspended;

        [SerializeField] private int _maxRunningFractures = 0;

        private List<FractureInstance> _runningFractures = new List<FractureInstance>();
        private List<FractureInstance> _pendingFractures = new List<FractureInstance>();

        private static new FractureEngine Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject inst = new GameObject("Fracture Engine");
                    _instance = inst.AddComponent<FractureEngine>();
                    FractureEngineBase.Instance = _instance;
                }

                return _instance;
            }
        }

        /// <summary>
        /// True if all further fracture operations should be a no-op.
        /// </summary>
        public static bool Suspended
        {
            get { return Instance._suspended; }
            set { Instance._suspended = value; }
        }

        /// <summary>
        /// Returns true if there are fractures currently in progress
        /// </summary>
        public static bool HasFracturesInProgress
        {
            get { return Instance._runningFractures.Count > 0; }
        }

        /// <summary>
        /// The maximum number of async fractures we can process at a time.
        /// If this is set to 0 (default), an unlimited number can be run.
        /// </summary>
        /// <remarks>
        /// NOTE: Synchronous fractures always run immediately
        /// </remarks>
        public static int MaxRunningFractures
        {
            get { return Instance._maxRunningFractures; }
        }

        private static int EffectiveMaxRunningFractures
        {
            get
            {
                if (!Application.isPlaying)
                {
                    return 4;
                }
                else
                {
                    return MaxRunningFractures;
                }
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                FractureEngineBase.Instance = _instance;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                FractureEngineBase.Instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            // Clear the cache data. This is mostly for stopping play in the
            // editor to get us back to a clean cache state.
            if (_instance == this)
            {
                ClearCachedFractureData();
            }
        }

        /// <summary>
        /// Starts a fracture operation
        /// </summary>
        /// <param name="details">Fracture info</param>
        /// <param name="callback">The object to fracture</param>
        /// <param name="piecesParent">The parent of the resulting fractured pieces root object</param>
        /// <param name="transferMass">True to distribute the original object's mass to the fracture pieces; false otherwise</param>
        /// <param name="hideAfterFracture">True to hide the originating object after fracturing</param>
        /// <returns></returns>
        public static AsyncFractureResult StartFracture(FractureDetails details, FractureGeometry callback, Transform piecesParent, bool transferMass, bool hideAfterFracture)
        {
            AsyncFractureResult res = new AsyncFractureResult();
            if (Suspended)
            {
                res.SetResult(new OnFractureEventArgs(callback, new Bounds(), null));
            }
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                FractureBuilder.DisableMultithreading = true;
#endif

                IEnumerator it = Instance.WaitForResults(details, callback, piecesParent, transferMass, hideAfterFracture, res);

                if (details.Asynchronous)
                {
                    if (EffectiveMaxRunningFractures <= 0 || Instance._runningFractures.Count < EffectiveMaxRunningFractures)
                    {
                        if (it.MoveNext())
                        {
#if UNITY_EDITOR
                            if (Instance._runningFractures.Count == 0 && !Application.isPlaying)
                            {
                                EditorApplication.update += Instance.OnEditorUpdate;
                            }
#endif
                            Instance._runningFractures.Add(new FractureInstance(res, it));
                        }
                    }
                    else
                    {
                        Instance._pendingFractures.Add(new FractureInstance(res, it));
                    }
                }
                else
                {
                    // There should only be one iteration
                    while (it.MoveNext())
                    {
                        Debug.LogWarning("DinoFracture: Sync fracture taking more than one iteration");
                    }

#if UNITY_EDITOR
                    // Force an update to do any work that was queued
                    if (!Application.isPlaying)
                    {
                        Instance.Update();
                    }
#endif
                }
            }
            return res;
        }

        private void OnEditorUpdate()
        {
            Update();

            if (_runningFractures.Count == 0)
            {
#if UNITY_EDITOR
                EditorApplication.update -= OnEditorUpdate;
#endif
                DestroyImmediate(gameObject);
            }
        }

        protected override void Update()
        {
            base.Update();

            UpdateFractures();
        }

        private void UpdateFractures()
        {
            for (int i = _runningFractures.Count - 1; i >= 0; i--)
            {
                if (_runningFractures[i].Result.StopRequested)
                {
                    _runningFractures.RemoveAt(i);
                }
                else
                {
                    if (!_runningFractures[i].Enumerator.MoveNext())
                    {
                        _runningFractures.RemoveAt(i);
                    }
                }
            }

            for (int i = 0; i < _pendingFractures.Count; i++)
            {
                if (EffectiveMaxRunningFractures <= 0 || _runningFractures.Count < EffectiveMaxRunningFractures)
                {
                    _runningFractures.Add(_pendingFractures[i]);
                    _pendingFractures.RemoveAt(i);
                    i--;
                }
            }
        }

        private IEnumerator WaitForResults(FractureDetails details, FractureGeometry callback, Transform piecesParent, bool transferMass, bool hideAfterFracture, AsyncFractureResult result)
        {
            AsyncFractureOperation operation;
            if (details is ShatterDetails shatterDetails)
            {
                operation = FractureBuilder.Shatter(shatterDetails);
            }
            else if (details is SliceDetails sliceDetails)
            {
                operation = FractureBuilder.Slice(sliceDetails);
            }
            else
            {
                Debug.LogError("Invalid operation type");
                result.SetResult(new OnFractureEventArgs(callback, new Bounds(), null));
                yield break;
            }

            while (!operation.IsComplete)
            {
                // Async fractures should not happen while in edit mode because game objects don't update too often
                // and the coroutine is not pumped. Sync fractures should not reach this point.
                System.Diagnostics.Debug.Assert(Application.isPlaying && operation.Details.Asynchronous);
                yield return null;
            }

            if (callback == null)
            {
                result.SetResult(new OnFractureEventArgs(callback, new Bounds(), null));
                yield break;
            }

            if (operation.Result == null)
            {
                // Something failed catastrophically during fracture

                Debug.LogError("DinoFracture: Fracture failed.");
                result.SetResult(new OnFractureEventArgs(callback, new Bounds(), null));
                yield break;
            }

            Rigidbody origBody = null;
            if (transferMass)
            {
                origBody = callback.GetComponent<Rigidbody>();
            }

            float density = 0.0f;
            if (origBody != null)
            {
                Collider collider = callback.GetComponent<Collider>();
                if (collider != null && collider.enabled)
                {
                    // Calculate the density by setting the density to
                    // a known value and see what the mass comes out to.
                    float mass = origBody.mass;
                    float volume;

                    // Unity can't calculate density of non-convex mesh colliders
                    if (collider is MeshCollider meshCollider && !meshCollider.convex)
                    {
                        // Estimate the volume from the bounding box
                        var colliderBounds = collider.bounds.size;
                        volume = colliderBounds.x * colliderBounds.y * colliderBounds.z;
                    }
                    else
                    {
                        origBody.SetDensity(1.0f);
                        volume = origBody.mass;
                    }

                    density = mass / volume;

                    // Reset the mass
                    origBody.mass = mass;
                }
                else
                {
                    // Estimate the density based on the size of the object
                    Bounds bounds = operation.Details.Mesh.bounds;
                    float volume = bounds.size.x * operation.Details.MeshScale.x * bounds.size.y * operation.Details.MeshScale.y * bounds.size.z * operation.Details.MeshScale.z;
                    density = origBody.mass / volume;
                }
            }

            IReadOnlyList<FracturedMesh> meshes = operation.Result.GetMeshes();

            GameObject rootGO = new GameObject(callback.gameObject.name + " - Fracture Root");
            rootGO.transform.parent = (piecesParent ?? callback.transform.parent);
            rootGO.transform.position = callback.transform.position;
            rootGO.transform.rotation = callback.transform.rotation;
            rootGO.transform.localScale = Vector3.one;  // Scale is controlled by the value in operation.Details

            Material[] sharedMaterials = callback.GetComponent<Renderer>().sharedMaterials;

            for (int i = 0; i < meshes.Count; i++)
            {
                var fractureTemplate = (callback.FractureTemplate != null) ? callback.FractureTemplate : callback.gameObject;

                GameObject go = Instantiate(fractureTemplate);
                go.name = "Fracture Object " + i;
                go.transform.parent = rootGO.transform;
                go.transform.localPosition = meshes[i].Offset;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                go.SetActive(true);

                MeshFilter mf = go.GetComponent<MeshFilter>();
                mf.sharedMesh = meshes[i].Mesh;

                // Copy the correct materials to the new mesh.
                // There are some things we need to account for:
                //
                // 1) Not every subMesh in the original mesh will still
                //    exist. It may have no triangles now and we should
                //    skip over those materials.
                // 2) We have added a new submesh for the inside triangles
                //    and need to add the inside material.
                // 3) The original mesh might have more materials than
                //    were subMeshes. In that case, we want to append
                //    the extra materials to the end of our list.
                //
                // The final material list will be:
                // * Used materials from the original mesh
                // * Inside material
                // * Extra materials from the original mesh
                MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    // There is an entry in EmptyTriangles for each subMesh,
                    // including the newly added inside triangles. The last
                    // subMesh is always the inside triangles we created.
                    int numOrigSubMeshes = meshes[i].EmptyTriangles.Count - 1;

                    Material[] materials = new Material[sharedMaterials.Length - meshes[i].EmptyTriangleCount + 1];
                    int matIdx = 0;
                    for (int m = 0; m < numOrigSubMeshes; m++)
                    {
                        if (!meshes[i].EmptyTriangles[m])
                        {
                            materials[matIdx++] = sharedMaterials[m];
                        }
                    }
                    if (!meshes[i].EmptyTriangles[numOrigSubMeshes])
                    {
                        materials[matIdx++] = callback.InsideMaterial;
                    }
                    for (int m = numOrigSubMeshes; m < sharedMaterials.Length; m++)
                    {
                        materials[matIdx++] = sharedMaterials[m];
                    }

                    meshRenderer.sharedMaterials = materials;
                }

                MeshCollider meshCol = go.GetComponent<MeshCollider>();
                if (meshCol != null)
                {
                    // Check if we have a "bad" mesh.
                    //
                    // A small vertex count can lead to errors being thrown by Unity because
                    // it is not able to generate a mesh collider with > 0 volume.
                    //
                    if (meshes[i].Flags.HasFlag(FracturedMeshResultFlags.SmallVertexCount) &&
                        operation.Details.IssueResolution != FractureIssueResolution.NoAction)
                    {
                        // Replace the mesh collider with a sphere collider
                        if (operation.Details.IssueResolution == FractureIssueResolution.ReplaceMeshCollider)
                        {
                            if (Application.isPlaying)
                            {
                                Destroy(meshCol);
                            }
                            else
                            {
                                DestroyImmediate(meshCol);
                            }

                            SphereCollider sphereCollider = go.AddComponent<SphereCollider>();
                            sphereCollider.radius = meshes[i].Mesh.bounds.size.magnitude * 0.5f;
                        }
                    }
                    else
                    {
                        meshCol.sharedMesh = mf.sharedMesh;
                    }
                }

                if (transferMass && origBody != null)
                {
                    Rigidbody rb = go.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        // Unity can't calculate density of non-convex mesh colliders
                        var meshCollider = rb.GetComponent<MeshCollider>();
                        if (meshCollider != null && !meshCollider.convex)
                        {
                            // Estimate the volume from the bounding box
                            var colliderBounds = meshCollider.bounds.size;
                            float volume = colliderBounds.x * colliderBounds.y * colliderBounds.z;

                            rb.mass = density * volume;
                        }
                        else
                        {
                            rb.SetDensity(density);
                            rb.mass = rb.mass;  // Need to explicity set it for the editor to reflect the changes
                        }
                    }
                }

                if (Application.isPlaying)
                {
                    CleanupMeshOnDestroy cleanupComp = go.GetComponent<CleanupMeshOnDestroy>();
                    if (cleanupComp)
                    {
                        cleanupComp.SetIsRuntimeAsset();
                    }
                }

                FractureGeometry fg = go.GetComponent<FractureGeometry>();
                if (fg != null)
                {
                    fg.InsideMaterial = callback.InsideMaterial;
                    fg.FractureTemplate = callback.FractureTemplate;
                    fg.PiecesParent = callback.PiecesParent;
                    fg.NumGenerations = (callback.NumGenerations > 0) ? callback.NumGenerations - 1 : callback.NumGenerations;
                    fg.DistributeMass = callback.DistributeMass;

                    // It is assumed that any geometry produced by the engine will be valid.
                    // No need to check in the future.
                    fg.ForceValidGeometry();
                }

                // Disable the game object if we have found errors
                if (meshes[i].Flags != FracturedMeshResultFlags.NoIssues &&
                    operation.Details.IssueResolution == FractureIssueResolution.DisableGameObject)
                {
                    go.SetActive(false);
                }
            }

            OnFractureEventArgs args = new OnFractureEventArgs(callback, operation.Result.EntireMeshBounds, rootGO);

            result.SetResult(args);

            if (hideAfterFracture)
            {
                callback.gameObject.SetActive(false);
            }
        }
    }
}