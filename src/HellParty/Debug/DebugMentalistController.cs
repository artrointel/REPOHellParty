#if DEBUG
using HarmonyLib;
using HellParty.Native;
using UnityEngine;

namespace HellParty.Debugging;

internal sealed class DebugMentalistController : MonoBehaviour
{
    internal bool HoldRoaming = true;
    private void Update()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        EnemyParent parent = GetComponent<EnemyParent>();
        if (HoldRoaming && NativeEnemy.Spawned(parent)) parent.SpawnedTimerPause(0.2f);
        if (!Input.GetKeyDown(KeyCode.F6)) return;
        HoldRoaming = !HoldRoaming;
        EnemyFloater mentalist = GetComponentInChildren<EnemyFloater>(true);
        if (HoldRoaming && mentalist != null && NativeEnemy.Spawned(parent) && !NativeEnemy.Dead(mentalist.enemy.Health))
        {
            mentalist.targetPlayer = null;
            // Shield eligibility is host-authoritative. The native state RPC
            // also restores the roaming animation on vanilla participants.
            AccessTools.Method(typeof(EnemyFloater), "UpdateState").Invoke(mentalist,
                new object[] { EnemyFloater.State.Roam });
        }
        Plugin.ModLogger.LogInfo($"DEBUG Mentalist targetless test mode {(HoldRoaming ? "ON: reflection test" : "OFF: native recognition/combat")}.");
    }
}

[HarmonyPatch(typeof(EnemyFloater), nameof(EnemyFloater.OnVision))]
internal static class DebugMentalistVisionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyFloater __instance) =>
        !SemiFunc.IsMasterClientOrSingleplayer() ||
        __instance.GetComponentInParent<DebugMentalistController>() is not DebugMentalistController control || !control.HoldRoaming;
}

[HarmonyPatch(typeof(EnemyFloater), nameof(EnemyFloater.OnGrabbed))]
internal static class DebugMentalistGrabPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyFloater __instance) =>
        !SemiFunc.IsMasterClientOrSingleplayer() ||
        __instance.GetComponentInParent<DebugMentalistController>() is not DebugMentalistController control || !control.HoldRoaming;
}
#endif
