using System;
using HarmonyLib;

namespace HellParty.Native;

// EnemyHealth.Hurt does not carry an attacker argument. Preserve only the
// current synchronous native damage call's source, never a stale last hitter.
internal static class EnemyDamageSource
{
    internal static PlayerAvatar? Current;
}

[HarmonyPatch(typeof(HurtCollider), "EnemyHurt")]
internal static class EnemyColliderDamageSourcePatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(HurtCollider __instance, out PlayerAvatar? __state)
    {
        __state = EnemyDamageSource.Current;
        EnemyDamageSource.Current = SemiFunc.IsMasterClientOrSingleplayer()
            ? (__instance.playerCausingHurtOverride != null ? __instance.playerCausingHurtOverride : __instance.playerCausingHurt)
            : null;
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, PlayerAvatar? __state)
    {
        EnemyDamageSource.Current = __state;
        return __exception;
    }
}

[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "OnCollisionStay")]
internal static class EnemyImpactDamageSourcePatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(PhysGrabObjectImpactDetector __instance, out PlayerAvatar? __state)
    {
        __state = EnemyDamageSource.Current;
        PhysGrabObject physical = __instance.GetComponent<PhysGrabObject>();
        // Match the native physical-attack attribution window. An object that
        // was handled long ago is not treated as a current player's kill.
        EnemyDamageSource.Current = SemiFunc.IsMasterClientOrSingleplayer() && physical != null && physical.grabbedTimer > 0
            ? physical.lastPlayerGrabbing : null;
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, PlayerAvatar? __state)
    {
        EnemyDamageSource.Current = __state;
        return __exception;
    }
}
