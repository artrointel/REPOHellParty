namespace HellParty.Rules;

internal sealed class AbductionClock
{
    internal const int Damage = 2;
    internal const double InitialDelay = 3;
    internal const double DamageInterval = 1;
    internal bool Holding { get; private set; }
    private double _readyAt;
    private double _damageAt;
    internal bool Ready(double now) => !Holding && now >= _readyAt;
    internal bool Start(double now)
    {
        if (!Ready(now)) return false;
        Holding = true;
        _damageAt = now + InitialDelay;
        return true;
    }
    internal bool DamageDue(double now)
    {
        if (!Holding || now < _damageAt) return false;
        _damageAt = now + DamageInterval;
        return true;
    }
    internal void Release(double now)
    {
        if (!Holding) return;
        Holding = false;
        _readyAt = now + 5;
    }
    internal static bool Eligible(int difficulty, int alivePlayers, bool combat, double squaredDistance, int unlockDifficulty = 2) =>
        difficulty >= unlockDifficulty && alivePlayers >= 2 && combat && squaredDistance >= 0 && squaredDistance <= 16;
}

internal sealed class AbductionSlot<T> where T : class
{
    internal T? Owner { get; private set; }
    internal bool Acquire(T owner)
    {
        if (Owner != null) return false;
        Owner = owner;
        return true;
    }
    internal void Release(T owner) { if (ReferenceEquals(Owner, owner)) Owner = null; }
}
