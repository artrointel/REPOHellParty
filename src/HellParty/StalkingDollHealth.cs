using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

internal sealed class StalkingDollHealth : MonoBehaviour
{
    private readonly StalkingDollLife _life = new();
    private PhysGrabObject _physical = null!;
    private PhysGrabObjectImpactDetector _impact = null!;
    private ValuableObject _valuable = null!;
    private float _fullValue;
    private bool _valueReady;
    private float _pausedUntil;
    private bool _deathHandled;
    internal bool Paused => _life.Dead || Time.time < _pausedUntil;
    internal bool Dead => _life.Dead;
    internal bool TerrainCollision { get; set; }
    private static readonly MethodInfo BreakRpc = AccessTools.Method(typeof(PhysGrabObjectImpactDetector), "BreakRPC");
    private static readonly MethodInfo HealRpc = AccessTools.Method(typeof(PhysGrabObjectImpactDetector), "HealRPC");

    private void Awake()
    {
        _physical = GetComponent<PhysGrabObject>();
        _impact = GetComponent<PhysGrabObjectImpactDetector>();
        _valuable = GetComponent<ValuableObject>();
    }

    internal void ValueInitialized()
    {
        if (_valueReady) return;
        _fullValue = _valuable.dollarValueOriginal;
        _valueReady = true;
        SynchronizeValue();
    }

    private void Update()
    {
        if (SemiFunc.IsMasterClientOrSingleplayer()) SynchronizeValue();
    }

    private void SynchronizeValue()
    {
        if (!_valueReady) return;
        float target = (float)_life.RemainingValue(_fullValue);
        float delta = target - _valuable.dollarValueCurrent;
        if (Mathf.Abs(delta) < 0.001f) return;
        // Keep the original full-health price intact. Native HealRPC accepts
        // signed deltas and adjusts current price on both host and vanilla
        // guests without invoking the valuable's 15% destruction threshold.
        if (SemiFunc.IsMultiplayer()) GetComponent<PhotonView>().RPC("HealRPC", RpcTarget.All, delta, transform.position);
        else HealRpc.Invoke(_impact, new object[] { delta, transform.position, default(PhotonMessageInfo) });
    }

    internal void Attack(HurtCollider source)
    {
        if (_life.Dead || source.GetComponentInParent<StalkingDollHealth>() == this) return;
        if (source.enemyStun) _pausedUntil = Mathf.Max(_pausedUntil, Time.time + source.enemyStunTime);
        _pausedUntil = Mathf.Max(_pausedUntil, Time.time + source.enemyFreezeTime);
        int damage = source.deathPit || source.enemyLogic && source.enemyKill ? _life.Health :
            source.enemyLogic ? source.enemyDamage : source.physDestroy ? _life.Health : ImpactDamage(source.physImpact);
        Hurt(damage, source.transform.position, _impact.breakLevelMedium);
        source.onImpactAny.Invoke();
        source.onImpactPhysObject.Invoke();
    }

    private static int ImpactDamage(HurtCollider.BreakImpact impact) => impact switch
    {
        HurtCollider.BreakImpact.Light => 5,
        HurtCollider.BreakImpact.Medium => 10,
        HurtCollider.BreakImpact.Heavy or HurtCollider.BreakImpact.Special => 20,
        _ => 0
    };

    internal void BreakHit(int level, Vector3 point) => Hurt(level == _impact.breakLevelHeavy ? 20 :
        level == _impact.breakLevelMedium ? 10 : 5, point, level);

    private void Hurt(int damage, Vector3 point, int level)
    {
        if (!_life.Hurt(damage)) return;
        SynchronizeValue();
        _pausedUntil = Mathf.Max(_pausedUntil, Time.time + 0.1f);
        // A false lose-value flag gives native impact feedback on all clients
        // without the valuable's 15% destruction rule. Price is synchronized
        // separately from HP, with the same native feedback on every peer.
        if (SemiFunc.IsMultiplayer()) GetComponent<PhotonView>().RPC("BreakRPC", RpcTarget.All, 0f, point, level, false);
        else BreakRpc.Invoke(_impact, new object[] { 0f, point, level, false, default(PhotonMessageInfo) });
        if (_life.Dead) FinishDeath();
    }

    internal void EnvironmentalDeath()
    {
        if (_life.Kill()) FinishDeath();
    }

    private void FinishDeath()
    {
        if (_deathHandled) return;
        _deathHandled = true;
        SynchronizeValue();
        Vector3 position = _physical.centerPoint;
        foreach (PhysGrabber grabber in _physical.playerGrabbing.ToArray())
            if (grabber != null) grabber.OverrideGrabRelease(GetComponent<PhotonView>().ViewID, 0.5f);
        try
        {
            EnemyParent.Difficulty difficulty = VanillaCatalog.BellaPrefab().Prefab.GetComponent<EnemyParent>().difficulty;
            GameObject orb = difficulty == EnemyParent.Difficulty.Difficulty3 ? AssetManager.instance.enemyValuableBig :
                difficulty == EnemyParent.Difficulty.Difficulty2 ? AssetManager.instance.enemyValuableMedium : AssetManager.instance.enemyValuableSmall;
            VanillaCatalog.Spawn("Valuables/" + orb.name, position, Quaternion.identity);
        }
        catch (Exception exception) { Plugin.ModLogger.LogError($"Stalking Doll core spawn failed: {exception}"); }
        _impact.destroyDisable = false;
        _impact.DestroyObject();
    }
}

[HarmonyPatch(typeof(HurtCollider), "EnemyHurtRigidbody")]
internal static class StalkingDollEnemyDamagePatch
{
    private static readonly MethodInfo CanHit = AccessTools.Method(typeof(HurtCollider), "CanHit");

    [HarmonyPrefix]
    private static void Prefix(HurtCollider __instance, EnemyRigidbody _enemyRigidbody, PhysGrabObject _physGrabObject)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _enemyRigidbody != null || _physGrabObject == null ||
            !__instance.enemyLogic || __instance.enemyDamageCooldown <= 0 ||
            _physGrabObject.GetComponent<StalkingDollHealth>() is not StalkingDollHealth health || health.Dead ||
            __instance.GetComponentInParent<StalkingDollHealth>() == health) return;
        if ((bool)CanHit.Invoke(__instance, new object[] { _physGrabObject.gameObject, __instance.enemyDamageCooldown,
            __instance.enemyRayCast, _physGrabObject.centerPoint, HurtCollider.HitType.Enemy })) health.Attack(__instance);
    }
}

[HarmonyPatch(typeof(HurtCollider), "CanHit")]
internal static class StalkingDollPhysicalDamagePatch
{
    [HarmonyPostfix]
    private static void Postfix(HurtCollider __instance, GameObject hitObject, HurtCollider.HitType hitType, ref bool __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !__result || hitType != HurtCollider.HitType.PhysObject ||
            hitObject.GetComponent<StalkingDollHealth>() is not StalkingDollHealth health) return;
        __result = false; // Never also run valuable break/destroy after monster damage.
        if (!__instance.enemyLogic) health.Attack(__instance);
    }
}

[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "Break")]
internal static class StalkingDollCollisionDamagePatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObjectImpactDetector __instance, Vector3 _contactPoint, int breakLevel)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<StalkingDollHealth>() is not StalkingDollHealth health) return true;
        if (!health.TerrainCollision) health.BreakHit(breakLevel, _contactPoint);
        return false;
    }
}

[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.DestroyObject))]
internal static class StalkingDollEnvironmentDeathPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PhysGrabObjectImpactDetector __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<StalkingDollHealth>() is not StalkingDollHealth health || health.Dead) return true;
        if (!health.TerrainCollision) health.EnvironmentalDeath();
        return false;
    }
}

[HarmonyPatch(typeof(ValuableObject), "DollarValueSet")]
internal static class StalkingDollValueInitializationPatch
{
    [HarmonyPostfix]
    private static void Postfix(ValuableObject __instance, ref IEnumerator __result)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && __instance.GetComponent<StalkingDollHealth>() is StalkingDollHealth health)
            __result = InitializeAfterNative(__result, __instance, health);
    }

    private static IEnumerator InitializeAfterNative(IEnumerator native, ValuableObject valuable, StalkingDollHealth health)
    {
        while (native.MoveNext()) yield return native.Current;
        // Native initialization adds the just-created item's current price.
        // The doll still cannot be extracted, so cancel that addition only.
        if (valuable != null && RoundDirector.instance != null)
            RoundDirector.instance.haulGoalMax -= (int)valuable.dollarValueCurrent;
        if (health != null) health.ValueInitialized();
    }
}

[HarmonyPatch(typeof(ValuableObject), "AddToDollarHaulList")]
internal static class StalkingDollHaulRegistrationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ValuableObject __instance) => __instance.GetComponent<StalkingDollHealth>() == null;
}

