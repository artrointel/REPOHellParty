using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace HellParty.Native;

// Native room prefabs, native setup/registration and native spawn RPCs only.
// Used by balloon Gnomes, Bowtie brothers and development scenarios.
internal sealed class SummonedEnemySpawnTicket : MonoBehaviour
{
    internal Vector3 Position;
    internal bool Pending = true;
    internal PlayerAvatar? InitialTarget;
    internal bool ShareAttackTarget;

    internal static EnemyParent Create(string path, Vector3 position, PlayerAvatar? target = null, bool shareAttackTarget = false)
    {
        Vector3 hidden = AssetManager.instance.physDisabledPosition;
        GameObject root = VanillaCatalog.Spawn(path, hidden, Quaternion.identity);
        EnemyParent parent = root.GetComponent<EnemyParent>();
        SummonedEnemySpawnTicket ticket = root.AddComponent<SummonedEnemySpawnTicket>();
        ticket.Position = position;
        ticket.InitialTarget = target;
        ticket.ShareAttackTarget = shareAttackTarget;
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
internal static class SummonedEnemyPositionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Enemy enemy, ref bool __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return true;
        SummonedEnemySpawnTicket ticket = enemy.GetComponentInParent<SummonedEnemySpawnTicket>();
        if (ticket == null || !ticket.Pending) return true;
        enemy.EnemyTeleported(ticket.Position);
        NativeEnemy.FirstPointUsed(NativeEnemy.Parent(enemy)) = true;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(EnemyParent), "SpawnRPC")]
internal static class SummonedEnemyReadyPatch
{
    private static readonly AccessTools.FieldRef<EnemyGnomeDirector, PlayerAvatar> DirectorTarget =
        AccessTools.FieldRefAccess<EnemyGnomeDirector, PlayerAvatar>("playerTarget");
    private static readonly System.Reflection.MethodInfo UpdateDirectorState =
        AccessTools.Method(typeof(EnemyGnomeDirector), "UpdateState");

    [HarmonyPostfix]
    private static void Postfix(EnemyParent __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !NativeEnemy.Spawned(__instance)) return;
        SummonedEnemySpawnTicket ticket = __instance.GetComponent<SummonedEnemySpawnTicket>();
        if (ticket == null || !ticket.Pending) return;
        // EnemyTeleported sets the transform before calling the native Warp.
        // Warp's <1m early return then skips rebinding an agent instantiated
        // off-mesh at the hidden holding position. Bind after SpawnRPC enables
        // the enemy object, and force the native Warp to bypass that shortcut.
        EnemyNavMeshAgent navigation = __instance.GetComponentInChildren<EnemyNavMeshAgent>(true);
        if (navigation != null)
        {
            navigation.Enable();
            // Native Enable only acts when DisableTimer > 0. A disabled agent
            // with no timer (including an off-mesh creation) needs explicit
            // enabling before Warp can bind it to the level's navigation.
            var agent = navigation.GetComponent<UnityEngine.AI.NavMeshAgent>();
            agent.enabled = true;
            navigation.Warp(ticket.Position, true);
            if (!agent.isOnNavMesh)
            {
                agent.enabled = false;
                agent.enabled = true;
                navigation.Warp(ticket.Position, true);
            }
            if (!agent.isOnNavMesh)
                Plugin.ModLogger.LogError($"Summoned enemy navigation could not bind at {ticket.Position}: {__instance.name}");
        }
        ticket.Pending = false;
        __instance.GetComponent<BowtieBrother>()?.Born();
        if (__instance.GetComponentInChildren<EnemyBang>(true) is EnemyBang bang && EnemyBangDirector.instance != null)
        {
            EnemyBangDirector.instance.SetupSingle(bang);
            if (ticket.InitialTarget != null && !ticket.InitialTarget.deadSet && !ticket.InitialTarget.isDisabled)
            {
                EnemyBangDirector.instance.playerTarget = ticket.InitialTarget;
                AccessTools.Method(typeof(EnemyBangDirector), "UpdateState").Invoke(EnemyBangDirector.instance,
                    new object[] { EnemyBangDirector.State.AttackSet });
            }
        }
        if (__instance.GetComponentInChildren<EnemyGnome>(true) != null && ticket.InitialTarget != null &&
            !ticket.InitialTarget.isDisabled && !ticket.InitialTarget.deadSet && EnemyGnomeDirector.instance != null)
        {
            if (ticket.ShareAttackTarget)
            {
                // Native SetTarget ignores a new player while already attacking
                // and may choose a valuable instead. This burst explicitly
                // requests the Birthday Boy's player, using native attack AI.
                DirectorTarget(EnemyGnomeDirector.instance) = ticket.InitialTarget;
                UpdateDirectorState.Invoke(EnemyGnomeDirector.instance, new object[] { EnemyGnomeDirector.State.AttackPlayer });
            }
            else EnemyGnomeDirector.instance.SetTarget(ticket.InitialTarget);
        }
        ticket.InitialTarget = null;
        ticket.ShareAttackTarget = false;
    }
}
