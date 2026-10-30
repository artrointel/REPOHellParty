using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

internal sealed class BirthdayBoyAbilities : MonoBehaviour
{
    private static readonly MethodInfo RemoveBalloon = AccessTools.Method(typeof(EnemyBirthdayBoy), "RemoveBalloon");
    private static readonly MethodInfo UpdateTarget = AccessTools.Method(typeof(EnemyBirthdayBoy), "UpdatePlayerTarget");
    private EnemyBirthdayBoy _birthday = null!;
    private EnemyHealth _health = null!;
    private readonly BirthdayBalloonRewards<Vector3> _rewards = new();
    private readonly Dictionary<Vector3, BalloonProximityClock> _proximity = new();
    private Vector3? _forcedKey;
    private BalloonPopCause _forcedCause;
    private PlayerAvatar? _forcedTarget;
    private bool _shareAttackTarget;

    private void Awake()
    {
        _birthday = GetComponentInChildren<EnemyBirthdayBoy>(true);
        _health = _birthday.GetComponent<EnemyHealth>();
        foreach (var balloon in _birthday.balloons) Register(balloon.Key);
    }

    internal void Register(Vector3 key)
    {
        _rewards.Register(key);
        _proximity[key] = new BalloonProximityClock();
    }
    internal void NewLife() => _rewards.NewLife();
    internal void BeginDeath() => _rewards.BeginDeath();

    private void Update()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel()) return;
        if (!NativeEnemy.Spawned(NativeEnemy.Parent(_birthday)) || NativeEnemy.Health(_health) <= 0 || NativeEnemy.Dead(_health))
        {
            foreach (BalloonProximityClock clock in _proximity.Values) clock.Observe(Time.time, false);
            return;
        }
        foreach (var pair in new List<KeyValuePair<Vector3, GameObject>>(_birthday.balloons))
        {
            if (pair.Value == null || !_proximity.TryGetValue(pair.Key, out BalloonProximityClock clock)) continue;
            BirthdayBoyBalloon balloon = pair.Value.GetComponentInChildren<BirthdayBoyBalloon>();
            PlayerAvatar? target = null;
            float nearest = BalloonProximityClock.Radius * BalloonProximityClock.Radius;
            if (balloon != null && !balloon.popped && balloon.collider1.enabled && balloon.collider2.enabled)
                foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
                {
                    if (player == null || player.isDisabled || player.deadSet) continue;
                    float distance = (player.transform.position - balloon.moveTransform.position).sqrMagnitude;
                    if (distance <= nearest) { nearest = distance; target = player; }
                }
            if (!clock.Observe(Time.time, target != null)) continue;
            Pop(pair.Key, BalloonPopCause.Automatic, target);
        }
    }

    private void Pop(Vector3 key, BalloonPopCause cause, PlayerAvatar? target, bool shareAttackTarget = false)
    {
        _forcedKey = key;
        _forcedCause = cause;
        _forcedTarget = target;
        _shareAttackTarget = shareAttackTarget;
        try
        {
            int tier = DifficultyRules.BirthdayBalloonTier(DifficultyService.Current);
            if (cause == BalloonPopCause.Automatic && tier >= 2 && target != null)
                UpdateTarget.Invoke(_birthday, new object[] { target });
            // Damage bursts bypass the native player-nearby gate; automatic
            // tier two/three pops retain the native angry player-pop branch.
            RemoveBalloon.Invoke(_birthday, new object[] { key, cause == BalloonPopCause.Damage || tier < 2 });
        }
        catch (Exception exception) { Plugin.ModLogger.LogError($"Birthday Boy balloon pop failed: {exception}"); }
        finally { _forcedKey = null; _forcedTarget = null; _shareAttackTarget = false; }
    }

    private static PlayerAvatar? NearestPlayer(Vector3 position)
    {
        PlayerAvatar? target = null;
        float nearest = 100f;
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (player == null || player.isDisabled || player.deadSet) continue;
            float distance = (player.transform.position - position).sqrMagnitude;
            if (DifficultyRules.BirthdayBalloonInRange(distance) && distance <= nearest)
            { nearest = distance; target = player; }
        }
        return target;
    }

    internal void Damaged()
    {
        if (!NativeEnemy.Spawned(NativeEnemy.Parent(_birthday))) return;
        Vector3 center = NativeEnemy.Position(_birthday);
        // Snapshot the damaged Birthday Boy's target for the entire burst,
        // including balloons owned by another Birthday Boy.
        PlayerAvatar? sharedTarget = _birthday.playerTarget;
        if (sharedTarget != null && (sharedTarget.isDisabled || sharedTarget.deadSet)) sharedTarget = null;
        // Include balloons belonging to other Birthday Boys in this radius.
        // Each controller still owns its reward ledger and native RPC sender.
        foreach (BirthdayBoyAbilities owner in FindObjectsByType<BirthdayBoyAbilities>(FindObjectsSortMode.InstanceID))
        {
            if (NativeEnemy.Health(owner._health) <= 0 || NativeEnemy.Dead(owner._health)) continue;
            foreach (var pair in new List<KeyValuePair<Vector3, GameObject>>(owner._birthday.balloons))
            {
                if (pair.Value == null) continue;
                BirthdayBoyBalloon balloon = pair.Value.GetComponentInChildren<BirthdayBoyBalloon>();
                Vector3 position = balloon != null ? balloon.moveTransform.position : pair.Value.transform.position;
                if (DifficultyRules.BirthdayBalloonInRange((position - center).sqrMagnitude))
                    owner.Pop(pair.Key, BalloonPopCause.Damage, sharedTarget != null ? sharedTarget : NearestPlayer(position), sharedTarget != null);
            }
        }
    }

    internal void Removed(Vector3 key, bool physicalPop)
    {
        if (!_birthday.balloons.TryGetValue(key, out GameObject root) || root == null) return;
        if (NativeEnemy.Health(_health) <= 0 || NativeEnemy.Dead(_health)) BeginDeath();
        BirthdayBoyBalloon balloon = root.GetComponentInChildren<BirthdayBoyBalloon>();
        bool forced = _forcedKey.HasValue && _forcedKey.Value == key;
        BalloonPopCause cause = forced ? _forcedCause : physicalPop ?
            (balloon != null && balloon.popper != null ? BalloonPopCause.Player : BalloonPopCause.Physical) : BalloonPopCause.Other;
        Vector3 position = balloon != null ? balloon.moveTransform.position : root.transform.position;
        PlayerAvatar? nearby = NearestPlayer(position);
        int tier = DifficultyRules.BirthdayBalloonTier(DifficultyService.Current);
        BalloonReward reward = _rewards.Remove(key, cause, nearby != null, tier);
        _proximity.Remove(key);
        if (reward == BalloonReward.None) return;
        try
        {
            PlayerAvatar? target = forced ? _forcedTarget : nearby;
            BirthdayBalloonSpawn.Emit(reward, position, target, forced && _shareAttackTarget);
        }
        catch (Exception exception) { Plugin.ModLogger.LogError($"Birthday Boy balloon reward failed: {exception}"); }
    }

    internal void RemovingAll()
    {
        // PopAll is native death cleanup, but never reinterpret administrative
        // cleanup while alive as a death reward.
        bool dying = NativeEnemy.Health(_health) <= 0 || NativeEnemy.Dead(_health);
        foreach (Vector3 key in new List<Vector3>(_birthday.balloons.Keys))
        {
            if (dying) Removed(key, false);
            else { _rewards.Remove(key, BalloonPopCause.Other, false, 0); _proximity.Remove(key); }
        }
    }
}

[HarmonyPatch(typeof(EnemyBirthdayBoy), "SpawnBalloonRPC")]
internal static class BirthdayBalloonCreatedPatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyBirthdayBoy __instance, Vector3 _keyPos, PhotonMessageInfo _info)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.MasterOnlyRPC(_info)) return;
        StageRuntime.TryAttach(NativeEnemy.Parent(__instance));
        NativeEnemy.Parent(__instance).GetComponent<BirthdayBoyAbilities>()?.Register(_keyPos);
    }
}

[HarmonyPatch(typeof(EnemyBirthdayBoy), "RemoveBalloonRPC")]
internal static class BirthdayBalloonRemovedPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyBirthdayBoy __instance, Vector3 _position, bool _global, PhotonMessageInfo _info)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.MasterOnlyRPC(_info))
            NativeEnemy.Parent(__instance).GetComponent<BirthdayBoyAbilities>()?.Removed(_position, _global);
    }
}

[HarmonyPatch(typeof(EnemyBirthdayBoy), "PopAllBalloonsRPC")]
internal static class BirthdayBalloonsDeathCleanupPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyBirthdayBoy __instance, PhotonMessageInfo _info)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.MasterOnlyRPC(_info))
            NativeEnemy.Parent(__instance).GetComponent<BirthdayBoyAbilities>()?.RemovingAll();
    }
}

[HarmonyPatch(typeof(EnemyHealth), "Hurt")]
internal static class BirthdayDamagePatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance, out int __state)
    {
        __state = -1;
        if (SemiFunc.IsMasterClientOrSingleplayer() && __instance.GetComponent<EnemyBirthdayBoy>() != null)
            __state = NativeEnemy.Health(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix(EnemyHealth __instance, int __state)
    {
        if (__state < 0 || !SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel()) return;
        if (!DifficultyRules.BirthdayDamageTriggers(__state, NativeEnemy.Health(__instance),
                DifficultyRules.BirthdayBalloonTier(DifficultyService.Current))) return;
        StageRuntime.TryAttach(NativeEnemy.Parent(__instance));
        NativeEnemy.Parent(__instance).GetComponent<BirthdayBoyAbilities>()?.Damaged();
    }
}

[HarmonyPatch(typeof(EnemyHealth), "Death")]
internal static class BirthdayDeathStartPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<EnemyBirthdayBoy>() == null) return;
        StageRuntime.TryAttach(NativeEnemy.Parent(__instance));
        NativeEnemy.Parent(__instance).GetComponent<BirthdayBoyAbilities>()?.BeginDeath();
    }
}

[HarmonyPatch(typeof(EnemyBirthdayBoy), nameof(EnemyBirthdayBoy.OnSpawn))]
internal static class BirthdayNewLifePatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyBirthdayBoy __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
            NativeEnemy.Parent(__instance).GetComponent<BirthdayBoyAbilities>()?.NewLife();
    }
}
