using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty;

internal sealed class BowtieBrothers : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<EnemyBowtie, PlayerAvatar> Target =
        AccessTools.FieldRefAccess<EnemyBowtie, PlayerAvatar>("playerTarget");
    private readonly BowtieWindCycle _cycle = new();
    private readonly BowtieRevengeTrigger _revenge = new();
    private EnemyBowtie _bowtie = null!;
    private BowtieBrother? _brother;

    private void Awake()
    {
        _bowtie = GetComponentInChildren<EnemyBowtie>(true);
        // Installed native Bowtie has a wind HurtCollider and a separate
        // leave/contact HurtCollider. Only mark the former as wind.
        foreach (HurtCollider collider in GetComponentsInChildren<HurtCollider>(true))
        {
            if (collider.transform.IsChildOf(_bowtie.hurtColliderLeave)) continue;
            BowtieWindSource source = collider.gameObject.GetComponent<BowtieWindSource>();
            if (source == null) source = collider.gameObject.AddComponent<BowtieWindSource>();
            source.Summoner = GetComponent<EnemyParent>();
        }
    }
    private void Update() => _cycle.Observe(_bowtie.currentState == EnemyBowtie.State.Yell);
    internal void NewLife() => _revenge.NewLife();

    internal void WindStarted()
    {
        if (_bowtie.currentState != EnemyBowtie.State.Yell || !NativeEnemy.Spawned(GetComponent<EnemyParent>())) return;
        EnemyHealth health = _bowtie.GetComponent<EnemyHealth>();
        PlayerAvatar target = Target(_bowtie);
        if (NativeEnemy.Health(health) <= 0 || NativeEnemy.Dead(health) || target == null || target.isDisabled || target.deadSet ||
            !_cycle.TrySummon(true)) return;
        try
        {
            Summon(target.transform.position, target);
        }
        catch (Exception exception) { Plugin.ModLogger.LogError($"Bowtie Brothers summon failed: {exception}"); }
    }

    private void Summon(Vector3 origin, PlayerAvatar? target, bool revenge = false)
    {
        // Both skills share one active summon and the same vanilla lifecycle.
        if (!NavMesh.SamplePosition(origin, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            throw new InvalidOperationException($"No walkable Elsa spawn near {origin}.");
        if (_brother != null) _brother.Retire();
        _brother = BowtieBrother.Create(GetComponent<EnemyParent>(), hit.position, target, revenge);
    }

    internal void Died(PlayerAvatar? killer)
    {
        if (!_revenge.TryQueue(DifficultyService.Current)) return;
        StageRuntime? stage = StageRuntime.Current;
        if (stage == null)
        {
            Plugin.ModLogger.LogError("Cannot schedule Bowtie revenge without an active stage.");
            return;
        }
        Vector3 position = NativeEnemy.Position(_bowtie);
        // The corpse despawns during the delay; retain its death position and
        // run on the stage rather than the disabled enemy object.
        stage.StartCoroutine(RevengeAfterDelay(stage, position, killer));
    }

    private IEnumerator RevengeAfterDelay(StageRuntime stage, Vector3 position, PlayerAvatar? killer)
    {
        yield return new WaitForSeconds(BowtieRevengeTrigger.Delay);
        if (this == null || StageRuntime.Current != stage || !SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() ||
            LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated) yield break;
        PlayerAvatar? target = ValidTarget(killer) ? killer : NearestPlayer(position);
        try { Summon(position, target, true); }
        catch (Exception exception) { Plugin.ModLogger.LogError($"Bowtie revenge summon failed: {exception}"); }
    }

    private static bool ValidTarget(PlayerAvatar? player) => player != null && !player.isDisabled && !player.deadSet;

    internal static PlayerAvatar? NearestPlayer(Vector3 position)
    {
        PlayerAvatar? nearest = null;
        float distance = float.PositiveInfinity;
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!ValidTarget(player)) continue;
            float squared = (player.transform.position - position).sqrMagnitude;
            if (squared < distance) { distance = squared; nearest = player; }
        }
        return nearest;
    }
}

internal sealed class BowtieBrother : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<EnemyElsa, PlayerAvatar> ElsaTarget =
        AccessTools.FieldRefAccess<EnemyElsa, PlayerAvatar>("playerTarget");
    private static readonly MethodInfo UpdateState = AccessTools.Method(typeof(EnemyElsa), "UpdateState");
    private static readonly MethodInfo ShrinkState = AccessTools.Method(typeof(EnemyElsa), "StateTransformBigToSmall");
    private static readonly MethodInfo DespawnState = AccessTools.Method(typeof(EnemyElsa), "StateDespawnSmall");
    private readonly BowtieBrotherLife _life = new();
    private EnemyParent _parent = null!;
    private EnemyElsa _elsa = null!;
    private EnemyHealth _health = null!;
    private PlayerAvatar? _target;
    private bool _chooseFallbackOnSpawn;
    private int _despawnFrame = -1;
    internal EnemyParent Summoner = null!;
    internal bool Retired => _life.Retired;

    private void Awake()
    {
        _parent = GetComponent<EnemyParent>();
        _elsa = GetComponentInChildren<EnemyElsa>(true);
        _health = _elsa.GetComponent<EnemyHealth>();
    }

    internal static BowtieBrother Create(EnemyParent summoner, Vector3 position, PlayerAvatar? target, bool chooseFallbackOnSpawn = false)
    {
        EnemyParent parent = SummonedEnemySpawnTicket.Create(VanillaCatalog.ElsaPrefab().ResourcePath, position);
        BowtieBrother brother = parent.gameObject.AddComponent<BowtieBrother>();
        brother.Summoner = summoner;
        brother._target = target;
        brother._chooseFallbackOnSpawn = chooseFallbackOnSpawn;
        brother._health.spawnValuable = false;
        return brother;
    }

    internal void Born()
    {
        if (_life.Retired) return;
        _life.Start(Time.time);
        _health.spawnValuable = false;
        if (_chooseFallbackOnSpawn && (_target == null || _target.isDisabled || _target.deadSet))
            _target = BowtieBrothers.NearestPlayer(NativeEnemy.Position(_elsa));
        if (_target == null || _target.isDisabled || _target.deadSet) { Retire(); return; }
        ElsaTarget(_elsa) = _target;
        if (SemiFunc.IsMultiplayer())
            _elsa.GetComponent<PhotonView>().RPC("UpdatePlayerTargetRPC", RpcTarget.All, _target.photonView.ViewID);
        // This state invokes the vanilla growth animation/RPC and waits 4.1s
        // before entering GoToPlayerBig. Never jump straight to the big model.
        SetState(EnemyElsa.State.TransformSmallToBig);
    }

    private void Update() => TickLife();

    internal void TickLife()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !_life.Started || _life.Retired) return;
        if (NativeEnemy.Health(_health) <= 0 || NativeEnemy.Dead(_health))
        {
            _health.spawnValuable = false;
            _life.Retire();
            return;
        }
        if (_life.Expire(Time.time)) BeginDespawn();
        else if (_target == null || _target.isDisabled || _target.deadSet) Retire();
    }

    internal void Retire()
    {
        if (_life.Retired) return;
        _life.Retire();
        BeginDespawn();
    }

    private void BeginDespawn()
    {
        _health.spawnValuable = false;
        if (!_life.Started || !NativeEnemy.Spawned(_parent) || NativeEnemy.Health(_health) <= 0) return;
        _elsa.enemy.CurrentState = EnemyState.Despawn;
        _elsa.enemy.NavMeshAgent.ResetPath();
        DriveDespawn();
    }

    internal void DriveDespawn()
    {
        if (_despawnFrame == Time.frameCount) return;
        _despawnFrame = Time.frameCount;
        // Native Elsa only has a small-form despawn animation. Finish her
        // native shrink state first, then enter native DespawnSmall. The
        // animation event calls EnemyParent.Despawn; retain the registry entry.
        if (_elsa.IsBig())
        {
            SetState(EnemyElsa.State.TransformBigToSmall);
            ShrinkState.Invoke(_elsa, null);
        }
        else
        {
            SetState(EnemyElsa.State.DespawnSmall);
            DespawnState.Invoke(_elsa, null);
        }
    }

    private void SetState(EnemyElsa.State state) => UpdateState.Invoke(_elsa, new object[] { state });
}

// Host-local markers; monster physics and stun are decided by the host.
internal sealed class BowtieWindSource : MonoBehaviour
{
    internal EnemyParent Summoner = null!;
}

[HarmonyPatch(typeof(HurtCollider), "EnemyHurt")]
internal static class BowtieBrotherWindImmunityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(HurtCollider __instance, Enemy _enemy, ref bool __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _enemy == null) return true;
        BowtieWindSource source = __instance.GetComponent<BowtieWindSource>();
        if (source == null) return true;
        BowtieBrother brother = _enemy.GetComponentInParent<BowtieBrother>();
        if (brother == null || brother.Summoner == null || brother.Summoner != source.Summoner) return true;
        // Skip the whole native wind hit, including stun/freeze/knockback,
        // across every native detection route (including trigger colliders).
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(EnemyBowtieAnim), nameof(EnemyBowtieAnim.YellStart))]
internal static class BowtieWindStartedPatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyBowtieAnim __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        EnemyParent parent = NativeEnemy.Parent(__instance.controller);
        StageRuntime.TryAttach(parent);
        parent.GetComponent<BowtieBrothers>()?.WindStarted();
    }
}

[HarmonyPatch(typeof(EnemyHealth), "Death")]
internal static class BowtieRevengeDeathPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !DifficultyRules.BowtieRevengeUnlocked(DifficultyService.Current) ||
            __instance.GetComponent<EnemyBowtie>() == null) return;
        EnemyParent parent = NativeEnemy.Parent(__instance);
        StageRuntime.TryAttach(parent);
        parent.GetComponent<BowtieBrothers>()?.Died(EnemyDamageSource.Current);
    }
}

[HarmonyPatch(typeof(EnemyBowtie), nameof(EnemyBowtie.OnSpawn))]
internal static class BowtieRevengeNewLifePatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyBowtie __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
            NativeEnemy.Parent(__instance).GetComponent<BowtieBrothers>()?.NewLife();
    }
}

[HarmonyPatch(typeof(EnemyElsa), "Update")]
internal static class BowtieBrotherDespawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyElsa __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return true;
        BowtieBrother brother = NativeEnemy.Parent(__instance).GetComponent<BowtieBrother>();
        if (brother == null) return true;
        brother.TickLife();
        if (!brother.Retired || NativeEnemy.Health(__instance.GetComponent<EnemyHealth>()) <= 0) return true;
        if (NativeEnemy.Spawned(NativeEnemy.Parent(__instance))) brother.DriveDespawn();
        // Do not let stunned/chasing logic interrupt an expired summon.
        return false;
    }
}

[HarmonyPatch(typeof(EnemyHealth), "Death")]
internal static class BowtieBrotherNoOrbPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && __instance.GetComponentInParent<BowtieBrother>() != null)
            __instance.spawnValuable = false;
    }
}

[HarmonyPatch(typeof(EnemyParent), "Spawn")]
internal static class BowtieBrotherNoRespawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyParent __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return true;
        BowtieBrother brother = __instance.GetComponent<BowtieBrother>();
        if (brother == null) return true;
        SummonedEnemySpawnTicket ticket = __instance.GetComponent<SummonedEnemySpawnTicket>();
        if (!brother.Retired && ticket != null && ticket.Pending) return true;
        __instance.DespawnedTimerSet(1);
        return false;
    }
}
