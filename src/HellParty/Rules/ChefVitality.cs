using System;

namespace HellParty.Rules;

// Native valuables break below 15% value. Normalize that remaining usable
// interval, rather than treating the break threshold as 15% monster health.
internal sealed class ChefVitality
{
    private const double BreakFraction = 0.15;
    private int _lastBodyHealth;
    private int _maximum;
    internal double Fraction { get; private set; } = 1;

    internal void NewLife(int health, int maximum)
    {
        _maximum = 0;
        CaptureBody(health, maximum);
    }

    internal void CaptureBody(int health, int maximum)
    {
        if (maximum <= 0) return;
        Fraction = Clamp(_maximum != maximum ? (double)health / maximum :
            Fraction + (double)(health - _lastBodyHealth) / maximum);
        _maximum = maximum;
        _lastBodyHealth = health;
    }

    internal double FrogValue(double original) => original * (BreakFraction + (1 - BreakFraction) * Fraction);

    internal void ApplyFrogChange(double change, double original)
    {
        if (original > 0) Fraction = Clamp(Fraction + change / (original * (1 - BreakFraction)));
    }

    internal int BodyHealth(int maximum)
    {
        _maximum = maximum;
        // Preserve fractional damage internally across repeated transformations.
        _lastBodyHealth = Fraction <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(Fraction * maximum - 0.000001));
        return _lastBodyHealth;
    }

    private static double Clamp(double fraction) => Math.Max(0, Math.Min(1, fraction));
}
