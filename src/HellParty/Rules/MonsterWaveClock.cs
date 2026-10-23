namespace HellParty.Rules;

internal sealed class MonsterWaveClock
{
    internal const double Duration = 20;
    internal const float Radius = 30;
    internal bool Active { get; private set; }
    private double _start;
    private int _next;

    internal void Start(double now) { Active = true; _start = now; _next = 1; }
    internal bool Expired(double now) => Active && now - _start >= Duration;
    internal bool TakeGnome(double now)
    {
        if (!Active || Expired(now) || _next >= Duration || now - _start < _next) return false;
        _next++;
        return true;
    }
    internal void Stop() => Active = false;
    internal static bool SuppressTrigger(bool active, float squaredDistance) => active && squaredDistance <= Radius * Radius;
    internal static bool CanStart(int difficulty, bool nearbyPlayer, bool hiddenPosition) => difficulty >= 1 && nearbyPlayer && hiddenPosition;
}

// Rate-limit admission attempts, including ones with no nearby player/blind spot.
// Rejected bursts do not move the deadline or postpone the next allowed check.
internal sealed class MonsterWaveTriggerGate
{
    internal const double Interval = 3;
    private double _nextCheck = double.NegativeInfinity;
    internal bool Ready(double now) => now >= _nextCheck;
    internal bool TryBeginCheck(double now)
    {
        if (!Ready(now)) return false;
        _nextCheck = now + Interval;
        return true;
    }
}
