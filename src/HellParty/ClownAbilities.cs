using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

internal sealed class ClownAbilities : MonoBehaviour
{
    private readonly MentalistReflectionRules _shots = new();
    private readonly List<ClownStaff> _staves = new();
    private EnemyBeamer _clown = null!;
    private EnemyHealth _health = null!;
    private EnemyParent _parent = null!;
    private PrefabRef? _staffPrefab;
    internal PrefabRef StaffPrefab => _staffPrefab ??= VanillaCatalog.Valuable(p => p.GetComponent<ValuableWizardStaff>() != null);
    internal Enemy Enemy = null!;
    internal bool Alive => _parent != null && NativeEnemy.Spawned(_parent) && !NativeEnemy.Dead(_health) && NativeEnemy.Health(_health) > 0;
    internal bool Dead => _health != null && (NativeEnemy.Dead(_health) || NativeEnemy.Health(_health) <= 0);

    private void Awake()
    {
        _parent = GetComponent<EnemyParent>();
        _clown = GetComponentInChildren<EnemyBeamer>(true);
        Enemy = _clown.GetComponent<Enemy>();
        _health = _clown.GetComponent<EnemyHealth>();
    }

    internal void Attack()
    {
        if (!DifficultyRules.ClownStaffUnlocked(DifficultyService.Current) || !Alive || !Valid(_clown.playerTarget)) return;
        SpawnStaff(_clown.playerTarget, 0);
    }

    internal bool Shield(GunBulletSource bullet, PlayerAvatar attacker)
    {
        if (!DifficultyRules.ClownShieldUnlocked(DifficultyService.Current) || !Alive) return false;
        ReflectedShotDecision decision = _shots.Observe(bullet.Shot, _clown.playerTarget != null);
        if (decision == ReflectedShotDecision.Ignore) return false;
        if (decision == ReflectedShotDecision.Reflect) SpawnStaff(attacker, MentalistReflectionRules.Delay);
        return true;
    }

    private static bool Valid(PlayerAvatar? player) => player != null && !player.deadSet && !player.isDisabled;
    private void SpawnStaff(PlayerAvatar target, double delay)
    {
        try
        {
            Vector3 center = Enemy.CenterTransform.position;
            for (int i = 0; i < 8; i++)
            {
                float angle = (i + _staves.Count) * Mathf.PI / 4;
                Vector3 point = center + Vector3.up * .3f + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1.4f;
                if (Physics.CheckSphere(point, .25f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore) ||
                    Physics.Linecast(center, point, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)) continue;
                _staves.Add(ClownStaff.Create(this, point, target.PlayerVisionTarget.VisionTransform.position, delay));
                return;
            }
            throw new InvalidOperationException("No clear Staff position near Clown.");
        }
        catch (Exception exception) { Plugin.ModLogger.LogError($"Clown Staff summon failed: {exception}"); }
    }

    internal void Died() { foreach (ClownStaff staff in _staves.ToArray()) if (staff != null) staff.Reward(); }
    internal void Forget(ClownStaff staff) => _staves.Remove(staff);
    private void OnDestroy() { foreach (ClownStaff staff in _staves.ToArray()) if (staff != null && staff.Pinned) staff.Remove(); }
}

internal sealed class ClownStaff : MonoBehaviour
{
    private ClownAbilities _owner = null!;
    private ValuableWizardStaff _staff = null!;
    private PhysGrabObject _physical = null!;
    private Rigidbody _body = null!;
    private Vector3 _position;
    private Quaternion _rotation;
    private double _fireAt;
    private readonly ClownStaffLife _life = new();
    private readonly StalkingDollGrabAttempts _attempts = new();
    private readonly HashSet<int> _releasing = new();
    internal bool Pinned => _life.Pinned;

    internal static ClownStaff Create(ClownAbilities owner, Vector3 muzzle, Vector3 aim, double delay)
    {
        PrefabRef prefab = owner.StaffPrefab;
        GameObject root = VanillaCatalog.Spawn(prefab, muzzle, Quaternion.identity);
        ClownStaff carrier = root.AddComponent<ClownStaff>();
        carrier._owner = owner;
        carrier._staff = root.GetComponent<ValuableWizardStaff>();
        carrier._physical = root.GetComponent<PhysGrabObject>();
        carrier._body = root.GetComponent<Rigidbody>();
        carrier._fireAt = Time.timeAsDouble + delay;
        Quaternion local = Quaternion.Inverse(root.transform.rotation) * carrier._staff.laserTransform.rotation;
        carrier._rotation = Quaternion.LookRotation(aim - muzzle) * Quaternion.Inverse(local);
        root.transform.rotation = carrier._rotation;
        carrier._position = muzzle - (carrier._staff.laserTransform.position - root.transform.position);
        root.GetComponent<PhysGrabObjectImpactDetector>().destroyDisable = true;
        carrier.Pin();
        return carrier;
    }

    private void Pin()
    {
        _physical.OverrideKinematic(.2f);
        _physical.OverrideZeroGravity(.2f);
        _physical.OverrideIndestructible(.2f);
        transform.SetPositionAndRotation(_position, _rotation);
        _body.position = _position;
        _body.rotation = _rotation;
    }

    private void LateUpdate()
    {
        if (!Pinned) return;
        if (!SemiFunc.RunIsLevel() || _owner == null) { Remove(); return; }
        if (_owner.Dead) { Reward(); return; }
        if (!_owner.Alive) { Remove(); return; }
        Pin();
    }

    private IEnumerator Start()
    {
        yield return null; yield return null;
        double timeout = Time.timeAsDouble + 5;
        while (Pinned && !_physical.spawned && Time.timeAsDouble < timeout) yield return null;
        if (!Pinned || !NetworkLifetime.Live(this)) yield break;
        if (!_physical.spawned) { Remove(); yield break; }
        if (SemiFunc.IsMultiplayer()) GetComponent<PhotonView>().RPC("SetPositionRPC", RpcTarget.All, _position, _rotation);
        else _physical.Teleport(_position, _rotation);
        while (Pinned && Time.timeAsDouble < _fireAt) yield return null;
        if (!Pinned || !NetworkLifetime.Live(this) || _owner == null || !_owner.Alive) yield break;
        _staff.semiLaser.hurtCollider.enemyHost = _owner.Enemy;
        float duration = UnityEngine.Random.Range(1f, 4f); // Native StaffLaser duration.
        if (SemiFunc.IsMultiplayer()) GetComponent<PhotonView>().RPC("StaffLaserRPC", RpcTarget.All, duration);
        else _staff.StaffLaserRPC(duration);
        double until = Time.timeAsDouble + duration;
        while (Pinned && Time.timeAsDouble < until) yield return null;
        if (_owner != null && _owner.Dead) Reward();
        else if (Pinned) Remove();
    }

    internal void Reject(PhysGrabber grabber)
    {
        if (!Pinned || grabber == null || grabber.playerAvatar == null || grabber.playerAvatar.deadSet) return;
        int id = grabber.GetInstanceID();
        if (_attempts.Accept(id, Time.time)) grabber.playerAvatar.playerHealth.HurtOther(10, Vector3.zero, savingGrace: false);
        if (_releasing.Add(id)) StartCoroutine(Release(grabber, id));
    }
    private IEnumerator Release(PhysGrabber grabber, int id)
    {
        yield return null;
        if (Pinned && grabber != null && NetworkLifetime.Live(this) && NetworkLifetime.Live(grabber))
            grabber.OverrideGrabRelease(GetComponent<PhotonView>().ViewID, StalkingDollGrabAttempts.ReleaseLockout);
        _releasing.Remove(id);
    }

    internal void Reward()
    {
        if (!_life.Reward()) return;
        StopAllCoroutines();
        if (NetworkLifetime.Live(this))
        {
            if (SemiFunc.IsMultiplayer()) GetComponent<PhotonView>().RPC("StaffLaserRPC", RpcTarget.All, 0f);
            else _staff.StaffLaserRPC(0);
        }
        _staff.semiLaser.hurtCollider.enemyHost = null;
        GetComponent<PhysGrabObjectImpactDetector>().destroyDisable = false;
        _physical.OverrideKinematic(0);
        _physical.OverrideZeroGravity(0);
        _physical.OverrideIndestructible(0);
        _body.isKinematic = false;
        _body.detectCollisions = true;
    }
    internal void Remove() { if (_life.Remove()) NetworkLifetime.Remove(gameObject); }
    private void OnDestroy() { if (_owner != null) _owner.Forget(this); }
}

[HarmonyPatch(typeof(EnemyBeamer), "UpdateState")]
internal static class ClownAttackPatch
{
    [HarmonyPrefix] private static void Prefix(EnemyBeamer __instance, out EnemyBeamer.State __state) => __state = __instance.currentState;
    [HarmonyPostfix]
    private static void Postfix(EnemyBeamer __instance, EnemyBeamer.State __state)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __state == __instance.currentState ||
            (__instance.currentState != EnemyBeamer.State.Attack && (__instance.currentState != EnemyBeamer.State.Melee || !__instance.meleePlayer))) return;
        StageRuntime.TryAttach(NativeEnemy.Parent(__instance));
        __instance.GetComponentInParent<ClownAbilities>()?.Attack();
    }
}

[HarmonyPatch(typeof(HurtCollider), "EnemyHurt")]
internal static class ClownShieldPatch
{
    [HarmonyPrefix]
    private static bool Prefix(HurtCollider __instance, Enemy _enemy, ref bool __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() || !DifficultyRules.ClownShieldUnlocked(DifficultyService.Current) ||
            !__instance.enemyLogic || _enemy.GetComponent<EnemyBeamer>() == null ||
            __instance.GetComponentInParent<GunBulletSource>() is not GunBulletSource bullet || bullet.Reflected) return true;
        PlayerAvatar attacker = __instance.playerCausingHurtOverride != null ? __instance.playerCausingHurtOverride : __instance.playerCausingHurt;
        if (attacker == null || attacker.deadSet || attacker.isDisabled) return true;
        StageRuntime.TryAttach(NativeEnemy.Parent(_enemy));
        ClownAbilities shield = _enemy.GetComponentInParent<ClownAbilities>();
        if (shield == null || !shield.Shield(bullet, attacker)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(EnemyBeamer), nameof(EnemyBeamer.OnDeath))]
internal static class ClownDeathPatch
{
    [HarmonyPrefix] private static void Prefix(EnemyBeamer __instance) { if (SemiFunc.IsMasterClientOrSingleplayer()) __instance.GetComponentInParent<ClownAbilities>()?.Died(); }
}

[HarmonyPatch(typeof(ValuableWizardStaff), "FixedUpdate")]
internal static class ClownStaffRecoilPatch
{
    [HarmonyPrefix] private static bool Prefix(ValuableWizardStaff __instance) => __instance.GetComponent<ClownStaff>() is not ClownStaff staff || !staff.Pinned;
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabStarted")]
internal static class ClownStaffGrabPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance, PhysGrabber player)
    {
        if (SemiFunc.IsMultiplayer() || __instance.GetComponent<ClownStaff>() is not ClownStaff staff || !staff.Pinned) return true;
        staff.Reject(player); return false;
    }
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabStartedRPC")]
internal static class ClownStaffGrabRpcPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance, int playerPhotonID, PhotonMessageInfo _info)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<ClownStaff>() is not ClownStaff staff || !staff.Pinned) return true;
        PhysGrabber? grabber = PhotonView.Find(playerPhotonID)?.GetComponent<PhysGrabber>();
        if (grabber != null && SemiFunc.OwnerOnlyRPC(_info, grabber.photonView)) staff.Reject(grabber);
        return false;
    }
}

[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "Break")]
internal static class ClownStaffBreakPatch
{
    [HarmonyPrefix] private static bool Prefix(PhysGrabObjectImpactDetector __instance) => __instance.GetComponent<ClownStaff>() is not ClownStaff staff || !staff.Pinned;
}

[HarmonyPatch(typeof(ValuableObject), "AddToDollarHaulList")]
internal static class ClownStaffExtractionPatch
{
    [HarmonyPrefix] private static bool Prefix(ValuableObject __instance) => __instance.GetComponent<ClownStaff>() is not ClownStaff staff || !staff.Pinned;
}
