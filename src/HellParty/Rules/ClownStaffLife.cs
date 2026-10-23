namespace HellParty.Rules;

internal sealed class ClownStaffLife
{
    internal bool Rewarded { get; private set; }
    internal bool Removed { get; private set; }
    internal bool Pinned => !Rewarded && !Removed;
    internal bool Reward()
    {
        if (!Pinned) return false;
        Rewarded = true;
        return true;
    }
    internal bool Remove()
    {
        if (!Pinned) return false;
        Removed = true;
        return true;
    }
}
