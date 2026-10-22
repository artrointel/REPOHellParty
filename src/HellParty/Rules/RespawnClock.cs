using System;

namespace HellParty.Rules;

// One clock per original duck, not per clone. Distance recalls do not touch it.
internal sealed class RespawnClock
{
    internal const double Cooldown = 30;
    private double? _readyAt;
    internal void Reset() => _readyAt = null;
    internal void NoteLoss(double now) => _readyAt ??= now + Cooldown;
    internal bool IsReady(double now) => _readyAt.HasValue && now >= _readyAt.Value;
    internal void Spawned(double now, bool stillMissing) => _readyAt = stillMissing ? now + Cooldown : (double?)null;
    internal void RetryLater(double now) => _readyAt = now + 1;
}
