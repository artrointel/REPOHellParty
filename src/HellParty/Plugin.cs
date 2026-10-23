using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace HellParty;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "artrointel.HellParty";
    public const string PluginName = "HellParty";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource ModLogger { get; private set; } = null!;

    private Harmony? _harmony;

    private void Awake()
    {
        ModLogger = Logger;
#if DEBUG
        Debugging.DebugHeadmanScenario.Initialize(Config);
        Debugging.DebugClownScenario.Initialize(Config);
        Debugging.DebugReaperScenario.Initialize(Config);
        Debugging.DebugUpscreamScenario.Initialize(Config);
        Debugging.DebugCleanupScenario.Initialize(Config);
        Debugging.DebugHuntsmanScenario.Initialize(Config);
#endif
        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        ModLogger.LogInfo($"{PluginName} v{PluginVersion} loaded.");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}

