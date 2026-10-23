using System;

namespace HellParty.Rules;

// Independent of Unity and the game's own difficulty/moon phases.
internal static class DifficultyRules
{
    internal static int FromCompletedStages(int completed) => Math.Min(5, Math.Max(0, completed) / 3);
    internal static int StageNumber(int completed) => completed < 0 ? 1 : completed == int.MaxValue ? int.MaxValue : completed + 1;
    internal static int CloneLimit(int difficulty) => difficulty <= 0 ? 0 : difficulty < 3 ? 1 : difficulty < 5 ? 2 : 3;
    internal static bool ShouldRecall(float squaredDistance) => squaredDistance >= 225f;
    internal static int AnimalGasCooldown(int difficulty) => difficulty < 2 ? 0 : 10;
    internal static bool AnimalGasInRange(float squaredDistance) => squaredDistance <= 9f;
    internal static bool AnimalDivisionUnlocked(int difficulty) => difficulty >= 3;
    internal static int AnimalOffspringHealth(int baseHealth) => Math.Max(1, baseHealth / 2);
    internal static int BirthdayBalloonTier(int difficulty) => Math.Min(3, Math.Max(0, difficulty));
    internal static bool BirthdayBalloonInRange(float squaredDistance) => squaredDistance <= 100f;
    internal static bool BirthdayDamageTriggers(int before, int after, int tier) => tier >= 3 && after > 0 && after < before;
    internal static bool BellaSuicideUnlocked(int difficulty) => difficulty >= 1;
    internal static bool BellaBaitUnlocked(int difficulty) => difficulty >= 3;
    internal static bool BellaBaitOutOfRange(float squaredDistance) => squaredDistance >= 225f;
    internal static bool BowtieBrothersUnlocked(int difficulty) => difficulty >= 2;
    internal static bool BowtieRevengeUnlocked(int difficulty) => difficulty >= 4;
    internal static bool ChefMimicUnlocked(int difficulty) => difficulty >= 1;
    internal static bool StalkingDollCompanionUnlocked(int difficulty) => difficulty >= 1;
    internal static bool StalkingDollRampageUnlocked(int difficulty) => difficulty >= 3;
    internal static bool PeeperGnomeUnlocked(int difficulty) => difficulty >= 1;
    internal static bool PeeperBangerUnlocked(int difficulty) => difficulty >= 2;
    internal static bool MentalistReflectionUnlocked(int difficulty) => difficulty >= 2;
}
