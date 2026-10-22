namespace HellParty.Rules;

internal sealed class AnimalOffspringLife
{
    internal const double Lifetime = 20;
    private double? _expiresAt;
    private bool _finished;
    internal bool Expired { get; private set; }
    internal bool Started => _expiresAt.HasValue;

    internal void Start(double now)
    {
        if (_expiresAt.HasValue) return;
        _expiresAt = now + Lifetime;
        _finished = false;
        Expired = false;
    }
    internal bool Expire(double now)
    {
        if (_finished || !_expiresAt.HasValue || now < _expiresAt.Value) return false;
        _finished = true;
        Expired = true;
        return true;
    }
    internal bool Die(double now)
    {
        if (_finished) return false;
        if (Expire(now)) return false;
        _finished = true;
        return _expiresAt.HasValue;
    }
}
