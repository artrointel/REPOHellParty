using System;
using System.Reflection;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

internal sealed class BellaAbilities : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<EnemyTricycle, PlayerAvatar> Target = AccessTools.FieldRefAccess<EnemyTricycle, PlayerAvatar>("playerTarget");
    private static readonly AccessTools.FieldRef<EnemyTricycle, float> AttackTimer = AccessTools.FieldRefAccess<EnemyTricycle, float>("attackTimer");
    private static readonly AccessTools.FieldRef<EnemyTricycle, float> AttackDuration = AccessTools.FieldRefAccess<EnemyTricycle, float>("attackTimerMax");
    private static readonly AccessTools.FieldRef<EnemyTricycle, float> LastSeen = AccessTools.FieldRefAccess<EnemyTricycle, float>("lastSeenPlayerTimer");
    private static readonly MethodInfo SetState = AccessTools.Method(typeof(EnemyTricycle), "SetState");
    private EnemyTricycle _bella = null!;
    private EnemyHealth _health = null!;
    private BellaDollCarrier? _doll;
    private BellaBait? _bait;
    private bool _dollCreated;
    private bool _dead;

    private void Awake()
    {
        _bella = GetComponentInChildren<EnemyTricycle>(true);
        _health = _bella.GetComponent<EnemyHealth>();
    }

    private bool Alive => !_dead && NativeEnemy.Health(_health) > 0 && !NativeEnemy.Dead(_health);
    private void Update()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (!Alive) { Died(); return; }
        RemoveDistantBait();
        if (NativeEnemy.Spawned(NativeEnemy.Parent(_bella))) EnsureDoll();
    }

    private void EnsureDoll()
    {
        if (_dollCreated || !Alive) return;
        _dollCreated = true;
        try { _doll = BellaDollCarrier.Create(_bella); }
        catch (Exception exception) { Plugin.ModLogger.LogError($"Bella Clown Doll creation failed: {exception}"); }
    }

    internal void Spawned()
    {
        if (_dead) { _dead = false; _dollCreated = false; }
        // A natural despawn/respawn does not restore a destroyed doll.
        EnsureDoll();
    }

    internal void Bell()
    {
        if (!Alive || !NativeEnemy.Spawned(NativeEnemy.Parent(_bella))) return;
        EnsureDoll();
        int stage = _bella.currentState == EnemyTricycle.State.BellRing ? 1 :
            _bella.currentState == EnemyTricycle.State.BellRing2 ? 2 :
            _bella.currentState == EnemyTricycle.State.BellRing3 ? 3 : 0;
        _doll?.Warning(stage);
        if (!DifficultyRules.BellaBaitUnlocked(DifficultyService.Current)) return;
        // Disarm before destruction, even when the old object is held.
        BellaBait? previous = _bait;
        _bait = null;
        if (previous != null) previous.RemoveQuietly();
        try
        {
            Vector3 origin = NativeEnemy.Position(_bella);
            Vector3 position = VanillaCatalog.Adjacent(origin, UnityEngine.Random.Range(0, 3), out Vector3 nearby) ? nearby : origin;
            PrefabRef prefab = VanillaCatalog.RandomTinyValuable();
            GameObject item = VanillaCatalog.Spawn(prefab, position + Vector3.up * 0.25f, Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0));
            _bait = item.AddComponent<BellaBait>();
            _bait.Owner = this;
        }
        catch (Exception exception) { Plugin.ModLogger.LogError($"Bella bait creation failed: {exception}"); }
    }

    internal void BaitGrabbed(BellaBait bait, PlayerAvatar player)
    {
        if (bait != _bait || !Alive || !NativeEnemy.Spawned(NativeEnemy.Parent(_bella)) || player == null || player.isDisabled || player.deadSet) return;
        if (RemoveDistantBait()) return;
        Target(_bella) = player;
        AttackTimer(_bella) = 0;
        AttackDuration(_bella) = 5;
        LastSeen(_bella) = 10;
        // Every accepted new grab restarts the attack window, even for the
        // same bait/player or while already attacking. No one-shot latch.
        // StateBeforeAttack would pick another nearby player; AttackTell would
        // add another delay. Use the native actual attack with this grabber.
        SetState.Invoke(_bella, new object[] { EnemyTricycle.State.Attack });
    }

    private bool RemoveDistantBait()
    {
        if (_bait == null || !DifficultyRules.BellaBaitOutOfRange(
                (_bait.transform.position - NativeEnemy.Position(_bella)).sqrMagnitude)) return false;
        BellaBait previous = _bait;
        _bait = null;
        previous.RemoveQuietly();
        return true;
    }

    internal void Died()
    {
        if (_dead) return;
        _dead = true;
        if (_doll != null) _doll.Release();
        _doll = null;
        // Check the boundary at death too, so a lethal hit before this frame's
        // Update cannot rescue bait that has already crossed fifteen metres.
        if (!RemoveDistantBait() && _bait != null) _bait.Disarm();
        _bait = null;
    }

    private void OnDestroy()
    {
        if (_bait != null) _bait.Disarm();
    }
}

internal sealed class BellaBait : MonoBehaviour
{
    internal BellaAbilities? Owner;
    internal void Grabbed(PhysGrabber player)
    {
        if (Owner != null) Owner.BaitGrabbed(this, player.playerAvatar);
    }
    internal void Disarm() { Owner = null; Destroy(this); }
    internal void RemoveQuietly()
    {
        Owner = null;
        // Network destruction removes cached instantiation too. Do not call
        // impact destruction: native tiny valuables may contain other traps.
        if (SemiFunc.IsMultiplayer()) PhotonNetwork.Destroy(gameObject);
        else Destroy(gameObject);
    }
}

[HarmonyPatch(typeof(EnemyTricycle), "DoBellRing")]
internal static class BellaBellPatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyTricycle __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        StageRuntime.TryAttach(NativeEnemy.Parent(__instance));
        NativeEnemy.Parent(__instance).GetComponent<BellaAbilities>()?.Bell();
    }
}

[HarmonyPatch(typeof(EnemyTricycle), nameof(EnemyTricycle.OnSpawn))]
internal static class BellaSpawnPatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyTricycle __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        StageRuntime.TryAttach(NativeEnemy.Parent(__instance));
        NativeEnemy.Parent(__instance).GetComponent<BellaAbilities>()?.Spawned();
    }
}

[HarmonyPatch(typeof(EnemyHealth), "Death")]
internal static class BellaDeathPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && __instance.GetComponent<EnemyTricycle>() != null)
            NativeEnemy.Parent(__instance).GetComponent<BellaAbilities>()?.Died();
    }
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabPlayerAddRPC")]
internal static class BellaBaitNetworkGrabPatch
{
    [HarmonyPrefix]
    private static void Prefix(PhysGrabObject __instance, int photonViewID, out bool __state)
    {
        PhysGrabber? player = PhotonView.Find(photonViewID)?.GetComponent<PhysGrabber>();
        __state = player != null && __instance.playerGrabbing.Contains(player);
    }
    [HarmonyPostfix]
    private static void Postfix(PhysGrabObject __instance, int photonViewID, PhotonMessageInfo _info, bool __state)
    {
        if (__state || !SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.MasterOnlyRPC(_info)) return;
        PhysGrabber? player = PhotonView.Find(photonViewID)?.GetComponent<PhysGrabber>();
        if (player != null && __instance.playerGrabbing.Contains(player)) __instance.GetComponent<BellaBait>()?.Grabbed(player);
    }
}

[HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.GrabStarted))]
internal static class BellaBaitSoloGrabPatch
{
    [HarmonyPrefix]
    private static void Prefix(PhysGrabObject __instance, PhysGrabber player, out bool __state) => __state = __instance.playerGrabbing.Contains(player);
    [HarmonyPostfix]
    private static void Postfix(PhysGrabObject __instance, PhysGrabber player, bool __state)
    {
        if (!__state && !SemiFunc.IsMultiplayer() && __instance.playerGrabbing.Contains(player)) __instance.GetComponent<BellaBait>()?.Grabbed(player);
    }
}
