using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace HellParty;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "artrointel.HellParty";
    public const string PluginName = "HellParty";
    public const string PluginVersion = "0.2.1";

    internal static ManualLogSource ModLogger { get; private set; } = null!;

    private Harmony? _harmony;

    private void Awake()
    {
        ModLogger = Logger;
        _harmony = new Harmony(PluginGuid);
        try
        {
            _harmony.PatchAll();
            ModLogger.LogInfo($"{PluginName} v{PluginVersion} loaded.");
#if DEBUG && HELLPARTY_TEST_CLEANUP_CREW
            ModLogger.LogWarning("TEST BUILD: CleanupCrew scenario compiled in. Host level difficulty is fixed at 5 with test spawns. Rebuild without -TestScenario for normal play.");
#endif
        }
        catch (Exception exception)
        {
            ModLogger.LogError($"{PluginName} initialization failed; removing its patches. Check the game version and installed mods.\n{exception}");
            try { _harmony.UnpatchSelf(); }
            finally { _harmony = null; enabled = false; }
        }
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
