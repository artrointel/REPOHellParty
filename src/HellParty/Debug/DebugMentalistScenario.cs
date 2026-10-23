#if DEBUG
using System;
using System.Collections;
using HellParty.Native;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty.Debugging;

internal static class DebugMentalistScenario
{
    internal const int ForcedDifficulty = 2;

    internal static IEnumerator SpawnTestSet()
    {
        float deadline = Time.time + 15;
        while (TruckSafetySpawnPoint.instance == null || StatsManager.instance == null)
        {
            if (Time.time >= deadline)
            {
                Plugin.ModLogger.LogError("DEBUG Mentalist: truck/item catalog setup timed out.");
                yield break;
            }
            yield return null;
        }
        Transform anchor = TruckSafetySpawnPoint.instance.transform;
        for (int kind = 0; kind < 4; kind++) SpawnWeapon(anchor, kind);
        deadline = Time.time + 20;
        while (Time.time < deadline)
        {
            if (TryFindPosition(anchor.position, out Vector3 point))
            {
                try
                {
                    EnemyParent parent = SpawnMentalist(point);
                    Plugin.ModLogger.LogInfo($"DEBUG Mentalist spawn requested at {point}; reflection shield tier 2. F6 toggles targetless test mode (initially ON).");
                }
                catch (Exception exception) { Plugin.ModLogger.LogError($"DEBUG Mentalist spawn failed: {exception}"); }
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }
        Plugin.ModLogger.LogError($"DEBUG Mentalist: no clear NavMesh position within 20m of start {anchor.position} after 20s. Weapon spawns ran independently.");
    }

    private static EnemyParent SpawnMentalist(Vector3 point)
    {
        EnemyParent parent = SummonedEnemySpawnTicket.Create(VanillaCatalog.MentalistPrefab().ResourcePath, point);
        parent.gameObject.AddComponent<DebugMentalistController>();
        return parent;
    }

    private static void SpawnWeapon(Transform anchor, int kind)
    {
        try
        {
            foreach (Item item in StatsManager.instance.itemDictionary.Values)
            {
                if (item == null || item.disabled || item.prefab == null || !item.prefab.IsValid()) continue;
                GameObject prefab = item.prefab.Prefab;
                if (prefab.GetComponent<ItemGun>() == null) continue;
                bool matches = kind switch
                {
                    0 => prefab.name.IndexOf("Shotgun", StringComparison.OrdinalIgnoreCase) >= 0,
                    1 => prefab.name.IndexOf("Handgun", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         prefab.name.IndexOf("Pistol", StringComparison.OrdinalIgnoreCase) >= 0,
                    2 => prefab.GetComponent<ItemGunTranq>() != null,
                    _ => prefab.GetComponent<ItemGunLaser>() != null
                };
                if (!matches) continue;
                Vector3 position = anchor.TransformPoint(new Vector3((kind % 2 == 0 ? -0.8f : 0.8f), 0.6f, 1.5f + kind / 2 * 0.8f));
                VanillaCatalog.Spawn(item.prefab, position, anchor.rotation * item.spawnRotationOffset);
                return;
            }
            throw new InvalidOperationException($"Native test gun kind {kind} not found (0=shotgun, 1=handgun, 2=tranquilizer, 3=laser).");
        }
        catch (Exception exception) { Plugin.ModLogger.LogError($"DEBUG Mentalist weapon spawn failed: {exception}"); }
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
        if (squared < 16 || squared > 400 || squared >= best || Mathf.Abs(hit.position.y - origin.y) > 4) return;
        if (Physics.CheckCapsule(hit.position + Vector3.up * 0.65f, hit.position + Vector3.up * 1.8f, 0.5f,
            LayerMask.GetMask("Default", "PhysGrabObject"), QueryTriggerInteraction.Ignore)) return;
        best = squared;
        result = hit.position;
    }
}

#endif
