using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HellParty.Native;

internal static class EnemyTarget
{
    private static readonly Dictionary<Type, FieldInfo?> TargetFields = new();
    internal static bool Valid(PlayerAvatar? target) => target != null && !target.deadSet && !target.isDisabled;
    internal static bool Pursuing(Enemy enemy) => enemy.CheckChase() || enemy.CurrentState is
        EnemyState.Chase or EnemyState.ChaseBegin or EnemyState.ChaseSlow or EnemyState.LookUnder or EnemyState.Sneak;

    internal static PlayerAvatar? Read(Enemy enemy)
    {
        // Generic AI retains its last player even after returning to roaming.
        if (Pursuing(enemy) && Valid(enemy.TargetPlayerAvatar)) return enemy.TargetPlayerAvatar;
        if (enemy.GetComponent<EnemyGnome>() != null && EnemyGnomeDirector.instance != null)
        {
            PlayerAvatar? target = ReadComponent(EnemyGnomeDirector.instance);
            if (Valid(target)) return target;
        }
        if (enemy.GetComponent<EnemyBang>() != null && EnemyBangDirector.instance != null && Valid(EnemyBangDirector.instance.playerTarget))
            return EnemyBangDirector.instance.playerTarget;
        if (enemy.GetComponent<EnemyHunter>() is EnemyHunter hunter && hunter.investigatePointHasTransform)
            foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
                if (Valid(player) && player.PlayerVisionTarget != null && player.PlayerVisionTarget.VisionTransform == hunter.investigatePointTransform)
                    return player;
        foreach (MonoBehaviour component in enemy.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null || component is Enemy || component.GetType().Assembly != typeof(Enemy).Assembly) continue;
            PlayerAvatar? target = ReadComponent(component);
            if (Valid(target)) return target;
        }
        return null;
    }

    private static PlayerAvatar? ReadComponent(MonoBehaviour component)
    {
        Type type = component.GetType();
        if (!TargetFields.TryGetValue(type, out FieldInfo? field))
        {
            foreach (FieldInfo candidate in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (candidate.FieldType == typeof(PlayerAvatar) &&
                    (candidate.Name.Equals("playerTarget", StringComparison.OrdinalIgnoreCase) ||
                     candidate.Name.Equals("targetPlayer", StringComparison.OrdinalIgnoreCase) ||
                     candidate.Name.Equals("chaseTarget", StringComparison.OrdinalIgnoreCase)))
                { field = candidate; break; }
            TargetFields[type] = field;
        }
        return field?.GetValue(component) as PlayerAvatar;
    }
}
