using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

// Host-driven native physics serialization carries the real, grabbable head.
// State transitions and explosion timers stay entirely with the native head.
internal sealed class CleanupStickyHead : MonoBehaviour
{
    private static readonly MethodInfo SetTarget = AccessTools.Method(typeof(EnemyBombThrowerHead), "UpdatePlayerTarget");
    private readonly CleanupStickyHeadClock<PlayerAvatar> _clock = new();
    private readonly List<(Collider Head, Collider Player)> _ignored = new();
    private EnemyBombThrowerHead _head = null!;
    private PhysGrabObject _physical = null!;
    private Rigidbody _body = null!;
    private RigidbodyInterpolation _interpolation;
    private Collider[] _colliders = null!;
    private float _bottomOffset;
    private bool _physicsPinned;
    internal bool Pinned => _clock.Carrier is not null;

    internal static CleanupStickyHead Marker(EnemyBombThrowerHead head) =>
        head.GetComponent<CleanupStickyHead>() ?? head.gameObject.AddComponent<CleanupStickyHead>();

    private void Awake()
    {
        _head = GetComponent<EnemyBombThrowerHead>();
        _physical = GetComponent<PhysGrabObject>();
        _body = _physical.rb != null ? _physical.rb : GetComponent<Rigidbody>();
        _interpolation = _body.interpolation;
        _colliders = GetComponentsInChildren<Collider>(true);
        float bottom = float.PositiveInfinity;
        foreach (Collider collider in _colliders)
            if (collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger && collider.attachedRigidbody == _body)
                bottom = Mathf.Min(bottom, collider.bounds.min.y - transform.position.y);
        _bottomOffset = float.IsPositiveInfinity(bottom) ? -.2f : bottom;
    }

    internal void NewLife() { ReleasePhysics(); _clock.NewLife(); }
    internal void Stop() { ReleasePhysics(); _clock.Stop(); }
    private void OnDisable() => Stop();
    private void LateUpdate()
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && Pinned && Valid(_clock.Carrier) && _physical.playerGrabbing.Count == 0) Pin();
    }

    internal void Grabbed()
    {
        if (_clock.Detach(Time.timeAsDouble)) ReleasePhysics();
    }

    internal void Tick()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (!SemiFunc.RunIsLevel() || !CleanupStickyHeadClock<PlayerAvatar>.Unlocked(DifficultyService.Current) ||
            _head.controller == null || _head.controller.head != _head || _physical.dead ||
            _head.currentState == EnemyBombThrowerHead.State.Disabled)
        { Stop(); return; }
        bool grabbed = _physical.playerGrabbing.Count > 0;
        if (grabbed) { Grabbed(); return; }
        if (Pinned && !Valid(_clock.Carrier))
        {
            _clock.Detach(Time.timeAsDouble);
            ReleasePhysics();
        }
        if (_clock.Ready(Time.timeAsDouble, grabbed))
        {
            PlayerAvatar? next = null;
            if (Pinned)
            {
                float nearest = float.PositiveInfinity;
                foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
                {
                    if (player == _clock.Carrier || !Valid(player)) continue;
                    float distance = (Eye(player) - transform.position).sqrMagnitude;
                    if (distance < nearest && CleanupStickyHeadClock<PlayerAvatar>.InRange(distance) && Clear(player))
                    { next = player; nearest = distance; }
                }
            }
            else if (Valid(_head.playerTarget) && CleanupStickyHeadClock<PlayerAvatar>.InRange((Eye(_head.playerTarget) - transform.position).sqrMagnitude) && Clear(_head.playerTarget))
                next = _head.playerTarget;
            if (next != null && _clock.Attach(Time.timeAsDouble, next, grabbed))
            {
                RestoreCollisions();
                IgnoreCarrier(next);
                // Preserve native eye/attack target and hatch inheritance on every peer.
                SetTarget.Invoke(_head, new object[] { next });
                _physical.Teleport(Above(next), Rotation(next));
            }
        }
        if (Pinned) Pin();
    }

    private static bool Valid(PlayerAvatar? player) => CleanupHeadPursuit.Valid(player) &&
        player!.PlayerVisionTarget != null && player.PlayerVisionTarget.VisionTransform != null;
    private static Vector3 Eye(PlayerAvatar player) => player.PlayerVisionTarget.VisionTransform.position;
    private Vector3 Above(PlayerAvatar player) => Eye(player) + Vector3.up * (.15f - _bottomOffset);
    private static Quaternion Rotation(PlayerAvatar player) => Quaternion.Euler(0, player.transform.eulerAngles.y, 0);

    private bool Clear(PlayerAvatar player)
    {
        Vector3 ray = Above(player) - transform.position;
        foreach (RaycastHit hit in Physics.RaycastAll(transform.position, ray.normalized, ray.magnitude,
                     SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.GetComponentInParent<PlayerAvatar>() != null ||
                hit.transform.GetComponentInParent<PlayerController>() != null || hit.transform.GetComponentInParent<PlayerTumble>() != null) continue;
            return false;
        }
        return true;
    }

    private void Pin()
    {
        if (!_body.isKinematic) { _body.linearVelocity = Vector3.zero; _body.angularVelocity = Vector3.zero; }
        _physical.OverrideKinematic(.2f);
        _physicsPinned = true;
        _body.interpolation = RigidbodyInterpolation.None;
        // Keep colliders and grab logic enabled; native clients must be able to pull it off.
        Vector3 position = Above(_clock.Carrier!);
        Quaternion rotation = Rotation(_clock.Carrier!);
        transform.SetPositionAndRotation(position, rotation);
        _body.position = position;
        _body.rotation = rotation;
    }

    private void IgnoreCarrier(PlayerAvatar player)
    {
        foreach (Collider body in player.GetComponentsInChildren<Collider>(true))
            foreach (Collider head in _colliders)
                if (head != null && body != null && head != body && head.enabled && body.enabled &&
                    head.gameObject.activeInHierarchy && body.gameObject.activeInHierarchy && !head.isTrigger && !body.isTrigger &&
                    head.attachedRigidbody == _body && !Physics.GetIgnoreCollision(head, body))
                {
                    Physics.IgnoreCollision(head, body, true);
                    _ignored.Add((head, body));
                }
    }
    private void RestoreCollisions()
    {
        foreach (var pair in _ignored)
            if (pair.Head != null && pair.Player != null) Physics.IgnoreCollision(pair.Head, pair.Player, false);
        _ignored.Clear();
    }
    private void ReleasePhysics()
    {
        RestoreCollisions();
        if (_body == null || !_physicsPinned) return;
        _physicsPinned = false;
        _physical.OverrideKinematic(0);
        if (_head.currentState != EnemyBombThrowerHead.State.Disabled) _body.isKinematic = false;
        _body.interpolation = _interpolation;
    }
}

[HarmonyPatch(typeof(EnemyBombThrowerHead), "Update")]
internal static class CleanupStickyHeadUpdatePatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyBombThrowerHead __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.RunIsLevel() &&
            CleanupStickyHeadClock<PlayerAvatar>.Unlocked(DifficultyService.Current) &&
            LevelGenerator.Instance != null && LevelGenerator.Instance.Generated &&
            __instance.currentState != EnemyBombThrowerHead.State.Disabled)
            CleanupStickyHead.Marker(__instance).Tick();
        else __instance.GetComponent<CleanupStickyHead>()?.Stop();
    }
}

[HarmonyPatch(typeof(EnemyBombThrowerHead), "UpdateStateRPC")]
internal static class CleanupStickyHeadStatePatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyBombThrowerHead __instance, EnemyBombThrowerHead.State _state, PhotonMessageInfo _info, out bool __state) =>
        __state = SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.MasterOnlyRPC(_info) && __instance.currentState != _state;
    [HarmonyPostfix]
    private static void Postfix(EnemyBombThrowerHead __instance, EnemyBombThrowerHead.State _state, bool __state)
    {
        if (!__state || __instance.currentState != _state) return;
        if (_state == EnemyBombThrowerHead.State.Spawn && CleanupStickyHeadClock<PlayerAvatar>.Unlocked(DifficultyService.Current))
            CleanupStickyHead.Marker(__instance).NewLife();
        else if (_state == EnemyBombThrowerHead.State.Disabled) __instance.GetComponent<CleanupStickyHead>()?.Stop();
    }
}

[HarmonyPatch(typeof(EnemyBombThrowerHead), "ExplodeRPC")]
internal static class CleanupStickyHeadExplosionPatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyBombThrowerHead __instance, PhotonMessageInfo _info)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.MasterOnlyRPC(_info)) __instance.GetComponent<CleanupStickyHead>()?.Stop();
    }
}

[HarmonyPatch]
internal static class CleanupStickyHeadPhysicsPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(EnemyBombThrowerHead), "StateActive");
        yield return AccessTools.Method(typeof(EnemyBombThrowerHead), "StateExplode");
    }
    [HarmonyPrefix]
    private static bool Prefix(EnemyBombThrowerHead __instance, bool _fixedUpdate) =>
        !SemiFunc.IsMasterClientOrSingleplayer() || !_fixedUpdate || __instance.GetComponent<CleanupStickyHead>() is not CleanupStickyHead sticky || !sticky.Pinned;
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabPlayerAddRPC")]
internal static class CleanupStickyHeadNetworkGrabPatch
{
    [HarmonyPostfix]
    private static void Postfix(PhysGrabObject __instance, int photonViewID, PhotonMessageInfo _info)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.MasterOnlyRPC(_info)) return;
        PhysGrabber? player = PhotonView.Find(photonViewID)?.GetComponent<PhysGrabber>();
        if (player != null && __instance.playerGrabbing.Contains(player)) __instance.GetComponent<CleanupStickyHead>()?.Grabbed();
    }
}

[HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.GrabStarted))]
internal static class CleanupStickyHeadSoloGrabPatch
{
    [HarmonyPostfix]
    private static void Postfix(PhysGrabObject __instance, PhysGrabber player)
    {
        if (!SemiFunc.IsMultiplayer() && __instance.playerGrabbing.Contains(player)) __instance.GetComponent<CleanupStickyHead>()?.Grabbed();
    }
}
