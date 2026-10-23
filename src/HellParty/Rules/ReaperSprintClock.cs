namespace HellParty.Rules;

internal enum ReaperSprintMode { Reaper, Preparing, Headman, Restoring, Dead }

internal sealed class ReaperSprintClock
{
    internal ReaperSprintMode Mode { get; private set; }
    private double _readyAt;
    internal static bool Unlocked(int difficulty) => difficulty >= 2;
    internal bool Begin(double now, bool chasing, double squaredDistance)
    {
        if (Mode != ReaperSprintMode.Reaper || !chasing || now < _readyAt || !(squaredDistance >= 100)) return false;
        Mode = ReaperSprintMode.Preparing;
        _readyAt = now + 10;
        return true;
    }
    internal bool Born()
    {
        if (Mode != ReaperSprintMode.Preparing) return false;
        Mode = ReaperSprintMode.Headman;
        return true;
    }
    internal bool ShouldReturn(bool validTarget, double squaredDistance) => Mode == ReaperSprintMode.Headman &&
        (!validTarget || squaredDistance <= 9);
    internal void Restore() => Mode = ReaperSprintMode.Restoring;
    internal void Restored() => Mode = ReaperSprintMode.Reaper;
    internal void ReturnFailed() => Mode = ReaperSprintMode.Headman;
    internal void Cancel() => Mode = ReaperSprintMode.Reaper;
    internal void Die() => Mode = ReaperSprintMode.Dead;
    internal void NewLife() { Mode = ReaperSprintMode.Reaper; _readyAt = 0; }
}
