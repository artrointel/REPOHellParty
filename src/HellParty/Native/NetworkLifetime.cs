using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty.Native;

internal static class NetworkLifetime
{
    internal static bool Registered(PhotonView view) => view != null && PhotonNetwork.InRoom &&
        view.ViewID > 0 && PhotonView.Find(view.ViewID) == view;

    internal static bool Live(Component component) => component != null &&
        component.GetComponent<RetiringNetworkObject>() == null &&
        (!SemiFunc.IsMultiplayer() || Registered(component.GetComponent<PhotonView>()));

    internal static void Remove(GameObject root)
    {
        if (root == null || root.GetComponent<RetiringNetworkObject>() != null) return;
        if (!SemiFunc.IsMultiplayer()) { Object.Destroy(root); return; }
        if (!Registered(root.GetComponent<PhotonView>())) { Object.Destroy(root); return; }
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        root.AddComponent<RetiringNetworkObject>();
    }
}

// Hide immediately, but leave the native view registered briefly so in-flight
// vanilla grab RPCs can finish. No replacement prefab, view ID or custom RPC.
internal sealed class RetiringNetworkObject : MonoBehaviour
{
    private PhysGrabObject? _physical;
    private PhotonView _view = null!;
    private readonly RpcRetirementClock _clock = new();
    private readonly Dictionary<int, double> _released = new();

    private void Awake()
    {
        _view = GetComponent<PhotonView>();
        _physical = GetComponent<PhysGrabObject>();
        _clock.Start(Time.realtimeSinceStartupAsDouble);
        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
        {
            if (component == this || component.GetType().Assembly != typeof(Plugin).Assembly) continue;
            component.StopAllCoroutines();
            component.enabled = false;
        }
        if (_physical == null) return;
        PhysGrabObjectImpactDetector impact = GetComponent<PhysGrabObjectImpactDetector>();
        if (impact != null) { impact.destroyDisable = true; impact.destroyDisableTeleport = false; }
        foreach (PhysGrabber grabber in _physical.playerGrabbing.ToArray()) Release(grabber);
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
            if (player != null && player.physGrabber != null && player.physGrabber.grabbedPhysGrabObject == _physical)
                Release(player.physGrabber);
        if (AssetManager.instance != null)
            _view.RPC("SetPositionRPC", RpcTarget.All, AssetManager.instance.physDisabledPosition, Quaternion.identity);
        Hold();
    }

    internal void Release(PhysGrabber grabber)
    {
        if (grabber == null || !NetworkLifetime.Registered(grabber.photonView) || !NetworkLifetime.Registered(_view)) return;
        int id = grabber.GetInstanceID();
        double now = Time.realtimeSinceStartupAsDouble;
        if (_released.TryGetValue(id, out double previous) && now - previous < .25) return;
        _released[id] = now;
        _clock.Draining(now);
        grabber.OverrideGrabRelease(_view.ViewID, .5f);
    }

    private void Hold()
    {
        if (_physical == null) return;
        _physical.OverrideGrabDisable(.2f);
        _physical.OverrideIndestructible(.2f);
        if (AssetManager.instance != null) _physical.OverrideDeactivate(.2f);
    }

    private void Update()
    {
        if (!NetworkLifetime.Registered(_view)) { Destroy(gameObject); return; }
        Hold();
        if (_clock.Due(Time.realtimeSinceStartupAsDouble)) PhotonNetwork.Destroy(gameObject);
    }
}

// Prevent this peer (including other installed plugins) from sending RPCs using
// an unregistered/stale view. This cannot intercept another peer's sends.
[HarmonyPatch(typeof(PhotonView), nameof(PhotonView.RPC), typeof(string), typeof(RpcTarget), typeof(object[]))]
internal static class RegisteredBroadcastRpcPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhotonView __instance) => NetworkLifetime.Registered(__instance);
}

[HarmonyPatch(typeof(PhotonView), nameof(PhotonView.RPC), typeof(string), typeof(Photon.Realtime.Player), typeof(object[]))]
internal static class RegisteredPlayerRpcPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhotonView __instance) => NetworkLifetime.Registered(__instance);
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabStartedRPC")]
internal static class RetiringObjectGrabPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool Prefix(PhysGrabObject __instance, int playerPhotonID, PhotonMessageInfo _info)
    {
        if (__instance.GetComponent<RetiringNetworkObject>() is not RetiringNetworkObject retiring) return true;
        PhysGrabber? grabber = PhotonView.Find(playerPhotonID)?.GetComponent<PhysGrabber>();
        if (grabber != null && SemiFunc.OwnerOnlyRPC(_info, grabber.photonView)) retiring.Release(grabber);
        return false;
    }
}

[HarmonyPatch]
internal static class LiveGrabLinkPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PhysGrabObject), "GrabLinkRPC");
        yield return AccessTools.Method(typeof(PhysGrabObject), "GrabPointSyncRPC");
    }

    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance, int playerPhotonID) =>
        __instance.GetComponent<RetiringNetworkObject>() == null && PhotonView.Find(playerPhotonID) != null;
}

[HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.DestroyPhysGrabObject))]
internal static class RetiringObjectNativeDestroyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance) => __instance.GetComponent<RetiringNetworkObject>() == null;
}
