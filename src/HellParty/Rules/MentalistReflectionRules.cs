using System.Collections.Generic;

namespace HellParty.Rules;

internal enum ReflectedShotDecision { Ignore, Reflect, AlreadyReflected }

internal sealed class MentalistReflectionRules
{
    private readonly Dictionary<long, bool> _shots = new();
    private readonly Queue<long> _order = new();
    internal const double Delay = 2;
    internal static bool Due(double now, double started) => now - started >= Delay;

    internal ReflectedShotDecision Observe(long shot, bool hasTarget)
    {
        if (_shots.TryGetValue(shot, out bool reflected))
            return reflected ? ReflectedShotDecision.AlreadyReflected : ReflectedShotDecision.Ignore;
        bool reflect = !hasTarget;
        _shots.Add(shot, reflect);
        _order.Enqueue(shot);
        if (_order.Count > 256) _shots.Remove(_order.Dequeue());
        return reflect ? ReflectedShotDecision.Reflect : ReflectedShotDecision.Ignore;
    }
}
