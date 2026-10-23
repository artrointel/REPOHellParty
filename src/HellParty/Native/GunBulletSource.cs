using System;
using HarmonyLib;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty.Native;

// Host-only metadata attached to native bullets. All peers still receive the
// original gun/bullet RPCs and render the original prefab and effects.
internal sealed class GunBulletSource : MonoBehaviour
{
    internal long Shot;
    internal bool Reflected;
}

internal sealed class GunShotIdentity : MonoBehaviour
{
    internal long Shot;
    private static long _next;
    internal void Begin() => Shot = ++_next;
}

internal static class GunBulletContext
{
    internal static ItemGun? Current;

}

// Laser guns keep one persistent HurtCollider instead of spawning bullets.
// A complete native continuous burst is one shot for shield deduplication.
[HarmonyPatch(typeof(ItemGunLaser), nameof(ItemGunLaser.OnStateShootStart))]
internal static class LaserGunSourcePatch
{
    [HarmonyPostfix]
    private static void Postfix(ItemGunLaser __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() ||
            !DifficultyRules.MentalistReflectionUnlocked(DifficultyService.Current) ||
            __instance.semiLaser == null || __instance.semiLaser.hurtCollider == null) return;
        ItemGun gun = __instance.GetComponent<ItemGun>();
        GunShotIdentity identity = gun.GetComponent<GunShotIdentity>() ?? gun.gameObject.AddComponent<GunShotIdentity>();
        identity.Begin();
        HurtCollider hurt = __instance.semiLaser.hurtCollider;
        GunBulletSource source = hurt.GetComponent<GunBulletSource>() ?? hurt.gameObject.AddComponent<GunBulletSource>();
        source.Shot = identity.Shot;
        MentalistReflectedGun reflected = gun.GetComponent<MentalistReflectedGun>();
        source.Reflected = reflected != null;
        if (reflected != null) hurt.enemyHost = reflected.Enemy;
    }
}

[HarmonyPatch(typeof(ItemGunLaser), "FixedUpdate")]
internal static class ReflectedLaserRecoilPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ItemGunLaser __instance) => __instance.GetComponent<MentalistReflectedGun>() == null;
}

[HarmonyPatch(typeof(ItemGun), nameof(ItemGun.ShootRPC))]
internal static class GunShotIdentityPatch
{
    [HarmonyPrefix]
    private static void Prefix(ItemGun __instance, PhotonMessageInfo _info)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.MasterOnlyRPC(_info) ||
            !DifficultyRules.MentalistReflectionUnlocked(DifficultyService.Current)) return;
        (__instance.GetComponent<GunShotIdentity>() ?? __instance.gameObject.AddComponent<GunShotIdentity>()).Begin();
    }
}

[HarmonyPatch(typeof(ItemGun), nameof(ItemGun.ShootBulletRPC))]
internal static class GunBulletContextPatch
{
    [HarmonyPrefix]
    private static void Prefix(ItemGun __instance, PhotonMessageInfo _info, out ItemGun? __state)
    {
        __state = GunBulletContext.Current;
        GunBulletContext.Current = SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.MasterOnlyRPC(_info) &&
            DifficultyRules.MentalistReflectionUnlocked(DifficultyService.Current) ? __instance : null;
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, ItemGun? __state)
    {
        GunBulletContext.Current = __state;
        return __exception;
    }
}

[HarmonyPatch(typeof(ItemGunBullet), nameof(ItemGunBullet.ActivateAll))]
internal static class GunBulletSourcePatch
{
    [HarmonyPrefix]
    private static void Prefix(ItemGunBullet __instance)
    {
        ItemGun? gun = GunBulletContext.Current;
        if (gun == null || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        GunShotIdentity identity = gun.GetComponent<GunShotIdentity>();
        if (identity == null || identity.Shot == 0) return;
        GunBulletSource source = __instance.gameObject.AddComponent<GunBulletSource>();
        source.Shot = identity.Shot;
        source.Reflected = gun.GetComponent<MentalistReflectedGun>() != null;
        if (source.Reflected && gun.GetComponent<MentalistReflectedGun>().Enemy != null && __instance.hurtCollider != null)
            __instance.hurtCollider.enemyHost = gun.GetComponent<MentalistReflectedGun>().Enemy;
    }
}
