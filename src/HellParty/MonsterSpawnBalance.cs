using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HellParty.Rules;
using Photon.Pun;

namespace HellParty;

[HarmonyPatch(typeof(EnemyDirector), nameof(EnemyDirector.AmountSetup))]
internal static class MonsterSpawnBalance
{
    private static readonly AccessTools.FieldRef<EnemyDirector, int> MediumCount =
        AccessTools.FieldRefAccess<EnemyDirector, int>("amountCurve2Value");
    private static readonly AccessTools.FieldRef<EnemyDirector, int> LargeCount =
        AccessTools.FieldRefAccess<EnemyDirector, int>("amountCurve3Value");

    // Run after both native curve branches have settled, before any picks or
    // totalAmount calculation. Native selection, order and level setup remain intact.
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        FieldInfo current = AccessTools.Field(typeof(EnemyDirector), "enemyListCurrent");
        int anchor = -1;
        for (int i = 1; i < codes.Count; i++)
            if (codes[i - 1].opcode == OpCodes.Ldfld && Equals(codes[i - 1].operand, current) &&
                codes[i].operand is MethodInfo method && method.Name == "Clear" && method.DeclaringType == typeof(List<EnemySetup>))
            {
                if (anchor >= 0) throw new InvalidOperationException("Ambiguous native monster amount setup anchor.");
                anchor = i;
            }
        if (anchor < 0) throw new InvalidOperationException("Native monster amount setup anchor missing; check the game version.");
        codes.InsertRange(anchor + 1, new[] {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MonsterSpawnBalance), nameof(Apply)))
        });
        return codes;
    }

    private static void Apply(EnemyDirector director)
    {
        if (RunManager.instance == null || DataDirector.instance == null) return;
        int medium = MediumCount(director), large = LargeCount(director);
        int completed = RunManager.instance.levelsCompleted;
        int players = PartySize();
        bool authority = SemiFunc.IsMasterClientOrSingleplayer(), level = SemiFunc.RunIsLevel();
        int difficulty = DifficultyService.Current;
        int nextMedium = MonsterSpawnBalanceRules.Count(medium, completed, players, false, difficulty, authority, level);
        int nextLarge = MonsterSpawnBalanceRules.Count(large, completed, players, true, difficulty, authority, level);
        if (nextMedium == medium && nextLarge == large) return;
        // Native PickEnemies adds a null setup when all entries are locked.
        // Do not create that new failure case on fresh profiles/early stages.
        int runs = DataDirector.instance.SettingValueFetch(DataDirector.Setting.RunsPlayed);
        MediumCount(director) = EligibleCount(director.enemiesDifficulty2, medium, nextMedium, completed, runs, "medium");
        LargeCount(director) = EligibleCount(director.enemiesDifficulty3, large, nextLarge, completed, runs, "large");
        Plugin.ModLogger.LogInfo($"Stage {DifficultyRules.StageNumber(completed)} ({players} players): native monster picks medium {medium} -> {MediumCount(director)}, large {large} -> {LargeCount(director)}.");
    }

    private static int EligibleCount(List<EnemySetup> choices, int original, int adjusted, int completed, int runs, string band)
    {
        if (adjusted == original) return original;
        foreach (EnemySetup setup in choices)
            if (setup != null && MonsterSpawnBalanceRules.Eligible(setup.levelsCompletedCondition, completed,
                    setup.levelsCompletedMin, setup.levelsCompletedMax, runs, setup.runsPlayed))
                return adjusted;
        Plugin.ModLogger.LogWarning($"Monster count bonus skipped for {band}: no native setup is unlocked for this run.");
        return original;
    }

    private static int PartySize()
    {
        if (!SemiFunc.IsMultiplayer()) return 1;
        if (PhotonNetwork.CurrentRoom == null) return 0;
        int count = 0;
        foreach (var player in PhotonNetwork.CurrentRoom.Players.Values)
            if (!player.IsInactive) count++;
        return count;
    }
}
