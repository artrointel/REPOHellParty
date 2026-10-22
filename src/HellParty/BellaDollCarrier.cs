using System;
using System.Collections;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

internal sealed class BellaDollCarrier : MonoBehaviour
{
    private EnemyTricycle _bella = null!;
    private Transform _seat = null!;
    private PhysGrabObject _physical = null!;
    private Rigidbody _body = null!;
    private ClownTrap _clown = null!;
    private readonly BellaDollSequence _sequence = new();
    private bool _released;
    private float _bottomOffset;
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private Quaternion _seatRotationOffset;
    internal bool IsPinned => !_released;
    internal Enemy? ExplosionOwner => !_released && _bella != null ? _bella.GetComponent<Enemy>() : null;

    internal static BellaDollCarrier Create(EnemyTricycle bella)
    {
        Transform? seat = null;
        foreach (Transform candidate in NativeEnemy.Parent(bella).GetComponentsInChildren<Transform>(true))
            if (candidate.name == "seat") { seat = candidate; break; }
        if (seat == null) throw new InvalidOperationException("Native Bella seat transform not found.");
        PrefabRef prefab = VanillaCatalog.Valuable(p => p.GetComponent<ClownTrap>() != null);
        GameObject root = VanillaCatalog.Spawn(prefab, seat.position, seat.rotation);
        BellaDollCarrier carrier = root.AddComponent<BellaDollCarrier>();
        carrier._bella = bella;
        carrier._seat = seat;
        carrier._physical = root.GetComponent<PhysGrabObject>();
        carrier._body = root.GetComponent<Rigidbody>();
        carrier._clown = root.GetComponent<ClownTrap>();
        carrier._seatRotationOffset = Quaternion.Inverse(seat.rotation) * Quaternion.Euler(0, bella.followTargetTransform.eulerAngles.y, 0);
        carrier.MeasureBottom();
        carrier.Pin();
        return carrier;
    }

    private IEnumerator Start()
    {
        // Let native ClownTrap/PhysGrabObject Start initialize on all peers.
        yield return null;
        if (_released) yield break;
        MeasureBottom();
        Pin();
        _physical.Teleport(transform.position, transform.rotation);
        while (!_released && !_physical.dead)
        {
            if (NativeEnemy.Spawned(NativeEnemy.Parent(_bella)) && _sequence.TryAdvance(!_clown.trapTriggered))
                _clown.TouchNose();
            yield return null;
        }
    }

    internal void Warning(int stage) => _sequence.ObserveWarning(stage);

    private void LateUpdate()
    {
        if (_released || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (_bella == null)
        {
            if (SemiFunc.IsMultiplayer() && PhotonNetwork.InRoom) PhotonNetwork.Destroy(gameObject);
            else Destroy(gameObject);
            return;
        }
        if (_physical.dead) { _sequence.Stop(); Destroy(this); return; }
        Pin();
    }

    private void MeasureBottom()
    {
        float bottom = float.PositiveInfinity;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        _bottomOffset = float.IsPositiveInfinity(bottom) ? 0 : bottom - transform.position.y;
    }

    private void Pin()
    {
        _physical.OverrideKinematic(0.2f);
        _physical.OverrideGrabDisable(0.2f);
        _body.detectCollisions = false;
        _body.interpolation = RigidbodyInterpolation.None;
        bool visible = NativeEnemy.Spawned(NativeEnemy.Parent(_bella));
        if (!visible) _physical.OverrideDeactivate(0.2f);
        Vector3 position = visible ? _seat.position + Vector3.up * (0.03f - _bottomOffset) : AssetManager.instance.physDisabledPosition;
        // Seat mesh orientation is not necessarily an upright item socket.
        Quaternion rotation = _seat.rotation * _seatRotationOffset;
        transform.SetPositionAndRotation(position, rotation);
        _body.position = position;
        _body.rotation = rotation;
        if (visible) { _lastPosition = position; _lastRotation = rotation; }
    }

    internal void Release()
    {
        if (_released) return;
        _released = true;
        _sequence.Stop();
        _body.detectCollisions = true;
        _body.interpolation = RigidbodyInterpolation.Interpolate;
        _physical.Teleport(_lastPosition, _lastRotation);
        // Native countdown already started before death continues normally.
        Destroy(this);
    }
}

[HarmonyPatch(typeof(ParticleScriptExplosion), nameof(ParticleScriptExplosion.Spawn))]
internal static class BellaDollExplosionOwnerPatch
{
    [HarmonyPostfix]
    private static void Postfix(ParticleScriptExplosion __instance, ParticlePrefabExplosion __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __result == null) return;
        BellaDollCarrier carrier = __instance.GetComponent<BellaDollCarrier>();
        Enemy? owner = carrier != null ? carrier.ExplosionOwner : null;
        if (owner == null) return;
        // The returned explosion has not yet activated its native damage
        // collider (ParticlePrefabExplosion.Update does that). Native
        // HurtCollider.EnemyHurt ignores exactly enemyHost, including every
        // expansion of this explosion, while all other victims stay native.
        __result.HurtCollider.enemyHost = owner;
    }
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabStartedRPC")]
internal static class BellaPinnedDollNetworkGrabPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance) => __instance.GetComponent<BellaDollCarrier>() is not BellaDollCarrier doll || !doll.IsPinned;
}

[HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.GrabStarted))]
internal static class BellaPinnedDollSoloGrabPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance) => __instance.GetComponent<BellaDollCarrier>() is not BellaDollCarrier doll || !doll.IsPinned;
}
