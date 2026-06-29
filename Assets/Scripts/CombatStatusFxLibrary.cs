using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CombatStatusFxEntry
{
    [SerializeField] private CombatStatusType statusType;
    [SerializeField] private GameObject applyFxPrefab;
    [Min(0f)]
    [SerializeField] private float applyFxLifetime = 2f;
    [SerializeField] private GameObject persistentFxPrefab;
    [SerializeField] private Vector3 persistentFxLocalOffset;

    public CombatStatusType StatusType => statusType;
    public GameObject ApplyFxPrefab => applyFxPrefab;
    public float ApplyFxLifetime => Mathf.Max(0f, applyFxLifetime);
    public GameObject PersistentFxPrefab => persistentFxPrefab;
    public Vector3 PersistentFxLocalOffset => persistentFxLocalOffset;
}

[CreateAssetMenu(fileName = "CombatStatusFxLibrary", menuName = "RogueSliders/Combat/Status FX Library")]
public class CombatStatusFxLibrary : ScriptableObject
{
    private const string DefaultResourcePath = "CombatStatusFxLibrary";

    [Header("Statuses")]
    [SerializeField] private List<CombatStatusFxEntry> statusEntries = new List<CombatStatusFxEntry>();

    [Header("Fear")]
    [SerializeField] private GameObject fearApplyFxPrefab;
    [Min(0f)]
    [SerializeField] private float fearApplyFxLifetime = 2f;

    private static CombatStatusFxLibrary cachedDefault;

    public static CombatStatusFxLibrary LoadDefault()
    {
        if (cachedDefault == null)
        {
            cachedDefault = Resources.Load<CombatStatusFxLibrary>(DefaultResourcePath);
        }

        return cachedDefault;
    }

    public bool TryGetEntry(CombatStatusType statusType, out CombatStatusFxEntry entry)
    {
        for (int index = 0; index < statusEntries.Count; index++)
        {
            CombatStatusFxEntry candidate = statusEntries[index];
            if (candidate != null && candidate.StatusType == statusType)
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }

    public GameObject SpawnApplyFx(CombatStatusType statusType, Transform anchor, Vector3 localOffset, bool parentToAnchor = false)
    {
        if (!TryGetEntry(statusType, out CombatStatusFxEntry entry) || entry == null)
        {
            return null;
        }

        return SpawnFx(entry.ApplyFxPrefab, entry.ApplyFxLifetime, anchor, localOffset, parentToAnchor);
    }

    public GameObject SpawnPersistentFx(CombatStatusType statusType, Transform anchor, Vector3 localOffset)
    {
        if (!TryGetEntry(statusType, out CombatStatusFxEntry entry) || entry == null)
        {
            return null;
        }

        return SpawnFx(entry.PersistentFxPrefab, 0f, anchor, entry.PersistentFxLocalOffset + localOffset, true);
    }

    public GameObject SpawnFearFx(Transform anchor, Vector3 localOffset)
    {
        return SpawnFx(fearApplyFxPrefab, fearApplyFxLifetime, anchor, localOffset, false);
    }

    private static GameObject SpawnFx(GameObject fxPrefab, float lifetime, Transform anchor, Vector3 localOffset, bool parentToAnchor)
    {
        if (fxPrefab == null || anchor == null)
        {
            return null;
        }

        Vector3 spawnPosition = parentToAnchor ? anchor.position : anchor.TransformPoint(localOffset);
        GameObject spawnedFx = Instantiate(fxPrefab, spawnPosition, fxPrefab.transform.rotation);
        if (parentToAnchor)
        {
            spawnedFx.transform.SetParent(anchor, true);
        }

        spawnedFx.transform.rotation = fxPrefab.transform.rotation;
        spawnedFx.transform.localScale = fxPrefab.transform.localScale;
        if (parentToAnchor)
        {
            spawnedFx.transform.localPosition = localOffset;
        }

        if (lifetime > 0f)
        {
            Destroy(spawnedFx, lifetime);
        }

        return spawnedFx;
    }
}
