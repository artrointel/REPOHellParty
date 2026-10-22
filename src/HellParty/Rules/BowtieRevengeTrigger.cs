namespace HellParty.Rules;

internal sealed class BowtieRevengeTrigger
{
    internal const float Delay = 0.5f;
    private bool _queued;
    internal bool TryQueue(int difficulty)
    {
        if (_queued || !DifficultyRules.BowtieRevengeUnlocked(difficulty)) return false;
        _queued = true;
        return true;
    }
    internal void NewLife() => _queued = false;
}
