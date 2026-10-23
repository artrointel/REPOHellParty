using HellParty.Rules;

namespace HellParty;

internal static class DifficultyService
{
    internal static int Stage => DifficultyRules.StageNumber(RunManager.instance != null ? RunManager.instance.levelsCompleted : 0);
    internal static int Current
    {
        get
        {
#if DEBUG
            if (Debugging.DebugClownScenario.Enabled) return Debugging.DebugClownScenario.ForcedDifficulty;
            if (Debugging.DebugReaperScenario.Enabled) return Debugging.DebugReaperScenario.ForcedDifficulty;
            if (Debugging.DebugUpscreamScenario.Enabled) return Debugging.DebugUpscreamScenario.ForcedDifficulty;
            if (Debugging.DebugCleanupScenario.Enabled) return Debugging.DebugCleanupScenario.Difficulty;
            if (Debugging.DebugHuntsmanScenario.Enabled) return Debugging.DebugHuntsmanScenario.ForcedDifficulty;
#endif
            return DifficultyRules.FromCompletedStages(RunManager.instance != null ? RunManager.instance.levelsCompleted : 0);
        }
    }
}
