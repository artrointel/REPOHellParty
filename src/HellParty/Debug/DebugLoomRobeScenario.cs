#if DEBUG
using System;
using System.Collections;
using BepInEx.Configuration;
using HellParty.Native;
using UnityEngine;

namespace HellParty.Debugging;

internal static class DebugLoomRobeScenario
{
    internal const int ForcedDifficulty = 5;
    private static ConfigEntry<bool>? _enabled;
    internal static bool Enabled => _enabled?.Value == true;
    internal static void Initialize(ConfigFile config) => _enabled = config.Bind("Debug.LoomRobe", "Enabled", true,
        "Test one Loom and one Robe with native Stalking Doll companions at difficulty 5. Allow solo abduction only on the debug Loom. F8 releases its captive. Restart after changing this setting.");
    internal static bool SoloEnabledFor(EnemyParent parent) => Enabled && parent.GetComponent<DebugSoloLoom>() != null;

    internal static IEnumerator SpawnTestSet()
    {
        float deadline = Time.time + 20;
        while (TruckSafetySpawnPoint.instance == null || StatsManager.instance == null || EnemyDirector.instance == null)
        {
            if (Time.time >= deadline) { Plugin.ModLogger.LogError("DEBUG Loom/Robe: start/catalog setup timed out."); yield break; }
            yield return null;
        }
        Transform anchor = TruckSafetySpawnPoint.instance.transform;
        DebugSpawnSupport.SpawnWeapon(anchor, false);
        DebugSpawnSupport.SpawnWeapon(anchor, true);
        bool loomCreated = false, robeCreated = false;
        deadline = Time.time + 20;
        while (Time.time < deadline && (!loomCreated || !robeCreated))
        {
            if (!loomCreated) loomCreated = TrySpawn(anchor.position, true);
            if (!robeCreated) robeCreated = TrySpawn(anchor.position, false);
            if (!loomCreated || !robeCreated) yield return new WaitForSeconds(.5f);
        }
        if (!loomCreated || !robeCreated)
            Plugin.ModLogger.LogError($"DEBUG Loom/Robe: no connected clear NavMesh within 20m for {(loomCreated ? "Robe" : robeCreated ? "Loom" : "either specimen")}. Weapons were prepared independently.");
    }

    private static bool TrySpawn(Vector3 origin, bool loom)
    {
        try
        {
            PrefabRef prefab = loom ? VanillaCatalog.LoomPrefab() : VanillaCatalog.RobePrefab();
            if (!DebugSpawnSupport.TryFindPosition(origin, prefab, out Vector3 point)) return false;
            EnemyParent parent = SummonedEnemySpawnTicket.Create(prefab.ResourcePath, point, prepareOnMesh: true);
            if (loom) parent.gameObject.AddComponent<DebugSoloLoom>();
            DebugSpawnSupport.ReservePosition(point);
            Plugin.ModLogger.LogInfo($"DEBUG {(loom ? "Loom" : "Robe")}: requested at {point}, difficulty 5; normal companion spawning enabled.{(loom ? " Solo abduction enabled; F8 releases." : "")}");
            return true;
        }
        catch (Exception exception)
        {
            Plugin.ModLogger.LogError($"DEBUG {(loom ? "Loom" : "Robe")} spawn failed: {exception}");
            return false;
        }
    }
}

internal sealed class DebugSoloLoom : MonoBehaviour
{
    private void Update()
    {
        if (DebugLoomRobeScenario.Enabled && SemiFunc.IsMasterClientOrSingleplayer() && Input.GetKeyDown(KeyCode.F8))
            GetComponent<HuntsmanAbduction>()?.Release();
    }
}
#endif
