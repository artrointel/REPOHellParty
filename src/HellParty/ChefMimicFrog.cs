using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HellParty.Native;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

// Host-only metadata on a native network valuable, not a new prefab or RPC.
internal sealed class ChefMimicFrog : MonoBehaviour
{
    private ChefMimic? _owner;
    private PhysGrabObject _physical = null!;
    private ValuableObject _valuable = null!;
    private bool _initialized;
    private float _lastFrogValue;
    private static readonly MethodInfo Heal = AccessTools.Method(typeof(PhysGrabObjectImpactDetector), "HealRPC");
    internal bool Active => _owner != null && (_owner.Disguised || _owner.Revealing);
    internal static bool BlocksTarget(Component? candidate) => candidate != null &&
        candidate.GetComponentInParent<ChefMimicFrog>() is ChefMimicFrog frog && frog.Active;

    internal static ChefMimicFrog Create(PrefabRef prefab, Vector3 ground, Quaternion rotation, ChefMimic owner)
    {
        GameObject root = VanillaCatalog.Spawn(prefab, ground + Vector3.up * 0.15f, rotation);
        ChefMimicFrog frog = root.AddComponent<ChefMimicFrog>();
        frog._owner = owner;
        frog._physical = root.GetComponent<PhysGrabObject>();
        frog._valuable = root.GetComponent<ValuableObject>();
        // Keep native Frog value/durability so normal weapon and impact damage
        // can break it. Its possible-haul contribution is excluded below.
        return frog;
    }

    internal void InitializeVitality()
    {
        if (_initialized || !Active) return;
        // Native Start and value initialization have completed on the host.
        // The existing HealRPC accepts a signed delta and only adjusts value,
        // preserving the original value and avoiding fake break effects.
        float desired = Mathf.Max(Mathf.Floor(_owner!.FrogValue(_valuable.dollarValueOriginal)),
            Mathf.Floor(_valuable.dollarValueOriginal * 0.15f) + 1);
        float delta = desired - _valuable.dollarValueCurrent;
        PhysGrabObjectImpactDetector impact = GetComponent<PhysGrabObjectImpactDetector>();
        if (SemiFunc.IsMultiplayer()) GetComponent<PhotonView>().RPC("HealRPC", RpcTarget.All, delta, transform.position);
        else Heal.Invoke(impact, new object[] { delta, transform.position, default(PhotonMessageInfo) });
        _lastFrogValue = _valuable.dollarValueCurrent;
        _initialized = true;
        SynchronizeVitality();
    }

    private void Update()
    {
        if (SemiFunc.IsMasterClientOrSingleplayer()) SynchronizeVitality();
    }

    internal void SynchronizeVitality()
    {
        if (!_initialized || !Active) return;
        float change = _valuable.dollarValueCurrent - _lastFrogValue;
        _lastFrogValue = _valuable.dollarValueCurrent;
        if (_owner!.CaptureFrog(change, _valuable.dollarValueOriginal))
            GetComponent<PhysGrabObjectImpactDetector>().DestroyObject();
    }

    internal void Grabbed(PhysGrabber player)
    {
        if (Active) _owner!.Grabbed(this, player);
    }
    internal void RemoveQuietly()
    {
        SynchronizeVitality();
        _owner = null;
        foreach (PhysGrabber player in _physical.playerGrabbing.ToArray())
            if (player != null) player.OverrideGrabRelease(_physical.GetComponent<PhotonView>().ViewID, 0.5f);
        if (SemiFunc.IsMultiplayer() && PhotonNetwork.InRoom && SemiFunc.IsMasterClientOrSingleplayer()) PhotonNetwork.Destroy(gameObject);
        else Destroy(gameObject);
    }

    internal void Destroyed()
    {
        _owner?.Destroyed(this);
    }
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabPlayerAddRPC")]
internal static class ChefFrogNetworkGrabPatch
{
    [HarmonyPostfix]
    private static void Postfix(PhysGrabObject __instance, int photonViewID, PhotonMessageInfo _info)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.MasterOnlyRPC(_info) ||
            __instance.GetComponent<ChefMimicFrog>() is not ChefMimicFrog frog || !frog.Active) return;
        PhysGrabber? player = PhotonView.Find(photonViewID)?.GetComponent<PhysGrabber>();
        if (player != null && __instance.playerGrabbing.Contains(player)) frog.Grabbed(player);
    }
}

[HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.GrabStarted))]
internal static class ChefFrogSoloGrabPatch
{
    [HarmonyPostfix]
    private static void Postfix(PhysGrabObject __instance, PhysGrabber player)
    {
        if (!SemiFunc.IsMultiplayer() && __instance.playerGrabbing.Contains(player))
            __instance.GetComponent<ChefMimicFrog>()?.Grabbed(player);
    }
}

[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.DestroyObject))]
internal static class ChefFrogDestructionPatch
{
    [HarmonyPrefix]
    private static void Prefix(PhysGrabObjectImpactDetector __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && !__instance.destroyDisable &&
            !__instance.GetComponent<PhysGrabObject>().dead &&
            __instance.GetComponent<ChefMimicFrog>() is ChefMimicFrog frog && frog.Active)
            frog.Destroyed();
    }
}

[HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.DestroyPhysGrabObject))]
internal static class ChefFrogDirectDestructionPatch
{
    [HarmonyPrefix]
    private static void Prefix(PhysGrabObject __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() &&
            __instance.GetComponent<ChefMimicFrog>() is ChefMimicFrog frog && frog.Active)
            frog.Destroyed();
    }
}

[HarmonyPatch(typeof(ValuableObject), "DollarValueSet")]
internal static class ChefFrogPossibleHaulPatch
{
    [HarmonyPostfix]
    private static void Postfix(ValuableObject __instance, ref IEnumerator __result)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && __instance.GetComponent<ChefMimicFrog>() != null)
            __result = ExcludeHaul(__result, __instance);
    }

    private static IEnumerator ExcludeHaul(IEnumerator native, ValuableObject valuable)
    {
        while (native.MoveNext()) yield return native.Current;
        // Native DollarValueSet adds the current value at the end. A disguise
        // is never extractable loot, so cancel only that synchronous addition.
        if (valuable != null && RoundDirector.instance != null)
            RoundDirector.instance.haulGoalMax -= (int)valuable.dollarValueCurrent;
        if (valuable != null) valuable.GetComponent<ChefMimicFrog>()?.InitializeVitality();
    }
}

[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "BreakRPC")]
internal static class ChefFrogDamageSyncPatch
{
    [HarmonyPostfix]
    private static void Postfix(PhysGrabObjectImpactDetector __instance, PhotonMessageInfo _info)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.MasterOnlyRPC(_info))
            __instance.GetComponent<ChefMimicFrog>()?.SynchronizeVitality();
    }
}

[HarmonyPatch(typeof(ItemDrone), "GetHighestParentWithRigidbody")]
internal static class ChefFrogDroneTargetPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Transform _child, ref Transform? __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !ChefMimicFrog.BlocksTarget(_child)) return true;
        // Native SphereCheck treats null as an ineligible attachment target,
        // so no attachment RPC is emitted for vanilla guests.
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(ItemDrone), "NewRayHitPoint")]
internal static class ChefFrogDroneAttachmentPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ItemDrone __instance, int photonViewId, Transform newMagnetTarget)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return true;
        Transform? target = newMagnetTarget != null ? newMagnetTarget : PhotonView.Find(photonViewId)?.transform;
        if (!ChefMimicFrog.BlocksTarget(target)) return true;
        __instance.DeactivateMagnet();
        return false;
    }
}
