using System;
using System.Collections;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace HellParty.Native;

// Only the host receives this marker. Vanilla guests instantiate the same
// native valuable and run its native onDestroy -> UraniumScript.Cloud event.
internal sealed class AnimalGas : MonoBehaviour
{
    internal const string CarrierPath = "Valuables/04 Big/Valuable Museum Uranium Mug Deluxe";
    private Vector3 _position;
    private PhysGrabObject _physical = null!;

    internal static void Emit(Vector3 position)
    {
        GameObject prefab = Resources.Load<GameObject>(CarrierPath);
        if (prefab == null || prefab.GetComponent<UraniumScript>() == null)
            throw new InvalidOperationException("Native Cleanup Crew-equivalent gas carrier was not found.");
        // 0.5 transmits teleports through host snapshots. Instantiate at the
        // cloud location so destruction cannot race a guest's first snapshot.
        GameObject root = VanillaCatalog.Spawn(CarrierPath, position, Quaternion.identity);
        AnimalGas carrier = root.AddComponent<AnimalGas>();
        root.AddComponent<Events.InternalWaveExcluded>();
        carrier._position = position;
        carrier._physical = root.GetComponent<PhysGrabObject>();
        carrier._physical.OverrideKinematic(1);
        carrier._physical.OverrideGrabDisable(1);
        carrier._physical.OverrideIndestructible(1);
        carrier._physical.rb.detectCollisions = false;
        // Set before ValuableObject.Start so haulGoalMax never gains this value.
        if (SemiFunc.IsMultiplayer()) root.GetComponent<PhotonView>().RPC("DollarValueSetRPC", RpcTarget.All, 0f);
        else root.GetComponent<ValuableObject>().DollarValueSetRPC(0);
        // Monster damage and physics effects are host-authoritative. Player
        // damage remains native and is evaluated on each player's own client.
        Restrict(root.GetComponent<UraniumScript>());
    }

    internal static void Restrict(UraniumScript uranium)
    {
        uranium.hurtCollider.enemyLogic = false;
        uranium.hurtCollider.physLogic = false;
    }

    private IEnumerator Start()
    {
        // Give native Start and remote instantiation time to finish.
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.25f);
        if (!NetworkLifetime.Live(this) || !SemiFunc.RunIsLevel()) yield break;
        try
        {
            _physical.Teleport(_position, Quaternion.identity);
        }
        catch (Exception exception)
        {
            Plugin.ModLogger.LogError($"Animal gas activation failed: {exception}");
            NetworkLifetime.Remove(gameObject);
            yield break;
        }
        // Let native centerPoint/sound positions update after the teleport.
        yield return new WaitForSeconds(0.1f);
        if (this != null && NetworkLifetime.Live(this)) GetComponent<PhysGrabObjectImpactDetector>().DestroyObject(false);
    }
}

[HarmonyPatch(typeof(UraniumScript), nameof(UraniumScript.Cloud))]
internal static class AnimalGasCloudPatch
{
    [HarmonyPrefix]
    private static void Prefix(UraniumScript __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && __instance.GetComponent<AnimalGas>() != null)
            AnimalGas.Restrict(__instance);
    }
}
