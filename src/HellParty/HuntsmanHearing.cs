using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HellParty;

// Native investigate messages carry a position, not a player ID. Preserve the
// emitter only for the synchronous native call chain, never infer it by distance.
internal static class HuntsmanSoundSource
{
    internal static PlayerAvatar? Current;

    internal static PlayerAvatar? Resolve(Component emitter)
    {
        if (emitter is PlayerVoiceChat voice) return voice.playerAvatar;
        if (emitter is PlayerAvatar player) return player;
        PlayerTumble tumble = emitter.GetComponentInParent<PlayerTumble>();
        if (tumble != null) return tumble.playerAvatar;
        PhysGrabObject physical = emitter.GetComponent<PhysGrabObject>();
        if (physical == null) return null;
        PlayerAvatar? holder = null;
        foreach (PhysGrabber grabber in physical.playerGrabbing)
        {
            if (grabber == null || grabber.playerAvatar == null) continue;
            if (holder != null && holder != grabber.playerAvatar) return null; // Shared noise is not exclusively the captive's.
            holder = grabber.playerAvatar;
        }
        // The native last-grabber attribution expires after three seconds and
        // also covers an item that has just been released or thrown.
        return holder != null ? holder : physical.grabbedTimer > 0 ? physical.lastPlayerGrabbing : null;
    }
}

[HarmonyPatch]
internal static class HuntsmanSoundSourceScope
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PlayerVoiceChat), "Update");
        yield return AccessTools.Method(typeof(PlayerAvatar), "Footstep");
        yield return AccessTools.Method(typeof(PlayerAvatar), "LandRPC");
        // The one-argument wrapper calls this overload in 0.5. Scope the actual
        // emission, avoiding both ambiguous lookup and nested duplicate scopes.
        yield return AccessTools.Method(typeof(PhysGrabObjectImpactDetector), "EnemyInvestigate", new[] { typeof(float), typeof(Vector3) });
        yield return AccessTools.Method(typeof(ItemGun), "ShootRPC");
        yield return AccessTools.Method(typeof(ItemGun), "StateShooting");
    }

    [HarmonyPrefix]
    private static void Prefix(Component __instance, out PlayerAvatar? __state)
    {
        __state = HuntsmanSoundSource.Current;
        HuntsmanSoundSource.Current = SemiFunc.IsMasterClientOrSingleplayer() && HuntsmanAbduction.Current != null
            ? HuntsmanSoundSource.Resolve(__instance) : null;
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, PlayerAvatar? __state)
    {
        HuntsmanSoundSource.Current = __state;
        return __exception;
    }
}

[HarmonyPatch(typeof(EnemyStateInvestigate), nameof(EnemyStateInvestigate.Set))]
internal static class HuntsmanCaptiveSoundPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyStateInvestigate __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || HuntsmanAbduction.Current is not HuntsmanAbduction carrier ||
            carrier.Victim == null || HuntsmanSoundSource.Current != carrier.Victim) return true;
        // Filter before Set broadcasts SetRPC and invokes the native event.
        // Other Hunters and all other enemies still receive the same sound.
        return __instance.GetComponentInParent<HuntsmanAbduction>() != carrier;
    }
}
