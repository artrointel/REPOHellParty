namespace HellParty.Rules;

internal sealed class PeeperSummonRules
{
    internal const int Limit = 10;
    internal int Total { get; private set; }
    internal bool Available => Total < Limit;
    internal static bool EffectiveHit(int before, int after) => before > 0 && after >= 0 && after < before;
    internal static bool BangerRoll(double value) => value >= 0 && value < 0.3;

    internal bool Spawned()
    {
        if (!Available) return false;
        Total++;
        return true;
    }
}
