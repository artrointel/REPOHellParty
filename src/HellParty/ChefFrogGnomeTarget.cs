using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace HellParty;

[HarmonyPatch(typeof(EnemyGnomeDirector), "StateAttackSet")]
internal static class ChefFrogGnomeTarget
{
    // Filter at candidate lookup so native selection can still choose the
    // highest-valued ordinary collectible instead of just cancelling an attack.
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (instruction.operand is MethodInfo method && method.Name == "GetComponentInParent" &&
                method.IsGenericMethod && method.GetGenericArguments()[0] == typeof(ValuableObject))
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChefFrogGnomeTarget), nameof(Eligible)));
        }
    }

    private static ValuableObject? Eligible(ValuableObject? candidate) =>
        SemiFunc.IsMasterClientOrSingleplayer() && ChefMimicFrog.BlocksTarget(candidate) ? null : candidate;
}
