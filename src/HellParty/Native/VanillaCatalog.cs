using System;
using System.Collections.Generic;
using System.Reflection;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty.Native;

internal static class VanillaCatalog
{
    internal static PrefabRef DuckPrefab()
    {
        foreach (EnemySetup setup in EnemyDirector.instance.enemiesDifficulty1)
        {
            foreach (PrefabRef prefab in setup.spawnObjects)
            {
                if (prefab != null && prefab.IsValid() && prefab.Prefab.GetComponentInChildren<EnemyDuck>(true) != null)
                    return prefab;
            }
        }
        throw new InvalidOperationException("Vanilla Apex Predator prefab not found.");
    }

    internal static PrefabRef Valuable(Func<GameObject, bool> predicate)
    {
        // Enumerate native preset references; never register a new network prefab ID.
        foreach (LevelValuables preset in Resources.FindObjectsOfTypeAll<LevelValuables>())
        {
            foreach (FieldInfo field in typeof(LevelValuables).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!(field.GetValue(preset) is IEnumerable<PrefabRef> prefabs)) continue;
                foreach (PrefabRef prefab in prefabs)
                {
                    if (prefab != null && prefab.IsValid() && prefab.ResourcePath.StartsWith("Valuables/", StringComparison.Ordinal) && predicate(prefab.Prefab)) return prefab;
                }
            }
        }
        throw new InvalidOperationException("Required vanilla valuable prefab not found in loaded presets.");
    }

    internal static GameObject Spawn(PrefabRef prefab, Vector3 position, Quaternion rotation) => SemiFunc.IsMultiplayer()
        ? PhotonNetwork.InstantiateRoomObject(prefab.ResourcePath, position, rotation, 0)
        : UnityEngine.Object.Instantiate(prefab.Prefab, position, rotation);

    internal static bool Adjacent(Vector3 origin, int slot, out Vector3 position)
    {
        // Validate both the navmesh and the path so we don't choose the other side of a wall.
        float angle = slot * 2.094395f;
        var path = new NavMeshPath();
        if (!NavMesh.SamplePosition(origin, out NavMeshHit start, 1.5f, NavMesh.AllAreas))
        {
            position = origin;
            return false;
        }
        for (int i = 0; i < 8; i++)
        {
            float a = angle + i * Mathf.PI / 4;
            Vector3 candidate = origin + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.2f;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) &&
                Vector3.Distance(origin, hit.position) < 3 &&
                NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete &&
                !Physics.Linecast(origin + Vector3.up * 0.3f, hit.position + Vector3.up * 0.3f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
            {
                position = hit.position;
                return true;
            }
        }
        position = origin;
        return false;
    }
}
