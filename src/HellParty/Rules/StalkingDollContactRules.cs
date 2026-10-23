using System;

namespace HellParty.Rules;

internal static class StalkingDollContactRules
{
    // The normal points out of the obstacle. Preserve motion away from or
    // parallel to it; only redirect motion that presses into the contact.
    internal static void Slide(double x, double z, double nx, double nz, out double sx, out double sz)
    {
        sx = x; sz = z;
        double normalLength = Math.Sqrt(nx * nx + nz * nz);
        double speed = Math.Sqrt(x * x + z * z);
        if (normalLength < .001 || speed < .001) return;
        nx /= normalLength; nz /= normalLength;
        if (x * nx + z * nz >= -.001 * speed) return;
        double tx = nz, tz = -nx;
        // A head-on contact needs a consistent side instead of projecting to
        // zero. Opposing dolls choose opposite world-space sides to pass.
        double sign = x * tx + z * tz < -.1 * speed ? -1 : 1;
        sx = tx * speed * sign;
        sz = tz * speed * sign;
    }
}
