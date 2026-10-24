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
    public const string PluginVersion = "0.2.0";

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
