namespace HellParty.Rules;

// First target acquisition is immediately ready. Losing a target does not
// reset the deadline; a return after expiry permits one cast, never a burst.
internal sealed class TargetSkillClock
{
    private double? _readyAt;

    internal bool IsReady(double now, bool hasTarget) => hasTarget && (!_readyAt.HasValue || now >= _readyAt.Value);
    internal void Used(double now, double cooldown) => _readyAt = now + cooldown;
}
