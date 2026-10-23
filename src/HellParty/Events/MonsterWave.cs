using System;
using System.Collections.Generic;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using UnityEngine;

namespace HellParty.Events;

internal sealed class MonsterWave : MonoBehaviour
{
    internal static MonsterWave? Current;
    private MonsterWaveClock _clock = new();
    private readonly MonsterWaveTriggerGate _triggerGate = new();
    private readonly Queue<string> _pending = new();
    private readonly List<WaveCreature> _creatures = new();
    private WaveGramophone? _music;
    private Warning? _warning;
    private Vector3 _center;
    private float _retryAt;
    internal bool Active => _clock.Active || _warning != null;
    internal bool CanSpawn => _clock.Spawning && !_clock.Expired(Time.time);
    internal Vector3 Center => _center;
    internal bool CanCheckTrigger => _triggerGate.Ready(Time.timeAsDouble);

    private void Awake() { Current = this; WaveSpawnPoints.PrepareStage(); }

    private sealed class Warning
    {
        internal readonly MonsterWaveClock Clock = new();
        internal WaveGramophone Music = null!;
        internal Vector3 Center;
        internal string Rugrat = null!, Upscream = null!, Headgrab = null!;
    }

    internal void Destroyed(Vector3 point)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() ||
            MonsterWaveClock.SuppressTrigger(_clock.Active, (point - _center).sqrMagnitude) ||
            _warning != null && MonsterWaveClock.SuppressTrigger(true, (point - _warning.Center).sqrMagnitude)) return;
        // Consume before player/navmesh/visibility checks, even if admission fails.
        // This also closes reentrant destruction paths during event creation.
        if (!_triggerGate.TryBeginCheck(Time.timeAsDouble)) return;
        bool nearby = WaveSpawnPoints.Nearest(point, MonsterWaveClock.Radius) != null;
        if (!MonsterWaveClock.CanStart(DifficultyService.Current, nearby, nearby && WaveSpawnPoints.Find(point, out _))) return;

        // Resolve all assets and create the new music prop before retiring the old wave.
        // A failed admission or missing asset must leave the current event running.
        WaveGramophone? next = null;
        string rugrat, upscream, headgrab;
        try
        {
            rugrat = VanillaCatalog.RugratPrefab().ResourcePath;
            upscream = VanillaCatalog.UpscreamPrefab().ResourcePath;
            headgrab = VanillaCatalog.HeadgrabPrefab().ResourcePath;
            if (Resources.Load<GameObject>("Enemies/Enemy - Gnome") == null) throw new InvalidOperationException("Native Gnome prefab missing.");
            next = WaveGramophone.Create(point);
        }
        catch (Exception e)
        {
            if (next != null) next.Remove();
            Plugin.ModLogger.LogError($"Monster wave could not start: {e}");
            return;
        }
        CancelWarning();
        _warning = new Warning { Center = point, Music = next, Rugrat = rugrat, Upscream = upscream, Headgrab = headgrab };
        _warning.Clock.Start(Time.time);
    }

    private void Update()
    {
        if (!Active || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (!SemiFunc.RunIsLevel()) { End(); return; }
        if (_clock.Expired(Time.time)) EndCombat();
        CheckWarning();
        if (!_clock.Spawning) return;
        while (_clock.TakeGnome(Time.time))
            for (int i = 0; i < MonsterWaveClock.GnomesPerTick; i++) _pending.Enqueue("Enemies/Enemy - Gnome");
        if (Time.time >= _retryAt) Pump();
    }

    private void CheckWarning()
    {
        Warning? next = _warning;
        if (next == null) return;
        if (next.Clock.Expired(Time.time) || next.Music == null) { CancelWarning(); return; }
        if (!next.Clock.AwaitingDecision(Time.time)) return;

        bool exposed = false;
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!WaveSpawnPoints.Alive(player)) continue;
            if (MonsterWaveClock.Exposed(true, (player.transform.position - next.Center).sqrMagnitude,
                    player.isCrouching, player.isCrawling, player.isTumbling)) { exposed = true; break; }
        }
        if (!next.Clock.Decide(Time.time, exposed)) { CancelWarning(); return; }

        // A cancelled warning must never retire an already running wave.
        // Only commit replacement once the three-second evasion check passes.
        _warning = null;
        EndCombat();
        _clock = next.Clock;
        _center = next.Center;
        _music = next.Music;
        _pending.Enqueue(next.Rugrat); _pending.Enqueue(next.Upscream); _pending.Enqueue(next.Headgrab);
        _retryAt = 0;
    }

    private void Pump()
    {
        _retryAt = Time.time + WaveSpawnRules.RetryInterval;
        // Native registration defers birth. Its Spawn prefix rechecks visibility
        // and chooses the nearest player at actual birth, not at request time.
        int attempts = Math.Min(4, _pending.Count);
        while (attempts-- > 0 && _pending.Count > 0)
        {
            string path = _pending.Dequeue();
            try
            {
                GameObject prefab = Resources.Load<GameObject>(path);
                if (!WaveSpawnPoints.Find(_center, out Vector3 point, prefab.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true)))
                {
                    // A large enemy waiting for space must not block Gnomes.
                    _pending.Enqueue(path);
                    continue;
                }
                if (path == "Enemies/Enemy - Gnome" && EnemyGnomeDirector.instance == null)
                    VanillaCatalog.Spawn("Enemies/Enemy - Gnome Director", Vector3.zero, Quaternion.identity);
                EnemyParent parent = SummonedEnemySpawnTicket.Create(path, point, prepareOnMesh: true);
                WaveCreature member = parent.gameObject.AddComponent<WaveCreature>();
                member.Owner = this;
                _creatures.Add(member);
            }
            catch (Exception e) { _pending.Enqueue(path); Plugin.ModLogger.LogError($"Monster wave summon failed: {e}"); break; }
        }
    }

    internal bool Occupied(Vector3 point, EnemyParent? ignore = null)
    {
        foreach (WaveCreature member in _creatures)
        {
            if (member == null || member.Retired) continue;
            EnemyParent parent = member.GetComponent<EnemyParent>();
            if (parent == ignore) continue;
            SummonedEnemySpawnTicket ticket = parent.GetComponent<SummonedEnemySpawnTicket>();
            if (ticket != null && ticket.Pending && (ticket.Position - point).sqrMagnitude < 4) return true;
            if (NativeEnemy.Spawned(parent) && (NativeEnemy.Position(parent) - point).sqrMagnitude < 4) return true;
        }
        return false;
    }

    private void EndCombat()
    {
        _clock.Stop(); _pending.Clear();
        foreach (WaveCreature creature in _creatures) if (creature != null) creature.Retire();
        _creatures.Clear();
        if (_music != null) _music.BreakAtEnd();
        _music = null;
    }

    private void CancelWarning()
    {
        if (_warning == null) return;
        _warning.Clock.Stop();
        if (_warning.Music != null) _warning.Music.Remove();
        _warning = null;
    }

    private void End() { CancelWarning(); EndCombat(); }

    private void OnDestroy() { End(); if (Current == this) Current = null; }
}

[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.DestroyObject))]
internal static class MonsterWaveDestructionPatch
{
    internal sealed class Destruction
    {
        internal Vector3 Position;
    }
    [HarmonyPrefix]
    private static void Prefix(PhysGrabObjectImpactDetector __instance, out Destruction? __state)
    {
        __state = null;
        if (!SemiFunc.IsMasterClientOrSingleplayer() || MonsterWave.Current == null ||
            !MonsterWave.Current.CanCheckTrigger || __instance.destroyDisable) return;
        PhysGrabObject physical = __instance.GetComponent<PhysGrabObject>();
        if (physical == null || physical.dead || __instance.GetComponent<ValuableObject>() == null ||
            __instance.GetComponentInParent<EnemyValuable>() != null ||
            __instance.GetComponentInChildren<EnemyValuable>(true) != null ||
            __instance.GetComponent<EnemyRigidbody>() != null || __instance.GetComponent<WaveGramophone>() != null ||
            __instance.GetComponent<ChefMimicFrog>() != null || __instance.GetComponent<StalkingDoll>() != null ||
            __instance.GetComponent<ClownStaff>() is ClownStaff staff && staff.Pinned ||
            __instance.GetComponent<CrownCarrier>() is CrownCarrier crown && crown.IsPinned ||
            __instance.GetComponent<BellaDollCarrier>() is BellaDollCarrier doll && doll.IsPinned ||
            __instance.GetComponent<InternalWaveExcluded>() != null) return;
        __state = new Destruction { Position = physical.centerPoint };
    }

    [HarmonyPostfix]
    private static void Postfix(PhysGrabObjectImpactDetector __instance, Destruction? __state)
    {
        if (__state != null && __instance != null && __instance.GetComponent<PhysGrabObject>().dead)
            MonsterWave.Current?.Destroyed(__state.Position);
    }
}

internal sealed class InternalWaveExcluded : MonoBehaviour { }
