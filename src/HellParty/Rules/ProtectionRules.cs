namespace HellParty.Rules;

internal static class ProtectionRules
{
    internal static bool Eligible(int difficulty, bool hasTarget, double squaredDistance) =>
        difficulty >= 4 && !hasTarget && squaredDistance >= 0 && squaredDistance <= 400;
}
