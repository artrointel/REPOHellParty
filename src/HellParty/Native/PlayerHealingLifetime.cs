using HarmonyLib;
using UnityEngine;

namespace HellParty.Native;

// SharedHealthPacks 1.0.0 calls HealOther for every player from its OnDestroy
// postfix, including unused packs during scene teardown. Scope the native
// callback so those side effects are rejected without depending on that DLL.
[HarmonyPatch(typeof(ItemHealthPack), nameof(ItemHealthPack.OnDestroy))]
internal static class HealthPackDestroyScope
{
    internal static bool SuppressHealing;

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(ItemHealthPack __instance, out bool __state)
    {
        __state = SuppressHealing;
        SuppressHealing |= !__instance.used || !__instance.gameObject.scene.isLoaded ||
            RunManager.instance == null || RunManager.instance.restarting ||
            RunManager.instance.waitToChangeScene || !SemiFunc.RunIsLevel();
    }

    [HarmonyFinalizer]
    private static void Finalizer(bool __state) => SuppressHealing = __state;
}

[HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.HealOther))]
internal static class LivePlayerHealingPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerHealth __instance)
    {
        if (HealthPackDestroyScope.SuppressHealing || __instance == null) return false;
        PlayerAvatar player = __instance.GetComponent<PlayerAvatar>();
        return player != null && player.gameObject.activeInHierarchy && player.gameObject.scene.isLoaded &&
            !player.isDisabled && !player.deadSet && NetworkLifetime.Live(__instance);
    }
}
