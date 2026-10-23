namespace HellParty.Rules;

internal static class CleanupPursuitRules
{
    internal const float Delay = 2;
    internal const double Lifetime = 10;
    internal static bool Unlocked(int difficulty) => difficulty >= 1;
    internal static bool Upscream(int difficulty) => difficulty >= 5;
}

// A native head is pooled and can explode once per Spawn cycle.
internal sealed class CleanupHeadCycle
{
    private bool _exploded;
    internal void Spawned() => _exploded = false;
    internal bool Exploded(int difficulty, bool hasTarget)
    {
        if (_exploded) return false;
        _exploded = true;
        return CleanupPursuitRules.Unlocked(difficulty) && hasTarget;
    }
}

internal sealed class CleanupSummonLife
{
    private double? _spawnedAt;
    internal bool Started => _spawnedAt.HasValue;
    internal bool Retired { get; private set; }
    internal void Born(double now) { if (!Started && !Retired) _spawnedAt = now; }
    internal bool Tick(double now)
    {
        if (Retired || !_spawnedAt.HasValue || now - _spawnedAt.Value < CleanupPursuitRules.Lifetime) return false;
        Retired = true;
        return true;
    }
    internal void Retire() => Retired = true;
    internal bool AttackApplied(bool upscream)
    {
        if (!upscream || !Started || Retired) return false;
        Retired = true;
        return true;
    }
}
