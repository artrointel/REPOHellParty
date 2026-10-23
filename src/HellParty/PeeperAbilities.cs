using System;
using System.Collections.Generic;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty;

internal sealed class PeeperAbilities : MonoBehaviour
{
    internal sealed class Hit
    {
        internal PeeperAbilities Owner = null!;
        internal PlayerHealth Health = null!;
        internal PlayerAvatar Target = null!;
        internal Vector3 Position;
        internal int Before;
        internal bool Local;
        internal double At;
    }

    private readonly PeeperSummonRules _summons = new();
    private readonly List<Hit> _pending = new();
    private EnemyCeilingEye _eye = null!;
    private void Awake() => _eye = GetComponentInChildren<EnemyCeilingEye>(true);

    internal Hit? Prepare(PlayerHealth health)
    {
        _pending.RemoveAll(hit => Time.timeAsDouble - hit.At > 2);
        PlayerAvatar target = _eye.targetPlayer;
        if (!_summons.Available || !NativeEnemy.Spawned(GetComponent<EnemyParent>()) ||
            target == null || target.deadSet || target.isDisabled || target.playerHealth != health) return null;
        Hit pending = new()
        {
            Owner = this, Health = health, Target = target, Position = NativeEnemy.Position(_eye),
            Before = health.health, Local = !SemiFunc.IsMultiplayer() || target.photonView.IsMine, At = Time.timeAsDouble
        };
        if (!pending.Local) _pending.Add(pending);
        return pending;
    }

    internal static void ConfirmRemote(PlayerHealth health)
    {
        Hit? oldest = null;
        foreach (PeeperAbilities owner in FindObjectsOfType<PeeperAbilities>())
        {
            owner._pending.RemoveAll(hit => Time.timeAsDouble - hit.At > 2);
            foreach (Hit hit in owner._pending)
                if (hit.Health == health && (oldest == null || hit.At < oldest.At)) oldest = hit;
        }
        // One real health decrease can consume at most one outstanding request.
        if (oldest == null) return;
        oldest.Owner._pending.Remove(oldest);
        oldest.Owner.Emit(oldest);
    }

    internal void Emit(Hit hit)
    {
        if (!_summons.Available || !SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel()) return;
        bool banger = DifficultyRules.PeeperBangerUnlocked(DifficultyService.Current) &&
            PeeperSummonRules.BangerRoll(UnityEngine.Random.value);
        // Gnome is guaranteed first. A bonus Banger needs its own remaining slot.
        Spawn(hit, false);
        if (banger && _summons.Available) Spawn(hit, true);
    }

    private void Spawn(Hit hit, bool banger)
    {
        if (!_summons.Available) return;
        try
        {
            // Peeper hangs from the ceiling; ground-based enemies must bind to
            // the NavMesh below it rather than appearing in mid-air.
            Vector3 sample = hit.Position;
            if (Physics.Raycast(sample - Vector3.up * 0.2f, Vector3.down, out RaycastHit floor, 40,
                    LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)) sample = floor.point;
            if (!NavMesh.SamplePosition(sample, out NavMeshHit ground, 3, NavMesh.AllAreas))
                throw new InvalidOperationException($"No walkable Peeper summon position below {hit.Position}.");
            string path;
            if (banger)
            {
                if (EnemyBangDirector.instance == null)
                    VanillaCatalog.Spawn("Enemies/Enemy - Bang Director", Vector3.zero, Quaternion.identity);
                path = VanillaCatalog.BangerPrefab().ResourcePath;
            }
            else
            {
                if (EnemyGnomeDirector.instance == null)
                    VanillaCatalog.Spawn(BirthdayBalloonSpawn.GnomeDirectorPath, Vector3.zero, Quaternion.identity);
                path = BirthdayBalloonSpawn.GnomePath;
            }
            SummonedEnemySpawnTicket.Create(path, ground.position, hit.Target, shareAttackTarget: true);
            _summons.Spawned();
        }
        catch (Exception exception) { Plugin.ModLogger.LogError($"Peeper {(banger ? "Banger" : "Gnome")} summon failed: {exception}"); }
    }
}

[HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.HurtOther))]
internal static class PeeperDamageRequestPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerHealth __instance, int damage, int enemyIndex, out PeeperAbilities.Hit? __state)
    {
        __state = null;
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() || damage <= 0 || enemyIndex < 0 ||
            !DifficultyRules.PeeperGnomeUnlocked(DifficultyService.Current)) return;
        Enemy enemy = SemiFunc.EnemyGetFromIndex(enemyIndex);
        if (enemy == null || enemy.GetComponent<EnemyCeilingEye>() == null) return;
        EnemyParent parent = NativeEnemy.Parent(enemy);
        StageRuntime.TryAttach(parent);
        __state = parent.GetComponent<PeeperAbilities>()?.Prepare(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix(PeeperAbilities.Hit? __state)
    {
        if (__state != null && __state.Local && PeeperSummonRules.EffectiveHit(__state.Before, __state.Health.health))
            __state.Owner.Emit(__state);
    }
}

[HarmonyPatch(typeof(PlayerHealth), "UpdateHealthRPC")]
internal static class PeeperHealthConfirmationPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerHealth __instance, out int __state) => __state = __instance.health;

    [HarmonyPostfix]
    private static void Postfix(PlayerHealth __instance, int __state)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && PeeperSummonRules.EffectiveHit(__state, __instance.health))
            PeeperAbilities.ConfirmRemote(__instance);
    }
}

[HarmonyPatch(typeof(PlayerAvatar), "PlayerDeathRPC")]
internal static class PeeperLethalHitConfirmationPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerAvatar __instance, out bool __state) => __state = __instance.deadSet;

    [HarmonyPostfix]
    private static void Postfix(PlayerAvatar __instance, bool __state)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && !__state && __instance.deadSet)
            PeeperAbilities.ConfirmRemote(__instance.playerHealth);
    }
}
