using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace HellParty.Native;

internal sealed class AnimalSpawnTicket : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<EnemyStateStunned, Enemy> StunOwner =
        AccessTools.FieldRefAccess<EnemyStateStunned, Enemy>("enemy");
    internal Vector3 Position;
    internal bool Pending = true;

    internal static bool IsPreparing(Component component)
    {
        AnimalSpawnTicket ticket = component.GetComponentInParent<AnimalSpawnTicket>();
        if (ticket == null) return false;
        if (ticket.Pending) return true;
        EnemyStateStunned stunned = ticket.GetComponentInChildren<EnemyStateStunned>(true);
        // SpawnRPC may activate a component whose Start has not run yet.
        return stunned != null && StunOwner(stunned) == null;
    }

    internal static EnemyParent Create(Vector3 position, bool offspring)
    {
        // Death may be invoked inside an attack collider's overlap loop. Keep
        // the new body out of that loop until native Setup/Start have finished.
        Vector3 hidden = AssetManager.instance.physDisabledPosition;
        GameObject root = VanillaCatalog.Spawn(VanillaCatalog.AnimalPrefab(), hidden, Quaternion.identity);
        EnemyParent parent = root.GetComponent<EnemyParent>();
        root.AddComponent<AnimalSpawnTicket>().Position = position;
        if (offspring) root.AddComponent<AnimalOffspring>();
        root.GetComponentInChildren<Enemy>(true).EnemyTeleported(hidden);
        NativeEnemy.SetupDone(parent) = true;
        return parent;
    }

    private IEnumerator Start()
    {
        EnemyParent parent = GetComponent<EnemyParent>();
        while (!EnemyDirector.instance.enemiesSpawned.Contains(parent)) yield return null;
        yield return null;
        parent.DespawnedTimerSet(0);
    }
}

[HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.EnemySpawn))]
internal static class AnimalBirthPositionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Enemy enemy, ref bool __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return true;
        AnimalSpawnTicket ticket = enemy.GetComponentInParent<AnimalSpawnTicket>();
        if (ticket == null || !ticket.Pending) return true;
        enemy.EnemyTeleported(ticket.Position);
        NativeEnemy.FirstPointUsed(NativeEnemy.Parent(enemy)) = true;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(EnemyParent), "SpawnRPC")]
internal static class AnimalBornPatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyParent __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !NativeEnemy.Spawned(__instance)) return;
        __instance.GetComponent<AnimalDivision>()?.ResetLife();
        AnimalSpawnTicket ticket = __instance.GetComponent<AnimalSpawnTicket>();
        if (ticket == null || !ticket.Pending) return;
        ticket.Pending = false;
        __instance.GetComponent<AnimalOffspring>()?.Born();
    }
}

[HarmonyPatch(typeof(EnemyParent), "Spawn")]
internal static class AnimalOffspringRespawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyParent __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<AnimalOffspring>() == null) return true;
        if (__instance.GetComponent<AnimalSpawnTicket>().Pending) return true;
        // Retain native registries and stable enemy indices on vanilla peers.
        // The instance remains permanently hidden until scene cleanup.
        __instance.DespawnedTimerSet(1);
        return false;
    }
}
