using System;
using System.Collections;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty;

// One companion for each native Loom/Robe instance for the whole stage.
// Native despawn/reappearance must not duplicate companions or their rewards.
internal sealed class StalkingDollCompanion : MonoBehaviour
{
    private readonly CompanionSpawnAttempt _attempt = new();
    private float _retryAt;
    private bool _warned;

    private void Update()
    {
        if (_attempt.Created || Time.time < _retryAt || !SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel()) return;
        EnemyParent parent = GetComponent<EnemyParent>();
        // A component can attach after the initial SpawnRPC. The native flag
        // proves a successful teleport, rather than a prefab awaiting setup.
        if (NativeEnemy.SetupDone(parent) && NativeEnemy.FirstPointUsed(parent) && NativeEnemy.Spawned(parent) &&
            parent.EnableObject.activeInHierarchy && parent.Enemy.CurrentState != EnemyState.Despawn)
            OnNativeSpawn();
    }

    internal void OnNativeSpawn()
    {
        if (Time.time < _retryAt || !_attempt.TryBegin()) return;
        StartCoroutine(SpawnNearOwner());
    }

    private IEnumerator SpawnNearOwner()
    {
        bool created = false;
        try
        {
            yield return null; // Native teleport and spawn callbacks finish first.
            EnemyParent parent = GetComponent<EnemyParent>();
            float deadline = Time.time + 10;
            while (parent != null && SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.RunIsLevel() &&
                   NativeEnemy.Spawned(parent) && Time.time < deadline)
            {
                if (NativeEnemy.SetupDone(parent) && parent.Enemy != null &&
                    (!parent.Enemy.HasHealth || !NativeEnemy.Dead(parent.Enemy.Health)))
                {
                    try
                    {
                        if (FindPosition(parent, out Vector3 point))
                        {
                            StalkingDoll.Spawn(point, parent.Enemy.Rigidbody.transform.rotation);
                            created = true;
                        }
                    }
                    catch (Exception exception) { Plugin.ModLogger.LogError($"Stalking Doll companion spawn failed: {exception}"); }
                    if (created) yield break;
                }
                yield return new WaitForSeconds(0.25f);
            }
            if (!_warned && parent != null && NativeEnemy.Spawned(parent))
            {
                _warned = true;
                Plugin.ModLogger.LogWarning($"No clear nearby ground for Stalking Doll companion: {parent.enemyName}; will retry while alive.");
            }
        }
        finally
        {
            _attempt.Complete(created);
            _retryAt = Time.time + 5;
        }
    }

    private static bool FindPosition(EnemyParent parent, out Vector3 position)
    {
        position = default;
        Vector3 body = NativeEnemy.Position(parent);
        // Navigation roots can lead the visible body, and the body can float
        // above the mesh. Ground the actual physical position first.
        if (!Physics.Raycast(body + Vector3.up * .25f, Vector3.down, out RaycastHit ground, 6,
                LayerMask.GetMask("Default", "NavmeshOnly"), QueryTriggerInteraction.Ignore)) return false;
        NavMeshAgent loom = VanillaCatalog.LoomPrefab().Prefab.GetComponentInChildren<NavMeshAgent>(true);
        var filter = new NavMeshQueryFilter { agentTypeID = loom.agentTypeID, areaMask = loom.areaMask };
        if (!NavMesh.SamplePosition(ground.point, out NavMeshHit start, .75f, filter)) return false;
        var path = new NavMeshPath();
        for (int ring = 0; ring < 3; ring++)
        for (int slot = 0; slot < 12; slot++)
        {
            float angle = slot * Mathf.PI / 6;
            Vector3 candidate = start.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (1.2f + ring * .8f);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, .5f, filter) ||
                (hit.position - start.position).sqrMagnitude > 12.25f ||
                !NavMesh.CalculatePath(start.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete ||
                Physics.Linecast(start.position + Vector3.up * .3f, hit.position + Vector3.up * .3f,
                    LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore) ||
                Physics.CheckCapsule(hit.position + Vector3.up * .25f, hit.position + Vector3.up * .65f, .2f,
                    LayerMask.GetMask("Default", "PhysGrabObject"), QueryTriggerInteraction.Ignore)) continue;
            position = hit.position;
            return true;
        }
        return false;
    }
}

[HarmonyPatch(typeof(EnemyParent), "SpawnRPC")]
internal static class StalkingDollCompanionSpawnPatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyParent __instance, PhotonMessageInfo _info)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.MasterOnlyRPC(_info) ||
            !SemiFunc.RunIsLevel() || !NativeEnemy.Spawned(__instance)) return;
        StageRuntime.TryAttach(__instance);
        __instance.GetComponent<StalkingDollCompanion>()?.OnNativeSpawn();
    }
}
