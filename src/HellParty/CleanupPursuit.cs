using System;
using System.Collections;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty;

internal sealed class CleanupHeadPursuit : MonoBehaviour
{
    internal readonly CleanupHeadCycle Cycle = new();
    internal static bool Valid(PlayerAvatar? target) => target != null && !target.deadSet && !target.isDisabled && NetworkLifetime.Live(target);

    internal static CleanupHeadPursuit Marker(EnemyBombThrowerHead head) =>
        head.GetComponent<CleanupHeadPursuit>() ?? head.gameObject.AddComponent<CleanupHeadPursuit>();

    internal static void Exploded(StageRuntime stage, Vector3 position, PlayerAvatar target, bool upscream) =>
        stage.StartCoroutine(SummonAfterDelay(stage, position, target, upscream));

    private static IEnumerator SummonAfterDelay(StageRuntime stage, Vector3 position, PlayerAvatar target, bool upscream)
    {
        yield return new WaitForSeconds(CleanupPursuitRules.Delay);
        if (stage == null || StageRuntime.Current != stage || !SemiFunc.IsMasterClientOrSingleplayer() ||
            !SemiFunc.RunIsLevel() || !Valid(target)) yield break;
        try
        {
            PrefabRef prefab = upscream ? VanillaCatalog.UpscreamPrefab() : VanillaCatalog.SpewerPrefab();
            if (!FindPosition(prefab, position, target, out Vector3 point))
            {
                Plugin.ModLogger.LogWarning($"Cleanup Crew pursuit skipped: no connected clear spawn near {position}.");
                yield break;
            }
            EnemyParent parent = SummonedEnemySpawnTicket.Create(prefab.ResourcePath, point, prepareOnMesh: true);
            CleanupPursuer pursuer = parent.gameObject.AddComponent<CleanupPursuer>();
            pursuer.Target = target;
            pursuer.Stage = stage;
            parent.GetComponentInChildren<EnemyHealth>(true).spawnValuable = false;
        }
        catch (Exception e) { Plugin.ModLogger.LogError($"Cleanup Crew pursuit summon failed: {e}"); }
    }

    private static bool FindPosition(PrefabRef prefab, Vector3 origin, PlayerAvatar target, out Vector3 result)
    {
        NavMeshAgent settings = prefab.Prefab.GetComponentInChildren<NavMeshAgent>(true);
        var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = settings.areaMask };
        result = default;
        if (!NavMesh.SamplePosition(target.transform.position, out NavMeshHit destination, 3, filter)) return false;
        float height = Mathf.Max(.3f, settings.height);
        float radius = Mathf.Clamp(settings.radius, .12f, height / 2);
        var path = new NavMeshPath();
        // Prefer the explosion point, then nearby clear floor, never another room
        // through a wall or an island on which the summon cannot pursue anyone.
        for (int index = 0; index < 25; index++)
        {
            float angle = (index - 1) * Mathf.PI / 4;
            float ring = index == 0 ? 0 : .75f * (1 + (index - 1) / 8);
            Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ring;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3, filter) ||
                (hit.position - origin).sqrMagnitude > 9 ||
                Physics.Linecast(origin + Vector3.up * .15f, hit.position + Vector3.up * .15f,
                    LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore) ||
                Physics.CheckCapsule(hit.position + Vector3.up * (radius + .05f),
                    hit.position + Vector3.up * (height - radius + .05f), radius,
                    LayerMask.GetMask("Default", "PhysGrabObject", "Enemy"), QueryTriggerInteraction.Ignore) ||
                !NavMesh.CalculatePath(hit.position, destination.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            result = hit.position;
            return true;
        }
        return false;
    }
}

internal sealed class CleanupPursuer : MonoBehaviour
{
    private readonly CleanupSummonLife _life = new();
    internal PlayerAvatar Target = null!;
    internal StageRuntime Stage = null!;
    private EnemyParent _parent = null!;
    private Enemy _enemy = null!;
    private EnemySlowMouth? _spewer;
    private EnemyUpscream? _upscream;
    private bool _targetSet;
    internal bool Retired => _life.Retired;

    private void Awake()
    {
        _parent = GetComponent<EnemyParent>();
        _enemy = GetComponentInChildren<Enemy>(true);
        _spewer = GetComponentInChildren<EnemySlowMouth>(true);
        _upscream = GetComponentInChildren<EnemyUpscream>(true);
        _enemy.Health.spawnValuable = false;
    }

    internal void Born()
    {
        _life.Born(Time.timeAsDouble);
        _enemy.Health.spawnValuable = false;
        if (!CleanupHeadPursuit.Valid(Target)) Retire();
    }

    private void Update() => Tick();

    internal void Tick()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (_life.Retired) { DriveDespawn(); return; }
        if (!_life.Started) return;
        if (NativeEnemy.Dead(_enemy.Health) || NativeEnemy.Health(_enemy.Health) <= 0 || !NativeEnemy.Spawned(_parent))
        { _life.Retire(); return; }
        if (Stage == null || StageRuntime.Current != Stage || !SemiFunc.RunIsLevel() ||
            !CleanupHeadPursuit.Valid(Target) || _life.Tick(Time.timeAsDouble)) { Retire(); return; }
        if (_targetSet || _enemy.IsStunned()) return;
        // Preserve native spawn and notice animations before starting pursuit.
        if (_spewer != null && _spewer.currentState == EnemySlowMouth.State.Spawn) return;
        if (_upscream != null && _upscream.currentState == EnemyUpscream.State.Spawn) return;
        _enemy.Vision.onVisionTriggeredPlayer = Target;
        _enemy.Vision.onVisionTriggeredID = Target.photonView.ViewID;
        if (_spewer != null)
        {
            // Notice uses its current target before consulting Vision. Populate
            // both via the native target setter before entering that state.
            _spewer.UpdatePlayerTarget(Target);
            _spewer.UpdateState(EnemySlowMouth.State.Notice);
        }
        else if (_upscream != null)
        {
            // A native vision tick may have noticed another player on the very
            // frame the spawn finished. Initialize this summon from the saved
            // head target once; subsequent retargeting stays with native AI.
            _upscream.UpdateState(EnemyUpscream.State.Idle);
            _upscream.OnVision();
        }
        _targetSet = true;
    }

    internal void Retire() { _life.Retire(); DriveDespawn(); }
    internal void AttackApplied()
    {
        if (_life.AttackApplied(_upscream != null)) DriveDespawn();
    }
    private void DriveDespawn()
    {
        _enemy.Health.spawnValuable = false;
        if (!NativeEnemy.Spawned(_parent) || NativeEnemy.Dead(_enemy.Health) || NativeEnemy.Health(_enemy.Health) <= 0) return;
        _enemy.CurrentState = EnemyState.Despawn;
        _enemy.NavMeshAgent.ResetPath();
        if (_spewer != null) _spewer.UpdateState(EnemySlowMouth.State.Despawn);
        // Upscream's native animator watches EnemyState.Despawn and completes
        // through its SetDespawn animation event. Never destroy the instance.
    }
}

[HarmonyPatch(typeof(EnemyBombThrowerHead), "ExplodeRPC")]
internal static class CleanupHeadExplosionPatch
{
    internal sealed class Snapshot
    {
        internal StageRuntime Stage = null!;
        internal PlayerAvatar Target = null!;
        internal Vector3 Position;
        internal bool Upscream;
    }
    [HarmonyPrefix]
    private static void Prefix(EnemyBombThrowerHead __instance, PhotonMessageInfo _info, out Snapshot? __state)
    {
        __state = null;
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.MasterOnlyRPC(_info) || !SemiFunc.RunIsLevel() ||
            __instance.controller == null || __instance.controller.head != __instance || StageRuntime.Current == null) return;
        PlayerAvatar target = __instance.playerTarget;
        if (!CleanupHeadPursuit.Marker(__instance).Cycle.Exploded(DifficultyService.Current, CleanupHeadPursuit.Valid(target))) return;
        __state = new Snapshot { Stage = StageRuntime.Current, Target = target, Position = __instance.transform.position,
            Upscream = CleanupPursuitRules.Upscream(DifficultyService.Current) };
    }
    [HarmonyPostfix]
    private static void Postfix(Snapshot? __state)
    {
        if (__state != null && __state.Stage != null)
            CleanupHeadPursuit.Exploded(__state.Stage, __state.Position, __state.Target, __state.Upscream);
    }
}

[HarmonyPatch(typeof(EnemyBombThrowerHead), "UpdateStateRPC")]
internal static class CleanupHeadSpawnCyclePatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyBombThrowerHead __instance, EnemyBombThrowerHead.State _state, PhotonMessageInfo _info, out bool __state) =>
        __state = SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.MasterOnlyRPC(_info) &&
            _state == EnemyBombThrowerHead.State.Spawn && __instance.currentState != _state;
    [HarmonyPostfix]
    private static void Postfix(EnemyBombThrowerHead __instance, bool __state)
    {
        if (__state && __instance.currentState == EnemyBombThrowerHead.State.Spawn) CleanupHeadPursuit.Marker(__instance).Cycle.Spawned();
    }
}

[HarmonyPatch(typeof(EnemyParent), "Spawn")]
internal static class CleanupPursuerNoRespawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyParent __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<CleanupPursuer>() is not CleanupPursuer pursuer) return true;
        if (!pursuer.Retired && __instance.GetComponent<SummonedEnemySpawnTicket>() is SummonedEnemySpawnTicket ticket && ticket.Pending) return true;
        __instance.DespawnedTimerSet(1);
        return false;
    }
}

[HarmonyPatch(typeof(EnemyHealth), "Death")]
internal static class CleanupPursuerNoOrbPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && __instance.GetComponentInParent<CleanupPursuer>() != null) __instance.spawnValuable = false;
    }
}

[HarmonyPatch(typeof(EnemyUpscream), "Update")]
internal static class CleanupUpscreamDespawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyUpscream __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponentInParent<CleanupPursuer>() is not CleanupPursuer pursuer) return true;
        pursuer.Tick();
        return !pursuer.Retired;
    }
}

[HarmonyPatch(typeof(EnemySlowMouth), "UpdateState")]
internal static class CleanupSpewerDespawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemySlowMouth __instance, EnemySlowMouth.State newState) =>
        !SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponentInParent<CleanupPursuer>() is not CleanupPursuer pursuer ||
        !pursuer.Retired || newState == EnemySlowMouth.State.Despawn || newState == EnemySlowMouth.State.Dead;
}

// Native Upscream applies a throw here, with landing damage occurring later on
// the owning vanilla client. The agreed hit boundary is completion of this throw.
[HarmonyPatch(typeof(EnemyUpscreamAnim), nameof(EnemyUpscreamAnim.AttackImpulse))]
internal static class CleanupUpscreamThrowPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyUpscreamAnim __instance, out CleanupPursuer? __state)
    {
        __state = null;
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return true;
        CleanupPursuer pursuer = __instance.controller.GetComponentInParent<CleanupPursuer>();
        if (pursuer == null) return true;
        pursuer.Tick();
        if (pursuer.Retired) return false;
        PlayerAvatar target = __instance.controller.targetPlayer;
        if (!CleanupHeadPursuit.Valid(target) || target.tumble == null) return false;
        __state = pursuer;
        return true;
    }

    [HarmonyPostfix]
    private static void Postfix(CleanupPursuer? __state)
    {
        // Run after native force/torque/impact setup, retaining that throw and
        // its later landing damage. Subsequent animation events cannot rethrow.
        if (__state != null) __state.AttackApplied();
    }
}
