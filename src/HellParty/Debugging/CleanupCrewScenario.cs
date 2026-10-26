#if DEBUG && HELLPARTY_TEST_CLEANUP_CREW
using System;
using System.Collections;
using HellParty.Native;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty.Debugging;

// Compiled only by an explicit test-scenario build. No config or save flags.
internal sealed class CleanupCrewScenario : MonoBehaviour
{
    internal static bool OverrideDifficulty => GameManager.instance != null &&
        SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.RunIsLevel();
    private StageRuntime _stage = null!;
    private bool Active => _stage != null && StageRuntime.Current == _stage && OverrideDifficulty;

    private IEnumerator Start()
    {
        _stage = GetComponent<StageRuntime>();
        float deadline = Time.realtimeSinceStartup + 60;
        while (Active && (PlayerAvatar.instance == null || PlayerAvatar.instance.isDisabled ||
               PlayerAvatar.instance.PlayerVisionTarget == null || PlayerAvatar.instance.PlayerVisionTarget.VisionTransform == null ||
               LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated))
        {
            if (Time.realtimeSinceStartup >= deadline) { Plugin.ModLogger.LogWarning("TEST CleanupCrew: timed out waiting for the local player."); yield break; }
            yield return null;
        }
        if (!Active) yield break;
        PlayerAvatar player = PlayerAvatar.instance!;
        if (player == null || player.deadSet) yield break;
        Plugin.ModLogger.LogWarning("TEST CleanupCrew ENABLED: difficulty 5; shotgun + tranquilizer; one native Cleanup Crew near the starting player. Rebuild without -TestScenario for normal play.");
        // Independent operations: failed placement must never swallow equipment.
        SpawnItem("Items/Item Gun Shotgun", player.transform.position + player.transform.right * .7f + Vector3.up);
        SpawnItem("Items/Item Gun Tranq", player.transform.position - player.transform.right * .7f + Vector3.up);
        PrefabRef? source = null;
        try { source = FindCleanupCrew(); }
        catch (Exception exception) { Plugin.ModLogger.LogError($"TEST CleanupCrew prefab failed: {exception}"); }
        if (source == null) yield break;
        for (int attempt = 0; attempt < 30 && Active; attempt++)
        {
            if (player == null || player.deadSet || player.isDisabled) yield break;
            bool finished = false;
            try
            {
                if (FindPosition(source.Prefab, player, out Vector3 point))
                {
                    SummonedEnemySpawnTicket.Create(source.ResourcePath, point, prepareOnMesh: true);
                    Plugin.ModLogger.LogInfo($"TEST CleanupCrew spawned at {point}; native perception and attack AI unchanged.");
                    finished = true;
                }
            }
            catch (Exception exception) { Plugin.ModLogger.LogError($"TEST CleanupCrew spawn failed: {exception}"); finished = true; }
            if (finished) yield break;
            if (attempt == 0) Plugin.ModLogger.LogWarning("TEST CleanupCrew: no clear nearby floor yet. Leave the truck; retrying placement for 30 seconds.");
            yield return new WaitForSeconds(1);
        }
        if (Active) Plugin.ModLogger.LogWarning("TEST CleanupCrew skipped: no connected clear floor near the player after 30 seconds. Equipment was spawned independently.");
    }

    private static void SpawnItem(string path, Vector3 position)
    {
        try { VanillaCatalog.Spawn(path, position, Quaternion.identity); }
        catch (Exception exception) { Plugin.ModLogger.LogError($"TEST CleanupCrew equipment failed ({path}): {exception}"); }
    }

    private static PrefabRef FindCleanupCrew()
    {
        foreach (var group in new[] { EnemyDirector.instance.enemiesDifficulty1, EnemyDirector.instance.enemiesDifficulty2, EnemyDirector.instance.enemiesDifficulty3 })
        foreach (EnemySetup setup in group)
        foreach (PrefabRef prefab in setup.spawnObjects)
            if (prefab != null && prefab.IsValid() && prefab.Prefab.GetComponentInChildren<EnemyBombThrower>(true) != null) return prefab;
        throw new InvalidOperationException("Native Cleanup Crew prefab not found in enemy presets.");
    }

    private static bool FindPosition(GameObject prefab, PlayerAvatar player, out Vector3 position)
    {
        NavMeshAgent agent = prefab.GetComponentInChildren<NavMeshAgent>(true);
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        Vector3 origin = player.transform.position;
        position = default;
        if (!NavMesh.SamplePosition(origin, out NavMeshHit destination, 5, filter)) return false;
        var path = new NavMeshPath();
        float height = Mathf.Max(.5f, agent.height), radius = Mathf.Clamp(agent.radius, .15f, height / 2);
        Vector3 forward = player.transform.forward;
        for (int i = 0; i < 48; i++)
        {
            Vector3 direction = Quaternion.Euler(0, i * 45, 0) * forward;
            Vector3 candidate = origin + direction * (6 + i / 8);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit ground, 2, filter) ||
                Mathf.Abs(ground.position.y - origin.y) > 2 || (ground.position - origin).sqrMagnitude < 16 ||
                (ground.position - origin).sqrMagnitude > 169 ||
                Physics.CheckCapsule(ground.position + Vector3.up * (radius + .05f), ground.position + Vector3.up * (height - radius + .05f),
                    radius, LayerMask.GetMask("Default", "PhysGrabObject", "Enemy"), QueryTriggerInteraction.Ignore) ||
                Physics.Linecast(ground.position + Vector3.up * 1.5f, player.PlayerVisionTarget.VisionTransform.position,
                    LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore) ||
                !NavMesh.CalculatePath(ground.position, destination.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            position = ground.position;
            return true;
        }
        return false;
    }
}
#endif
