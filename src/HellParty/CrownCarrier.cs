using System;
using System.Collections;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

internal sealed class CrownCarrier : MonoBehaviour
{
    private const float CrownHeightScale = 0.7f;
    private const float CrownRearwardOffset = 0.25f;

    private EnemyDuck _duck = null!;
    private PhysGrabObject _physical = null!;
    private Rigidbody _body = null!;
    private bool _released;
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private float _meshBottomOffset;
    private float _createdAt;
    private bool _hidden;

    internal static CrownCarrier Create(EnemyDuck duck)
    {
        PrefabRef prefab = VanillaCatalog.Valuable(p => p.GetComponent<ValuableObject>() != null &&
            p.name.IndexOf("Crown", StringComparison.OrdinalIgnoreCase) >= 0);
        GameObject root = VanillaCatalog.Spawn(prefab, NativeEnemy.Position(duck) + Vector3.up, Quaternion.identity);
        CrownCarrier carrier = root.AddComponent<CrownCarrier>();
        carrier._duck = duck;
        carrier._physical = root.GetComponent<PhysGrabObject>();
        carrier._body = root.GetComponent<Rigidbody>();
        carrier._createdAt = Time.time;
        carrier.MeasureCrown();
        carrier.Pin();
        return carrier;
    }

    private IEnumerator Start()
    {
        yield return null;
        if (_released) yield break;
        MeasureCrown();
        Pin();
        _physical.Teleport(transform.position, transform.rotation);
    }

    private void LateUpdate()
    {
        if (_released || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (_duck == null)
        {
            // Scene cleanup is not a kill/reward. Native room destruction clears RPC cache.
            NetworkLifetime.Remove(gameObject);
            return;
        }
        Pin();
    }

    private void Pin()
    {
        bool spawned = NativeEnemy.Spawned(NativeEnemy.Parent(_duck));
        bool visible = spawned && DifficultyRules.CrownVisible(DifficultyService.Current, Time.time - _createdAt);
        bool revealing = visible && _hidden;
        if (revealing) _physical.OverrideDeactivateReset();
        _physical.OverrideKinematic(0.2f);
        _physical.OverrideGrabDisable(0.2f);
        _physical.OverrideIndestructible(0.2f);
        _body.detectCollisions = false;
        _body.interpolation = RigidbodyInterpolation.None;
        // headLookAtSource is a rig look-direction helper, not a head socket.
        // Use the visible duck geometry, anchored to the physical body, instead.
        Quaternion rotation = Quaternion.Euler(0, _duck.bodyTransform.eulerAngles.y, 0);
        // Keep the reward location current even throughout the hidden interval.
        if (spawned) { _lastPosition = AboveDuck(); _lastRotation = rotation; }
        if (!visible) _physical.OverrideDeactivate(0.2f);
        Vector3 position = visible ? _lastPosition : AssetManager.instance.physDisabledPosition;
        // Native Teleport RPC snaps vanilla guests back from the hidden position.
        if (revealing) _physical.Teleport(position, rotation);
        transform.SetPositionAndRotation(position, rotation);
        _body.position = position;
        _body.rotation = rotation;
        _hidden = !visible;
    }

    private void MeasureCrown()
    {
        bool found = false;
        float bottom = float.PositiveInfinity;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
            bottom = Mathf.Min(bottom, renderer.bounds.min.y);
            found = true;
        }
        _meshBottomOffset = found ? bottom - transform.position.y : 0;
    }

    private Vector3 AboveDuck()
    {
        Vector3 body = NativeEnemy.Position(_duck);
        float top = body.y + 0.5f;
        foreach (Renderer renderer in NativeEnemy.Parent(_duck).GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer)) continue;
            Bounds bounds = renderer.bounds;
            if ((bounds.center - body).sqrMagnitude < 9f && bounds.size.sqrMagnitude < 36f)
                top = Mathf.Max(top, bounds.max.y);
        }
        float height = (top + 0.08f - _meshBottomOffset - body.y) * CrownHeightScale;
        Vector3 forward = Quaternion.Euler(0, _duck.bodyTransform.eulerAngles.y, 0) * Vector3.forward;
        return body + Vector3.up * height - forward * CrownRearwardOffset;
    }

    internal bool IsPinned => !_released;

    internal void Release()
    {
        if (_released) return;
        _released = true;
        _physical.OverrideDeactivateReset();
        _body.detectCollisions = true;
        _body.interpolation = RigidbodyInterpolation.Interpolate;
        _physical.Teleport(_lastPosition, _lastRotation);
        // Timed native kinematic/grab/indestructible overrides expire naturally.
        Destroy(this);
    }
}
