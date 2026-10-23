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
    private readonly MonsterWaveClock _clock = new();
    private readonly MonsterWaveTriggerGate _triggerGate = new();
    private readonly Queue<string> _pending = new();
    private readonly List<WaveCreature> _creatures = new();
    private WaveGramophone? _music;
    private Vector3 _center;
    private float _retryAt;
    internal bool Active => _clock.Active;
    internal bool CanSpawn => Active && !_clock.Expired(Time.time);
    internal Vector3 Center => _center;
    internal bool CanCheckTrigger => _triggerGate.Ready(Time.timeAsDouble);

    private void Awake() => Current = this;

    internal void Destroyed(Vector3 point)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() ||
            MonsterWaveClock.SuppressTrigger(Active, (point - _center).sqrMagnitude)) return;
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
        End();
        _center = point;
        _music = next;
        _clock.Start(Time.time);
        _pending.Enqueue(rugrat); _pending.Enqueue(upscream); _pending.Enqueue(headgrab);
        _retryAt = 0;
        Pump();
    }

    private void Update()
    {
        if (!Active || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (!SemiFunc.RunIsLevel() || _clock.Expired(Time.time)) { End(); return; }
        while (_clock.TakeGnome(Time.time)) _pending.Enqueue("Enemies/Enemy - Gnome");
        if (Time.time >= _retryAt) Pump();
    }

    private void Pump()
    {
        _retryAt = Time.time + .25f;
        // Native registration defers birth. Its Spawn prefix rechecks visibility
        // and chooses the nearest player at actual birth, not at request time.
        while (_pending.Count > 0 && WaveSpawnPoints.Find(_center, out Vector3 point))
        {
            try
            {
                string path = _pending.Peek();
                if (path == "Enemies/Enemy - Gnome" && EnemyGnomeDirector.instance == null)
                    VanillaCatalog.Spawn("Enemies/Enemy - Gnome Director", Vector3.zero, Quaternion.identity);
                EnemyParent parent = SummonedEnemySpawnTicket.Create(path, point);
                WaveCreature member = parent.gameObject.AddComponent<WaveCreature>();
                member.Owner = this;
                _creatures.Add(member);
                _pending.Dequeue();
            }
            catch (Exception e) { Plugin.ModLogger.LogError($"Monster wave summon failed: {e}"); break; }
        }
    }

    internal bool Occupied(Vector3 point)
    {
        foreach (WaveCreature member in _creatures)
        {
            if (member == null || member.Retired) continue;
            EnemyParent parent = member.GetComponent<EnemyParent>();
            if (NativeEnemy.Spawned(parent) && (NativeEnemy.Position(parent) - point).sqrMagnitude < 4) return true;
        }
        return false;
    }

    private void End()
    {
        _clock.Stop(); _pending.Clear();
        foreach (WaveCreature creature in _creatures) if (creature != null) creature.Retire();
        _creatures.Clear();
        if (_music != null) _music.Remove();
        _music = null;
    }

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
