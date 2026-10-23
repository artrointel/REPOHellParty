namespace HellParty.Rules;

internal sealed class HuntsmanDeathTrigger
{
    private bool _used;
    internal void NewLife() => _used = false;
    internal bool TryUse(int difficulty)
    {
        if (_used || difficulty < 4) return false;
        _used = true;
        return true;
    }
}
