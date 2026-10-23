using System;
using System.Collections;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using UnityEngine;

namespace HellParty;

internal sealed class HuntsmanDeathTactics : MonoBehaviour
{
    internal const string GrenadePath = "Items/Item Grenade Human";
    private readonly HuntsmanDeathTrigger _trigger = new();
    internal void NewLife() => _trigger.NewLife();

    internal void Died(EnemyHunter hunter)
    {
        StageRuntime? stage = StageRuntime.Current;
        if (stage == null || !_trigger.TryUse(DifficultyService.Current)) return;
        // Snapshot the physical death point before OnDeath disables the body.
        Vector3 position = NativeEnemy.Position(hunter) + Vector3.up * .5f;
        Quaternion rotation = hunter.enemy.Rigidbody.transform.rotation;
        stage.StartCoroutine(SpawnAndArm(stage, position, rotation));
    }

    private static IEnumerator SpawnAndArm(StageRuntime stage, Vector3 position, Quaternion rotation)
    {
        GameObject? grenade = null;
        try
        {
            GameObject prefab = Resources.Load<GameObject>(GrenadePath);
            if (prefab == null || prefab.GetComponent<ItemGrenadeHuman>() == null || prefab.GetComponent<ItemEquippable>() == null)
                throw new InvalidOperationException("Native collectable Human Grenade prefab not found.");
            grenade = VanillaCatalog.Spawn(GrenadePath, position, rotation);
        }
        catch (Exception e) { Plugin.ModLogger.LogError($"Huntsman death grenade spawn failed: {e}"); }
        if (grenade == null) yield break;
        // Native item Start initializes toggle, pin, inventory registration and
        // physics. A new item must not be armed before those components exist.
        yield return null;
        yield return null;
        PhysGrabObject physical = grenade.GetComponent<PhysGrabObject>();
        ItemAttributes attributes = grenade.GetComponent<ItemAttributes>();
        Rigidbody body = grenade.GetComponent<Rigidbody>();
        float deadline = Time.time + 5;
        while (grenade != null && (!physical.spawned || body.isKinematic || string.IsNullOrEmpty(attributes.instanceName)))
        {
            // A player who already secured the item keeps an ordinary grenade.
            if (grenade.GetComponent<ItemEquippable>().isEquipped) yield break;
            if (StageRuntime.Current != stage || !SemiFunc.RunIsLevel()) yield break;
            if (Time.time >= deadline)
            {
                Plugin.ModLogger.LogError("Huntsman death grenade initialization timed out.");
                RemoveFailed(grenade);
                yield break;
            }
            yield return null;
        }
        if (grenade == null || StageRuntime.Current != stage || !SemiFunc.IsMasterClientOrSingleplayer() ||
            !SemiFunc.RunIsLevel() || !NetworkLifetime.Live(physical)) yield break;
        if (grenade.GetComponent<ItemEquippable>().isEquipped) yield break;
        try
        {
            ItemGrenadeHuman human = grenade.GetComponent<ItemGrenadeHuman>();
            human.Initialize();
            // Native Spawn pulls the pin, randomizes its 1.5–3s fuse and
            // applies the native random throw force/torque. Grab remains enabled.
            human.Spawn();
            // This is registered loot, not a disposable secondary grenade.
            // Keep native inventory persistence and remove its Stats entry on
            // eventual detonation, including after a player safely equips it.
            grenade.GetComponent<ItemGrenade>().isSpawnedGrenade = false;
        }
        catch (Exception e)
        {
            Plugin.ModLogger.LogError($"Huntsman death grenade activation failed: {e}");
            RemoveFailed(grenade);
        }
    }

    private static void RemoveFailed(GameObject grenade)
    {
        ItemAttributes attributes = grenade.GetComponent<ItemAttributes>();
        if (attributes != null && !string.IsNullOrEmpty(attributes.instanceName) && StatsManager.instance != null)
            StatsManager.instance.ItemRemove(attributes.instanceName);
        NetworkLifetime.Remove(grenade);
    }
}

[HarmonyPatch(typeof(EnemyHunter), nameof(EnemyHunter.OnDeath))]
internal static class HuntsmanDeathTacticsPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHunter __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() || DifficultyService.Current < 4) return;
        EnemyParent parent = NativeEnemy.Parent(__instance);
        StageRuntime.TryAttach(parent);
        parent.GetComponent<HuntsmanDeathTactics>()?.Died(__instance);
    }
}

[HarmonyPatch(typeof(EnemyHunter), nameof(EnemyHunter.OnSpawn))]
internal static class HuntsmanDeathTacticsNewLifePatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyHunter __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
            NativeEnemy.Parent(__instance).GetComponent<HuntsmanDeathTactics>()?.NewLife();
    }
}
