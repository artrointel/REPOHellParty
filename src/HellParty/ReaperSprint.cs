using System;
using System.Reflection;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty;

internal sealed class ReaperSprint : MonoBehaviour
{
    private static readonly MethodInfo RunnerState = AccessTools.Method(typeof(EnemyRunner), "UpdateState");
    private static readonly AccessTools.FieldRef<EnemyParent, float> ValuableTimer =
        AccessTools.FieldRefAccess<EnemyParent, float>("valuableSpawnTimer");
    private readonly ReaperSprintClock _clock = new();
    private EnemyParent _parent = null!;
    private EnemyRunner _runner = null!;
    private EnemyHealth _health = null!;
    private EnemyParent? _headParent;
    private Enemy? _head;
    private EnemyHealth _headHealth = null!;
    private PrefabRef? _headPrefab;
    private PlayerAvatar? _target;
    private Vector3 _returnPoint;
    private Vector3 _fallbackPoint;
    private int _savedHealth;
    private float _savedStun;
    private float _prepareDeadline;
    private float _retryAt;
    internal bool Preparing => _clock.Mode == ReaperSprintMode.Preparing;
    internal bool Restoring => _clock.Mode == ReaperSprintMode.Restoring;
    internal bool Hidden => _clock.Mode == ReaperSprintMode.Headman;
    private static bool Valid(PlayerAvatar? player) => player != null && !player.deadSet && !player.isDisabled;

    private void Awake()
    {
        _parent = GetComponent<EnemyParent>();
        _runner = GetComponentInChildren<EnemyRunner>(true);
        _health = _runner.GetComponent<EnemyHealth>();
    }

    private static bool Pursuing(EnemyRunner.State state) => state is EnemyRunner.State.Sneak or EnemyRunner.State.Notice or
        EnemyRunner.State.AttackPlayer or EnemyRunner.State.AttackPlayerOver or EnemyRunner.State.AttackPlayerBackToNavMesh or
        EnemyRunner.State.StuckAttackNotice or EnemyRunner.State.StuckAttack or EnemyRunner.State.LookUnderStart or
        EnemyRunner.State.LookUnder or EnemyRunner.State.LookUnderStop;

    private void Update()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel()) return;
        if (_clock.Mode == ReaperSprintMode.Dead) return;
        if (Preparing)
        {
            if (NativeEnemy.Health(_health) <= 0 || NativeEnemy.Dead(_health)) { SourceDied(); return; }
            if (Time.time >= _prepareDeadline) { CancelPreparation(); Plugin.ModLogger.LogError("Reaper sprint Headman setup timed out."); }
            return;
        }
        if (Hidden)
        {
            if (_head == null || _headParent == null) { Return(); return; }
            if (NativeEnemy.Health(_headHealth) <= 0 || NativeEnemy.Dead(_headHealth)) { HeadDied(); return; }
            NativeEnemy.Health(_health) = NativeEnemy.Health(_headHealth);
            if (_clock.ShouldReturn(Valid(_target), Valid(_target)
                    ? (NativeEnemy.Position(_head) - _target!.transform.position).sqrMagnitude : 0) ||
                !NativeEnemy.Spawned(_headParent)) { Return(); return; }
            // Keep the inherited target; native chase supplies Headman's speed,
            // acceleration and pathing. Do not cancel native stuns or freezes.
            if (!_head.IsStunned() && _head.CurrentState != EnemyState.Stunned)
            {
                _head.TargetPlayerAvatar = _target;
                _head.TargetPlayerViewID = _target!.photonView.ViewID;
                _head.StateChase.VisionTimer = _head.StateChase.VisionTime;
                _head.CurrentState = EnemyState.Chase;
            }
            return;
        }
        if (Restoring || !ReaperSprintClock.Unlocked(DifficultyService.Current) || !NativeEnemy.Spawned(_parent) ||
            NativeEnemy.Health(_health) <= 0 || NativeEnemy.Dead(_health) || _runner.enemy.IsStunned()) return;
        PlayerAvatar target = _runner.targetPlayer;
        if (!Valid(target) || !_clock.Begin(Time.timeAsDouble, Pursuing(_runner.currentState),
                (NativeEnemy.Position(_runner) - target.transform.position).sqrMagnitude)) return;
        _target = target;
        Begin();
    }

    private void Begin()
    {
        try
        {
            PrefabRef prefab = _headPrefab ??= VanillaCatalog.HeadmanPrefab();
            if (!TryPoint(NativeEnemy.Position(_runner), prefab.Prefab.GetComponentInChildren<NavMeshAgent>(true), out Vector3 point) ||
                !TryPoint(NativeEnemy.Position(_runner), _runner.enemy.NavMeshAgent.GetComponent<NavMeshAgent>(), out _fallbackPoint))
            {
                _clock.Cancel();
                Plugin.ModLogger.LogWarning("Reaper sprint: no valid Headman NavMesh point near the current body; retaining Reaper form.");
                return;
            }
            _prepareDeadline = Time.time + 5;
            if (_headParent == null)
            {
                _headParent = SummonedEnemySpawnTicket.Create(prefab.ResourcePath, point, prepareOnMesh: true);
                _headParent.gameObject.AddComponent<ReaperHeadman>().Owner = this;
                _head = _headParent.GetComponentInChildren<Enemy>(true);
                _headHealth = _head.GetComponent<EnemyHealth>();
                _headHealth.spawnValuable = false;
            }
            else
            {
                SummonedEnemySpawnTicket ticket = _headParent.GetComponent<SummonedEnemySpawnTicket>();
                ticket.Position = point;
                ticket.Pending = true;
                NativeEnemy.Spawn(_headParent);
            }
        }
        catch (Exception exception)
        {
            CancelPreparation();
            Plugin.ModLogger.LogError($"Reaper sprint setup failed: {exception}");
        }
    }

    internal void HeadBorn()
    {
        // The original stays active until native Headman registration succeeds.
        if (!Preparing || _head == null || !NativeEnemy.Spawned(_parent) || NativeEnemy.Health(_health) <= 0 ||
            _runner.enemy.IsStunned() || !Valid(_runner.targetPlayer) || !Pursuing(_runner.currentState))
        { CancelPreparation(); return; }
        _target = _runner.targetPlayer;
        if ((NativeEnemy.Position(_runner) - _target.transform.position).sqrMagnitude <= 9) { CancelPreparation(); return; }
        _headHealth.health = _health.health;
        NativeEnemy.Health(_headHealth) = NativeEnemy.Health(_health);
        _headHealth.spawnValuable = false;
        _head.TargetPlayerAvatar = _target;
        _head.TargetPlayerViewID = _target.photonView.ViewID;
        _head.StateChase.VisionTimer = _head.StateChase.VisionTime;
        _head.CurrentState = EnemyState.Chase;
        _clock.Born();
        _parent.Despawn();
    }

    private void CancelPreparation()
    {
        if (Preparing) _clock.Cancel();
        if (_headParent != null && NativeEnemy.Spawned(_headParent)) _headParent.Despawn();
    }

    private void Return()
    {
        if (!Hidden || Time.time < _retryAt) return;
        Vector3 point = _head != null ? NativeEnemy.Position(_head) : NativeEnemy.Position(_runner);
        if (!TryPoint(point, _runner.enemy.NavMeshAgent.GetComponent<NavMeshAgent>(), out _returnPoint))
        {
            // If native AI already hid the temporary body, do not leave both
            // forms invisible. Fall back to the certified departure floor.
            if (_headParent != null && NativeEnemy.Spawned(_headParent) ||
                !TryPoint(_fallbackPoint, _runner.enemy.NavMeshAgent.GetComponent<NavMeshAgent>(), out _returnPoint))
            { _retryAt = Time.time + 1; return; }
        }
        _savedHealth = _head != null ? NativeEnemy.Health(_headHealth) : NativeEnemy.Health(_health);
        _savedStun = _head != null && _head.HasStateStunned ? Mathf.Max(0, _head.StateStunned.stunTimer) : 0;
        _clock.Restore();
        try { NativeEnemy.Spawn(_parent); }
        catch (Exception exception)
        {
            // Keep the visible Headman rather than losing the monster on error.
            _clock.ReturnFailed();
            if (NativeEnemy.Spawned(_parent) && _headParent != null && NativeEnemy.Spawned(_headParent))
            {
                NativeEnemy.Health(_health) = _savedHealth;
                _parent.Despawn();
            }
            _retryAt = Time.time + 1;
            Plugin.ModLogger.LogError($"Reaper sprint return failed: {exception}");
        }
    }

    internal void PlaceForReturn(Enemy enemy)
    {
        enemy.EnemyTeleported(_returnPoint);
        NativeEnemy.FirstPointUsed(_parent) = true;
    }

    internal void SourceBorn()
    {
        if (!Restoring) { _clock.NewLife(); return; }
        NativeEnemy.Health(_health) = Math.Max(1, Math.Min(_health.health, _savedHealth));
        _runner.enemy.NavMeshAgent.Enable();
        _runner.enemy.NavMeshAgent.GetComponent<NavMeshAgent>().enabled = true;
        _runner.enemy.NavMeshAgent.Warp(_returnPoint, true);
        _runner.targetPlayer = Valid(_target) ? _target! : null!;
        if (SemiFunc.IsMultiplayer() && Valid(_target))
            _runner.GetComponent<PhotonView>().RPC("TargetPlayerRPC", RpcTarget.All, _target!.photonView.ViewID);
        RunnerState.Invoke(_runner, new object[] { Valid(_target) ? EnemyRunner.State.AttackPlayer : EnemyRunner.State.Roam });
        if (_savedStun > 0)
        {
            _runner.enemy.StateStunned.stunTimer = _savedStun;
            _runner.enemy.CurrentState = EnemyState.Stunned;
            RunnerState.Invoke(_runner, new object[] { EnemyRunner.State.Stun });
        }
        _clock.Restored();
        if (_headParent != null && NativeEnemy.Spawned(_headParent)) _headParent.Despawn();
    }

    internal void HeadDied()
    {
        if (!Hidden || _head == null) return;
        _clock.Die();
        _headHealth.spawnValuable = false;
        // Award through the original parent's native core rule exactly once.
        // No original spawn/heal or second death is required for this reward.
        _runner.enemy.EnemyTeleported(NativeEnemy.Position(_head));
        NativeEnemy.Health(_health) = 0;
        NativeEnemy.Dead(_health) = true;
        ValuableTimer(_parent) = 10;
        _parent.Despawn();
    }

    internal void SourceDied()
    {
        _clock.Die();
        if (_headParent != null && NativeEnemy.Spawned(_headParent)) _headParent.Despawn();
    }

    private bool TryPoint(Vector3 origin, NavMeshAgent settings, out Vector3 point)
    {
        point = default;
        if (settings == null) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = settings.areaMask };
        if (!NavMesh.SamplePosition(origin, out NavMeshHit hit, 2f, filter) ||
            Physics.Linecast(origin + Vector3.up * .1f, hit.position + Vector3.up * .1f,
                LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)) return false;
        point = hit.position;
        return true;
    }
}

internal sealed class ReaperHeadman : MonoBehaviour { internal ReaperSprint Owner = null!; }

[HarmonyPatch(typeof(EnemyParent), "Spawn")]
internal static class ReaperSprintSpawnGate
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyParent __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return true;
        ReaperHeadman head = __instance.GetComponent<ReaperHeadman>();
        ReaperSprint source = __instance.GetComponent<ReaperSprint>();
        if (head != null && (head.Owner == null || !head.Owner.Preparing) || source != null && source.Hidden)
        { __instance.DespawnedTimerSet(1); return false; }
        return true;
    }
}

[HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.EnemySpawn))]
internal static class ReaperSprintReturnPosition
{
    [HarmonyPrefix]
    private static bool Prefix(Enemy enemy, ref bool __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || enemy.GetComponentInParent<ReaperSprint>() is not ReaperSprint sprint || !sprint.Restoring) return true;
        sprint.PlaceForReturn(enemy);
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(EnemyParent), "SpawnRPC")]
internal static class ReaperSprintBornPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix(EnemyParent __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !NativeEnemy.Spawned(__instance)) return;
        __instance.GetComponent<ReaperSprint>()?.SourceBorn();
        ReaperHeadman head = __instance.GetComponent<ReaperHeadman>();
        if (head != null && head.Owner != null) head.Owner.HeadBorn();
    }
}

[HarmonyPatch(typeof(EnemyHealth), "Death")]
internal static class ReaperSprintDeathPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        EnemyParent parent = NativeEnemy.Parent(__instance);
        parent.GetComponent<ReaperSprint>()?.SourceDied();
        ReaperHeadman head = parent.GetComponent<ReaperHeadman>();
        if (head != null && head.Owner != null) head.Owner.HeadDied();
    }
}
