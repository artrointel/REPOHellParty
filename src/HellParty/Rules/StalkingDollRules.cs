using System;
using System.Collections.Generic;

namespace HellParty.Rules;

internal enum StalkingDollMode { Waiting, Observed, Held, Following, Attacking }

internal static class StalkingDollRules
{
    internal const float AttackDistance = 1.2f;
    internal const int PlayerDamage = 10;
    internal static int AdditionalPlayerDamage(int nativeDamage) => Math.Max(0, PlayerDamage - nativeDamage);

    internal static StalkingDollMode Decide(bool observed, bool held, bool hasTarget, bool inReach) =>
        observed ? StalkingDollMode.Observed : held ? StalkingDollMode.Held :
        !hasTarget ? StalkingDollMode.Waiting : inReach ? StalkingDollMode.Attacking : StalkingDollMode.Following;

    internal static bool Nearer(double distance, int id, double bestDistance, int bestId) =>
        !double.IsNaN(distance) && !double.IsInfinity(distance) && distance >= 0 &&
        (distance < bestDistance || distance == bestDistance && id < bestId);

    internal static bool InView(double x, double y, double z, double horizontalTangent, double verticalTangent) =>
        z > 0 && Math.Abs(x) <= z * horizontalTangent && Math.Abs(y) <= z * verticalTangent;
}

internal sealed class StalkingDollGrabAttempts
{
    internal const float ReleaseLockout = 0.5f;
    private readonly Dictionary<int, float> _nextAttempt = new();

    internal bool Accept(int player, float time)
    {
        if (_nextAttempt.TryGetValue(player, out float next) && time < next) return false;
        _nextAttempt[player] = time + ReleaseLockout;
        return true;
    }
}
