#if DEBUG
using System;
using System.Collections;
using BepInEx.Configuration;
using HellParty.Native;
using UnityEngine;

namespace HellParty.Debugging;

internal static class DebugCleanupScenario
{
    internal const int ForcedDifficulty = 5;
    private static ConfigEntry<bool>? _enabled;
    internal static bool Enabled => !DebugUpscreamScenario.Enabled && _enabled?.Value == true;
    internal static int Difficulty { get; private set; } = ForcedDifficulty;
    internal static void Initialize(ConfigFile config) => _enabled = config.Bind("Debug.CleanupCrew", "Enabled", true,
        "Spawn Cleanup Crew near the start alongside any enabled Huntsman test. Difficulty 5; F9 toggles 1/5, F10 requests notice, F11 starts an active head's explosion tell. Debug only; restart after changing this setting.");
    internal static void ToggleDifficulty()
    {
        Difficulty = Difficulty == 5 ? 1 : 5;
        Plugin.ModLogger.LogInfo($"DEBUG difficulty {Difficulty}: next head explosion hatches {(Difficulty == 5 ? "Upscream" : "Spewer")} after 2s, lifetime 10s. Huntsman abduction requires difficulty 2 or higher.");
    }

    internal static IEnumerator SpawnTestSet()
    {
        float deadline = Time.time + 20;
        while (TruckSafetySpawnPoint.instance == null || StatsManager.instance == null || EnemyDirector.instance == null)
        {
            if (Time.time >= deadline) { Plugin.ModLogger.LogError("DEBUG Cleanup Crew: start/catalog setup timed out."); yield break; }
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
                PrefabRef prefab = VanillaCatalog.CleanupCrewPrefab();
                if (DebugSpawnSupport.TryFindPosition(anchor.position, prefab, out Vector3 position))
                {
                    EnemyParent parent = SummonedEnemySpawnTicket.Create(prefab.ResourcePath, position, prepareOnMesh: true);
                    DebugSpawnSupport.ReservePosition(position);
                    parent.gameObject.AddComponent<DebugCleanupController>();
                    Plugin.ModLogger.LogInfo($"DEBUG Cleanup Crew requested at {position}. Difficulty 5. F9: tier 1/5, F10: native notice host, F11: active head explosion tell.");
                    created = true;
                }
            }
            catch (Exception e) { Plugin.ModLogger.LogError($"DEBUG Cleanup Crew spawn failed: {e}"); yield break; }
            if (created) yield break;
            yield return new WaitForSeconds(.5f);
        }
        Plugin.ModLogger.LogError("DEBUG Cleanup Crew: no connected clear NavMesh near start; weapon spawns ran independently.");
    }
}

internal sealed class DebugCleanupController : MonoBehaviour
{
    private void Update()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !DebugCleanupScenario.Enabled) return;
        if (Input.GetKeyDown(KeyCode.F9)) DebugCleanupScenario.ToggleDifficulty();
        EnemyBombThrower cleanup = GetComponentInChildren<EnemyBombThrower>(true);
        if (cleanup == null || !NativeEnemy.Spawned(GetComponent<EnemyParent>()) || NativeEnemy.Dead(cleanup.enemy.Health)) return;
        if (Input.GetKeyDown(KeyCode.F10) && PlayerController.instance != null)
        {
            PlayerAvatar target = PlayerController.instance.playerAvatarScript;
            cleanup.enemy.Vision.onVisionTriggeredPlayer = target;
            cleanup.enemy.Vision.onVisionTriggeredID = target.photonView.ViewID;
            cleanup.OnVision();
            Plugin.ModLogger.LogInfo($"DEBUG Cleanup Crew native notice: {cleanup.currentState}. Wait for its native head attack.");
        }
        if (Input.GetKeyDown(KeyCode.F11))
        {
            if (cleanup.head != null && cleanup.head.currentState == EnemyBombThrowerHead.State.Active)
                cleanup.head.UpdateState(EnemyBombThrowerHead.State.Explode);
            else Plugin.ModLogger.LogInfo("DEBUG Cleanup Crew: F11 requires an active detached head. Wait for the native attack first.");
        }
    }
}
#endif
