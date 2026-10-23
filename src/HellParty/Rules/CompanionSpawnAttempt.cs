namespace HellParty.Rules;

internal sealed class CompanionSpawnAttempt
{
    internal bool Created { get; private set; }
    private bool _pending;
    internal bool TryBegin()
    {
        if (Created || _pending) return false;
        _pending = true;
        return true;
    }
    internal void Complete(bool success)
    {
        Created |= success;
        _pending = false;
    }
}
