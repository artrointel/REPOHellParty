using System;

namespace HellParty.Rules;

// Independent of Unity and the game's own difficulty/moon phases.
internal static class DifficultyRules
{
    internal static int FromCompletedStages(int completed) => Math.Min(5, Math.Max(0, completed) / 3);
    internal static int StageNumber(int completed) => completed < 0 ? 1 : completed == int.MaxValue ? int.MaxValue : completed + 1;
    internal static int CloneLimit(int difficulty) => difficulty <= 0 ? 0 : difficulty < 3 ? 1 : difficulty < 5 ? 2 : 3;
    internal static bool ShouldRecall(float squaredDistance) => squaredDistance >= 225f;
}
