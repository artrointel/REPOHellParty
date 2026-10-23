#if DEBUG
using System;
using System.Collections;
using BepInEx.Configuration;
using HellParty.Native;
using UnityEngine;

namespace HellParty.Debugging;

internal static class DebugHeadmanScenario
{
    internal const int ForcedDifficulty = 5;
    private static ConfigEntry<bool>? _enabled;
    internal static bool Enabled => _enabled?.Value == true;

    internal static void Initialize(ConfigFile config) => _enabled = config.Bind("Debug.Headman", "Enabled", true,
        "Test Headman Protection at difficulty 5. Spawn Headman and a vanilla Animal ally, shotgun and tranquilizer. Restart after changing this setting.");

    internal static IEnumerator SpawnTestSet()
    {
        float deadline = Time.time + 20;
        while (TruckSafetySpawnPoint.instance == null || StatsManager.instance == null || EnemyDirector.instance == null)
        {
            if (Time.time >= deadline) { Plugin.ModLogger.LogError("DEBUG Headman: start/catalog setup timed out."); yield break; }
            yield return null;
        }
        Transform anchor = TruckSafetySpawnPoint.instance.transform;
        DebugSpawnSupport.SpawnWeapon(anchor, false);
        DebugSpawnSupport.SpawnWeapon(anchor, true);
        deadline = Time.time + 20;
        while (Time.time < deadline)
        {
            bool created = false;
            try
            {
                PrefabRef prefab = VanillaCatalog.HeadmanPrefab();
                if (DebugSpawnSupport.TryFindPosition(anchor.position, prefab, out Vector3 point))
                {
                    SummonedEnemySpawnTicket.Create(prefab.ResourcePath, point, prepareOnMesh: true);
                    DebugSpawnSupport.ReservePosition(point);
                    Plugin.ModLogger.LogInfo($"DEBUG Headman: requested at {point}, difficulty 5. Native AI unchanged. Protection inherits a nearby ally target or attacker only when idle.");
                    PrefabRef ally = VanillaCatalog.AnimalPrefab();
                    if (DebugSpawnSupport.TryFindPosition(point, ally, out Vector3 allyPoint))
                    {
                        SummonedEnemySpawnTicket.Create(ally.ResourcePath, allyPoint, prepareOnMesh: true, vanillaOnly: true);
                        DebugSpawnSupport.ReservePosition(allyPoint);
                        Plugin.ModLogger.LogInfo($"DEBUG Headman: vanilla Animal ally at {allyPoint}.");
                    }
                    else Plugin.ModLogger.LogWarning("DEBUG Headman: no clear ally point within 20m; use a natural monster to test protection.");
                    created = true;
                }
            }
            catch (Exception exception) { Plugin.ModLogger.LogError($"DEBUG Headman spawn failed: {exception}"); yield break; }
            if (created) yield break;
            yield return new WaitForSeconds(.5f);
        }
        Plugin.ModLogger.LogError("DEBUG Headman: no connected clear NavMesh within 20m of start. Weapons were spawned independently.");
    }
}
#endif
