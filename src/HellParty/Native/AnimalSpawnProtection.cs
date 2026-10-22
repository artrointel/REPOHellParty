using HarmonyLib;

namespace HellParty.Native;

// Spawned Animals become interactive only after their native spawn and Start
// initialization. Do not swallow exceptions or change other native monsters.
[HarmonyPatch(typeof(HurtCollider), "EnemyHurt")]
internal static class AnimalBirthColliderPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Enemy _enemy, ref bool __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _enemy == null || !AnimalSpawnTicket.IsPreparing(_enemy)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Hurt))]
internal static class AnimalBirthDamagePatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyHealth __instance) =>
        !SemiFunc.IsMasterClientOrSingleplayer() || !AnimalSpawnTicket.IsPreparing(__instance);
}

[HarmonyPatch(typeof(EnemyStateStunned), nameof(EnemyStateStunned.Set))]
internal static class AnimalBirthStunPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyStateStunned __instance) =>
        !SemiFunc.IsMasterClientOrSingleplayer() || !AnimalSpawnTicket.IsPreparing(__instance);
}

[HarmonyPatch(typeof(EnemyRigidbody), "FixedUpdate")]
internal static class AnimalBirthPhysicsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyRigidbody __instance) =>
        !SemiFunc.IsMasterClientOrSingleplayer() || !AnimalSpawnTicket.IsPreparing(__instance);
}
