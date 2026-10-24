namespace HellParty.Rules;

// A fully hidden native enemy must drain old RPCs before a new logical life.
internal sealed class SummonReuseClock
{
    internal const double DrainSeconds = 2;
    private double? _hiddenAt;
    internal void Observe(double now, bool parked)
    {
        if (!parked) _hiddenAt = null;
        else if (!_hiddenAt.HasValue) _hiddenAt = now;
    }
    internal bool Ready(double now) => _hiddenAt.HasValue && now - _hiddenAt.Value >= DrainSeconds;
    internal void Reset() => _hiddenAt = null;
}

internal static class RoomViewBudgetRules
{
    internal const int Reserve = 32;
    internal static bool Fits(int capacity, int used, int required) =>
        capacity > 0 && used >= 0 && required > 0 && (long)used + required + Reserve <= capacity;
}
