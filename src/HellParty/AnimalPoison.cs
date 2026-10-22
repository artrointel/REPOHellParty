using System;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using UnityEngine;

namespace HellParty;

internal sealed class AnimalPoison : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<EnemyAnimal, PlayerAvatar> Target =
        AccessTools.FieldRefAccess<EnemyAnimal, PlayerAvatar>("playerTarget");
    private EnemyParent _parent = null!;
    private EnemyAnimal _animal = null!;
    private EnemyHealth _health = null!;
    private readonly TargetSkillClock _clock = new();
    private float _retryAt;
    private bool _loggedError;

    private void Awake()
    {
        _parent = GetComponent<EnemyParent>();
        _animal = GetComponentInChildren<EnemyAnimal>(true);
        _health = _animal.GetComponent<EnemyHealth>();
    }

    private void Update()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() ||
            LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated ||
            !NativeEnemy.Spawned(_parent) || NativeEnemy.Dead(_health) || NativeEnemy.Health(_health) <= 0) return;
        int cooldown = DifficultyRules.AnimalGasCooldown(DifficultyService.Current);
        PlayerAvatar target = Target(_animal);
        if (target == null) return;
        AnimalOffspring offspring = GetComponent<AnimalOffspring>();
        if (cooldown == 0 || Time.time < _retryAt || (offspring != null && offspring.Expired) ||
            !_clock.IsReady(Time.time, true) ||
            !DifficultyRules.AnimalGasInRange((_animal.transform.position - target.transform.position).sqrMagnitude)) return;
        try
        {
            AnimalGas.Emit(target.transform.position);
            _clock.Used(Time.time, cooldown);
            _loggedError = false;
        }
        catch (Exception exception)
        {
            _retryAt = Time.time + 1;
            if (!_loggedError) Plugin.ModLogger.LogError($"Animal gas failed; retrying: {exception}");
            _loggedError = true;
        }
    }
}
