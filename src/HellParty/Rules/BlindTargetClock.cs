namespace HellParty.Rules;

// One clock per monster. Each uninterrupted target tenure starts a fresh
// three-second interval; late frames never emit a backlog of clouds.
internal sealed class BlindTargetClock
{
    internal const double Interval = 3;
    internal static bool Unlocked(int difficulty) => difficulty >= 1;
    private object? _target;
    private double _readyAt;

    internal bool TryCast(double now, object? target)
    {
        if (target == null) { Reset(); return false; }
        if (!ReferenceEquals(_target, target))
        {
            _target = target;
            _readyAt = now + Interval;
            return false;
        }
        if (now < _readyAt) return false;
        _readyAt = now + Interval;
        return true;
    }

    internal void Reset() { _target = null; _readyAt = 0; }
}
