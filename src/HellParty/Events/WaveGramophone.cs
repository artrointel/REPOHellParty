using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty.Events;

internal sealed class WaveGramophone : MonoBehaviour
{
    private PhysGrabObject _physical = null!;
    private Rigidbody _body = null!;
    private Vector3 _position;
    private readonly StalkingDollGrabAttempts _attempts = new();
    private readonly HashSet<int> _releasing = new();

    internal static WaveGramophone Create(Vector3 point)
    {
        PrefabRef prefab = VanillaCatalog.Valuable(p => p.GetComponent<TrapGramophone>() != null);
        GameObject root = VanillaCatalog.Spawn(prefab, point, Quaternion.identity);
        WaveGramophone music = root.AddComponent<WaveGramophone>();
        music._position = point;
        return music;
    }

    private void Awake()
    {
        _physical = GetComponent<PhysGrabObject>();
        _body = GetComponent<Rigidbody>();
        GetComponent<PhysGrabObjectImpactDetector>().destroyDisable = true;
    }

    private IEnumerator Start()
    {
        // All peers must finish native Start (including the synced random/timer)
        // before the existing master-authorized RPC activates the music.
        yield return null; yield return null;
        SyncedEventTimer timer = GetComponent<SyncedEventTimer>();
        timer.timerMin = timer.timerMax = 20; // Host owns timeout; wave removes prop at 15s.
        if (SemiFunc.IsMultiplayer()) GetComponent<PhotonView>().RPC("TrapStartRPC", RpcTarget.All);
        else GetComponent<TrapGramophone>().TrapStartRPC();
        Pin();
    }

    private void FixedUpdate() => Pin();
    private void Pin()
    {
        _physical.OverrideKinematic(.2f);
        _physical.OverrideZeroGravity(.2f);
        _physical.OverrideIndestructible(.2f);
        // Keep raycast/grab interaction available so attempts can be punished.
        _body.position = _position;
        _body.rotation = Quaternion.identity;
        transform.SetPositionAndRotation(_position, Quaternion.identity);
    }

    internal void Reject(PhysGrabber player)
    {
        if (player == null || !WaveSpawnPoints.Alive(player.playerAvatar)) return;
        int id = player.GetInstanceID();
        if (_attempts.Accept(id, Time.time)) player.playerAvatar.playerHealth.HurtOther(10, Vector3.zero, savingGrace: false);
        if (_releasing.Add(id)) StartCoroutine(Release(player, id));
    }
    private IEnumerator Release(PhysGrabber player, int id)
    {
        yield return null;
        if (player != null) player.OverrideGrabRelease(GetComponent<PhotonView>().ViewID, StalkingDollGrabAttempts.ReleaseLockout);
        _releasing.Remove(id);
    }
    internal void Remove()
    {
        if (SemiFunc.IsMultiplayer()) PhotonNetwork.Destroy(gameObject);
        else Destroy(gameObject);
    }
}

[HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.GrabStarted))]
internal static class WaveMusicGrabPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance, PhysGrabber player)
    {
        if (SemiFunc.IsMultiplayer() || __instance.GetComponent<WaveGramophone>() is not WaveGramophone music) return true;
        music.Reject(player); return false;
    }
}
[HarmonyPatch(typeof(PhysGrabObject), "GrabStartedRPC")]
internal static class WaveMusicGrabRpcPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance, int playerPhotonID, PhotonMessageInfo _info)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<WaveGramophone>() is not WaveGramophone music) return true;
        PhysGrabber? player = PhotonView.Find(playerPhotonID)?.GetComponent<PhysGrabber>();
        if (player != null && SemiFunc.OwnerOnlyRPC(_info, player.photonView)) music.Reject(player);
        return false;
    }
}
[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "Break")]
internal static class WaveMusicBreakPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObjectImpactDetector __instance) => __instance.GetComponent<WaveGramophone>() == null;
}
[HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.DestroyPhysGrabObject))]
internal static class WaveMusicDestroyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObject __instance) => __instance.GetComponent<WaveGramophone>() == null;
}
[HarmonyPatch(typeof(ValuableObject), "AddToDollarHaulList")]
internal static class WaveMusicExtractionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ValuableObject __instance) => __instance.GetComponent<WaveGramophone>() == null;
}
[HarmonyPatch(typeof(ValuableObject), "DollarValueSet")]
internal static class WaveMusicValuePatch
{
    [HarmonyPostfix]
    private static void Postfix(ValuableObject __instance, ref IEnumerator __result)
    {
        if (__instance.GetComponent<WaveGramophone>() != null) __result = Exclude(__result, __instance);
    }
    private static IEnumerator Exclude(IEnumerator native, ValuableObject valuable)
    {
        while (native.MoveNext()) yield return native.Current;
        if (valuable == null) yield break;
        if (RoundDirector.instance != null) RoundDirector.instance.haulGoalMax -= (int)valuable.dollarValueCurrent;
        if (SemiFunc.IsMultiplayer()) valuable.GetComponent<PhotonView>().RPC("DollarValueSetRPC", RpcTarget.All, 0f);
        else valuable.DollarValueSetRPC(0);
    }
}
