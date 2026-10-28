#if DEBUG && HELLPARTY_TEST_HEADGRAB
using System;
using System.Collections.Generic;
using HarmonyLib;
using HellParty.Native;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace HellParty.Debugging;

// Branch-only experiment. Never compiled into normal Debug or Release.
// The owner and host remain authoritative/dead. Only explicit remote observers
// receive the visual probe. No voice recording or damage redirection yet.
internal sealed class HeadgrabScenario : MonoBehaviour
{
    internal static HeadgrabScenario? Current;
    private EnemyHeadGrabber? _monster;
    private PlayerAvatar? _corpse;
    private readonly List<Photon.Realtime.Player> _observers = new();
    private bool _follow;
    private float _endsAt, _nextMove, _nextLog;
    private bool _equipment;

    private void Start()
    {
        Current = this;
        Plugin.ModLogger.LogWarning("TEST Headgrab: F6 spawn/equipment, F7 observer-only revive probe (30s), F8 toggle 10Hz follow, F9 end. Requires host + dead guest + living vanilla guest. Use a disposable test save; return to lobby after each trial. No audio or automatic damage forwarding.");
    }

    private void Update()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() ||
            LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated) return;
        try
        {
            if (Input.GetKeyDown(KeyCode.F9)) StopProbe("manual stop");
            if (Input.GetKeyDown(KeyCode.F6)) Prepare();
            if (Input.GetKeyDown(KeyCode.F7)) BeginProbe();
            if (Input.GetKeyDown(KeyCode.F8) && _corpse != null)
            {
                _follow = !_follow;
                Plugin.ModLogger.LogInfo($"TEST Headgrab: follow={_follow}");
            }
            if (_corpse == null) return;
            if (!_corpse.isDisabled || !_corpse.deadSet)
            {
                // Actual native revival already restores everybody: never kill it again.
                ForgetProbe("real revival detected");
                return;
            }
            if (_monster == null || !_monster.headTargetActive || _monster.headTarget == null ||
                _monster.headTarget.playerAvatar != _corpse ||
                NativeEnemy.Dead(_monster.GetComponent<EnemyHealth>()) || Time.time >= _endsAt)
            {
                StopProbe("timeout, death or head release");
                return;
            }
            if (_follow && Time.time >= _nextMove)
            {
                _nextMove = Time.time + .1f;
                Vector3 point = NativeEnemy.Position(_monster);
                Quaternion rotation = Quaternion.Euler(0, _monster.transform.eulerAngles.y, 0);
                foreach (var observer in _observers)
                    if (Connected(observer)) _corpse.photonView.RPC("SpawnRPC", observer, point, rotation);
            }
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + 1;
                Plugin.ModLogger.LogInfo($"TEST Headgrab snapshot: corpseView={_corpse.photonView.ViewID}, dead={_corpse.deadSet}, disabled={_corpse.isDisabled}, playerHP={_corpse.playerHealth.health}, enemyHP={NativeEnemy.Health(_monster.GetComponent<EnemyHealth>())}, follow={_follow}, monster={NativeEnemy.Position(_monster)}. Observer visuals require human verification.");
            }
        }
        catch (Exception exception)
        {
            Plugin.ModLogger.LogError($"TEST Headgrab failed: {exception}");
            StopProbe("exception");
        }
    }

    private static bool Connected(Photon.Realtime.Player player) => PhotonNetwork.CurrentRoom != null &&
        PhotonNetwork.CurrentRoom.Players.TryGetValue(player.ActorNumber, out var current) && !current.IsInactive;

    private void Prepare()
    {
        PlayerAvatar player = PlayerAvatar.instance;
        if (player == null || player.isDisabled) { Plugin.ModLogger.LogWarning("TEST Headgrab: living host required for setup."); return; }
        if (!_equipment)
        {
            VanillaCatalog.Spawn("Items/Item Gun Shotgun", player.transform.position + Vector3.up + player.transform.right, Quaternion.identity);
            VanillaCatalog.Spawn("Items/Item Gun Tranq", player.transform.position + Vector3.up - player.transform.right, Quaternion.identity);
            _equipment = true;
        }
        if (_monster != null) { Plugin.ModLogger.LogInfo("TEST Headgrab: existing test monster retained; no duplicate spawn."); return; }
        PrefabRef prefab = VanillaCatalog.HeadgrabPrefab();
        NavMeshAgent agent = prefab.Prefab.GetComponentInChildren<NavMeshAgent>(true);
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(player.transform.position, out var origin, 5, filter)) return;
        var path = new NavMeshPath();
        for (int i = 0; i < 48; i++)
        {
            Vector3 candidate = origin.position + Quaternion.Euler(0, i * 45, 0) * Vector3.forward * (5 + i / 8);
            if (!NavMesh.SamplePosition(candidate, out var hit, 1.5f, filter) ||
                Mathf.Abs(hit.position.y - origin.position.y) > 2 ||
                (hit.position - origin.position).sqrMagnitude < 9 ||
                !NavMesh.CalculatePath(origin.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete ||
                Physics.CheckCapsule(hit.position + Vector3.up * .4f, hit.position + Vector3.up * 1.5f, .35f,
                    LayerMask.GetMask("Default", "PhysGrabObject", "Enemy"), QueryTriggerInteraction.Ignore)) continue;
            _monster = SummonedEnemySpawnTicket.Create(prefab.ResourcePath, hit.position, prepareOnMesh: true, vanillaOnly: true)
                .GetComponentInChildren<EnemyHeadGrabber>(true);
            Plugin.ModLogger.LogInfo($"TEST Headgrab spawned at {hit.position}. Let a guest die and allow native head pickup, then F7.");
            return;
        }
        Plugin.ModLogger.LogWarning("TEST Headgrab: no clear connected floor. Leave truck and press F6 again.");
    }

    private void BeginProbe()
    {
        if (_corpse != null) return;
        if (!SemiFunc.IsMultiplayer() || _monster == null || !_monster.headTargetActive || _monster.headTarget == null)
        { Plugin.ModLogger.LogWarning("TEST Headgrab: multiplayer and a native attached death head required."); return; }
        PlayerAvatar corpse = _monster.headTarget.playerAvatar;
        if (corpse == null || !corpse.deadSet || !corpse.isDisabled || corpse.photonView.IsMine)
        { Plugin.ModLogger.LogWarning("TEST Headgrab: use a fully dead remote guest as the donor."); return; }
        _observers.Clear();
        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
            if (player != null && !player.deadSet && !player.isDisabled && !player.photonView.IsMine &&
                player.photonView.Owner != null && player.photonView.Owner != corpse.photonView.Owner &&
                Connected(player.photonView.Owner) && !_observers.Contains(player.photonView.Owner))
                _observers.Add(player.photonView.Owner);
        if (_observers.Count == 0)
        { Plugin.ModLogger.LogWarning("TEST Headgrab: need a living vanilla observer guest in addition to host and dead guest."); return; }
        _corpse = corpse;
        _endsAt = Time.time + 30;
        _follow = false;
        foreach (var observer in _observers) corpse.photonView.RPC("ReviveRPC", observer, false);
        Plugin.ModLogger.LogWarning($"TEST Headgrab PROBE START: donorView={corpse.photonView.ViewID}, owner={corpse.photonView.OwnerActorNr}, observers={_observers.Count}. Host/owner NOT revived. F8 follow; F9 stop. Headgrab mesh remains visible as a reference.");
    }

    private void StopProbe(string reason)
    {
        if (_corpse != null && _corpse.isDisabled && _corpse.deadSet && PhotonNetwork.InRoom)
            foreach (var observer in _observers)
                if (Connected(observer))
                {
                    try { _corpse.photonView.RPC("PlayerDeathRPC", observer, -1); }
                    catch (Exception exception) { Plugin.ModLogger.LogWarning($"TEST Headgrab cleanup RPC failed: {exception.Message}"); }
                }
        if (_corpse != null) ForgetProbe(reason + "; observer death replay sent (best effort); return to lobby before next trial");
    }

    private void ForgetProbe(string reason)
    {
        Plugin.ModLogger.LogWarning("TEST Headgrab PROBE END: " + reason);
        _corpse = null; _observers.Clear(); _follow = false;
    }

    private void OnDestroy()
    {
        StopProbe("stage teardown");
        if (Current == this) Current = null;
    }

    [HarmonyPatch(typeof(PlayerHealth), "HurtOtherRPC")]
    private static class HitProbe
    {
        private static void Prefix(PlayerHealth __instance, int damage, Vector3 hurtPosition)
        {
            var test = Current;
            if (test == null || test._corpse == null || __instance != test._corpse.playerHealth) return;
            Plugin.ModLogger.LogInfo($"TEST Headgrab player-hit RPC observed: damage={damage}, point={hurtPosition}. No redirection; receipt alone does not prove an accepted hit.");
        }
    }
}
#endif
