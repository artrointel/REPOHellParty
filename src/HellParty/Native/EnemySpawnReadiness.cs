using HarmonyLib;
using UnityEngine;

namespace HellParty.Native;

internal static class EnemySpawnReadiness
{
    private static readonly AccessTools.FieldRef<EnemyStateStunned, Enemy> StunOwner =
        AccessTools.FieldRefAccess<EnemyStateStunned, Enemy>("enemy");

    internal static bool IsPreparing(Component component)
    {
        AnimalSpawnTicket animal = component.GetComponentInParent<AnimalSpawnTicket>();
        SummonedEnemySpawnTicket summoned = component.GetComponentInParent<SummonedEnemySpawnTicket>();
        if (animal == null && summoned == null) return false;
        if ((animal != null && animal.Pending) || (summoned != null && summoned.Pending)) return true;
        EnemyStateStunned stunned = component.GetComponentInParent<EnemyParent>().GetComponentInChildren<EnemyStateStunned>(true);
        return stunned != null && StunOwner(stunned) == null;
    }
}
