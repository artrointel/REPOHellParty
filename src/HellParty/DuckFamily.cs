using System;
using System.Collections.Generic;
using HellParty.Native;
using HellParty.Rules;
using UnityEngine;

namespace HellParty;

// One host-local controller per native original. Clone roots are pooled, keeping
// native EnemyDirector registries valid without accumulating new enemies per kill.
internal sealed class DuckFamily : MonoBehaviour
{
    private EnemyParent _parent = null!;
    private EnemyDuck _duck = null!;
    private readonly List<DuckClone> _clones = new();
    private readonly RespawnClock _clock = new();
    private CrownCarrier? _crown;
    private bool _lifeStarted;
    private bool _deathHandled;
    private bool _initialFill;
    private float _retryAt;
    private bool _loggedError;

    private void Awake()
    {
        _parent = GetComponent<EnemyParent>();
        _duck = NativeEnemy.Duck(_parent)!;
    }

    private void Update()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() ||
            LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated) return;
        DuckSpawnTicket ticket = GetComponent<DuckSpawnTicket>();
        if (ticket != null && !ticket.HasSpawned) return;
        if (!NativeEnemy.IsAlive(_duck)) return;
        if (!NativeEnemy.Spawned(_parent))
        {
            SuspendClones();
            return;
        }
        try { Tick(); }
        catch (Exception exception)
        {
            _retryAt = Time.time + 2;
            if (!_loggedError) Plugin.ModLogger.LogError($"Apex Predator ability failed; retrying: {exception}");
            _loggedError = true;
        }
    }

    private void Tick()
    {
        if (Time.time < _retryAt) return;
        int limit = DifficultyRules.CloneLimit(DifficultyService.Current);
        if (limit == 0) return;
        if (!_lifeStarted)
        {
            _lifeStarted = true;
            _deathHandled = false;
            _initialFill = true;
            _clock.Reset();
            Plugin.ModLogger.LogInfo($"Apex Predator spawned: clone limit {limit} (no recognition gate).");
        }
        if (_crown == null) _crown = CrownCarrier.Create(_duck);

        if (_initialFill)
        {
            for (int i = 0; i < limit; i++)
            {
                if (i < _clones.Count && _clones[i].Alive) continue;
                if (!SpawnSlot(i)) return;
            }
            _initialFill = false;
        }

        for (int i = 0; i < _clones.Count; i++)
        {
            DuckClone clone = _clones[i];
            if (!clone.Alive || !EnemyDirector.instance.enemiesSpawned.Contains(clone.GetComponent<EnemyParent>())) continue;
            EnemyParent cloneParent = clone.GetComponent<EnemyParent>();
            EnemyDuck cloneDuck = NativeEnemy.Duck(cloneParent)!;
            if (!NativeEnemy.Spawned(cloneParent) || DifficultyRules.ShouldRecall((NativeEnemy.Position(_duck) - NativeEnemy.Position(cloneDuck)).sqrMagnitude))
            {
                Recall(clone, i);
            }
        }

        int living = LivingCount();
        if (living >= limit) return;
        _clock.NoteLoss(Time.time);
        if (!_clock.IsReady(Time.time)) return;
        for (int i = 0; i < limit; i++)
        {
            if (i < _clones.Count && _clones[i].Alive) continue;
            if (SpawnSlot(i))
            {
                _clock.Spawned(Time.time, LivingCount() < limit);
                Plugin.ModLogger.LogInfo("Apex Predator: respawned one clone (shared 30-second cooldown).");
            }
            else _clock.RetryLater(Time.time);
            break;
        }
    }

    private int LivingCount()
    {
        int count = 0;
        foreach (DuckClone clone in _clones) if (clone != null && clone.Alive) count++;
        return count;
    }

    private bool SpawnSlot(int slot)
    {
        if (!VanillaCatalog.Adjacent(NativeEnemy.Position(_duck), slot, out Vector3 point))
        {
            _retryAt = Time.time + 1;
            return false;
        }
        if (slot == _clones.Count)
        {
            EnemyParent root = DuckSpawnTicket.Create(point, this);
            _clones.Add(root.GetComponent<DuckClone>());
        }
        else
        {
            DuckClone clone = _clones[slot];
            clone.Retired = false;
            clone.Killed = false;
            EnemyParent parent = clone.GetComponent<EnemyParent>();
            parent.GetComponent<DuckSpawnTicket>().Prepare(point);
            NativeEnemy.Spawn(parent);
        }
        return true;
    }

    private void Recall(DuckClone clone, int slot)
    {
        EnemyParent parent = clone.GetComponent<EnemyParent>();
        if (parent.GetComponent<DuckSpawnTicket>().Pending) return;
        EnemyDuck duck = NativeEnemy.Duck(parent)!;
        if (NativeEnemy.Spawned(parent))
        {
            // Let the native duck state machine broadcast its Despawn state.
            // The animation event calls EnemyParent.Despawn when it finishes;
            // only then may this same instance be moved and spawned again.
            duck.enemy.CurrentState = EnemyState.Despawn;
            return;
        }
        if (!VanillaCatalog.Adjacent(NativeEnemy.Position(_duck), slot, out Vector3 point)) return;
        int health = NativeEnemy.Health(duck.GetComponent<EnemyHealth>());
        // SpawnRPC resets health; our postfix restores this snapshot. Neither
        // the native animation nor this relocation fires death or our clock.
        parent.GetComponent<DuckSpawnTicket>().Prepare(point, health);
        NativeEnemy.Spawn(parent);
    }

    private void SuspendClones()
    {
        foreach (DuckClone clone in _clones)
        {
            if (clone != null && clone.Alive && NativeEnemy.Spawned(clone.GetComponent<EnemyParent>()))
                clone.GetComponent<EnemyParent>().Despawn();
        }
    }

    internal void CloneDied(DuckClone clone)
    {
        if (!_deathHandled) _clock.NoteLoss(Time.time);
    }

    internal void OriginalDied()
    {
        if (_deathHandled) return;
        _deathHandled = true;
        _lifeStarted = false;
        _clock.Reset();
        if (_crown != null) { _crown.Release(); _crown = null; }
        foreach (DuckClone clone in _clones)
        {
            if (clone == null) continue;
            clone.Retired = true;
            EnemyDuck duck = NativeEnemy.Duck(clone.GetComponent<EnemyParent>())!;
            if (!clone.Killed && NativeEnemy.IsAlive(duck)) duck.GetComponent<EnemyHealth>().Hurt(1000000, Vector3.zero);
        }
        Plugin.ModLogger.LogInfo("Apex Predator died: Crown released; clones killed; respawns stopped.");
    }
}
