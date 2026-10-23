using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using HellParty.Native;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty;

internal sealed class ChefMimic : MonoBehaviour
{
    private static readonly MethodInfo UpdateState = AccessTools.Method(typeof(EnemyTumbler), "UpdateState");
    private static readonly MethodInfo Death = AccessTools.Method(typeof(EnemyHealth), "Death");
    private static readonly AccessTools.FieldRef<EnemyParent, float> ValuableSpawnTimer =
        AccessTools.FieldRefAccess<EnemyParent, float>("valuableSpawnTimer");
    private readonly ChefMimicState _state = new();
    private readonly ChefVitality _vitality = new();
    private EnemyParent _parent = null!;
    private EnemyTumbler _chef = null!;
    private EnemyUpscream? _upscream;
    private Enemy _enemy = null!;
    private EnemyHealth _health = null!;
    private ChefMimicFrog? _frog;
    private PlayerAvatar? _target;
    private Vector3 _revealPosition;
    private int _savedHealth;
    private bool _pursuingBeforeUpdate;
    internal bool Disguised => _state.Mode == ChefMimicMode.Disguised;
    internal bool Revealing => _state.Mode == ChefMimicMode.Revealing;
    internal bool Restoring => Revealing || _state.Mode is ChefMimicMode.Defeating or ChefMimicMode.ReturningToRoam;

    private void Awake()
    {
        _parent = GetComponent<EnemyParent>();
        _chef = GetComponentInChildren<EnemyTumbler>(true);
        _upscream = GetComponentInChildren<EnemyUpscream>(true);
        _enemy = GetComponentInChildren<Enemy>(true);
        _health = _enemy.GetComponent<EnemyHealth>();
    }

    private void Update()
    {
        // EnemyTumbler is inactive while disguised. Keep visibility checks on
        // the active EnemyParent instead of the hidden native AI's Update.
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() ||
            _frog == null || !_state.TryDisguiseScan(Time.time)) return;
        Vector3 position = _frog.transform.position;
        if (!_state.ReturnIfUnseen(Time.time, HasVisiblePlayer(position + Vector3.up * 0.25f, position))) return;
        SaveRevealPosition(position);
        ChefMimicFrog frog = _frog;
        _frog = null;
        _target = null;
        frog.RemoveQuietly();
        Restore();
    }

    internal void Tick()
    {
        _pursuingBeforeUpdate = false;
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel()) return;
        if (NativeEnemy.Health(_health) <= 0 || NativeEnemy.Dead(_health)) { Cleanup(); return; }
        if (!NativeEnemy.Spawned(_parent)) return;
        if (_state.Mode == ChefMimicMode.Attacking)
        {
            if (_state.ShouldReleaseTarget(Valid(_target), UpscreamPursuitEnded)) { ReturnToRoam(); return; }
            if (NativePassive)
            {
                // Post-attack Idle is recovery, not target loss. Give native
                // pursuit its full timer rather than disguising in front of
                // the grabber when the vision coroutine briefly sees nothing.
                ResumePursuit();
            }
            _pursuingBeforeUpdate = NativePursuing;
            return;
        }
        if (!_state.TryScan(Time.time, NativeRoaming)) return;
        Vector3 origin = _enemy.Vision.VisionTransform.position;
        if (HasVisiblePlayer(origin, NativeEnemy.Position(_enemy))) Disguise();
    }

    internal void AfterNativeTick()
    {
        // The native chase timer/navigation decides pursuit expiry. Disguise
        // distance/raycast requirements must never override that decision.
        if (_state.ShouldReleaseTarget(Valid(_target), UpscreamPursuitEnded || _pursuingBeforeUpdate && NativePassive))
            ReturnToRoam();
    }

    internal float FrogValue(float original) => (float)_vitality.FrogValue(original);

    internal bool CaptureFrog(float change, float original)
    {
        _vitality.ApplyFrogChange(change, original);
        _savedHealth = _vitality.BodyHealth(_health.health);
        NativeEnemy.Health(_health) = _savedHealth;
        return _savedHealth <= 0;
    }

    private bool HasVisiblePlayer(Vector3 origin, Vector3 rangeOrigin)
    {
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!Valid(player) || !ChefMimicState.InRange((player.transform.position - rangeOrigin).sqrMagnitude)) continue;
            Vector3 destination = player.PlayerVisionTarget.VisionTransform.position;
            if (Unobstructed(origin, destination, player)) return true;
        }
        return false;
    }

    private static bool Valid(PlayerAvatar? player) => player != null && !player.isDisabled && !player.deadSet;
    private bool Unobstructed(Vector3 origin, Vector3 destination, PlayerAvatar player)
    {
        Vector3 direction = destination - origin;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
                     SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(_parent.transform) ||
                _frog != null && hit.transform.IsChildOf(_frog.transform) || hit.transform.GetComponentInParent<PlayerAvatar>() == player ||
                hit.transform.GetComponentInParent<PlayerTumble>() is PlayerTumble tumble && tumble.playerAvatar == player ||
                hit.transform.GetComponentInParent<PlayerController>() is PlayerController controller && controller.playerAvatarScript == player) continue;
            return false;
        }
        return true;
    }
    private static bool Passive(EnemyTumbler.State state) => state is EnemyTumbler.State.Idle or EnemyTumbler.State.Roam or EnemyTumbler.State.Investigate or EnemyTumbler.State.Leave;
    private bool NativePassive => _upscream != null
        ? _upscream.currentState is EnemyUpscream.State.Idle or EnemyUpscream.State.Roam or EnemyUpscream.State.Investigate or EnemyUpscream.State.Leave or EnemyUpscream.State.IdleBreak
        : Passive(_chef.currentState);
    private bool NativeRoaming => _upscream != null
        ? _upscream.currentState is EnemyUpscream.State.Idle or EnemyUpscream.State.Roam
        : _chef.currentState is EnemyTumbler.State.Idle or EnemyTumbler.State.Roam;
    private bool NativePursuing => _upscream != null
        ? _upscream.currentState == EnemyUpscream.State.GoToPlayer : _chef.currentState == EnemyTumbler.State.MoveToPlayer;
    private bool UpscreamPursuitEnded => _upscream != null && _upscream.currentState == EnemyUpscream.State.Leave;
    private void ResumePursuit()
    {
        if (_upscream != null) _upscream.UpdateState(EnemyUpscream.State.GoToPlayer);
        else SetState(EnemyTumbler.State.MoveToPlayer);
    }

    private void Disguise()
    {
        if (_state.Mode != ChefMimicMode.Roaming) return;
        try
        {
            Vector3 position = NativeEnemy.Position(_enemy);
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas)) position = hit.position;
            string disguisePath = _upscream != null ? VanillaCatalog.RandomUpscreamDisguisePath()
                : VanillaCatalog.Valuable(p => p.GetComponent<FrogTrap>() != null).ResourcePath;
            _savedHealth = NativeEnemy.Health(_health);
            _vitality.CaptureBody(_savedHealth, _health.health);
            _revealPosition = position;
            _frog = ChefMimicFrog.Create(disguisePath, position, transform.rotation, this);
            _state.Disguise();
            _parent.Despawn();
        }
        catch (Exception exception)
        {
            Cleanup();
            Plugin.ModLogger.LogError($"{_enemy.name} disguise failed: {exception}");
        }
    }

    internal void Grabbed(ChefMimicFrog frog, PhysGrabber grabber)
    {
        if (frog != _frog || !Valid(grabber.playerAvatar) || !_state.Reveal()) return;
        _target = grabber.playerAvatar;
        StartCoroutine(RevealAfterGrab(frog));
    }

    private IEnumerator RevealAfterGrab(ChefMimicFrog frog)
    {
        // GrabStarted is still on the stack in single player. Let the native
        // grab finish before releasing it, removing the prop and restoring Chef.
        yield return null;
        if (!Revealing || _frog != frog || frog == null) yield break;
        SaveRevealPosition(frog.transform.position);
        _frog = null;
        frog.RemoveQuietly();
        Restore();
    }

    internal void Destroyed(ChefMimicFrog frog)
    {
        if (frog != _frog || !_state.Defeat()) return;
        SaveRevealPosition(frog.transform.position);
        _frog = null;
        _target = null;
        // Let the collectible's native destruction finish first. The same Chef
        // then runs its native death, including exactly one native orb drop.
        StartCoroutine(DefeatAfterBreak());
    }

    private IEnumerator DefeatAfterBreak()
    {
        yield return null;
        if (_state.Mode == ChefMimicMode.Defeating) Restore();
    }

    private void SaveRevealPosition(Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) _revealPosition = hit.position;
    }

    private void Restore()
    {
        try { NativeEnemy.Spawn(_parent); }
        catch (Exception exception)
        {
            _state.Roam(Time.time);
            _parent.DespawnedTimerSet(0);
            Plugin.ModLogger.LogError($"{_enemy.name} reveal failed: {exception}");
        }
    }

    internal void PlaceForReveal(Enemy enemy)
    {
        enemy.EnemyTeleported(_revealPosition);
        NativeEnemy.FirstPointUsed(_parent) = true;
    }

    internal void Spawned()
    {
        if (!Restoring)
        {
            _vitality.NewLife(NativeEnemy.Health(_health), _health.health);
            _state.Roam(Time.time);
            return;
        }
        // Native Spawn resets health; disguise must never heal the monster.
        NativeEnemy.Health(_health) = Math.Max(1, Math.Min(_health.health, _savedHealth));
        _enemy.NavMeshAgent.Enable();
        _enemy.NavMeshAgent.Warp(_revealPosition, true);
        _enemy.Rigidbody.GrabRelease();
        _enemy.Rigidbody.GetComponent<PhysGrabObject>().OverrideGrabDisable(0.5f);
        if (_state.Mode == ChefMimicMode.Defeating)
        {
            // The hidden body's proximity timer may have expired while the
            // Frog was elsewhere. A confirmed disguise kill earns its orb.
            ValuableSpawnTimer(_parent) = 10f;
            NativeEnemy.Health(_health) = 0;
            Death.Invoke(_health, new object[] { Vector3.zero });
            return;
        }
        if (_state.Mode == ChefMimicMode.ReturningToRoam)
        {
            ReturnToRoam();
            return;
        }
        _state.Attack();
        if (!Valid(_target)) { ReturnToRoam(); return; }
        if (_upscream != null)
        {
            _enemy.Vision.onVisionTriggeredPlayer = _target;
            _enemy.Vision.onVisionTriggeredID = _target!.photonView.ViewID;
            _upscream.UpdateState(EnemyUpscream.State.Idle);
            _upscream.OnVision();
            return;
        }
        _chef.targetPlayer = _target;
        if (SemiFunc.IsMultiplayer())
            _chef.GetComponent<PhotonView>().RPC("TargetPlayerRPC", RpcTarget.All, _target!.photonView.ViewID);
        // Preserve native aiming, approach, tell animation and jump/attack.
        SetState(EnemyTumbler.State.Notice);
    }

    internal bool AllowVision(EnemyTumbler chef) => _state.Mode == ChefMimicMode.Attacking &&
        Valid(_target) && chef.enemy.Vision.onVisionTriggeredPlayer == _target;
    internal bool AllowUpscreamVision() => _state.Mode == ChefMimicMode.Attacking &&
        Valid(_target) && _enemy.Vision.onVisionTriggeredPlayer == _target;
    internal bool Attacking => _state.Mode == ChefMimicMode.Attacking;

    private void ReturnToRoam()
    {
        _target = null;
        if (_upscream != null)
        {
            _upscream.targetPlayer = null!;
            _state.Roam(Time.time);
            _upscream.UpdateState(EnemyUpscream.State.Roam);
            return;
        }
        _chef.targetPlayer = null!;
        _state.Roam(Time.time);
        SetState(_chef.currentState is EnemyTumbler.State.Tumble or EnemyTumbler.State.TumbleEnd or EnemyTumbler.State.BackToNavmesh
            ? EnemyTumbler.State.BackToNavmesh : EnemyTumbler.State.Roam);
    }

    private void SetState(EnemyTumbler.State state) => UpdateState.Invoke(_chef, new object[] { state });

    internal void Cleanup()
    {
        if (_frog != null) _frog.RemoveQuietly();
        _frog = null;
        _target = null;
        _state.Roam(Time.time);
    }
    private void OnDestroy() => Cleanup();
}

[HarmonyPatch(typeof(EnemyTumbler), "Update")]
internal static class ChefMimicUpdatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyTumbler __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return true;
        ChefMimic mimic = NativeEnemy.Parent(__instance).GetComponent<ChefMimic>();
        if (mimic == null) return true;
        mimic.Tick();
        return !mimic.Disguised && !mimic.Restoring;
    }

    [HarmonyPostfix]
    private static void Postfix(EnemyTumbler __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
            NativeEnemy.Parent(__instance).GetComponent<ChefMimic>()?.AfterNativeTick();
    }
}

[HarmonyPatch(typeof(EnemyTumbler), nameof(EnemyTumbler.OnVision))]
internal static class ChefMimicVisionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyTumbler __instance) => !SemiFunc.IsMasterClientOrSingleplayer() ||
        NativeEnemy.Parent(__instance).GetComponent<ChefMimic>() is not ChefMimic mimic || mimic.AllowVision(__instance);
}

[HarmonyPatch(typeof(EnemyTumbler), nameof(EnemyTumbler.OnGrabbed))]
internal static class ChefMimicBodyGrabPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyTumbler __instance) => !SemiFunc.IsMasterClientOrSingleplayer() ||
        NativeEnemy.Parent(__instance).GetComponent<ChefMimic>() is not ChefMimic mimic || mimic.Attacking;
}

[HarmonyPatch(typeof(EnemyParent), "Spawn")]
internal static class ChefMimicHiddenSpawnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyParent __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.GetComponent<ChefMimic>() is not ChefMimic mimic || !mimic.Disguised) return true;
        __instance.DespawnedTimerSet(1);
        return false;
    }
}

[HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.EnemySpawn))]
internal static class ChefMimicRevealPositionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Enemy enemy, ref bool __result)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || enemy.GetComponentInParent<ChefMimic>() is not ChefMimic mimic || !mimic.Restoring) return true;
        mimic.PlaceForReveal(enemy);
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(EnemyParent), "SpawnRPC")]
internal static class ChefMimicRevealedPatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyParent __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && NativeEnemy.Spawned(__instance))
            __instance.GetComponent<ChefMimic>()?.Spawned();
    }
}

[HarmonyPatch(typeof(EnemyHealth), "Death")]
internal static class ChefMimicDeathPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && (__instance.GetComponent<EnemyTumbler>() != null || __instance.GetComponent<EnemyUpscream>() != null))
            NativeEnemy.Parent(__instance).GetComponent<ChefMimic>()?.Cleanup();
    }
}
