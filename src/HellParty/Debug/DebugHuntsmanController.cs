#if DEBUG
using HellParty.Native;
using UnityEngine;

namespace HellParty.Debugging;

internal sealed class DebugHuntsmanController : MonoBehaviour
{
    internal bool AllowSolo;

    private void Update()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !DebugHuntsmanScenario.Enabled) return;
        if (Input.GetKeyDown(KeyCode.F6))
        {
            AllowSolo = !AllowSolo;
            Plugin.ModLogger.LogInfo($"DEBUG Huntsman solo population override {(AllowSolo ? "ON" : "OFF")}. Other abduction conditions remain active.");
        }
        if (Input.GetKeyDown(KeyCode.F8)) GetComponent<HuntsmanAbduction>()?.Release();
        if (!Input.GetKeyDown(KeyCode.F7) || PlayerController.instance == null) return;
        EnemyHunter hunter = GetComponentInChildren<EnemyHunter>(true);
        if (hunter == null || !NativeEnemy.Spawned(GetComponent<EnemyParent>()) || NativeEnemy.Dead(hunter.enemy.Health)) return;
        hunter.enemy.Rigidbody.onTouchPlayerAvatar = PlayerController.instance.playerAvatarScript;
        hunter.OnTouchPlayer();
        Plugin.ModLogger.LogInfo($"DEBUG Huntsman native touch response: {hunter.currentState}. Approach within 7m; two living players required unless F6 is ON.");
    }
}
#endif
