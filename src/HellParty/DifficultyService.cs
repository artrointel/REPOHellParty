using HellParty.Rules;

namespace HellParty;

internal static class DifficultyService
{
    internal static int Stage => DifficultyRules.StageNumber(RunManager.instance != null ? RunManager.instance.levelsCompleted : 0);
    internal static int Current
    {
        get
        {
#if DEBUG && HELLPARTY_TEST_CLEANUP_CREW
            if (Debugging.CleanupCrewScenario.OverrideDifficulty) return 5;
#endif
            return DifficultyRules.FromCompletedStages(RunManager.instance != null ? RunManager.instance.levelsCompleted : 0);
        }
    }
}
