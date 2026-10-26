namespace HellParty.Rules;

internal sealed class CleanupStickyHeadClock<T> where T : class
{
    internal const double Cooldown = 3;
    internal T? Carrier { get; private set; }
    private double _readyAt;
    private bool _stopped;
    internal static bool Unlocked(int difficulty) => difficulty >= 1;
    internal static bool InRange(double squaredDistance) => squaredDistance >= 0 && squaredDistance <= 2.25;
    internal bool Ready(double now, bool grabbed) => !_stopped && !grabbed && now >= _readyAt;
    internal bool Attach(double now, T? player, bool grabbed)
    {
        if (player == null || ReferenceEquals(Carrier, player) || !Ready(now, grabbed)) return false;
        // First attachment does not consume cooldown. An atomic transfer leaves
        // the previous carrier, so its three seconds start at that detachment.
        if (Carrier != null) _readyAt = now + Cooldown;
        Carrier = player;
        return true;
    }
    internal bool Detach(double now)
    {
        if (Carrier == null) return false;
        Carrier = null;
        _readyAt = now + Cooldown;
        return true;
    }
    internal void Stop() { Carrier = null; _stopped = true; }
    internal void NewLife() { Carrier = null; _readyAt = 0; _stopped = false; }
}
