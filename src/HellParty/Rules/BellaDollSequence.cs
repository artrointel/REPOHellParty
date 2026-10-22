using System;

namespace HellParty.Rules;

// Warning stages may contain several bell pulses. Native clown animations
// must finish before another nose touch, and warnings never rewind mid-life.
internal sealed class BellaDollSequence
{
    private int _requested;
    private int _applied;
    private bool _stopped;

    internal void ObserveWarning(int stage) => _requested = Math.Max(_requested, Math.Min(3, Math.Max(0, stage)));
    internal bool TryAdvance(bool nativeReady)
    {
        if (_stopped || !nativeReady || _applied >= _requested) return false;
        _applied++;
        return true;
    }
    internal void Stop() => _stopped = true;
}
