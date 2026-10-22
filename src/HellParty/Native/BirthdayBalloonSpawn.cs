using System;
using HellParty.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty.Native;

internal static class BirthdayBalloonSpawn
{
    internal const string GnomePath = "Enemies/Enemy - Gnome";
    internal const string GnomeDirectorPath = "Enemies/Enemy - Gnome Director";
    internal const string EyeballPath = "Valuables/01 Tiny/Valuable Wizard Eye Ball";
    internal const string BirthdayBoyPath = "Enemies/Enemy - Birthday boy";

    internal static void Emit(BalloonReward reward, Vector3 position, PlayerAvatar? target, bool shareAttackTarget = false)
    {
        if (reward == BalloonReward.Eyeball)
        {
            VanillaCatalog.Spawn(EyeballPath, position, Quaternion.identity);
            return;
        }
        if (reward != BalloonReward.Gnome) return;
        // Balloons float above ground; place the native navigation agent on
        // nearby walkable ground rather than warping it into empty space.
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 3, NavMesh.AllAreas))
            throw new InvalidOperationException($"No walkable Gnome spawn near balloon {position}.");
        if (EnemyGnomeDirector.instance == null)
            VanillaCatalog.Spawn(GnomeDirectorPath, Vector3.zero, Quaternion.identity);
        SummonedEnemySpawnTicket.Create(GnomePath, hit.position, target, shareAttackTarget);
    }
}
