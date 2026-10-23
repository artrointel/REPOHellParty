#if DEBUG
using System;
using System.Collections;
using BepInEx.Configuration;
using HellParty.Native;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty.Debugging;

internal static class DebugHuntsmanScenario
{
    internal const int ForcedDifficulty = 5;
    private static ConfigEntry<bool>? _enabled;
    internal static bool Enabled => !DebugUpscreamScenario.Enabled && _enabled?.Value == true;

    internal static void Initialize(ConfigFile config) => _enabled = config.Bind("Debug.Huntsman", "Enabled", true,
        "Force HellParty difficulty 5 and spawn Huntsman, shotgun and tranquilizer near the start. Debug build only. Restart the game after changing this value.");

    internal static bool SoloEnabledFor(EnemyParent parent) => Enabled &&
        parent.GetComponent<DebugHuntsmanController>() is DebugHuntsmanController control && control.AllowSolo;

    internal static IEnumerator SpawnTestSet(bool spawnWeapons = true)
    {
        float deadline = Time.time + 20;
        while (TruckSafetySpawnPoint.instance == null || StatsManager.instance == null || EnemyDirector.instance == null)
        {
            if (Time.time >= deadline)
            {
                Plugin.ModLogger.LogError("DEBUG Huntsman: start/item catalog setup timed out.");
                yield break;
            }
            yield return null;
        }
        Transform anchor = TruckSafetySpawnPoint.instance.transform;
        if (spawnWeapons)
        {
            DebugSpawnSupport.SpawnWeapon(anchor, false);
            DebugSpawnSupport.SpawnWeapon(anchor, true);
        }
        deadline = Time.time + 20;
        while (Time.time < deadline)
        {
            bool created = false;
            try
            {
                PrefabRef prefab = VanillaCatalog.HuntsmanPrefab();
                if (DebugSpawnSupport.TryFindPosition(anchor.position, prefab, out Vector3 point))
                {
                    EnemyParent parent = SummonedEnemySpawnTicket.Create(prefab.ResourcePath, point, prepareOnMesh: true);
                    DebugSpawnSupport.ReservePosition(point);
                    parent.gameObject.AddComponent<DebugHuntsmanController>();
                    Plugin.ModLogger.LogInfo($"DEBUG Huntsman requested at {point}. F6: solo override (initially OFF), F7: native aim at host, F8: release. Normally requires two living players within the stage.");
                    created = true;
                }
            }
            catch (Exception e) { Plugin.ModLogger.LogError($"DEBUG Huntsman spawn failed: {e}"); yield break; }
            if (created) yield break;
            yield return new WaitForSeconds(.5f);
        }
        Plugin.ModLogger.LogError("DEBUG Huntsman: no clear native NavMesh point within 20m of start. Weapons were spawned independently.");
    }

}
#endif
