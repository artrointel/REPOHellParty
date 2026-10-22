namespace HellParty.Rules;

internal enum ChefMimicMode { Roaming, Disguised, Revealing, Attacking, Defeating, ReturningToRoam }

internal sealed class ChefMimicState
{
    private double _nextScan;
    private double? _unseenSince;
    internal ChefMimicMode Mode { get; private set; }
    internal static bool InRange(float squaredDistance) => squaredDistance <= 400f;
    internal bool ShouldReleaseTarget(bool targetValid, bool nativePursuitEnded) =>
        Mode == ChefMimicMode.Attacking && (!targetValid || nativePursuitEnded);
    internal bool TryScan(double now, bool nativeRoaming)
    {
        if (Mode != ChefMimicMode.Roaming || !nativeRoaming || now < _nextScan) return false;
        _nextScan = now + 1;
        return true;
    }
    internal bool Disguise()
    {
        if (Mode != ChefMimicMode.Roaming) return false;
        _unseenSince = null;
        Mode = ChefMimicMode.Disguised;
        return true;
    }
    internal bool TryDisguiseScan(double now)
    {
        if (Mode != ChefMimicMode.Disguised || now < _nextScan) return false;
        _nextScan = now + 1;
        return true;
    }
    internal bool ReturnIfUnseen(double now, bool hasVisiblePlayer)
    {
        if (Mode != ChefMimicMode.Disguised) return false;
        if (hasVisiblePlayer)
        {
            _unseenSince = null;
            return false;
        }
        _unseenSince ??= now;
        if (now - _unseenSince.Value < 5) return false;
        Mode = ChefMimicMode.ReturningToRoam;
        return true;
    }
    internal bool Reveal()
    {
        if (Mode != ChefMimicMode.Disguised) return false;
        Mode = ChefMimicMode.Revealing;
        return true;
    }
    internal bool Attack()
    {
        if (Mode != ChefMimicMode.Revealing) return false;
        Mode = ChefMimicMode.Attacking;
        return true;
    }
    internal bool Defeat()
    {
        if (Mode != ChefMimicMode.Disguised && Mode != ChefMimicMode.Revealing) return false;
        Mode = ChefMimicMode.Defeating;
        return true;
    }
    internal void Roam(double now)
    {
        Mode = ChefMimicMode.Roaming;
        _unseenSince = null;
        _nextScan = now + 1;
    }
}
