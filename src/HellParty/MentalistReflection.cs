using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

internal sealed class MentalistReflection : MonoBehaviour
{
    private readonly MentalistReflectionRules _shots = new();
    private readonly List<MentalistReflectedGun> _guns = new();
    private EnemyFloater _mentalist = null!;
    private void Awake() => _mentalist = GetComponentInChildren<EnemyFloater>(true);
    internal Enemy Enemy => _mentalist.enemy;
    internal bool Alive => _mentalist != null && NativeEnemy.Spawned(GetComponent<EnemyParent>()) &&
        (!Enemy.HasHealth || !NativeEnemy.Dead(Enemy.Health));

    internal bool Shield(GunBulletSource bullet, PlayerAvatar attacker)
    {
        ReflectedShotDecision decision = _shots.Observe(bullet.Shot, _mentalist.targetPlayer != null);
        if (decision == ReflectedShotDecision.Ignore) return false;
        if (decision == ReflectedShotDecision.AlreadyReflected) return true;
        try
        {
            Vector3 center = Enemy.CenterTransform.position;
            Vector3 point = center + Vector3.up * 0.3f;
            bool found = false;
            for (int i = 0; i < 8; i++)
            {
                float angle = (i + _guns.Count) * Mathf.PI / 4;
                Vector3 candidate = point + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1.4f;
                if (Physics.CheckSphere(candidate, 0.25f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore) ||
                    Physics.Linecast(center, candidate, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)) continue;
                point = candidate; found = true; break;
            }
            if (!found) throw new InvalidOperationException("No clear position for reflected gun near Mentalist.");
            Vector3 aim = attacker.PlayerVisionTarget.VisionTransform.position;
            MentalistReflectedGun gun = MentalistReflectedGun.Create(this, point, aim);
            _guns.Add(gun);
        }
        catch (Exception exception) { Plugin.ModLogger.LogError($"Mentalist reflection failed: {exception}"); }
        return true;
    }

    internal void Forget(MentalistReflectedGun gun) => _guns.Remove(gun);
    private void OnDestroy()
    {
        foreach (MentalistReflectedGun gun in _guns.ToArray())
            if (gun != null) gun.Remove();
    }
}

// One native room-object gun per reflected shot. Its world pose is captured
// once, and never follows the player or re-aims during the two-second warning.
internal sealed class MentalistReflectedGun : MonoBehaviour
{
    private MentalistReflection _owner = null!;
    private ItemGun _gun = null!;
    private PhysGrabObject _physical = null!;
    private Rigidbody _body = null!;
    private Vector3 _position;
    private Quaternion _rotation;
    private double _started;
    private bool _removed;
    internal Enemy? Enemy => _owner != null ? _owner.Enemy : null;

    internal static MentalistReflectedGun Create(MentalistReflection owner, Vector3 muzzle, Vector3 aim)
    {
        GameObject root = VanillaCatalog.Spawn(VanillaCatalog.LaserGunPrefab(), muzzle, Quaternion.identity);
        MentalistReflectedGun carrier = root.AddComponent<MentalistReflectedGun>();
        carrier._owner = owner;
        carrier._gun = root.GetComponent<ItemGun>();
        carrier._physical = root.GetComponent<PhysGrabObject>();
        carrier._body = root.GetComponent<Rigidbody>();
        carrier._started = Time.timeAsDouble;
        Transform laserMuzzle = root.GetComponent<ItemGunLaser>().muzzleTransform;
        Quaternion muzzleLocal = Quaternion.Inverse(root.transform.rotation) * laserMuzzle.rotation;
        carrier._rotation = Quaternion.LookRotation(aim - muzzle) * Quaternion.Inverse(muzzleLocal);
        root.transform.rotation = carrier._rotation;
        carrier._position = muzzle - (laserMuzzle.position - root.transform.position);
        carrier.Pin();
        return carrier;
    }

    private void Pin()
    {
        _physical.OverrideGrabDisable(0.2f);
        _physical.OverrideZeroGravity(0.2f);
        if (!_body.isKinematic) { _body.velocity = Vector3.zero; _body.angularVelocity = Vector3.zero; }
        _physical.OverrideKinematic(0.2f);
        _body.detectCollisions = false;
        transform.SetPositionAndRotation(_position, _rotation);
        _body.position = _position;
        _body.rotation = _rotation;
    }

    private void LateUpdate()
    {
        if (_removed) return;
        if (_owner == null || !_owner.Alive || !SemiFunc.RunIsLevel()) { Remove(); return; }
        Pin();
    }

    private IEnumerator Start()
    {
        yield return null; // Native ItemGun / battery / Photon Start initialization.
        if (_removed) yield break;
        _physical.Teleport(_position, _rotation);
        while (!_removed && !MentalistReflectionRules.Due(Time.timeAsDouble, _started)) yield return null;
        if (_removed) yield break;
        if (_owner != null && _owner.Alive && SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.RunIsLevel())
        {
            // All incoming weapon types use the native laser shooting state.
            if (SemiFunc.IsMultiplayer()) GetComponent<PhotonView>().RPC("StateSetRPC", RpcTarget.All, (int)ItemGun.State.Shooting);
            else AccessTools.Method(typeof(ItemGun), "StateSetRPC").Invoke(_gun,
                new object[] { (int)ItemGun.State.Shooting, default(PhotonMessageInfo) });
            double until = Time.timeAsDouble + Mathf.Max(0.05f, _gun.shootTime);
            while (!_removed && Time.timeAsDouble < until) yield return null;
        }
        yield return null; // Bullet objects have their own native effect lifetime.
        Remove();
    }

    internal void RejectGrab(PhysGrabber player) => StartCoroutine(Release(player));
    private IEnumerator Release(PhysGrabber player)
    {
        yield return null;
        if (player != null) player.OverrideGrabRelease(GetComponent<PhotonView>().ViewID, 0.5f);
    }

    internal void Remove()
    {
        if (_removed) return;
        _removed = true;
        if (SemiFunc.IsMultiplayer() && PhotonNetwork.InRoom) PhotonNetwork.Destroy(gameObject);
        else Destroy(gameObject);
    }

    private void OnDestroy() { if (_owner != null) _owner.Forget(this); }
}

[HarmonyPatch(typeof(HurtCollider), "EnemyHurt")]
internal static class MentalistReflectionHitPatch
{
    [HarmonyPrefix]
    private static bool Prefix(HurtCollider __instance, Enemy _enemy, ref bool __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() || !__instance.enemyLogic ||
            !DifficultyRules.MentalistReflectionUnlocked(DifficultyService.Current) ||
            _enemy.GetComponent<EnemyFloater>() == null ||
            __instance.GetComponentInParent<GunBulletSource>() is not GunBulletSource bullet ||
            bullet.Reflected) return true;
        PlayerAvatar attacker = __instance.playerCausingHurtOverride != null ? __instance.playerCausingHurtOverride : __instance.playerCausingHurt;
        if (attacker == null || attacker.deadSet || attacker.isDisabled) return true;
        EnemyParent parent = NativeEnemy.Parent(_enemy);
        StageRuntime.TryAttach(parent);
        MentalistReflection shield = parent.GetComponent<MentalistReflection>();
        if (shield == null || !shield.Alive || !shield.Shield(bullet, attacker)) return true;
        __result = false; // Skip native damage, stun, freeze and physical enemy knockback.
        return false;
    }
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabStarted")]
internal static class MentalistReflectedGunGrabPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance, PhysGrabber player)
    {
        if (SemiFunc.IsMultiplayer() || __instance.GetComponent<MentalistReflectedGun>() is not MentalistReflectedGun gun) return true;
        gun.RejectGrab(player);
        return false;
    }
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabStartedRPC")]
internal static class MentalistReflectedGunGrabRpcPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance, int playerPhotonID, PhotonMessageInfo _info)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<MentalistReflectedGun>() is not MentalistReflectedGun gun) return true;
        PhotonView view = PhotonView.Find(playerPhotonID);
        PhysGrabber? grabber = view != null ? view.GetComponent<PhysGrabber>() : null;
        if (grabber != null && SemiFunc.OwnerOnlyRPC(_info, grabber.photonView)) gun.RejectGrab(grabber);
        return false;
    }
}
