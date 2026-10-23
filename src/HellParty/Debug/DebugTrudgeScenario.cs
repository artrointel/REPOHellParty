#if DEBUG
using System;
using System.Collections;
using BepInEx.Configuration;
using HellParty.Native;
using UnityEngine;

namespace HellParty.Debugging;

internal static class DebugTrudgeScenario
{
    internal const int ForcedDifficulty = 5;
    private static ConfigEntry<bool>? _enabled;
    internal static bool Enabled => !DebugLoomRobeScenario.Enabled && _enabled?.Value == true;

    internal static void Initialize(ConfigFile config) => _enabled = config.Bind("Debug.Trudge", "Enabled", true,
        "Test Trudge Muster at difficulty 5. Spawn one Trudge, shotgun and tranquilizer near the start. Restart after changing this setting.");

    internal static IEnumerator SpawnTestSet()
    {
        float deadline = Time.time + 20;
        while (TruckSafetySpawnPoint.instance == null || StatsManager.instance == null || EnemyDirector.instance == null)
        {
            if (Time.time >= deadline) { Plugin.ModLogger.LogError("DEBUG Trudge: start/catalog setup timed out."); yield break; }
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
                PrefabRef prefab = VanillaCatalog.TrudgePrefab();
                if (DebugSpawnSupport.TryFindPosition(anchor.position, prefab, out Vector3 point))
                {
                    SummonedEnemySpawnTicket.Create(prefab.ResourcePath, point, prepareOnMesh: true);
                    DebugSpawnSupport.ReservePosition(point);
                    Plugin.ModLogger.LogInfo($"DEBUG Trudge: requested at {point}, difficulty 5. Native AI unchanged. Each floor slam summons two vanilla reinforcements after 3s, regardless of player damage.");
                    created = true;
                }
            }
            catch (Exception exception) { Plugin.ModLogger.LogError($"DEBUG Trudge spawn failed: {exception}"); yield break; }
            if (created) yield break;
            yield return new WaitForSeconds(.5f);
        }
        Plugin.ModLogger.LogError("DEBUG Trudge: no connected clear NavMesh within 20m of start. Weapons were spawned independently.");
    }
}
#endif
