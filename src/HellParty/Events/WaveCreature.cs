using System.Reflection;
using HarmonyLib;
using HellParty.Native;
using UnityEngine;

namespace HellParty.Events;

internal sealed class WaveCreature : MonoBehaviour
{
    internal MonsterWave Owner = null!;
    internal PlayerAvatar? Target;
    internal bool Retired;
    private bool _targetSet;
    private EnemyParent Parent => GetComponent<EnemyParent>();

    internal void Retire()
    {
        Retired = true;
        Enemy enemy = GetComponentInChildren<Enemy>(true);
        if (NativeEnemy.Spawned(Parent) && enemy != null && !NativeEnemy.Dead(enemy.Health))
            enemy.CurrentState = EnemyState.Despawn;
    }

    private void Update()
    {
        if (Retired || _targetSet || !NativeEnemy.Spawned(Parent)) return;
        if (Target == null || !WaveSpawnPoints.Alive(Target)) Target = WaveSpawnPoints.Nearest(NativeEnemy.Position(Parent));
        if (Target == null) return;
        // Let the full native spawn animation finish before requesting notice.
        if (GetComponentInChildren<EnemyGnome>() is EnemyGnome gnome)
        {
            if (gnome.currentState == EnemyGnome.State.Spawn) return;
        }
        else if (GetComponentInChildren<EnemyValuableThrower>() is EnemyValuableThrower rugrat)
        {
            if (rugrat.currentState == EnemyValuableThrower.State.Spawn) return;
            rugrat.GetComponent<Enemy>().Vision.onVisionTriggeredPlayer = Target;
            rugrat.OnVisionTriggered();
        }
        else if (GetComponentInChildren<EnemyUpscream>() is EnemyUpscream upscream)
        {
            if (upscream.currentState == EnemyUpscream.State.Spawn) return;
            Enemy enemy = upscream.GetComponent<Enemy>();
            enemy.Vision.onVisionTriggeredPlayer = Target;
            enemy.Vision.onVisionTriggeredID = Target.photonView.ViewID;
            upscream.OnVision();
        }
        else if (GetComponentInChildren<EnemyHeadGrabber>() is EnemyHeadGrabber headgrab)
        {
            if (headgrab.currentState == EnemyHeadGrabber.State.Spawn) return;
            headgrab.GetComponent<Enemy>().Vision.onVisionTriggeredPlayer = Target;
            headgrab.OnVision();
        }
        _targetSet = true;
    }
}

[HarmonyPatch(typeof(EnemyParent), "Spawn")]
internal static class WaveCreatureSpawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyParent __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<WaveCreature>() is not WaveCreature member) return true;
        SummonedEnemySpawnTicket ticket = __instance.GetComponent<SummonedEnemySpawnTicket>();
        if (member.Retired || member.Owner == null || !member.Owner.CanSpawn || ticket == null || !ticket.Pending ||
            !WaveSpawnPoints.Find(member.Owner.Center, out Vector3 point, __instance.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true)))
        { __instance.DespawnedTimerSet(.25f); return false; }
        ticket.Position = point;
        ticket.InitialTarget = null; // Avoid the shared native Gnome group's target setter.
        member.Target = WaveSpawnPoints.Nearest(point);
        return true;
    }
}

// Native Gnome AI reads a singleton director. Supply a per-unit target only
// during this unit's synchronous native Update, then restore every shared field
// it can mutate. Vanilla Gnomes and other summoners keep their own group target.
[HarmonyPatch(typeof(EnemyGnome), "Update")]
internal static class WaveGnomeTargetScope
{
    private static readonly FieldInfo[] Fields = {
        AccessTools.Field(typeof(EnemyGnomeDirector), "playerTarget"),
        AccessTools.Field(typeof(EnemyGnomeDirector), "valuableTarget"),
        AccessTools.Field(typeof(EnemyGnomeDirector), "attackPosition"),
        AccessTools.Field(typeof(EnemyGnomeDirector), "attackVisionPosition"),
        AccessTools.Field(typeof(EnemyGnomeDirector), "currentState"),
        AccessTools.Field(typeof(EnemyGnomeDirector), "stateTimer"),
        AccessTools.Field(typeof(EnemyGnomeDirector), "stateImpulse")
    };
    internal sealed class Snapshot
    {
        internal EnemyGnomeDirector Director = null!;
        internal object[] Values = null!;
    }

    [HarmonyPrefix]
    private static void Prefix(EnemyGnome __instance, out Snapshot? __state)
    {
        __state = null;
        if (!SemiFunc.IsMasterClientOrSingleplayer() || EnemyGnomeDirector.instance == null ||
            __instance.GetComponentInParent<WaveCreature>() is not WaveCreature member || member.Retired) return;
        if (member.Target == null || !WaveSpawnPoints.Alive(member.Target)) member.Target = WaveSpawnPoints.Nearest(__instance.transform.position);
        if (member.Target == null) return;
        EnemyGnomeDirector director = EnemyGnomeDirector.instance;
        var snapshot = new Snapshot { Director = director, Values = new object[Fields.Length] };
        for (int i = 0; i < Fields.Length; i++) snapshot.Values[i] = Fields[i].GetValue(director);
        __state = snapshot;
        Fields[0].SetValue(director, member.Target);
        Fields[1].SetValue(director, null);
        Fields[2].SetValue(director, member.Target.transform.position);
        Fields[3].SetValue(director, member.Target.PlayerVisionTarget.VisionTransform.position);
        director.currentState = EnemyGnomeDirector.State.AttackPlayer;
    }

    [HarmonyFinalizer]
    private static void Finalizer(Snapshot? __state)
    {
        if (__state == null || __state.Director == null) return;
        for (int i = 0; i < Fields.Length; i++) Fields[i].SetValue(__state.Director, __state.Values[i]);
    }
}

[HarmonyPatch(typeof(EnemyGnome), "OnVision")]
internal static class WaveGnomeVisionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyGnome __instance) => __instance.GetComponentInParent<WaveCreature>() == null;
}

[HarmonyPatch(typeof(EnemyGnome), "OnInvestigate")]
internal static class WaveGnomeInvestigationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyGnome __instance) => __instance.GetComponentInParent<WaveCreature>() == null;
}

[HarmonyPatch(typeof(EnemyGnomeDirector), nameof(EnemyGnomeDirector.OnSpawn))]
internal static class WaveGnomeGroupSpawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyGnome _gnomeSpawn) => _gnomeSpawn.GetComponentInParent<WaveCreature>() == null;
}
