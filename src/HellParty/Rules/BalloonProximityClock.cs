namespace HellParty.Rules;

internal sealed class BalloonProximityClock
{
    internal const float Radius = 3;
    internal const double Duration = 3;
    private double? _enteredAt;
    private bool _triggered;

    internal bool Observe(double now, bool playerInRange)
    {
        if (_triggered) return false;
        if (!playerInRange) { _enteredAt = null; return false; }
        if (!_enteredAt.HasValue) _enteredAt = now;
        if (now - _enteredAt.Value < Duration) return false;
        _triggered = true;
        return true;
    }
}
