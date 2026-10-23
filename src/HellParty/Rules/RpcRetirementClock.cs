using System;

namespace HellParty.Rules;

internal sealed class RpcRetirementClock
{
    private bool _started;
    private double _deadline;
    private double _limit;
    internal void Start(double now)
    {
        if (_started) return;
        _started = true;
        _deadline = now + 1;
        _limit = now + 3;
    }
    internal void Draining(double now)
    {
        if (_started) _deadline = Math.Min(_limit, Math.Max(_deadline, now + 1));
    }
    internal bool Due(double now) => _started && now >= _deadline;
}
