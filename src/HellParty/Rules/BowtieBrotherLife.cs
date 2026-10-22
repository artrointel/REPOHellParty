namespace HellParty.Rules;

internal sealed class BowtieBrotherLife
{
    internal const double Lifetime = 10;
    private double? _spawnedAt;
    internal bool Retired { get; private set; }
    internal bool Started => _spawnedAt.HasValue;

    internal void Start(double now)
    {
        if (!Retired && !_spawnedAt.HasValue) _spawnedAt = now;
    }
    internal bool Expire(double now)
    {
        if (Retired || !_spawnedAt.HasValue || now - _spawnedAt.Value < Lifetime) return false;
        Retired = true;
        return true;
    }
    internal void Retire() => Retired = true;
}

internal sealed class BowtieWindCycle
{
    private bool _summoned;
    internal void Observe(bool blowing) { if (!blowing) _summoned = false; }
    internal bool TrySummon(bool hasTarget)
    {
        if (_summoned || !hasTarget) return false;
        _summoned = true;
        return true;
    }
}
