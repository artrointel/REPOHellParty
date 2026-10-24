using System.Reflection;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

internal sealed class ElsaAssassination : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<EnemyElsa, PlayerAvatar> Target =
        AccessTools.FieldRefAccess<EnemyElsa, PlayerAvatar>("playerTarget");
    private static readonly AccessTools.FieldRef<EnemyElsa, float> Anger =
        AccessTools.FieldRefAccess<EnemyElsa, float>("angryTimer");
    private static readonly AccessTools.FieldRef<EnemyElsa, float> ChaseTime =
        AccessTools.FieldRefAccess<EnemyElsa, float>("chaseTimer");
    private static readonly MethodInfo SetState = AccessTools.Method(typeof(EnemyElsa), "UpdateState");
    private static readonly MethodInfo Shrink = AccessTools.Method(typeof(EnemyElsa), "TransformBigToSmallRPC");
    private readonly ElsaAssassinationClock _clock = new();
    private EnemyParent _parent = null!;
    private EnemyElsa _elsa = null!;
    private EnemyHealth _health = null!;
    internal bool Active => _clock.Active;

    private void Awake()
    {
        _parent = GetComponent<EnemyParent>();
        _elsa = GetComponentInChildren<EnemyElsa>(true);
        _health = _elsa.GetComponent<EnemyHealth>();
    }

    internal void NewLife() => _clock.NewLife();
    private void OnDisable() => _clock.End(Time.timeAsDouble);

    internal void BeforeNativeUpdate()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        // Check origins again even if another system adds a marker after attachment.
        if (!SemiFunc.RunIsLevel() || LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated ||
            !ElsaAssassinationClock.Eligible(DifficultyService.Current, GetComponent<SummonedEnemySpawnTicket>() != null) ||
            GetComponent<VanillaAbilityExcluded>() != null || GetComponent<BowtieBrother>() != null ||
            !NativeEnemy.Spawned(_parent) || NativeEnemy.Dead(_health) || NativeEnemy.Health(_health) <= 0)
        { _clock.End(Time.timeAsDouble); return; }

        PlayerAvatar target = Target(_elsa);
        bool valid = EnemyTarget.Valid(target);
        bool companion = false;
        if (valid)
            foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
            {
                if (player == null) continue;
                if (!ElsaAssassinationClock.CompanionNearby(EnemyTarget.Valid(player), player == target,
                        (player.transform.position - target.transform.position).sqrMagnitude)) continue;
                companion = true;
                break;
            }

        if (_clock.Active)
        {
            if (!_elsa.IsBig()) { _clock.End(Time.timeAsDouble); return; } // Respect native escape/state changes.
            if (!valid || companion || _elsa.enemy.IsStunned() || _elsa.enemy.CurrentState == EnemyState.Despawn)
            {
                _clock.End(Time.timeAsDouble);
                RestoreGentle(valid);
            }
            else if (_elsa.currentState == EnemyElsa.State.TransformBigToSmall)
                _clock.End(Time.timeAsDouble); // Native terrain/attack recovery chose to shrink.
            else ChaseTime(_elsa) = 0; // Under-table recovery also reads this native duration.
            return;
        }

        if (!valid || _elsa.enemy.IsStunned() || _elsa.enemy.CurrentState == EnemyState.Despawn) return;
        bool following = _elsa.currentState is EnemyElsa.State.GoToPlayerSmall or
            EnemyElsa.State.GoToPlayerOverSmall or EnemyElsa.State.GoToPlayerUnderSmall;
        if (!_clock.Begin(Time.timeAsDouble, following, companion,
                (NativeEnemy.Position(_elsa) - target.transform.position).sqrMagnitude)) return;
        // Skip the vanilla anger accumulation / search-for-transformation phase,
        // but retain the all-client growth animation and native attack sequence.
        SetState.Invoke(_elsa, new object[] { EnemyElsa.State.TransformSmallToBig });
    }

    private void RestoreGentle(bool validTarget)
    {
        // Cancel attacks now, including during growth, rather than waiting for
        // either the reentry cooldown or the native shrink state's .3s timer.
        _elsa.hurtCollider.SetActive(false);
        if (SemiFunc.IsMultiplayer())
            _elsa.GetComponent<PhotonView>().RPC("TransformBigToSmallRPC", RpcTarget.All, true);
        else Shrink.Invoke(_elsa, new object[] { true, default(PhotonMessageInfo) });
        Anger(_elsa) = 0;
        EnemyElsa.State state = _elsa.enemy.CurrentState == EnemyState.Despawn ? EnemyElsa.State.DespawnSmall :
            _elsa.enemy.IsStunned() ? EnemyElsa.State.StunSmall :
            validTarget ? EnemyElsa.State.GoToPlayerSmall : EnemyElsa.State.IdleSmall;
        SetState.Invoke(_elsa, new object[] { state });
    }
}

[HarmonyPatch(typeof(EnemyElsa), "Update")]
internal static class ElsaAssassinationUpdatePatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyElsa __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
            NativeEnemy.Parent(__instance).GetComponent<ElsaAssassination>()?.BeforeNativeUpdate();
    }
}

[HarmonyPatch(typeof(EnemyElsa), "ChaseStop")]
internal static class ElsaAssassinationChaseStopPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyElsa __instance) => !SemiFunc.IsMasterClientOrSingleplayer() ||
        NativeEnemy.Parent(__instance).GetComponent<ElsaAssassination>()?.Active != true;
}

[HarmonyPatch(typeof(EnemyElsa), nameof(EnemyElsa.OnSpawn))]
internal static class ElsaAssassinationNewLifePatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyElsa __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
            NativeEnemy.Parent(__instance).GetComponent<ElsaAssassination>()?.NewLife();
    }
}
