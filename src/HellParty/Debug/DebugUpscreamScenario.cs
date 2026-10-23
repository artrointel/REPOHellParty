#if DEBUG
using System;
using System.Collections;
using BepInEx.Configuration;
using HellParty.Native;
using UnityEngine;

namespace HellParty.Debugging;

internal static class DebugUpscreamScenario
{
    internal const int ForcedDifficulty = 5;
    private static ConfigEntry<bool>? _enabled;
    internal static bool Enabled => !DebugHeadmanScenario.Enabled && !DebugClownScenario.Enabled && !DebugReaperScenario.Enabled && _enabled?.Value == true;
    internal static void Initialize(ConfigFile config) => _enabled = config.Bind("Debug.Upscream", "Enabled", true,
        "Test Upscream's random Big valuable mimic ability at difficulty 5. Spawns one ability-enabled Upscream, shotgun and tranquilizer near the start. Overrides older debug scenarios. Restart after changing this setting.");

    internal static IEnumerator SpawnTestSet()
    {
        float deadline = Time.time + 20;
        while (TruckSafetySpawnPoint.instance == null || StatsManager.instance == null || EnemyDirector.instance == null)
        {
            if (Time.time >= deadline) { Plugin.ModLogger.LogError("DEBUG Upscream: start/catalog setup timed out."); yield break; }
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
                PrefabRef prefab = VanillaCatalog.UpscreamPrefab();
                if (DebugSpawnSupport.TryFindPosition(anchor.position, prefab, out Vector3 position))
                {
                    SummonedEnemySpawnTicket.Create(prefab.ResourcePath, position, prepareOnMesh: true, allowUpscreamMimic: true);
                    DebugSpawnSupport.ReservePosition(position);
                    Plugin.ModLogger.LogInfo($"DEBUG Upscream mimic requested at {position}. Approach within 20m without cover to trigger a random Big valuable disguise; grab to reveal and attack, break to defeat.");
                    created = true;
                }
            }
            catch (Exception e) { Plugin.ModLogger.LogError($"DEBUG Upscream spawn failed: {e}"); yield break; }
            if (created) yield break;
            yield return new WaitForSeconds(.5f);
        }
        Plugin.ModLogger.LogError("DEBUG Upscream: no connected clear NavMesh near start; weapon spawns ran independently.");
    }
}
#endif
