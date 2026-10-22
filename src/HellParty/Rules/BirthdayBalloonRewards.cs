using System.Collections.Generic;

namespace HellParty.Rules;

internal enum BalloonReward { None, Gnome, Eyeball }
internal enum BalloonPopCause { Other, Player, Physical, Automatic, Damage }

// Native dictionary keys identify balloons; each registration starts a new
// generation even if the same placement position is reused later.
internal sealed class BirthdayBalloonRewards<TKey> where TKey : notnull
{
    private readonly List<TKey> _ordered = new();
    private readonly HashSet<TKey> _deathRewards = new();
    private bool _dying;

    internal void Register(TKey key)
    {
        _ordered.Remove(key);
        _deathRewards.Remove(key);
        _ordered.Add(key);
    }

    internal void BeginDeath()
    {
        if (_dying) return;
        _dying = true;
        for (int i = System.Math.Max(0, _ordered.Count - 3); i < _ordered.Count; i++)
            _deathRewards.Add(_ordered[i]);
    }

    internal BalloonReward Remove(TKey key, BalloonPopCause cause, bool playerNearby, int tier)
    {
        if (!_ordered.Remove(key)) return BalloonReward.None;
        bool deathReward = _deathRewards.Remove(key);
        if (tier == 0) return BalloonReward.None;
        if (_dying) return tier >= 2 && deathReward ? BalloonReward.Eyeball : BalloonReward.None;
        if (cause == BalloonPopCause.Damage) return tier >= 3 ? BalloonReward.Gnome : BalloonReward.None;
        return cause != BalloonPopCause.Other && playerNearby ? BalloonReward.Gnome : BalloonReward.None;
    }

    internal void NewLife()
    {
        _dying = false;
        _deathRewards.Clear();
    }
}
