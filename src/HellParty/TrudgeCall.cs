using System;
using System.Collections;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty;

internal sealed class TrudgeCall : MonoBehaviour
{
    private readonly TrudgeCallCycle _cycle = new();
    private EnemySlowWalker _trudge = null!;
    private PlayerAvatar? _lastTarget;
    private void Awake() => _trudge = GetComponentInChildren<EnemySlowWalker>(true);
    private void Update() { if (EnemyTarget.Valid(_trudge.targetPlayer)) _lastTarget = _trudge.targetPlayer; }
    internal void Begin() => _cycle.Begin();
    internal void Strike(Vector3 point)
    {
        if (!_cycle.Strike(DifficultyService.Current) || StageRuntime.Current == null) return;
        if (EnemyTarget.Valid(_trudge.targetPlayer)) _lastTarget = _trudge.targetPlayer;
        StageRuntime stage = StageRuntime.Current;
        stage.StartCoroutine(Summon(stage, point, _lastTarget));
    }

    private static IEnumerator Summon(StageRuntime stage, Vector3 point, PlayerAvatar? target)
    {
        yield return new WaitForSeconds(TrudgeCallCycle.Delay);
        if (stage == null || StageRuntime.Current != stage || !SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel()) yield break;
        // Roll independently for each slot; duplicate species are allowed.
        for (int i = 0; i < TrudgeCallCycle.Count; i++)
        {
            try
            {
                int choice = UnityEngine.Random.Range(0, 3);
                PrefabRef prefab;
                if (choice == 0) prefab = VanillaCatalog.TickPrefab();
                else if (choice == 1)
                {
                    if (EnemyGnomeDirector.instance == null)
                        VanillaCatalog.Spawn(BirthdayBalloonSpawn.GnomeDirectorPath, Vector3.zero, Quaternion.identity);
                    prefab = VanillaCatalog.GnomePrefab();
                }
                else
                {
                    if (EnemyBangDirector.instance == null)
                        VanillaCatalog.Spawn("Enemies/Enemy - Bang Director", Vector3.zero, Quaternion.identity);
                    prefab = VanillaCatalog.BangerPrefab();
                }
                if (!TryPosition(prefab, point, i, out Vector3 spawn))
                    throw new InvalidOperationException($"No clear walkable Trudge reinforcement point near {point}.");
                EnemyParent parent = SummonedEnemySpawnTicket.Create(prefab.ResourcePath, spawn,
                    EnemyTarget.Valid(target) ? target : null, shareAttackTarget: true, prepareOnMesh: true, vanillaOnly: true);
                parent.gameObject.AddComponent<TrudgeReinforcement>().Target = target;
            }
            catch (Exception exception) { Plugin.ModLogger.LogError($"Trudge reinforcement summon failed: {exception}"); }
        }
    }

    private static bool TryPosition(PrefabRef prefab, Vector3 center, int slot, out Vector3 point)
    {
        NavMeshAgent agent = prefab.Prefab.GetComponentInChildren<NavMeshAgent>(true);
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        for (int i = 0; i < 24; i++)
        {
            float angle = (i + slot * 4) * Mathf.PI / 4;
            float radius = i < 8 ? .6f : i < 16 ? 1.2f : 2f;
            Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit ground, 1.5f, filter) || Mathf.Abs(ground.position.y - center.y) > 2) continue;
            float height = Mathf.Max(.4f, agent.height), clearance = Mathf.Clamp(agent.radius, .12f, height / 2);
            if (Physics.CheckCapsule(ground.position + Vector3.up * (clearance + .05f),
                    ground.position + Vector3.up * (height - clearance + .05f), clearance,
                    LayerMask.GetMask("Default", "PhysGrabObject", "Enemy"), QueryTriggerInteraction.Ignore)) continue;
            point = ground.position; return true;
        }
        point = default; return false;
    }
}

internal sealed class TrudgeReinforcement : MonoBehaviour
{
    internal PlayerAvatar? Target;
    private bool _targetSet;
    internal bool Consumed;
    internal bool KeepPresent
    {
        get
        {
            EnemyParent parent = GetComponent<EnemyParent>();
            EnemyHealth health = parent.GetComponentInChildren<EnemyHealth>(true);
            return !Consumed && SemiFunc.RunIsLevel() && NativeEnemy.Spawned(parent) &&
                !parent.GetComponent<SummonedEnemySpawnTicket>().Pending && !NativeEnemy.Dead(health) && NativeEnemy.Health(health) > 0;
        }
    }
    internal static bool PreventRetirement(Component component)
    {
        TrudgeReinforcement member = component.GetComponentInParent<TrudgeReinforcement>();
        if (!SemiFunc.IsMasterClientOrSingleplayer() || member == null || !member.KeepPresent) return false;
        member.GetComponentInChildren<Enemy>(true).CurrentState = EnemyState.Roaming;
        return true;
    }
    private void Update()
    {
        // No summon lifetime. Keep the native director's distance-based
        // presence timer from retiring an otherwise living reinforcement.
        EnemyParent parent = GetComponent<EnemyParent>();
        if (NativeEnemy.Spawned(parent) && NativeEnemy.Health(parent.GetComponentInChildren<EnemyHealth>(true)) > 0)
            parent.SpawnedTimerSet(60);
        if (_targetSet || !NativeEnemy.Spawned(GetComponent<EnemyParent>())) return;
        EnemyTick tick = GetComponentInChildren<EnemyTick>(true);
        if (tick == null) { _targetSet = true; return; }
        if (tick.currentState == EnemyTick.State.Spawn) return;
        _targetSet = true;
        if (!EnemyTarget.Valid(Target)) return;
        // Preserve Tick's native response to the player's grab beam.
        Enemy enemy = tick.GetComponent<Enemy>();
        enemy.Vision.onVisionTriggeredPlayer = Target;
        enemy.Vision.onVisionTriggeredID = Target!.photonView.ViewID;
        tick.OnVision();
        AccessTools.Method(typeof(EnemyTick), "UpdateState").Invoke(tick, new object[] { EnemyTick.State.Notice });
    }
}

[HarmonyPatch(typeof(EnemyGnome), "UpdateState")]
internal static class TrudgeGnomeStayPatch
{
    [HarmonyPrefix] private static void Prefix(EnemyGnome __instance, ref EnemyGnome.State _state)
    {
        if (_state == EnemyGnome.State.Despawn && TrudgeReinforcement.PreventRetirement(__instance)) _state = EnemyGnome.State.Idle;
    }
}

[HarmonyPatch(typeof(EnemyBang), "UpdateState")]
internal static class TrudgeBangerStayPatch
{
    [HarmonyPrefix] private static void Prefix(EnemyBang __instance, ref EnemyBang.State _state)
    {
        if (_state == EnemyBang.State.Despawn && TrudgeReinforcement.PreventRetirement(__instance)) _state = EnemyBang.State.Idle;
    }
}

[HarmonyPatch(typeof(EnemyTick), "UpdateState")]
internal static class TrudgeTickStayPatch
{
    [HarmonyPrefix] private static void Prefix(EnemyTick __instance, ref EnemyTick.State _state)
    {
        if (_state == EnemyTick.State.Despawn && TrudgeReinforcement.PreventRetirement(__instance)) _state = EnemyTick.State.Idle;
    }
}

[HarmonyPatch(typeof(EnemyBang), "ExplodeRPC")]
internal static class TrudgeBangerConsumedPatch
{
    [HarmonyPrefix] private static void Prefix(EnemyBang __instance, PhotonMessageInfo _info)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.MasterOnlyRPC(_info) &&
            __instance.GetComponentInParent<TrudgeReinforcement>() is TrudgeReinforcement member) member.Consumed = true;
    }
}

[HarmonyPatch(typeof(EnemyParent), "Spawn")]
internal static class TrudgeReinforcementNoRespawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyParent __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<TrudgeReinforcement>() == null) return true;
        SummonedEnemySpawnTicket ticket = __instance.GetComponent<SummonedEnemySpawnTicket>();
        if (ticket != null && ticket.Pending) return true;
        __instance.DespawnedTimerSet(1);
        return false;
    }
}

[HarmonyPatch(typeof(SlowWalkerAttack), nameof(SlowWalkerAttack.SlowWalkerAttackStart))]
internal static class TrudgeCallBeginPatch
{
    [HarmonyPrefix]
    private static void Prefix(SlowWalkerAttack __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.enemy == null ||
            __instance.enemy.GetComponent<EnemySlowWalker>() == null) return;
        EnemyParent parent = NativeEnemy.Parent(__instance.enemy);
        StageRuntime.TryAttach(parent);
        parent.GetComponent<TrudgeCall>()?.Begin();
    }
}

[HarmonyPatch(typeof(SlowWalkerAttack), "ActiveHurtColliderFirstHit")]
internal static class TrudgeCallStrikePatch
{
    // Native floor-impact entry; damage is irrelevant to the revised trigger.
    [HarmonyPrefix]
    private static void Prefix(SlowWalkerAttack __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() || __instance.enemy == null ||
            __instance.enemy.GetComponent<EnemySlowWalker>() == null) return;
        EnemyParent parent = NativeEnemy.Parent(__instance.enemy);
        StageRuntime.TryAttach(parent);
        Vector3 point = __instance.transform.position;
        if (Physics.Raycast(point + Vector3.up * .5f, Vector3.down, out RaycastHit floor, 3,
                LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)) point = floor.point;
        parent.GetComponent<TrudgeCall>()?.Strike(point);
    }
}
