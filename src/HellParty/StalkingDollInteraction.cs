using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

// Native guests retain the doll's two-damage collider. Add only the missing
// damage through the existing owner-authorized player-health RPC.
[HarmonyPatch(typeof(HurtCollider), "PlayerHurt")]
internal static class StalkingDollPlayerDamagePatch
{
    private static readonly MethodInfo CanHit = AccessTools.Method(typeof(HurtCollider), "CanHit");
    private static readonly AccessTools.FieldRef<PlayerHealth, int> Health =
        AccessTools.FieldRefAccess<PlayerHealth, int>("health");

    [HarmonyPrefix]
    private static void Prefix(PlayerAvatar _player, out int __state) =>
        __state = _player != null && _player.playerHealth != null ? Health(_player.playerHealth) : -1;

    [HarmonyPostfix]
    private static void Postfix(HurtCollider __instance, PlayerAvatar _player, int __state)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _player == null || _player.deadSet ||
            __instance.detectionOnly || __instance.playerKill || __instance.ignorePlayers.Contains(_player) ||
            __instance.GetComponentInParent<StalkingDoll>() is not StalkingDoll doll ||
            doll.Mode != StalkingDollMode.Attacking) return;
        bool local = !SemiFunc.IsMultiplayer() || _player.photonView.IsMine;
        if (local)
        {
            // Native PlayerHurt has already consumed CanHit on the owner's peer.
            if (__state <= Health(_player.playerHealth)) return;
        }
        else if (!(bool)CanHit.Invoke(__instance, new object[] { _player.gameObject,
            __instance.playerDamageCooldown, __instance.playerRayCast,
            _player.PlayerVisionTarget.VisionTransform.position, HurtCollider.HitType.Player })) return;
        int extra = StalkingDollRules.AdditionalPlayerDamage(__instance.playerDamage);
        if (extra > 0) _player.playerHealth.HurtOther(extra, Vector3.zero, savingGrace: false);
    }
}

// Suppress wall/floor collision damage only within this native callback.
// Weapon HurtColliders and collisions with thrown physical objects still hurt.
[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "OnCollisionStay")]
internal static class StalkingDollTerrainCollisionPatch
{
    [HarmonyPrefix]
    private static void Prefix(PhysGrabObjectImpactDetector __instance, Collision collision, out bool __state)
    {
        StalkingDollHealth? health = __instance.GetComponent<StalkingDollHealth>();
        __state = health != null && health.TerrainCollision;
        if (health == null || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        Collider other = collision.collider;
        health.TerrainCollision = __state || other != null &&
            (other.attachedRigidbody == null || other.attachedRigidbody.isKinematic) &&
            other.GetComponentInParent<PhysGrabObject>() == null &&
            other.GetComponentInParent<Enemy>() == null && other.GetComponentInParent<PlayerAvatar>() == null;
    }

    [HarmonyFinalizer]
    private static void Finalizer(PhysGrabObjectImpactDetector __instance, bool __state)
    {
        if (__instance.GetComponent<StalkingDollHealth>() is StalkingDollHealth health)
            health.TerrainCollision = __state;
    }
}

internal sealed class StalkingDollGrabGuard : MonoBehaviour
{
    private readonly StalkingDollGrabAttempts _attempts = new();
    private readonly HashSet<int> _releasePending = new();

    internal void Reject(PhysGrabber grabber)
    {
        if (grabber == null || grabber.playerAvatar == null || grabber.playerAvatar.deadSet ||
            GetComponent<StalkingDollHealth>().Dead) return;
        int id = grabber.GetInstanceID();
        if (_attempts.Accept(id, Time.time))
        {
            // Match the native release lockout and collapse duplicate requests.
            grabber.playerAvatar.playerHealth.HurtOther(StalkingDollRules.PlayerDamage, Vector3.zero, savingGrace: false);
        }
        // Even a duplicate must release the client's optimistically created beam.
        if (_releasePending.Add(id)) StartCoroutine(ReleaseAfterGrabStarted(grabber, id));
    }

    private IEnumerator ReleaseAfterGrabStarted(PhysGrabber grabber, int id)
    {
        // In singleplayer GrabStarted is nested inside the beam's setup.
        // Releasing synchronously would be overwritten by that outer setup.
        yield return null;
        if (grabber != null) grabber.OverrideGrabRelease(GetComponent<PhotonView>().ViewID, StalkingDollGrabAttempts.ReleaseLockout);
        _releasePending.Remove(id);
    }

    internal static StalkingDollGrabGuard? For(PhysGrabObject physical)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || physical.GetComponent<StalkingDoll>() == null) return null;
        return physical.GetComponent<StalkingDollGrabGuard>() ?? physical.gameObject.AddComponent<StalkingDollGrabGuard>();
    }
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabStarted")]
internal static class StalkingDollGrabPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance, PhysGrabber player)
    {
        if (SemiFunc.IsMultiplayer() || StalkingDollGrabGuard.For(__instance) is not StalkingDollGrabGuard guard) return true;
        guard.Reject(player);
        return false;
    }
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabStartedRPC")]
internal static class StalkingDollGrabRpcPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance, int playerPhotonID, PhotonMessageInfo _info)
    {
        if (StalkingDollGrabGuard.For(__instance) is not StalkingDollGrabGuard guard) return true;
        PhotonView view = PhotonView.Find(playerPhotonID);
        PhysGrabber? grabber = view != null ? view.GetComponent<PhysGrabber>() : null;
        if (grabber != null && SemiFunc.OwnerOnlyRPC(_info, grabber.photonView)) guard.Reject(grabber);
        return false;
    }
}
