#if DEBUG
using System;
using System.Collections.Generic;
using HellParty.Native;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty.Debugging;

internal static class DebugSpawnSupport
{
    private static StageRuntime? _stage;
    private static readonly List<Vector3> Reserved = new();
    internal static void ReservePosition(Vector3 position) => Reserved.Add(position);
    internal static void SpawnWeapon(Transform anchor, bool tranquilizer)
    {
        try
        {
            foreach (Item item in StatsManager.instance.itemDictionary.Values)
            {
                if (item == null || item.disabled || item.prefab == null || !item.prefab.IsValid()) continue;
                GameObject prefab = item.prefab.Prefab;
                if (prefab.GetComponent<ItemGun>() == null || (tranquilizer
                    ? prefab.GetComponent<ItemGunTranq>() == null
                    : prefab.name.IndexOf("Shotgun", StringComparison.OrdinalIgnoreCase) < 0)) continue;
                VanillaCatalog.Spawn(item.prefab, anchor.TransformPoint(new Vector3(tranquilizer ? .8f : -.8f, .6f, 1.5f)),
                    anchor.rotation * item.spawnRotationOffset);
                return;
            }
            throw new InvalidOperationException(tranquilizer ? "Native tranquilizer not found." : "Native shotgun not found.");
        }
        catch (Exception e) { Plugin.ModLogger.LogError($"DEBUG test weapon spawn failed: {e}"); }
    }

    internal static bool TryFindPosition(Vector3 origin, PrefabRef prefab, out Vector3 result)
    {
        if (_stage != StageRuntime.Current) { _stage = StageRuntime.Current; Reserved.Clear(); }
        NavMeshAgent settings = prefab.Prefab.GetComponentInChildren<NavMeshAgent>(true);
        var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = settings.areaMask };
        float best = float.PositiveInfinity;
        result = default;
        foreach (LevelPoint point in UnityEngine.Object.FindObjectsOfType<LevelPoint>())
            if (!point.Truck) Consider(point.transform.position, origin, settings, filter, ref best, ref result);
        if (!float.IsPositiveInfinity(best)) return true;
        NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
        for (int i = 0; i + 2 < mesh.indices.Length; i += 3)
            Consider((mesh.vertices[mesh.indices[i]] + mesh.vertices[mesh.indices[i + 1]] + mesh.vertices[mesh.indices[i + 2]]) / 3,
                origin, settings, filter, ref best, ref result);
        return !float.IsPositiveInfinity(best);
    }

    private static void Consider(Vector3 candidate, Vector3 origin, NavMeshAgent settings, NavMeshQueryFilter filter,
        ref float best, ref Vector3 result)
    {
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, filter)) return;
        float squared = (hit.position - origin).sqrMagnitude;
        if (squared < 16 || squared > 400 || squared >= best || Mathf.Abs(hit.position.y - origin.y) > 4) return;
        foreach (Vector3 reserved in Reserved)
            if ((reserved - hit.position).sqrMagnitude < 9) return;
        float height = Mathf.Max(1.8f, settings.height), radius = Mathf.Max(.4f, settings.radius);
        if (Physics.CheckCapsule(hit.position + Vector3.up * (radius + .05f), hit.position + Vector3.up * (height - radius + .05f),
            radius, LayerMask.GetMask("Default", "PhysGrabObject", "Enemy"), QueryTriggerInteraction.Ignore)) return;
        if (!NavMesh.SamplePosition(origin, out NavMeshHit start, 8, filter)) return;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(start.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) return;
        best = squared;
        result = hit.position;
    }
}
#endif
