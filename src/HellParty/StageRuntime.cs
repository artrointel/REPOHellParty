using System;
using System.Collections;
using HarmonyLib;
using HellParty.Native;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

internal sealed class StageRuntime : MonoBehaviour
{
    internal static StageRuntime? Current;

    private IEnumerator Start()
    {
        Current = this;
        yield return null;
        yield return null;
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel()) yield break;
        Plugin.ModLogger.LogInfo($"Stage {DifficultyService.Stage}: HellParty difficulty {DifficultyService.Current}.");
        if (GetComponent<DifficultyNotice>() == null) gameObject.AddComponent<DifficultyNotice>();
        if (DifficultyService.Current >= 1 && GetComponent<Events.MonsterWave>() == null)
            gameObject.AddComponent<Events.MonsterWave>();
        foreach (EnemyParent parent in FindObjectsOfType<EnemyParent>()) TryAttach(parent);
#if DEBUG && HELLPARTY_TEST_CLEANUP_CREW
        if (GetComponent<Debugging.CleanupCrewScenario>() == null) gameObject.AddComponent<Debugging.CleanupCrewScenario>();
#endif
    }

    internal static void TryAttach(EnemyParent parent)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel()) return;
        if (parent.GetComponent<VanillaAbilityExcluded>() != null) return;
        if (Rules.ElsaAssassinationClock.Eligible(DifficultyService.Current, parent.GetComponent<SummonedEnemySpawnTicket>() != null) &&
            parent.GetComponent<BowtieBrother>() == null && parent.GetComponentInChildren<EnemyElsa>(true) != null &&
            parent.GetComponent<ElsaAssassination>() == null) parent.gameObject.AddComponent<ElsaAssassination>();
        if (Rules.TrudgeCallCycle.Unlocked(DifficultyService.Current) && parent.GetComponentInChildren<EnemySlowWalker>(true) != null &&
            parent.GetComponent<TrudgeCall>() == null) parent.gameObject.AddComponent<TrudgeCall>();
        if (DifficultyService.Current >= 4 && parent.GetComponentInChildren<EnemyHeadController>(true) != null &&
            parent.GetComponent<ReaperHeadman>() == null && parent.GetComponent<HeadmanProtection>() == null)
            parent.gameObject.AddComponent<HeadmanProtection>();
        if (Rules.BlindTargetClock.Unlocked(DifficultyService.Current) &&
            parent.GetComponentInChildren<EnemyThinMan>(true) != null &&
            parent.GetComponent<BlindAbility>() == null) parent.gameObject.AddComponent<BlindAbility>();
        if (Rules.ReaperSprintClock.Unlocked(DifficultyService.Current) && parent.GetComponentInChildren<EnemyRunner>(true) != null &&
            parent.GetComponent<ReaperSprint>() == null) parent.gameObject.AddComponent<ReaperSprint>();
        if (DifficultyService.Current >= 1 && parent.GetComponentInChildren<EnemyHunter>(true) != null &&
            parent.GetComponent<HuntsmanAbduction>() == null) parent.gameObject.AddComponent<HuntsmanAbduction>();
        if (DifficultyService.Current >= 4 && parent.GetComponentInChildren<EnemyShadow>(true) != null &&
            parent.GetComponent<HuntsmanAbduction>() == null) parent.gameObject.AddComponent<HuntsmanAbduction>();
        if (Rules.DifficultyRules.ClownStaffUnlocked(DifficultyService.Current) && parent.GetComponentInChildren<EnemyBeamer>(true) != null &&
            parent.GetComponent<ClownAbilities>() == null) parent.gameObject.AddComponent<ClownAbilities>();
        if (DifficultyService.Current >= 4 && parent.GetComponentInChildren<EnemyHunter>(true) != null &&
            parent.GetComponent<HuntsmanDeathTactics>() == null) parent.gameObject.AddComponent<HuntsmanDeathTactics>();
        if (Rules.DifficultyRules.MentalistReflectionUnlocked(DifficultyService.Current) &&
            parent.GetComponentInChildren<EnemyFloater>(true) != null && parent.GetComponent<MentalistReflection>() == null)
            parent.gameObject.AddComponent<MentalistReflection>();
        if (Rules.DifficultyRules.PeeperGnomeUnlocked(DifficultyService.Current) &&
            parent.GetComponentInChildren<EnemyCeilingEye>(true) != null && parent.GetComponent<PeeperAbilities>() == null)
            parent.gameObject.AddComponent<PeeperAbilities>();
        if (Rules.DifficultyRules.StalkingDollCompanionUnlocked(DifficultyService.Current) &&
            (parent.GetComponentInChildren<EnemyShadow>(true) != null || parent.GetComponentInChildren<EnemyRobe>(true) != null) &&
            parent.GetComponent<StalkingDollCompanion>() == null)
            parent.gameObject.AddComponent<StalkingDollCompanion>();
        if (Rules.DifficultyRules.ChefMimicUnlocked(DifficultyService.Current) && parent.GetComponentInChildren<EnemyTumbler>(true) != null &&
            parent.GetComponent<ChefMimic>() == null)
            parent.gameObject.AddComponent<ChefMimic>();
        if (parent.GetComponentInChildren<EnemyUpscream>(true) != null && parent.GetComponent<ChefMimic>() == null)
        {
            SummonedEnemySpawnTicket ticket = parent.GetComponent<SummonedEnemySpawnTicket>();
            if (Rules.DifficultyRules.UpscreamMimicUnlocked(DifficultyService.Current, ticket != null))
                parent.gameObject.AddComponent<ChefMimic>();
        }
        if (Rules.DifficultyRules.BowtieBrothersUnlocked(DifficultyService.Current) && parent.GetComponentInChildren<EnemyBowtie>(true) != null &&
            parent.GetComponent<BowtieBrothers>() == null)
            parent.gameObject.AddComponent<BowtieBrothers>();
        if (Rules.DifficultyRules.BellaSuicideUnlocked(DifficultyService.Current) && parent.GetComponentInChildren<EnemyTricycle>(true) != null &&
            parent.GetComponent<BellaAbilities>() == null)
            parent.gameObject.AddComponent<BellaAbilities>();
        if (DifficultyService.Current > 0 && parent.GetComponentInChildren<EnemyBirthdayBoy>(true) != null &&
            parent.GetComponent<BirthdayBoyAbilities>() == null)
            parent.gameObject.AddComponent<BirthdayBoyAbilities>();
        if (DifficultyService.Current > 0 && parent.GetComponent<DuckClone>() == null &&
            NativeEnemy.Duck(parent) != null && parent.GetComponent<DuckFamily>() == null)
            parent.gameObject.AddComponent<DuckFamily>();
        if (Rules.DifficultyRules.AnimalGasCooldown(DifficultyService.Current) > 0 &&
            parent.GetComponentInChildren<EnemyAnimal>(true) != null && parent.GetComponent<AnimalPoison>() == null)
            parent.gameObject.AddComponent<AnimalPoison>();
        if (Rules.DifficultyRules.AnimalDivisionUnlocked(DifficultyService.Current) &&
            parent.GetComponentInChildren<EnemyAnimal>(true) != null && parent.GetComponent<AnimalOffspring>() == null &&
            parent.GetComponent<AnimalDivision>() == null)
            parent.gameObject.AddComponent<AnimalDivision>();
    }

    internal static void EmitDeathGas(EnemyDuck duck)
    {
        if (Current != null && duck != null)
            Current.StartCoroutine(Current.DeathGas(NativeEnemy.Position(duck)));
    }

    private IEnumerator DeathGas(Vector3 position)
    {
        GameObject? instance = null;
        try
        {
            PrefabRef source = VanillaCatalog.Valuable(HasNativeDeathCloud);
            instance = VanillaCatalog.Spawn(source, position, Quaternion.identity);
            instance.AddComponent<Events.InternalWaveExcluded>();
            // This prop is only a native all-client cloud carrier, never extra loot
            // or an addition to the round's possible dollar haul.
            if (SemiFunc.IsMultiplayer()) instance.GetComponent<PhotonView>().RPC("DollarValueSetRPC", RpcTarget.All, 0f);
            else instance.GetComponent<ValuableObject>().DollarValueSetRPC(0);
        }
        catch (Exception e) { Plugin.ModLogger.LogError($"Cannot create native clone death gas: {e}"); }
        if (instance == null) yield break;
        // Let native Awake/Start initialize the cloud, grab object and impact detector on all peers.
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.25f);
        if (instance != null && SemiFunc.IsMasterClientOrSingleplayer())
        {
            instance.GetComponent<PhysGrabObject>().Teleport(position, Quaternion.identity);
            instance.GetComponent<PhysGrabObjectImpactDetector>().DestroyObject(false);
        }
    }

    private static bool HasNativeDeathCloud(GameObject prefab)
    {
        UraniumScript uranium = prefab.GetComponent<UraniumScript>();
        PhysGrabObjectImpactDetector impact = prefab.GetComponent<PhysGrabObjectImpactDetector>();
        if (uranium == null || uranium.instant || impact == null) return false;
        for (int i = 0; i < impact.onDestroy.GetPersistentEventCount(); i++)
        {
            if (impact.onDestroy.GetPersistentMethodName(i) == nameof(UraniumScript.Cloud)) return true;
        }
        return false;
    }

    private void OnDestroy() { if (Current == this) Current = null; }
}

[HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
internal static class StageReadyPatch
{
    [HarmonyPostfix]
    private static void Postfix(LevelGenerator __instance)
    {
        if (__instance.Generated && SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.RunIsLevel() && __instance.GetComponent<StageRuntime>() == null)
            __instance.gameObject.AddComponent<StageRuntime>();
    }
}
