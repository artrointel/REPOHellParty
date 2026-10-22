using HellParty.Rules;

namespace HellParty;

internal static class DifficultyService
{
    internal static int Stage => DifficultyRules.StageNumber(RunManager.instance != null ? RunManager.instance.levelsCompleted : 0);
    internal static int Current
    {
        get
        {
            return DifficultyRules.FromCompletedStages(RunManager.instance != null ? RunManager.instance.levelsCompleted : 0);
        }
    }
}
