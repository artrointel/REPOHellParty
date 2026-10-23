using HarmonyLib;
using HellParty.Native;

namespace HellParty;

// Share Frog durability, visibility, destruction and grab handling with Chef.
// These hooks only run when the shared mimic component was explicitly attached.
[HarmonyPatch(typeof(EnemyUpscream), "Update")]
internal static class UpscreamMimicUpdatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyUpscream __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return true;
        ChefMimic mimic = NativeEnemy.Parent(__instance).GetComponent<ChefMimic>();
        if (mimic == null) return true;
        mimic.Tick();
        return !mimic.Disguised && !mimic.Restoring;
    }

    [HarmonyPostfix]
    private static void Postfix(EnemyUpscream __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer()) NativeEnemy.Parent(__instance).GetComponent<ChefMimic>()?.AfterNativeTick();
    }
}

[HarmonyPatch(typeof(EnemyUpscream), nameof(EnemyUpscream.OnVision))]
internal static class UpscreamMimicVisionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyUpscream __instance) => !SemiFunc.IsMasterClientOrSingleplayer() ||
        NativeEnemy.Parent(__instance).GetComponent<ChefMimic>() is not ChefMimic mimic || mimic.AllowUpscreamVision();
}

[HarmonyPatch(typeof(EnemyUpscream), nameof(EnemyUpscream.OnGrabbed))]
internal static class UpscreamMimicBodyGrabPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyUpscream __instance) => !SemiFunc.IsMasterClientOrSingleplayer() ||
        NativeEnemy.Parent(__instance).GetComponent<ChefMimic>() is not ChefMimic mimic || mimic.Attacking;
}
