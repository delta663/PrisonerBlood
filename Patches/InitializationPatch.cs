using HarmonyLib;
using ProjectM;

namespace PrisonerBlood.Patches;

[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUpdate))]
public static class InitializationPatch
{
    private static bool _initialized;

    [HarmonyPostfix]
    public static void OneShot_AfterServerBootstrap()
    {
        if (_initialized)
            return;

        if (!Plugin.HasLoaded())
            return;

        _initialized = true;
        Core.InitializeAfterLoaded();
    }
}
