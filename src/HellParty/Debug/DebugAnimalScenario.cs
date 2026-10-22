#if DEBUG
using System;
using System.Collections;
using HellParty.Native;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty.Debugging;

internal static class DebugAnimalScenario
{
    internal const int ForcedDifficulty = 5;

    internal static IEnumerator SpawnTestSet()
    {
        float deadline = Time.time + 15;
        while (TruckSafetySpawnPoint.instance == null || StatsManager.instance == null)
        {
            if (Time.time >= deadline)
            {
                Plugin.ModLogger.LogError("DEBUG Animal: truck/item catalog setup timed out.");
                yield break;
            }
            yield return null;
        }
        Transform anchor = TruckSafetySpawnPoint.instance.transform;
        SpawnWeapon(anchor, false);
        SpawnWeapon(anchor, true);
        deadline = Time.time + 20;
        while (Time.time < deadline)
        {
            if (TryFindPosition(anchor.position, out Vector3 point))
            {
                try
                {
                    SpawnAnimal(point);
                }
                catch (Exception exception) { Plugin.ModLogger.LogError($"DEBUG Animal spawn failed: {exception}"); }
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }
        Plugin.ModLogger.LogError($"DEBUG Animal: no clear NavMesh position within 20m of start {anchor.position} after 20s. Weapon spawns ran independently.");
    }

    private static EnemyParent SpawnAnimal(Vector3 point)
    {
        return AnimalSpawnTicket.Create(point, false);
    }

    private static void SpawnWeapon(Transform anchor, bool tranquilizer)
    {
        try
        {
            foreach (Item item in StatsManager.instance.itemDictionary.Values)
            {
                if (item == null || item.disabled || item.prefab == null || !item.prefab.IsValid()) continue;
                GameObject prefab = item.prefab.Prefab;
                bool matches = tranquilizer ? prefab.GetComponent<ItemGunTranq>() != null :
                    prefab.GetComponent<ItemGun>() != null && prefab.name.IndexOf("Shotgun", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matches) continue;
                Vector3 position = anchor.TransformPoint(new Vector3(tranquilizer ? 0.8f : -0.8f, 0.6f, 1.5f));
                VanillaCatalog.Spawn(item.prefab, position, anchor.rotation * item.spawnRotationOffset);
                return;
            }
            throw new InvalidOperationException($"Native {(tranquilizer ? "tranquilizer gun" : "shotgun")} not found.");
        }
        catch (Exception exception) { Plugin.ModLogger.LogError($"DEBUG Animal weapon spawn failed: {exception}"); }
    }

    private static bool TryFindPosition(Vector3 origin, out Vector3 result)
    {
        float best = float.PositiveInfinity;
        result = default;
        foreach (LevelPoint point in UnityEngine.Object.FindObjectsOfType<LevelPoint>())
            if (!point.Truck) Consider(point.transform.position, origin, ref best, ref result);
        // Truck interiors may lack NavMesh. Fall back to native triangle centers.
        if (!float.IsPositiveInfinity(best)) return true;
        NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
        for (int i = 0; i + 2 < mesh.indices.Length; i += 3)
            Consider((mesh.vertices[mesh.indices[i]] + mesh.vertices[mesh.indices[i + 1]] + mesh.vertices[mesh.indices[i + 2]]) / 3,
                origin, ref best, ref result);
        return !float.IsPositiveInfinity(best);
    }

    private static void Consider(Vector3 candidate, Vector3 origin, ref float best, ref Vector3 result)
    {
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return;
        float squared = (hit.position - origin).sqrMagnitude;
        if (squared < 1 || squared > 400 || squared >= best || Mathf.Abs(hit.position.y - origin.y) > 4) return;
        if (Physics.CheckCapsule(hit.position + Vector3.up * 0.25f, hit.position + Vector3.up * 0.65f, 0.2f,
            LayerMask.GetMask("Default", "PhysGrabObject"), QueryTriggerInteraction.Ignore)) return;
        best = squared;
        result = hit.position;
    }
}

#endif
