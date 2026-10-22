using System;
using System.Collections;
using HarmonyLib;
using HellParty.Native;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

// One companion for each native Loom/Robe instance for the whole stage.
// Native despawn/reappearance must not duplicate companions or their rewards.
internal sealed class StalkingDollCompanion : MonoBehaviour
{
    private bool _created;
    private bool _pending;

    internal void OnNativeSpawn()
    {
        if (_created || _pending) return;
        _pending = true;
        StartCoroutine(SpawnNearOwner());
    }

    private IEnumerator SpawnNearOwner()
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
                Vector3 origin = parent.Enemy.transform.position;
                for (int slot = 0; slot < 3; slot++)
                {
                    if (!VanillaCatalog.Adjacent(origin, slot, out Vector3 point) ||
                        Physics.CheckCapsule(point + Vector3.up * 0.25f, point + Vector3.up * 0.65f, 0.2f,
                            LayerMask.GetMask("Default", "PhysGrabObject"), QueryTriggerInteraction.Ignore)) continue;
                    try
                    {
                        StalkingDoll.Spawn(point, parent.Enemy.transform.rotation);
                        _created = true;
                    }
                    catch (Exception exception) { Plugin.ModLogger.LogError($"Stalking Doll companion spawn failed: {exception}"); }
                    _pending = false;
                    yield break;
                }
            }
            yield return new WaitForSeconds(0.25f);
        }
        _pending = false;
        if (parent != null && NativeEnemy.Spawned(parent))
            Plugin.ModLogger.LogWarning($"No clear adjacent NavMesh position for Stalking Doll companion: {parent.name}.");
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
