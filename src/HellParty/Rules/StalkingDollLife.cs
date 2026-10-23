using System;

namespace HellParty.Rules;

internal sealed class StalkingDollLife
{
    internal const int MaximumHealth = 100;
    internal int Health { get; private set; } = MaximumHealth;
    internal bool Dead => Health == 0;
    internal double RemainingValue(double fullValue) => double.IsNaN(fullValue) || double.IsInfinity(fullValue) || fullValue <= 0
        ? 0 : Math.Floor(fullValue * Health / MaximumHealth);

    internal bool Hurt(int damage)
    {
        if (Dead || damage <= 0) return false;
        Health = Math.Max(0, Health - damage);
        return true;
    }

    internal bool Kill()
    {
        if (Dead) return false;
        Health = 0;
        return true;
    }
}
