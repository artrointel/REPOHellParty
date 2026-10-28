namespace HellParty.Rules;

internal static class MonsterSpawnBalanceRules
{
    internal static bool Eligible(bool restrictLevels, int completedStages, int minimum, int maximum, int runsPlayed, int requiredRuns) =>
        (!restrictLevels || (completedStages >= minimum && (maximum == 0 || completedStages <= maximum))) && runsPlayed >= requiredRuns;

    internal static int Count(int vanillaCount, int completedStages, int playerCount, bool large, int hellDifficulty, bool authoritative, bool normalLevel)
    {
        if (!authoritative || !normalLevel || hellDifficulty <= 0 || completedStages < 0 || playerCount < 1 ||
            vanillaCount < 0) return vanillaCount;
        bool increased = large ? completedStages >= 4 : completedStages < 4 || completedStages >= 9;
        int bonus = increased ? (playerCount >= 7 ? 2 : 1) : 0;
        return (int)System.Math.Min(int.MaxValue, (long)vanillaCount + bonus);
    }
}
