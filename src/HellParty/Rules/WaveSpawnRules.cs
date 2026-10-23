namespace HellParty.Rules;

internal static class WaveSpawnRules
{
    internal const float RetryInterval = 1;
    internal const int CandidateLimit = 240;
    internal const int CachedPointLimit = 16;
    internal static bool PlayerDistanceAllowed(float squaredDistance) => squaredDistance > 9;
}
