namespace HellParty.Rules;

internal sealed class MonsterWaveClock
{
    internal const double Duration = 20;
    internal const int WarningDuration = 3;
    internal const int GnomesPerTick = 2;
    internal const float Radius = 20;
    internal bool Active { get; private set; }
    internal bool Spawning { get; private set; }
    private double _start;
    private int _next;

    internal void Start(double now) { Active = true; Spawning = false; _start = now; _next = WarningDuration + 1; }
    internal bool Expired(double now) => Active && now - _start >= Duration;
    internal bool AwaitingDecision(double now) => Active && !Spawning && !Expired(now) && now - _start >= WarningDuration;
    internal bool Decide(double now, bool exposedPlayer)
    {
        if (!AwaitingDecision(now)) return false;
        if (!exposedPlayer) { Stop(); return false; }
        Spawning = true;
        return true;
    }
    internal bool TakeGnome(double now)
    {
        if (!Spawning || !Active || Expired(now) || _next >= Duration || now - _start < _next) return false;
        _next++;
        return true;
    }
    internal void Stop() { Active = false; Spawning = false; }
    internal static bool Exposed(bool alive, float squaredDistance, bool crouching, bool crawling, bool tumbling) =>
        alive && squaredDistance <= Radius * Radius && !crouching && !crawling && !tumbling;
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
