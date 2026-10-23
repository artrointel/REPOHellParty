namespace HellParty.Rules;

internal sealed class TrudgeCallCycle
{
    internal const float Delay = 1;
    internal const int Count = 2;
    private bool _queued;
    internal static bool Unlocked(int difficulty) => difficulty >= 2;
    internal void Begin() => _queued = false;
    internal bool Strike(int difficulty)
    {
        if (!Unlocked(difficulty) || _queued) return false;
        _queued = true;
        return true;
    }
}
