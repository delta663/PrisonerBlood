using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using VampireCommandFramework;
using PrisonerBlood.Services;

namespace PrisonerBlood;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework")]
public class Plugin : BasePlugin
{
    internal static Harmony Harmony;
    internal static ManualLogSource PluginLog;

    public override void Load()
    {
        if (Application.productName != "VRisingServer")
            return;

        PluginLog = Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} version {MyPluginInfo.PLUGIN_VERSION} is loaded!");

        Harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        Harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

        ConfigService.Initialize();
        CommandRegistry.RegisterAll();
    }

    public override bool Unload()
    {
        CommandRegistry.UnregisterAssembly();
        Harmony?.UnpatchSelf();
        return true;
    }

    internal static bool HasLoaded()
    {
        var server = Core.GetWorld("Server");
        if (server == null)
            return false;

        var collectionSystem = server.GetExistingSystemManaged<ProjectM.PrefabCollectionSystem>();
        return collectionSystem?.SpawnableNameToPrefabGuidDictionary.Count > 0;
    }
}
