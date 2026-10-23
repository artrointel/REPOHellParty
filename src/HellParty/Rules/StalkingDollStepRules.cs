namespace HellParty.Rules;

internal static class StalkingDollStepRules
{
    internal const float MaximumHeight = .5f;
    internal const float Skin = .02f;
    internal const float MaximumAdvance = .5f;
    internal static bool CanAdvance(double distance) => distance > 0 && distance <= MaximumAdvance;
    internal static bool CanStep(double rise, bool grounded, bool clear, bool walkable) =>
        grounded && clear && walkable && rise >= .005 && rise <= MaximumHeight;
}
