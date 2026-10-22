using System;
using System.Collections;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty;

internal sealed class AnimalDivision : MonoBehaviour
{
    private bool _handled;
    internal void ResetLife() => _handled = false;

    internal void Died(EnemyHealth health)
    {
        if (_handled) return;
        _handled = true;
        health.spawnValuable = false;
        Vector3 origin = NativeEnemy.Position(health);
        StageRuntime? stage = StageRuntime.Current;
        if (stage == null)
        {
            Plugin.ModLogger.LogError("Cannot schedule Animal division without an active stage.");
            return;
        }
        // Run on the stage, since the dying Animal will despawn before this
        // delay completes. Capture the death position/direction now.
        stage.StartCoroutine(SpawnAfterDelay(stage, origin, transform.right));
    }

    private static IEnumerator SpawnAfterDelay(StageRuntime stage, Vector3 origin, Vector3 right)
    {
        // Let the killing shot's overlapping attack volumes finish first.
        yield return new WaitForSeconds(0.5f);
        if (StageRuntime.Current != stage || !SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() ||
            LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated) yield break;
        for (int i = 0; i < 2; i++)
        {
            try
            {
                Vector3 candidate = origin + right * (i == 0 ? -0.4f : 0.4f);
                Vector3 point = NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ? hit.position : origin;
                AnimalSpawnTicket.Create(point, true);
            }
            catch (Exception exception) { Plugin.ModLogger.LogError($"Animal division child {i + 1} failed: {exception}"); }
        }
    }
}

internal sealed class AnimalOffspring : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<EnemyParent, float> ValuableTimer =
        AccessTools.FieldRefAccess<EnemyParent, float>("valuableSpawnTimer");
    private readonly AnimalOffspringLife _life = new();
    private EnemyParent _parent = null!;
    private Enemy _enemy = null!;
    private bool _deathHandled;
    internal bool Expired => _life.Expired;

    private void Awake()
    {
        _parent = GetComponent<EnemyParent>();
        _enemy = GetComponentInChildren<Enemy>(true);
    }

    internal void Born()
    {
        _deathHandled = false;
        int health = DifficultyRules.AnimalOffspringHealth(_enemy.Health.health);
        _enemy.Health.health = health;
        NativeEnemy.Health(_enemy.Health) = health;
        _enemy.Health.spawnValuable = true;
        _enemy.Health.spawnValuableMax = 1;
        _life.Start(Time.time);
    }

    private void Update()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !_life.Started) return;
        if (_life.Expire(Time.time))
        {
            _enemy.Health.spawnValuable = false;
            // Native AnimalAnim observes this state and its animation event
            // calls EnemyParent.Despawn. Do not kill it or delete its registry.
            if (NativeEnemy.Spawned(_parent)) _enemy.CurrentState = EnemyState.Despawn;
        }
    }

    internal void Died(EnemyHealth health)
    {
        if (_deathHandled) return;
        _deathHandled = true;
        bool reward = _life.Die(Time.time);
        health.spawnValuable = reward;
        if (reward)
        {
            // Use native orb generation, including its normal prefab/value.
            // Ensure newly spawned children can reward even before the native
            // one-second player-proximity poll has initialized this timer.
            ValuableTimer(_parent) = 10;
        }
    }
}

[HarmonyPatch(typeof(EnemyHealth), "Death")]
internal static class AnimalDeathPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<EnemyAnimal>() == null) return;
        EnemyParent parent = NativeEnemy.Parent(__instance);
        AnimalOffspring offspring = parent.GetComponent<AnimalOffspring>();
        if (offspring != null) offspring.Died(__instance);
        else if (DifficultyRules.AnimalDivisionUnlocked(DifficultyService.Current))
        {
            StageRuntime.TryAttach(parent);
            parent.GetComponent<AnimalDivision>()?.Died(__instance);
        }
    }
}
