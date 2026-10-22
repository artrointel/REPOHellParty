using HarmonyLib;

namespace HellParty.Native;

// Mod-spawned enemies become interactive only after native spawn and Start
// initialization. Includes Animals, balloon Gnomes and the Birthday Boy test.
[HarmonyPatch(typeof(HurtCollider), "EnemyHurt")]
internal static class AnimalBirthColliderPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Enemy _enemy, ref bool __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _enemy == null || !EnemySpawnReadiness.IsPreparing(_enemy)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Hurt))]
internal static class AnimalBirthDamagePatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyHealth __instance) =>
        !SemiFunc.IsMasterClientOrSingleplayer() || !EnemySpawnReadiness.IsPreparing(__instance);
}

[HarmonyPatch(typeof(EnemyStateStunned), nameof(EnemyStateStunned.Set))]
internal static class AnimalBirthStunPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyStateStunned __instance) =>
        !SemiFunc.IsMasterClientOrSingleplayer() || !EnemySpawnReadiness.IsPreparing(__instance);
}

[HarmonyPatch(typeof(EnemyRigidbody), "FixedUpdate")]
internal static class AnimalBirthPhysicsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyRigidbody __instance) =>
        !SemiFunc.IsMasterClientOrSingleplayer() || !EnemySpawnReadiness.IsPreparing(__instance);
}
