#if DEBUG
using System;
using System.Collections;
using BepInEx.Configuration;
using HellParty.Native;
using UnityEngine;

namespace HellParty.Debugging;

internal static class DebugReaperScenario
{
    internal const int ForcedDifficulty = 2;
    private static ConfigEntry<bool>? _enabled;
    internal static bool Enabled => !DebugClownScenario.Enabled && _enabled?.Value == true;

    internal static void Initialize(ConfigFile config) => _enabled = config.Bind("Debug.Reaper", "Enabled", true,
        "Test Reaper Sprint at difficulty 2. Spawn one Reaper, shotgun and tranquilizer near the start. Restart after changing this setting.");

    internal static IEnumerator SpawnTestSet()
    {
        float deadline = Time.time + 20;
        while (TruckSafetySpawnPoint.instance == null || StatsManager.instance == null || EnemyDirector.instance == null)
        {
            if (Time.time >= deadline) { Plugin.ModLogger.LogError("DEBUG Reaper: start/catalog setup timed out."); yield break; }
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
                PrefabRef prefab = VanillaCatalog.ReaperPrefab();
                if (DebugSpawnSupport.TryFindPosition(anchor.position, prefab, out Vector3 point))
                {
                    SummonedEnemySpawnTicket.Create(prefab.ResourcePath, point, prepareOnMesh: true);
                    DebugSpawnSupport.ReservePosition(point);
                    Plugin.ModLogger.LogInfo($"DEBUG Reaper: requested at {point}, difficulty 2. Sprint: Headman at >=10m, Reaper at <=3m, cooldown 10s. Native target acquisition is unchanged.");
                    created = true;
                }
            }
            catch (Exception exception) { Plugin.ModLogger.LogError($"DEBUG Reaper spawn failed: {exception}"); yield break; }
            if (created) yield break;
            yield return new WaitForSeconds(.5f);
        }
        Plugin.ModLogger.LogError("DEBUG Reaper: no connected clear NavMesh within 20m of start. Weapons were spawned independently.");
    }
}
#endif
