using System.Collections;
using HarmonyLib;
using HellParty.Rules;
using UnityEngine;

namespace HellParty.Native;

internal enum SummonPool { None, Wave, Cleanup }

// Native room prefabs, native setup/registration and native spawn RPCs only.
// Used by monster abilities and global waves.
internal sealed class SummonedEnemySpawnTicket : MonoBehaviour
{
    internal Vector3 Position;
    internal bool Pending = true;
    internal PlayerAvatar? InitialTarget;
    internal bool ShareAttackTarget;
    private SummonPool _pool;
    private string _path = "";
    private readonly SummonReuseClock _reuse = new();

    internal static EnemyParent Create(string path, Vector3 position, PlayerAvatar? target = null, bool shareAttackTarget = false, bool prepareOnMesh = false, bool vanillaOnly = false, SummonPool pool = SummonPool.None)
    {
        if (pool != SummonPool.None && EnemyDirector.instance != null)
            foreach (EnemyParent existing in EnemyDirector.instance.enemiesSpawned)
            {
                if (existing == null || existing.transform.parent != LevelGenerator.Instance.EnemyParent.transform) continue;
                SummonedEnemySpawnTicket parked = existing.GetComponent<SummonedEnemySpawnTicket>();
                if (parked == null || parked._pool != pool || parked._path != path || parked.Pending ||
                    NativeEnemy.Spawned(existing) || !parked._reuse.Ready(Time.timeAsDouble)) continue;
                parked.Prepare(position, target, shareAttackTarget);
                existing.GetComponentInChildren<EnemyHealth>(true).spawnValuableCurrent = 0;
                existing.DespawnedTimerSet(0);
                return existing;
            }
        Vector3 holding = AssetManager.instance.physDisabledPosition;
        if (prepareOnMesh)
        {
            // Wave callers have already certified this point is unseen and
            // navigable for this agent. Creating the agent off-map first emits
            // native NavMesh errors before a later Warp can repair anything.
            GameObject prefab = Resources.Load<GameObject>(path);
            var agent = prefab.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
            holding = position - (agent.transform.position - prefab.transform.position);
        }
        GameObject root = VanillaCatalog.Spawn(path, holding, Quaternion.identity);
        EnemyParent parent = root.GetComponent<EnemyParent>();
        if (vanillaOnly) root.AddComponent<VanillaAbilityExcluded>();
        SummonedEnemySpawnTicket ticket = root.AddComponent<SummonedEnemySpawnTicket>();
        ticket._pool = pool;
        ticket._path = path;
        ticket.Prepare(position, target, shareAttackTarget);
        root.GetComponentInChildren<Enemy>(true).EnemyTeleported(prepareOnMesh ? position : holding);
        NativeEnemy.SetupDone(parent) = true;
        return parent;
    }

    private void Prepare(Vector3 position, PlayerAvatar? target, bool shareAttackTarget)
    {
        Pending = true;
        Position = position;
        InitialTarget = target;
        ShareAttackTarget = shareAttackTarget;
        _reuse.Reset();
    }

    private void Update()
    {
        if (_pool != SummonPool.None)
            _reuse.Observe(Time.timeAsDouble, !Pending && !NativeEnemy.Spawned(GetComponent<EnemyParent>()));
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
        __instance.GetComponent<CleanupPursuer>()?.Born();
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
