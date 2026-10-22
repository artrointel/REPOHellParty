using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HellParty.Native;

internal static class NativeEnemy
{
    internal static readonly AccessTools.FieldRef<EnemyParent, bool> Spawned = AccessTools.FieldRefAccess<EnemyParent, bool>("Spawned");
    internal static readonly AccessTools.FieldRef<EnemyParent, bool> SetupDone = AccessTools.FieldRefAccess<EnemyParent, bool>("SetupDone");
    internal static readonly AccessTools.FieldRef<EnemyParent, bool> FirstPointUsed = AccessTools.FieldRefAccess<EnemyParent, bool>("firstSpawnPointUsed");
    internal static readonly AccessTools.FieldRef<EnemyHealth, int> Health = AccessTools.FieldRefAccess<EnemyHealth, int>("healthCurrent");
    internal static readonly AccessTools.FieldRef<EnemyHealth, bool> Dead = AccessTools.FieldRefAccess<EnemyHealth, bool>("dead");
    private static readonly MethodInfo SpawnMethod = AccessTools.Method(typeof(EnemyParent), "Spawn");

    internal static EnemyDuck? Duck(EnemyParent parent) => parent.GetComponentInChildren<EnemyDuck>(true);
    internal static EnemyParent Parent(Component component) => component.GetComponentInParent<EnemyParent>();
    internal static Vector3 Position(EnemyDuck duck)
    {
        EnemyRigidbody body = Parent(duck).GetComponentInChildren<EnemyRigidbody>(true);
        return body != null ? body.transform.position : duck.transform.position;
    }
    internal static bool IsAlive(EnemyDuck duck)
    {
        EnemyHealth health = duck.GetComponent<EnemyHealth>();
        return health != null && Health(health) > 0 && !Dead(health);
    }
    internal static void Spawn(EnemyParent parent) => SpawnMethod.Invoke(parent, null);
}
