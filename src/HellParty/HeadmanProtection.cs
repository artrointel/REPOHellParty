using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using UnityEngine;

namespace HellParty;

internal sealed class HeadmanProtection : MonoBehaviour
{
    private Enemy _enemy = null!;
    private EnemyParent _parent = null!;
    private float _scanAt;
    private void Awake()
    {
        _parent = GetComponent<EnemyParent>();
        _enemy = GetComponentInChildren<Enemy>(true);
    }
    private bool Available => SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.RunIsLevel() &&
        DifficultyService.Current >= 4 && _parent.GetComponent<ReaperHeadman>() == null && NativeEnemy.Spawned(_parent) && NativeEnemy.Health(_enemy.Health) > 0 &&
        !NativeEnemy.Dead(_enemy.Health) && !_enemy.IsStunned() && _enemy.CurrentState is not (EnemyState.Spawn or EnemyState.Despawn) &&
        !(EnemyTarget.Pursuing(_enemy) && EnemyTarget.Valid(_enemy.TargetPlayerAvatar));

    private void Update()
    {
        if (!Available || Time.time < _scanAt || EnemyDirector.instance == null) return;
        _scanAt = Time.time + .5f;
        Enemy? closest = null;
        PlayerAvatar? target = null;
        float best = float.PositiveInfinity;
        foreach (EnemyParent companion in EnemyDirector.instance.enemiesSpawned)
        {
            if (companion == null || companion == _parent || !NativeEnemy.Spawned(companion)) continue;
            Enemy enemy = companion.GetComponentInChildren<Enemy>(true);
            if (enemy == null || NativeEnemy.Dead(enemy.Health) || NativeEnemy.Health(enemy.Health) <= 0) continue;
            float distance = (NativeEnemy.Position(enemy) - NativeEnemy.Position(_enemy)).sqrMagnitude;
            if (distance >= best || !ProtectionRules.Eligible(DifficultyService.Current, false, distance)) continue;
            PlayerAvatar? candidate = EnemyTarget.Read(enemy);
            if (!EnemyTarget.Valid(candidate)) continue;
            closest = enemy; target = candidate; best = distance;
        }
        if (closest != null && target != null) Acquire(target);
    }

    private void Acquire(PlayerAvatar target)
    {
        if (!Available || !EnemyTarget.Valid(target)) return;
        _enemy.TargetPlayerAvatar = target;
        _enemy.TargetPlayerViewID = target.photonView.ViewID;
        _enemy.StateChase.VisionTimer = _enemy.StateChase.VisionTime;
        _enemy.StateChase.ChasePosition = target.transform.position;
        _enemy.StateChase.ChaseCanReach = true;
        _enemy.CurrentState = EnemyState.Chase;
    }

    internal static void CompanionHurt(EnemyParent companion, Vector3 position, PlayerAvatar? attacker)
    {
        if (!EnemyTarget.Valid(attacker) || StageRuntime.Current == null) return;
        if (companion == null) return;
        foreach (HeadmanProtection headman in FindObjectsOfType<HeadmanProtection>())
            if (headman._parent != companion && headman.Available &&
                ProtectionRules.Eligible(DifficultyService.Current, false,
                    (position - NativeEnemy.Position(headman._enemy)).sqrMagnitude))
                headman.Acquire(attacker!);
    }
}

[HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Hurt))]
internal static class HeadmanCompanionDamagePatch
{
    internal sealed class Hit
    {
        internal int Before;
        internal PlayerAvatar? Attacker;
        internal EnemyParent? Companion;
        internal Vector3 Position;
    }
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance, out Hit? __state)
    {
        __state = null;
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() || DifficultyService.Current < 4 ||
            !EnemyTarget.Valid(EnemyDamageSource.Current)) return;
        EnemyParent companion = NativeEnemy.Parent(__instance);
        if (companion == null || !NativeEnemy.Spawned(companion)) return;
        __state = new Hit { Before = NativeEnemy.Health(__instance), Attacker = EnemyDamageSource.Current,
            Companion = companion, Position = NativeEnemy.Position(companion) };
    }
    [HarmonyPostfix]
    private static void Postfix(EnemyHealth __instance, Hit? __state)
    {
        if (__state?.Companion != null && NativeEnemy.Health(__instance) < __state.Before)
            HeadmanProtection.CompanionHurt(__state.Companion, __state.Position, __state.Attacker);
    }
}
