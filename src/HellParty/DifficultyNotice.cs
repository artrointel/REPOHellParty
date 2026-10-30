using System;
using System.Collections;
using HellParty.Rules;
using Photon.Pun;
using UnityEngine;

namespace HellParty;

// Local banner plus one native host TTS message. No custom guest RPC or save flag.
internal sealed class DifficultyNotice : MonoBehaviour
{
    private IEnumerator Start()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || RunManager.instance == null ||
            !SemiFunc.RunIsLevel()) yield break;
        int completed = RunManager.instance.levelsCompleted;
        int difficulty = DifficultyService.Current;
        if (!DifficultyNoticeRules.ShouldAnnounce(completed, difficulty)) yield break;
        // Speech starts with the players in the truck; it does not wait for the
        // host's moon presentation or for the shared large-message UI to clear.
        StartCoroutine(AnnounceSpeech(completed));

        StageRuntime stage = GetComponent<StageRuntime>();
        string message = completed == 0
            ? "HELLPARTY IS NOW ACTIVE"
            : $"HELLPARTY - DIFFICULTY UP: {difficulty}\n<size=65%>{DifficultyNoticeRules.Highlights(difficulty)}</size>";
        float quietTime = 0, shownTime = 0;
        // Avoid delivering a stale start-of-stage notification much later.
        float deadline = Time.realtimeSinceStartup + 90;
        while (StageRuntime.Current == stage && SemiFunc.RunIsLevel() &&
               SemiFunc.IsMasterClientOrSingleplayer() && RunManager.instance != null &&
               RunManager.instance.levelsCompleted == completed && Time.realtimeSinceStartup < deadline)
        {
            BigMessageUI ui = BigMessageUI.instance;
            bool ready = LevelGenerator.Instance != null && LevelGenerator.Instance.Generated &&
                GameDirector.instance != null && GameDirector.instance.currentState == GameDirector.gameState.Main &&
                PlayerAvatar.instance != null && !PlayerAvatar.instance.isDisabled && !PlayerAvatar.instance.deadSet &&
                ui != null && ui.isActiveAndEnabled &&
                (MoonUI.instance == null || MoonUI.instance.state == MoonUI.State.None) &&
                (LevelUI.instance == null || LevelUI.instance.showTimer <= 0);
            // Yield to native messages, including ones which interrupt our banner.
            bool occupied = ui != null && ui.bigMessageTimer > 0 && ui.bigMessage != message;
            if (!ready || occupied)
                quietTime = 0;
            else
            {
                quietTime += Time.deltaTime;
                if (quietTime >= 1)
                {
                    ui!.BigMessage(message, "", 36, new Color(1f, .28f, .08f), Color.white);
                    shownTime += Time.deltaTime;
                    if (shownTime >= 6) yield break; // Native UI expires and hides itself.
                }
            }
            yield return null;
        }
    }

    private IEnumerator AnnounceSpeech(int completed)
    {
        StageRuntime stage = GetComponent<StageRuntime>();
        float deadline = Time.realtimeSinceStartup + 45;
        float readyTime = 0;
        while (StageRuntime.Current == stage && SemiFunc.RunIsLevel() &&
               SemiFunc.IsMasterClientOrSingleplayer() && RunManager.instance != null &&
               RunManager.instance.levelsCompleted == completed && Time.realtimeSinceStartup < deadline)
        {
            PlayerAvatar host = PlayerAvatar.instance;
            GameDirector director = GameDirector.instance;
            if (host == null || director == null || host.voiceChat == null || host.voiceChat.ttsVoice == null)
            {
                readyTime = 0;
                yield return null;
                continue;
            }
            bool ready = director.currentState == GameDirector.gameState.Main &&
                !host.isDisabled && !host.deadSet && host.voiceChat.TTSinstantiated &&
                !host.voiceChat.ttsVoice.isSpeaking;
            if (ready)
            {
                // Do not send before the connected living guests finish loading.
                foreach (PlayerAvatar player in director.PlayerList)
                    if (player != null && !player.isDisabled &&
                        (player.photonView.Owner == null || !player.photonView.Owner.IsInactive) &&
                        !player.levelAnimationCompleted) { ready = false; break; }
            }
            readyTime = ready ? readyTime + Time.deltaTime : 0;
            if (readyTime >= 2)
            {
                try
                {
                    string speech = DifficultyNoticeRules.SpokenMessage(completed);
                    if (SemiFunc.IsMultiplayer())
                        host.photonView.RPC("ChatMessageSendRPC", RpcTarget.All, speech, false, "en-US");
                    else
                        host.voiceChat.ttsVoice.TTSSpeakNow(speech, false, "en-US");
                }
                catch (Exception exception)
                {
                    Plugin.ModLogger.LogWarning($"HellParty difficulty speech failed: {exception.Message}");
                }
                yield break;
            }
            yield return null;
        }
    }
}
