using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

[DefaultExecutionOrder(1000)]
internal sealed class HuntsmanAbduction : MonoBehaviour
{
    private static readonly AbductionSlot<HuntsmanAbduction> Slot = new();
    private readonly AbductionClock _clock = new();
    private EnemyHunter _hunter = null!;
    private EnemyShadow? _loom;
    private Enemy _enemy = null!;
    private static readonly AccessTools.FieldRef<EnemyShadow, PlayerAvatar> LoomTarget = AccessTools.FieldRefAccess<EnemyShadow, PlayerAvatar>("playerTarget");
    private static readonly System.Reflection.MethodInfo LoomSetTarget = AccessTools.Method(typeof(EnemyShadow), "UpdatePlayerTarget");
    private static readonly System.Reflection.MethodInfo LoomSetState = AccessTools.Method(typeof(EnemyShadow), "UpdateState");
    private EnemyParent _parent = null!;
    private PlayerAvatar? _victim;
    private bool _collisions;
    private Vector3 _fallback;
    private Transform? _pausedTarget;
    private Vector3 _pausedPoint;
    private readonly HashSet<(int Grabber, int Target)> _pendingReleases = new();
    internal static HuntsmanAbduction? Current => Slot.Owner;
    internal PlayerAvatar? Victim => _victim;
    internal static bool Carrying(EnemyHunter hunter) => Current is HuntsmanAbduction owner &&
        owner._clock.Holding && owner._victim != null && owner._hunter == hunter;
    internal static bool Carrying(EnemyShadow loom) => Current is HuntsmanAbduction owner &&
        owner._clock.Holding && owner._victim != null && owner._loom == loom;
    internal static bool Holds(PlayerTumble tumble) => Current is HuntsmanAbduction owner &&
        owner._victim != null && owner._victim.tumble == tumble;

    private void Awake()
    {
        _parent = GetComponent<EnemyParent>();
        _hunter = GetComponentInChildren<EnemyHunter>(true);
        _loom = GetComponentInChildren<EnemyShadow>(true);
        _enemy = GetComponentInChildren<Enemy>(true);
    }

    private bool Alive => _parent != null && NativeEnemy.Spawned(_parent) &&
        _enemy != null && !NativeEnemy.Dead(_enemy.Health) && NativeEnemy.Health(_enemy.Health) > 0;
    private static bool Valid(PlayerAvatar? player) => player != null && !player.deadSet && !player.isDisabled &&
        player.tumble != null && player.tumble.setup && NetworkLifetime.Live(player) && NetworkLifetime.Live(player.tumble);

    private bool Combat()
    {
        if (_loom != null) return Valid(LoomTarget(_loom)) && _loom.currentState is EnemyShadow.State.Follow or EnemyShadow.State.Slap;
        if (_hunter == null) return false;
        if (_hunter.currentState == EnemyHunter.State.Aim || _hunter.currentState == EnemyHunter.State.Shoot) return true;
        if (_hunter.currentState != EnemyHunter.State.InvestigateWalk || !_hunter.investigatePointHasTransform ||
            _hunter.investigatePointTransform == null) return false;
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
            if (Valid(player) && player.PlayerVisionTarget.VisionTransform == _hunter.investigatePointTransform) return true;
        return false;
    }

    private void FixedUpdate()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (_clock.Holding)
        {
            if (!Alive || !Valid(_victim) || !SemiFunc.RunIsLevel() || _enemy.IsStunned() ||
                _hunter != null && _hunter.currentState == EnemyHunter.State.Stun || _enemy.CurrentState == EnemyState.Despawn)
            { Release(); return; }
            if (!Hold()) { Release(); return; }
            if (_clock.DamageDue(Time.timeAsDouble))
                _victim!.playerHealth.HurtOther(AbductionClock.Damage, Vector3.zero, savingGrace: false, enemyIndex: SemiFunc.EnemyGetIndex(_enemy));
            return;
        }
        TryCapture();
    }

    internal void TryCapture()
    {
        if (!Alive || !SemiFunc.RunIsLevel() || !_clock.Ready(Time.timeAsDouble) || Current != null ||
            _enemy.IsStunned() || !Combat()) return;
        PlayerAvatar? nearest = null;
        int alive = 0;
        double best = double.PositiveInfinity;
        Vector3 origin = NativeEnemy.Position(_enemy);
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!Valid(player)) continue;
            alive++;
            double squared = (player.transform.position - origin).sqrMagnitude;
            if (squared < best) { best = squared; nearest = player; }
        }
        if (nearest == null || !AbductionClock.Eligible(DifficultyService.Current, alive, true, best, _loom != null ? 4 : 2) ||
            !AbductionSight.Clear(_enemy, _parent, nearest) || !BackClear() || !Slot.Acquire(this)) return;
        if (!_clock.Start(Time.timeAsDouble)) { Slot.Release(this); return; }
        _victim = nearest;
        _fallback = nearest.transform.position + Vector3.up * .3f;
        _collisions = nearest.tumble.rb.detectCollisions;
        try
        {
            PhysGrabObject carrierBody = _enemy.Rigidbody.physGrabObject;
            if (nearest.physGrabber != null && nearest.physGrabber.grabbedPhysGrabObject == carrierBody)
                RejectGrab(nearest.physGrabber, carrierBody);
            foreach (PhysGrabber grabber in nearest.tumble.physGrabObject.playerGrabbing.ToArray())
                if (grabber != null && NetworkLifetime.Live(grabber)) grabber.OverrideGrabRelease(nearest.tumble.photonView.ViewID, .5f);
            // We already are the physics authority. Refresh the native timer
            // locally instead of sending a MasterClient RPC every physics tick.
            nearest.tumble.tumbleOverrideTimer = .5f;
            nearest.tumble.TumbleOverride(true);
            nearest.tumble.TumbleSet(true, false);
            if (!nearest.tumble.isTumbling) { Release(); return; }
            // A target pinned behind its pursuer must not make the pursuer
            // endlessly turn towards its own back. Keep that aim point static.
            if (_hunter != null && _hunter.investigatePointHasTransform && _hunter.investigatePointTransform == nearest.PlayerVisionTarget.VisionTransform)
            {
                _pausedTarget = _hunter.investigatePointTransform;
                _pausedPoint = _hunter.investigatePoint;
                _hunter.investigatePointHasTransform = false;
            }
            if (!Hold()) Release();
        }
        catch (Exception e) { Plugin.ModLogger.LogError($"Huntsman abduction failed: {e}"); Release(); }
    }

    private Vector3 BackPosition => _loom != null
        ? _enemy.CenterTransform.position - _enemy.Rigidbody.transform.forward * .8f + Vector3.up * .25f
        : _enemy.Rigidbody.transform.TransformPoint(new Vector3(0, .65f, -.8f));
    private bool BackClear() =>
        !Physics.Linecast(NativeEnemy.Position(_enemy), BackPosition, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore) &&
        !Physics.CheckSphere(BackPosition, .3f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);

    private bool Hold()
    {
        if (_victim == null || !_victim.tumble.isTumbling || !BackClear()) return false;
        if (_loom != null && LoomTarget(_loom) == _victim)
        {
            PlayerAvatar? other = null;
            float closest = float.PositiveInfinity;
            foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
                if (player != _victim && Valid(player) && (player.transform.position - _enemy.transform.position).sqrMagnitude < closest)
                { closest = (player.transform.position - _enemy.transform.position).sqrMagnitude; other = player; }
            if (other != null) LoomSetTarget.Invoke(_loom, new object[] { other });
            LoomSetState.Invoke(_loom, new object[] { other != null ? EnemyShadow.State.Follow : EnemyShadow.State.Leave });
        }
        PlayerTumble tumble = _victim.tumble;
        tumble.tumbleOverrideTimer = .5f;
        tumble.OverrideDisableLookAtCamera(.2f);
        tumble.OverrideEnemyHurt(.2f);
        tumble.DisableCustomGravity(.2f);
        tumble.physGrabObject.OverrideKinematic(.2f);
        tumble.physGrabObject.OverrideGrabDisable(.2f);
        tumble.rb.detectCollisions = false;
        tumble.rb.position = BackPosition;
        tumble.rb.rotation = Quaternion.LookRotation(-_enemy.Rigidbody.transform.forward, Vector3.up);
        return true;
    }

    internal static HuntsmanAbduction? For(PhysGrabObject physical, PhysGrabber? grabber) =>
        SemiFunc.IsMasterClientOrSingleplayer() && Current is HuntsmanAbduction owner &&
        owner._victim != null && (owner._victim.tumble.physGrabObject == physical ||
            grabber != null && grabber.playerAvatar == owner._victim && owner._enemy.Rigidbody.physGrabObject == physical) ? owner : null;

    internal void RejectGrab(PhysGrabber grabber, PhysGrabObject target)
    {
        if (grabber == null || target == null) return;
        var key = (grabber.GetInstanceID(), target.GetInstanceID());
        if (_pendingReleases.Add(key)) StartCoroutine(ReleaseGrab(grabber, target, key));
    }

    private IEnumerator ReleaseGrab(PhysGrabber grabber, PhysGrabObject target, (int Grabber, int Target) key)
    {
        yield return null; // Let the vanilla client's optimistic beam finish setup.
        if (target != null && grabber != null && NetworkLifetime.Live(target) && NetworkLifetime.Live(grabber))
            grabber.OverrideGrabRelease(target.GetComponent<PhotonView>().ViewID, .5f);
        _pendingReleases.Remove(key);
    }

    internal void Release()
    {
        if (!_clock.Holding) { Slot.Release(this); return; }
        _clock.Release(Time.timeAsDouble);
        Slot.Release(this);
        PlayerAvatar? player = _victim;
        _victim = null;
        try
        {
            if (player != null && player.tumble != null)
            {
                PlayerTumble tumble = player.tumble;
                if (tumble.physGrabObject != null) tumble.physGrabObject.OverrideKinematic(0);
                if (tumble.rb != null)
                {
                    tumble.rb.isKinematic = !tumble.isTumbling;
                    tumble.rb.detectCollisions = _collisions;
                }
                tumble.tumbleForce = Vector3.zero;
                tumble.tumbleTorque = Vector3.zero;
                tumble.tumbleOverrideTimer = 0;
                if (NetworkLifetime.Live(tumble))
                {
                    tumble.TumbleOverride(false);
                    if (!player.deadSet && !player.isDisabled && tumble.physGrabObject != null && tumble.rb != null)
                    {
                        Vector3 point = _fallback;
                        if (_enemy != null && SemiFunc.RunIsLevel())
                            for (int slot = 0; slot < 3; slot++)
                                if (VanillaCatalog.Adjacent(NativeEnemy.Position(_enemy), slot, out Vector3 ground) &&
                                    !Physics.CheckSphere(ground + Vector3.up * .4f, .3f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
                                { point = ground + Vector3.up * .4f; break; }
                        tumble.physGrabObject.Teleport(point, tumble.rb.rotation);
                    }
                }
            }
            if (_hunter != null && _pausedTarget != null && !_hunter.investigatePointHasTransform &&
                _hunter.investigatePointTransform == _pausedTarget && _hunter.investigatePoint == _pausedPoint &&
                player != null && !player.deadSet && !player.isDisabled) _hunter.investigatePointHasTransform = true;
        }
        catch (Exception e) { Plugin.ModLogger.LogError($"Huntsman release cleanup failed: {e}"); }
        finally { _pausedTarget = null; }
    }

    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}

// Prevent the attack at its host-authoritative entry, before damage and the
// all-client ShootRPC. Keep native idle/roaming active during transport.
[HarmonyPatch(typeof(EnemyHunter), "StateShoot")]
internal static class HuntsmanAbductionNoShootingPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyHunter __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !HuntsmanAbduction.Carrying(__instance)) return true;
        __instance.UpdateState(EnemyHunter.State.Idle);
        return false;
    }
}

[HarmonyPatch(typeof(EnemyHunter), nameof(EnemyHunter.OnHurt))]
internal static class HuntsmanAbductionHurtPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHunter __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer()) __instance.GetComponentInParent<HuntsmanAbduction>()?.Release();
    }
}

[HarmonyPatch(typeof(EnemyHunter), nameof(EnemyHunter.OnDeath))]
internal static class HuntsmanAbductionDeathPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHunter __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer()) __instance.GetComponentInParent<HuntsmanAbduction>()?.Release();
    }
}

[HarmonyPatch(typeof(EnemyShadow), nameof(EnemyShadow.OnHurt))]
internal static class LoomAbductionHurtPatch
{
    [HarmonyPrefix] private static void Prefix(EnemyShadow __instance) { if (SemiFunc.IsMasterClientOrSingleplayer()) __instance.GetComponentInParent<HuntsmanAbduction>()?.Release(); }
}

[HarmonyPatch(typeof(EnemyShadow), nameof(EnemyShadow.OnDeath))]
internal static class LoomAbductionDeathPatch
{
    [HarmonyPrefix] private static void Prefix(EnemyShadow __instance) { if (SemiFunc.IsMasterClientOrSingleplayer()) __instance.GetComponentInParent<HuntsmanAbduction>()?.Release(); }
}

[HarmonyPatch(typeof(EnemyShadow), "UpdateState")]
internal static class LoomAbductionAttackPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyShadow __instance, ref EnemyShadow.State _nextState)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _nextState != EnemyShadow.State.Slap) return true;
        if (HuntsmanAbduction.Carrying(__instance))
        {
            _nextState = EnemyShadow.State.Follow;
            return true;
        }
        EnemyParent parent = NativeEnemy.Parent(__instance);
        StageRuntime.TryAttach(parent);
        parent.GetComponent<HuntsmanAbduction>()?.TryCapture();
        // Capture chooses Follow (another target) or Leave (solo). Preserve
        // that choice instead of overwriting it with the pending slap.
        return !HuntsmanAbduction.Carrying(__instance);
    }
}

[HarmonyPatch(typeof(PlayerTumble), "FixedUpdate")]
internal static class HuntsmanAbductionPhysicsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerTumble __instance) =>
        !SemiFunc.IsMasterClientOrSingleplayer() || !HuntsmanAbduction.Holds(__instance);
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabStarted")]
internal static class HuntsmanAbductionGrabPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance, PhysGrabber player)
    {
        if (SemiFunc.IsMultiplayer() || HuntsmanAbduction.For(__instance, player) is not HuntsmanAbduction owner) return true;
        owner.RejectGrab(player, __instance);
        return false;
    }
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabStartedRPC")]
internal static class HuntsmanAbductionGrabRpcPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance, int playerPhotonID, PhotonMessageInfo _info)
    {
        PhotonView view = PhotonView.Find(playerPhotonID);
        PhysGrabber? grabber = view != null ? view.GetComponent<PhysGrabber>() : null;
        if (HuntsmanAbduction.For(__instance, grabber) is not HuntsmanAbduction owner) return true;
        if (grabber != null && SemiFunc.OwnerOnlyRPC(_info, grabber.photonView)) owner.RejectGrab(grabber, __instance);
        return false;
    }
}

[HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Hurt))]
internal static class HuntsmanAbductionDamagePatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance, out int __state) => __state = NativeEnemy.Health(__instance);
    [HarmonyPostfix]
    private static void Postfix(EnemyHealth __instance, int __state)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && NativeEnemy.Health(__instance) < __state)
            __instance.GetComponentInParent<HuntsmanAbduction>()?.Release();
    }
}

// Do not accept manual recovery while held, even when a vanilla guest's local
// override message arrives late. Normal recovery resumes immediately on release.
[HarmonyPatch(typeof(PlayerTumble), nameof(PlayerTumble.TumbleRequestRPC))]
internal static class HuntsmanAbductionRecoveryPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerTumble __instance, bool _isTumbling) =>
        !SemiFunc.IsMasterClientOrSingleplayer() || _isTumbling ||
        !HuntsmanAbduction.Holds(__instance);
}
