using System;
using System.Collections.Generic;

namespace HellParty.Rules;

internal enum StalkingDollMode { Waiting, Observed, Held, Following, Attacking, Rushing }

internal static class StalkingDollRules
{
    internal const float AttackDistance = 1.2f;
    internal const int PlayerDamage = 10;
    internal static int AdditionalPlayerDamage(int nativeDamage) => Math.Max(0, PlayerDamage - nativeDamage);
    internal static bool IncomingCollisionHurts(bool physicalObject, bool actor, double incomingSpeed) =>
        physicalObject && !actor && !double.IsNaN(incomingSpeed) && !double.IsInfinity(incomingSpeed) && incomingSpeed > .5;

    internal static StalkingDollMode Decide(bool observed, bool held, bool hasTarget, bool inReach, bool enraged = false) =>
        enraged && hasTarget && !held ? StalkingDollMode.Rushing :
        observed && !enraged ? StalkingDollMode.Observed : held ? StalkingDollMode.Held :
        !hasTarget ? StalkingDollMode.Waiting : inReach ? StalkingDollMode.Attacking : StalkingDollMode.Following;

    internal static bool Attacks(StalkingDollMode mode) => mode == StalkingDollMode.Attacking || mode == StalkingDollMode.Rushing;

    internal static bool Nearer(double distance, int id, double bestDistance, int bestId) =>
        !double.IsNaN(distance) && !double.IsInfinity(distance) && distance >= 0 &&
        (distance < bestDistance || distance == bestDistance && id < bestId);

    internal static bool InView(double x, double y, double z, double horizontalTangent, double verticalTangent) =>
        z > 0 && Math.Abs(x) <= z * horizontalTangent && Math.Abs(y) <= z * verticalTangent;
}

internal sealed class StalkingDollRage
{
    internal const double HitTimeout = 30;
    private double _lastHitAt;
    private readonly Dictionary<int, (double Until, bool CanTriggerRampage)> _pendingHits = new();
    internal int GrabCount { get; private set; }
    internal bool Enraged { get; private set; }
    internal float SpeedMultiplier => Enraged ? 3f : 1f;

    internal void GrabAttempt(double now)
    {
        Tick(now);
        if (Enraged) return;
        GrabCount++;
        if (GrabCount >= 3) Begin(now);
    }

    private void Begin(double now) { Enraged = true; _lastHitAt = now; }

    internal void Tick(double now)
    {
        if (Enraged && now - _lastHitAt >= HitTimeout) PlayerDied();
    }

    internal void Hit(double now, bool canTriggerRampage = false)
    {
        Tick(now);
        if (!Enraged && canTriggerRampage) Begin(now);
        if (Enraged) _lastHitAt = now;
    }

    internal void ExpectRemoteHit(int player, double now, bool canTriggerRampage = false)
    {
        Tick(now);
        if (Enraged || canTriggerRampage)
        {
            bool priorRampage = _pendingHits.TryGetValue(player, out var prior) && prior.Until >= now && prior.CanTriggerRampage;
            _pendingHits[player] = (now + 2, canTriggerRampage || priorRampage);
        }
    }

    internal void ConfirmRemoteHit(int player, double now, int before, int after)
    {
        if (!_pendingHits.TryGetValue(player, out var pending)) return;
        if (now > pending.Until) { _pendingHits.Remove(player); return; }
        if (after >= before) return;
        _pendingHits.Remove(player);
        Hit(now, pending.CanTriggerRampage);
    }

    internal void PlayerDied()
    {
        _pendingHits.Clear();
        if (!Enraged) return;
        Enraged = false;
        GrabCount = 0;
    }
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
