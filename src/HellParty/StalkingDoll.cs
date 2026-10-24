using System;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty;

// Only explicitly spawned dolls receive this host-only AI. Ordinary valuables
// keep their native grab, animation, damage and movement behaviour.
internal sealed class StalkingDoll : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<ScreamDollValuable, bool> LoopPlaying =
        AccessTools.FieldRefAccess<ScreamDollValuable, bool>("loopPlaying");
    private ScreamDollValuable _doll = null!;
    private PhysGrabObject _physical = null!;
    private Rigidbody _body = null!;
    private Animator _animator = null!;
    private HurtCollider[] _hurts = Array.Empty<HurtCollider>();
    private Collider[] _solidColliders = Array.Empty<Collider>();
    private PlayerAvatar? _target;
    private readonly NavMeshPath _path = new();
    private Vector3[] _corners = Array.Empty<Vector3>();
    private int _corner;
    private float _pathAt;
    private float _speed;
    private float _height;
    private Vector3 _centerOffset;
    private float _halfHeight;
    private float _radius;
    private bool _ready;
    private bool _driving;
    private readonly StalkingDollContacts _contacts = new();
    private readonly StalkingDollRage _rage = new();
    private ScreamDollValuable.States? _sentState;
    internal StalkingDollMode Mode { get; private set; } = StalkingDollMode.Waiting;
    internal float MovementSpeed => _speed * _rage.SpeedMultiplier;

    internal void GrabAttempt()
    {
        _rage.GrabAttempt(Time.timeAsDouble);
        if (_ready) ApplyDecision();
    }

    internal void PlayerDied()
    {
        _rage.PlayerDied();
        if (_ready) ApplyDecision();
    }

    internal void ValidHit(bool normalAttack = false) => _rage.Hit(Time.timeAsDouble,
        DifficultyRules.StalkingDollRampageUnlocked(DifficultyService.Current) && normalAttack);

    internal void DamagePlayer(PlayerAvatar player, int damage, bool normalAttack = false)
    {
        bool local = !SemiFunc.IsMultiplayer() || player.photonView.IsMine;
        int before = player.playerHealth.health;
        if (!local) _rage.ExpectRemoteHit(player.playerHealth.GetInstanceID(), Time.timeAsDouble,
            DifficultyRules.StalkingDollRampageUnlocked(DifficultyService.Current) && normalAttack);
        player.playerHealth.HurtOther(damage, Vector3.zero, savingGrace: false);
        if (local && !player.deadSet && player.playerHealth.health < before) ValidHit(normalAttack);
    }

    internal void ConfirmRemoteHit(PlayerHealth player, int before) =>
        _rage.ConfirmRemoteHit(player.GetInstanceID(), Time.timeAsDouble, before, player.health);

    internal static StalkingDoll Spawn(Vector3 ground, Quaternion rotation)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel())
            throw new InvalidOperationException("Stalking Doll must be spawned by the level host.");
        NavMeshAgent loomAgent = VanillaCatalog.LoomPrefab().Prefab.GetComponentInChildren<NavMeshAgent>(true);
        if (loomAgent == null || loomAgent.speed <= 0)
            throw new InvalidOperationException("Loom native pursuit speed could not be read.");
        GameObject root = VanillaCatalog.Spawn(VanillaCatalog.ScreamDollPrefab(), ground + Vector3.up * 0.5f, rotation);
        StalkingDoll doll = root.AddComponent<StalkingDoll>();
        doll._speed = loomAgent.speed;
        root.AddComponent<StalkingDollHealth>();
        return doll;
    }

    private void Start()
    {
        _doll = GetComponent<ScreamDollValuable>();
        _physical = GetComponent<PhysGrabObject>();
        _body = GetComponent<Rigidbody>();
        _physical.OverrideZeroGravity(0);
        _body.useGravity = true;
        _body.rotation = Quaternion.Euler(0, _body.rotation.eulerAngles.y, 0);
        _body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _animator = GetComponent<Animator>();
        _hurts = GetComponentsInChildren<HurtCollider>(true);
        _solidColliders = GetComponentsInChildren<Collider>(true);
        Bounds bounds = new(transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
            if (first) { bounds = renderer.bounds; first = false; }
            else bounds.Encapsulate(renderer.bounds);
        }
        _centerOffset = transform.InverseTransformPoint(bounds.center);
        _halfHeight = Mathf.Max(0.15f, bounds.extents.y * 0.8f);
        _radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.8f, 0.1f, 0.3f);
        _height = transform.position.y - bounds.min.y;
        _ready = true;
        ApplyDecision();
    }

    private static bool Alive(PlayerAvatar? player) => player != null && !player.isDisabled && !player.deadSet;

    private void Update()
    {
        if (_ready && SemiFunc.IsMasterClientOrSingleplayer()) ApplyDecision();
    }

    private void ApplyDecision()
    {
        _rage.Tick(Time.timeAsDouble);
        if (_physical.dead || GetComponent<StalkingDollHealth>() is StalkingDollHealth health && health.Paused || !SemiFunc.RunIsLevel())
        {
            Mode = StalkingDollMode.Waiting;
            SetNativeAttack(false);
            foreach (HurtCollider hurt in _hurts) hurt.enabled = false;
            return;
        }
        PlayerAvatar? nearest = null;
        double best = double.PositiveInfinity;
        int bestId = int.MaxValue;
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!Alive(player)) continue;
            double distance = (player.transform.position - transform.position).sqrMagnitude;
            int id = player.photonView.ViewID;
            if (!StalkingDollRules.Nearer(distance, id, best, bestId)) continue;
            nearest = player; best = distance; bestId = id;
        }
        if (_target != nearest) { _target = nearest; _pathAt = 0; _corners = Array.Empty<Vector3>(); }
        bool seen = !_rage.Enraged && Observed();
        bool reach = Alive(_target) && best <= StalkingDollRules.AttackDistance * StalkingDollRules.AttackDistance &&
            ClearRay(transform.TransformPoint(_centerOffset), _target!.PlayerVisionTarget.VisionTransform.position, _target);
        Mode = StalkingDollRules.Decide(seen, _physical.playerGrabbing.Count > 0, Alive(_target), reach, _rage.Enraged);
        SetNativeAttack(StalkingDollRules.Attacks(Mode));
        foreach (HurtCollider hurt in _hurts)
            hurt.enabled = StalkingDollRules.Attacks(Mode);
    }

    private void SetNativeAttack(bool attack)
    {
        var state = attack ? ScreamDollValuable.States.Active : ScreamDollValuable.States.Idle;
        if (_sentState == state) return;
        _sentState = state;
        if (SemiFunc.IsMultiplayer()) GetComponent<PhotonView>().RPC("SetStateRPC", RpcTarget.All, state);
        else _doll.SetStateRPC(state);
    }

    internal bool Observed()
    {
        Vector3 center = transform.TransformPoint(_centerOffset);
        Vector3[] points = { center, center + Vector3.up * _halfHeight, center - Vector3.up * _halfHeight,
            center + transform.right * _radius, center - transform.right * _radius,
            center + transform.forward * _radius, center - transform.forward * _radius };
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!Alive(player) || player.localCamera == null) continue;
            Transform eye = player.localCamera.GetOverrideTransform();
            // Guests do not send their FOV/aspect ratio. Use a conservative
            // 170 x 140 degree envelope, stopping before common viewport edges.
            float tanV = player.isLocal && Camera.main != null ? Mathf.Tan(Camera.main.fieldOfView * Mathf.Deg2Rad / 2) : Mathf.Tan(70 * Mathf.Deg2Rad);
            float tanH = player.isLocal && Camera.main != null ? tanV * Camera.main.aspect : Mathf.Tan(85 * Mathf.Deg2Rad);
            foreach (Vector3 point in points)
            {
                Vector3 view = eye.InverseTransformPoint(point);
                if (StalkingDollRules.InView(view.x, view.y, view.z, tanH * 1.05f, tanV * 1.05f) && ClearRay(eye.position, point, player)) return true;
            }
        }
        return false;
    }

    private bool ClearRay(Vector3 from, Vector3 to, PlayerAvatar player)
    {
        Vector3 delta = to - from;
        foreach (RaycastHit hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude,
                     SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.GetComponentInParent<PlayerAvatar>() == player ||
                hit.transform.GetComponentInParent<PlayerController>() is PlayerController controller && controller.playerAvatarScript == player ||
                hit.transform.GetComponentInParent<PlayerTumble>() is PlayerTumble tumble && tumble.playerAvatar == player) continue;
            return false;
        }
        return true;
    }

    private void FixedUpdate()
    {
        if (!_ready || !SemiFunc.IsMasterClientOrSingleplayer() || !_physical.spawned || _physical.dead || !SemiFunc.RunIsLevel()) return;
        ApplyDecision();
        if (_physical.playerGrabbing.Count > 0) { ReleaseMotion(); return; }
        if (_body.isKinematic) return; // Respect native setup/deactivation and other temporary physics controls.
        if ((Mode == StalkingDollMode.Following || Mode == StalkingDollMode.Rushing) && Follow()) return;
        ReleaseMotion();
        if (Mode == StalkingDollMode.Attacking && _target != null) Face(_target.transform.position);
    }

    private void ReleaseMotion()
    {
        if (!_driving) return;
        // Stop our horizontal pursuit once, preserving gravity/fall velocity
        // and any impulses applied on subsequent stationary ticks.
        if (!_body.isKinematic)
            _body.velocity = new Vector3(0, _body.velocity.y, 0);
        _driving = false;
        _pathAt = 0;
    }

    private void DriveMotion(Vector3 velocity)
    {
        // Drive only along the floor. Vertical motion remains gravity-driven
        // rather than pinning the doll to a floating NavMesh offset.
        _body.velocity = new Vector3(velocity.x, _body.velocity.y, velocity.z);
        _driving = true;
    }

    private void OnCollisionEnter(Collision collision) => _contacts.Record(collision);
    private void OnCollisionStay(Collision collision) => _contacts.Record(collision);
    private void OnCollisionExit(Collision collision) => _contacts.Forget(collision.collider);

    private bool Follow()
    {
        if (_target == null) return false;
        if (Time.time >= _pathAt)
        {
            _pathAt = Time.time + 0.25f;
            _corners = Array.Empty<Vector3>();
            Vector3 ground = transform.position - Vector3.up * _height;
            if (StalkingDollStep.GroundPosition(_body, _solidColliders, out Vector3 support)) ground = support;
            if (NavMesh.SamplePosition(ground, out NavMeshHit start, 2f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(_target.transform.position, out NavMeshHit end, 3f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _path) && _path.status == NavMeshPathStatus.PathComplete)
            { _corners = _path.corners; _corner = 0; }
        }
        while (_corner < _corners.Length)
        {
            Vector3 gap = _corners[_corner] + Vector3.up * _height - _body.position;
            // NavMesh stairs can be smoothed above the physical tread. A
            // same-XZ corner must not stall a horizontal-only motor forever.
            if (gap.x * gap.x + gap.z * gap.z >= .0144f) break;
            _corner++;
        }
        if (_corner >= _corners.Length) return false;
        Vector3 direction = _corners[_corner] - _body.position;
        direction.y = 0;
        Vector3 delta = Vector3.ClampMagnitude(direction, MovementSpeed * Time.fixedDeltaTime);
        if (delta.sqrMagnitude < .000001f) return false;
        if (StalkingDollStep.TryClimb(_body, _solidColliders, delta))
        {
            // The step already swept and placed the body beyond the ledge.
            // Do not reuse the old pre-step direction/corner this physics tick.
            _pathAt = 0;
            _corners = Array.Empty<Vector3>();
            _corner = 0;
            _driving = false;
            Face(_body.position + delta);
            return true;
        }
        // Let native collisions stop the actual body, then slide along their
        // contact normals. An overlapping speculative sphere must not cancel
        // movement out of a wall or keep two neighbouring dolls locked forever.
        delta = _contacts.Steer(delta);
        DriveMotion(delta / Time.fixedDeltaTime);
        Face(_body.position + delta);
        return true;
    }

    private void Face(Vector3 point)
    {
        Vector3 direction = point - transform.position; direction.y = 0;
        if (direction.sqrMagnitude > 0.0001f)
            _body.MoveRotation(Quaternion.RotateTowards(_body.rotation, Quaternion.LookRotation(direction), 360f * Time.fixedDeltaTime));
    }

    internal void NativeActive()
    {
        _animator.enabled = true;
        _animator.SetBool("active", true);
        LoopPlaying(_doll) = true;
    }
}

[HarmonyPatch(typeof(ScreamDollValuable), "SetState")]
internal static class StalkingDollNativeGrabStatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ScreamDollValuable __instance) =>
        !SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<StalkingDoll>() == null;
}

[HarmonyPatch(typeof(ScreamDollValuable), "StateActive")]
internal static class StalkingDollNativeActivePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ScreamDollValuable __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<StalkingDoll>() is not StalkingDoll doll) return true;
        doll.NativeActive(); // Preserve native animation/sound without random kinematic forces.
        return false;
    }
}
