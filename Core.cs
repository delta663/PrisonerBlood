using System;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using PrisonerBlood.Services;
using ProjectM;
using ProjectM.Scripting;
using Unity.Entities;

namespace PrisonerBlood;

internal static class Core
{
    private static bool _hasInitialized;
    private static World _server;
    private static EntityManager _entityManager;
    private static ServerScriptMapper _serverScriptMapper;
    private static DebugEventsSystem _debugEventsSystem;

    public static World Server => _server ??= GetWorld("Server") ?? throw new Exception("There is no Server world (yet). Did you install a server mod on the client?");
    public static EntityManager EntityManager => _entityManager == default ? (_entityManager = Server.EntityManager) : _entityManager;
    public static ServerScriptMapper ServerScriptMapper => _serverScriptMapper ??= Server.GetExistingSystemManaged<ServerScriptMapper>();
    public static ServerGameManager ServerGameManager => ServerScriptMapper.GetServerGameManager();
    public static DebugEventsSystem DebugEventsSystem => _debugEventsSystem ??= Server.GetExistingSystemManaged<DebugEventsSystem>();
    public static ManualLogSource Log => Plugin.PluginLog;
    public static UnitSpawnerService UnitSpawner { get; private set; }

    public static void LogException(Exception e, [CallerMemberName] string caller = null)
    {
        Log.LogError($"Failure in {caller}\nMessage: {e.Message} Inner:{e.InnerException?.Message}\n\nStack: {e.StackTrace}\nInner Stack: {e.InnerException?.StackTrace}");
    }

    internal static void InitializeAfterLoaded()
    {
        if (_hasInitialized)
            return;

        _server = GetWorld("Server") ?? throw new Exception("There is no Server world (yet). Did you install a server mod on the client?");
        _entityManager = _server.EntityManager;
        _serverScriptMapper = _server.GetExistingSystemManaged<ServerScriptMapper>();
        _debugEventsSystem = _server.GetExistingSystemManaged<DebugEventsSystem>();

        UnitSpawner = new UnitSpawnerService();
        BuyPrisonerService.Initialize();
        BuyBloodPotionService.Initialize();
        SellPrisonerService.Initialize();

        _hasInitialized = true;
        Log.LogInfo($"{nameof(InitializeAfterLoaded)} completed");
    }

    internal static World GetWorld(string name)
    {
        foreach (var world in World.s_AllWorlds)
        {
            if (world != null && world.Name == name)
                return world;
        }

        return null;
    }
}
