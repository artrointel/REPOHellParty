namespace HellParty.Rules;

internal sealed class ElsaAssassinationClock
{
    internal const double Cooldown = 10;
    internal bool Active { get; private set; }
    private double _readyAt;

    internal static bool Eligible(int difficulty, bool summoned) => difficulty >= 5 && !summoned;
    internal static bool CompanionNearby(bool alive, bool isTarget, double squaredDistance) =>
        alive && !isTarget && squaredDistance >= 0 && squaredDistance <= 400;

    internal bool Begin(double now, bool following, bool companionNearby, double squaredDistance)
    {
        if (Active || !following || companionNearby || !(now >= _readyAt) ||
            !(squaredDistance >= 0 && squaredDistance <= 25)) return false;
        Active = true;
        return true;
    }

    // Cancel immediately; only an actual exit starts the reentry cooldown.
    internal bool End(double now)
    {
        if (!Active) return false;
        Active = false;
        _readyAt = now + Cooldown;
        return true;
    }

    internal void NewLife() { Active = false; _readyAt = 0; }
}
