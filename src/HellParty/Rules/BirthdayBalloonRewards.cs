using System.Collections.Generic;

namespace HellParty.Rules;

internal enum BalloonReward { None, Gnome }
internal enum BalloonPopCause { Other, Player, Physical, Automatic, Damage }

// Native dictionary keys identify balloons; each registration starts a new
// generation even if the same placement position is reused later.
internal sealed class BirthdayBalloonRewards<TKey> where TKey : notnull
{
    private readonly HashSet<TKey> _registered = new();
    private bool _dying;

    internal void Register(TKey key)
    {
        _registered.Add(key);
    }

    internal void BeginDeath()
    {
        _dying = true;
    }

    internal BalloonReward Remove(TKey key, BalloonPopCause cause, bool playerNearby, int tier)
    {
        if (!_registered.Remove(key)) return BalloonReward.None;
        if (tier == 0) return BalloonReward.None;
        if (_dying) return BalloonReward.None;
        if (cause == BalloonPopCause.Damage) return tier >= 3 ? BalloonReward.Gnome : BalloonReward.None;
        return cause != BalloonPopCause.Other && playerNearby ? BalloonReward.Gnome : BalloonReward.None;
    }

    internal void NewLife()
    {
        _dying = false;
    }
}
