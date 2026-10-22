using HarmonyLib;
using HellParty.Native;
using UnityEngine;

namespace HellParty;

[HarmonyPatch(typeof(EnemyParent), "Spawn")]
internal static class CloneAutomaticRespawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyParent __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return true;
        DuckClone clone = __instance.GetComponent<DuckClone>();
        if (clone == null) return true;
        bool permitted = !clone.Retired && __instance.GetComponent<DuckSpawnTicket>().Pending;
        if (!permitted) __instance.DespawnedTimerSet(1);
        return permitted;
    }
}

[HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.EnemySpawn))]
internal static class AdjacentDuckSpawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Enemy enemy, ref bool __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return true;
        DuckSpawnTicket ticket = enemy.GetComponentInParent<DuckSpawnTicket>();
        if (ticket == null || !ticket.Pending) return true;
        enemy.EnemyTeleported(ticket.Position);
        NativeEnemy.FirstPointUsed(NativeEnemy.Parent(enemy)) = true;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(EnemyParent), "SpawnRPC")]
internal static class DuckSpawnedPatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyParent __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !NativeEnemy.Spawned(__instance)) return;
        DuckSpawnTicket ticket = __instance.GetComponent<DuckSpawnTicket>();
        EnemyDuck? duck = NativeEnemy.Duck(__instance);
        if (ticket != null && duck != null)
        {
            if (ticket.SavedHealth.HasValue) NativeEnemy.Health(duck.GetComponent<EnemyHealth>()) = ticket.SavedHealth.Value;
            ticket.SavedHealth = null;
            ticket.Pending = false;
            ticket.HasSpawned = true;
        }
        StageRuntime.TryAttach(__instance);
    }
}

[HarmonyPatch(typeof(EnemyHealth), "Death")]
internal static class DuckDeathPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<EnemyDuck>() == null) return;
        EnemyParent parent = NativeEnemy.Parent(__instance);
        DuckClone clone = parent.GetComponent<DuckClone>();
        if (clone != null)
        {
            __instance.spawnValuable = false;
            clone.Died();
        }
        else parent.GetComponent<DuckFamily>()?.OriginalDied();
    }
}

[HarmonyPatch(typeof(EnemyParent), nameof(EnemyParent.Despawn))]
internal static class CloneDropPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyParent __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && __instance.GetComponent<DuckClone>() != null && NativeEnemy.Duck(__instance) is EnemyDuck duck)
            duck.GetComponent<EnemyHealth>().spawnValuable = false;
    }
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabStartedRPC")]
internal static class PinnedCrownGrabPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance) => __instance.GetComponent<CrownCarrier>() is not CrownCarrier crown || !crown.IsPinned;
}
