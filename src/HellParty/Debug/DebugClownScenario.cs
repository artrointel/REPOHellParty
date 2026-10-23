#if DEBUG
using System;
using System.Collections;
using BepInEx.Configuration;
using HellParty.Native;
using UnityEngine;

namespace HellParty.Debugging;

internal static class DebugClownScenario
{
    internal const int ForcedDifficulty = 5;
    private static ConfigEntry<bool>? _enabled;
    internal static bool Enabled => _enabled?.Value == true;

    internal static void Initialize(ConfigFile config) => _enabled = config.Bind("Debug.Clown", "Enabled", true,
        "Test Clown Staff and Laser Shield at difficulty 5. Spawn one Clown, shotgun and tranquilizer near the start. Restart after changing this setting.");

    internal static IEnumerator SpawnTestSet()
    {
        float deadline = Time.time + 20;
        while (TruckSafetySpawnPoint.instance == null || StatsManager.instance == null || EnemyDirector.instance == null)
        {
            if (Time.time >= deadline) { Plugin.ModLogger.LogError("DEBUG Clown: start/catalog setup timed out."); yield break; }
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
                PrefabRef prefab = VanillaCatalog.ClownPrefab();
                if (DebugSpawnSupport.TryFindPosition(anchor.position, prefab, out Vector3 point))
                {
                    SummonedEnemySpawnTicket.Create(prefab.ResourcePath, point, prepareOnMesh: true);
                    DebugSpawnSupport.ReservePosition(point);
                    Plugin.ModLogger.LogInfo($"DEBUG Clown: requested at {point}, difficulty 5. Native AI unchanged. Attacks summon Staff; targetless incoming gunfire is blocked and retaliated after 2s.");
                    created = true;
                }
            }
            catch (Exception exception) { Plugin.ModLogger.LogError($"DEBUG Clown spawn failed: {exception}"); yield break; }
            if (created) yield break;
            yield return new WaitForSeconds(.5f);
        }
        Plugin.ModLogger.LogError("DEBUG Clown: no connected clear NavMesh within 20m of start. Weapons were spawned independently.");
    }
}
#endif
