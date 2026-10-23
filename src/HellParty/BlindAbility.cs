using System;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using UnityEngine;

namespace HellParty;

// Shadow Child uses a host-side stable-target clock and native cloud.
internal sealed class BlindAbility : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<EnemyThinMan, PlayerAvatar> ChildTarget =
        AccessTools.FieldRefAccess<EnemyThinMan, PlayerAvatar>("playerTarget");
    private readonly BlindTargetClock _clock = new();
    private EnemyParent _parent = null!;
    private EnemyHealth _health = null!;
    private EnemyThinMan? _child;
    private bool _loggedError;

    private void Awake()
    {
        _parent = GetComponent<EnemyParent>();
        _health = GetComponentInChildren<EnemyHealth>(true);
        _child = GetComponentInChildren<EnemyThinMan>(true);
    }

    internal void NewLife() { _clock.Reset(); _loggedError = false; }
    private void OnDisable() => NewLife();

    private void Update()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() ||
            !BlindTargetClock.Unlocked(DifficultyService.Current) || LevelGenerator.Instance == null ||
            !LevelGenerator.Instance.Generated || !NativeEnemy.Spawned(_parent) ||
            _health == null || NativeEnemy.Dead(_health) || NativeEnemy.Health(_health) <= 0)
        {
            _clock.Reset();
            return;
        }

        PlayerAvatar? target = _child != null ? ChildTarget(_child) : null;
        if (target == null || target.isDisabled || target.deadSet || target.PlayerVisionTarget == null ||
            target.PlayerVisionTarget.VisionTransform == null)
        {
            _clock.Reset();
            return;
        }
        if (!_clock.TryCast(Time.time, target)) return;
        try
        {
            // Native Cloud detaches its particles and hurt collider locally on
            // every peer. Use a stationary native cloud at eye level rather
            // than a host-only attachment that vanilla peers cannot follow.
            AnimalGas.Emit(target.PlayerVisionTarget.VisionTransform.position);
            _loggedError = false;
        }
        catch (Exception exception)
        {
            if (!_loggedError) Plugin.ModLogger.LogError($"Blind gas failed; retrying next interval: {exception}");
            _loggedError = true;
        }
    }
}

[HarmonyPatch(typeof(EnemyThinMan), nameof(EnemyThinMan.OnSpawn))]
internal static class BlindNewLifePatch
{
    [HarmonyPostfix]
    private static void Postfix(Component __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
            NativeEnemy.Parent(__instance).GetComponent<BlindAbility>()?.NewLife();
    }
}
