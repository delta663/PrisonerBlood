using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PrisonerBlood.Models;
using ProjectM;
using ProjectM.Behaviours;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PrisonerBlood.Services;

internal static class BuyPrisonerService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string LOG_FILE = Path.Combine(CONFIG_DIR, "buyprisoner_log.csv");
    private static readonly object LOG_LOCK = new();

    private static readonly PrefabGUID PrisonerPrefab = new(-744966291);
    private static readonly PrefabGUID ImprisonedBuffPrefab = new(1603329680);

    private const float PrisonCellSearchRadius = 3f;

    public static void Initialize() => ConfigService.Initialize();
    public static void Reload() => ConfigService.Reload();

    public static string CurrencyName
    {
        get
        {
            var (_, name) = GetCurrency();
            return name;
        }
    }

    public static bool IsEnabled() => GetConfig().Enabled;

    public static (PrefabGUID Prefab, string Name) GetCurrency()
    {
        var config = GetConfig();
        return (new PrefabGUID(config.CurrencyPrefab), config.CurrencyName);
    }

    public static (int DefaultCost, Dictionary<string, int> Prices) GetPriceSnapshot()
    {
        var config = GetConfig();
        return (config.DefaultCost, new Dictionary<string, int>(config.BloodCosts, StringComparer.OrdinalIgnoreCase));
    }

    public static bool TryParseBloodTypeStrict(string input, out BloodType type)
    {
        type = default;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim();
        if (int.TryParse(input, out _))
            return false;

        if (!Enum.TryParse(input, true, out type))
            return false;

        return Helper.AllowedBloodTypes.Contains(type);
    }

    public static void ReplyHelp(Action<string> reply, string warningLine = null)
    {
        var (defaultCost, prices) = GetPriceSnapshot();

        if (!string.IsNullOrWhiteSpace(warningLine))
            reply(warningLine);

        reply("<color=yellow>Command:</color> <color=green>.buy prisoner <BloodType></color> e.g., <color=green>.buy prisoner rogue</color>");

        var sb = new StringBuilder();
        sb.AppendLine($"<color=yellow>Blood types</color> : <color=yellow>Costs</color> (<color=#87CEFA>{CurrencyName}</color>)");

        var parts = prices
            .OrderBy(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"{kv.Key} : {kv.Value}")
            .ToList();

        if (parts.Count == 0)
        {
            sb.AppendLine($"<color=#87CEFA>All blood types default cost</color> : {defaultCost}");
        }
        else
        {
            const int chunkSize = 3;
            for (int start = 0; start < parts.Count; start += chunkSize)
                sb.AppendLine(string.Join(" <color=white>|</color> ", parts.Skip(start).Take(chunkSize)));
        }

        reply(sb.ToString().TrimEnd());
        reply($"<color=yellow>A prisoner with <color=green>100%</color> blood quality will be placed in the nearest empty prison cell you own within {PrisonCellSearchRadius:0.0}m.</color>");
    }

    public static void BuyPrisoner(Entity senderUserEntity, Entity senderCharacterEntity, ulong steamId, string playerName, BloodType type, Action<string> reply)
    {
        if (!IsEnabled())
        {
            reply("<color=red>BuyPrisoner: Disabled.</color>");
            return;
        }

        var em = Core.EntityManager;
        if (senderCharacterEntity == Entity.Null || !em.Exists(senderCharacterEntity))
        {
            LogPurchase(steamId, playerName, type.ToString(), 0, false, "Character not ready");
            reply("<color=red>Character not ready.</color>");
            return;
        }

        int cost = GetCost(type.ToString());

        if (!TryFindClosestEmptyOwnedPrisonCell(senderCharacterEntity, PrisonCellSearchRadius, out var prisonCellEntity, out var prisonCellPosition, out var reason))
        {
            LogPurchase(steamId, playerName, type.ToString(), 0, false, reason);
            reply($"<color=yellow>Unsuccessful!</color> {reason}");
            return;
        }
        
        if (!TrySpendCurrency(senderCharacterEntity, cost, out reason))
        {
            LogPurchase(steamId, playerName, type.ToString(), 0, false, reason);
            reply($"<color=yellow>Unsuccessful!</color> {reason}");
            return;
        }

        Core.UnitSpawner.SpawnWithCallback(senderUserEntity, PrisonerPrefab, new float2(prisonCellPosition.x, prisonCellPosition.z),
            -1,
            e =>
            {
                try
                {
                    if (e == Entity.Null || !em.Exists(e))
                        return;

                    SetupPurchasedPrisoner(senderUserEntity, e, prisonCellEntity, type);

                    LogPurchase(steamId, playerName, type.ToString(), cost, true, "Successful");
                    reply("<color=green>Success!</color> A prisoner was placed in your prison cell.");

                    var broadcastMessage =
                        $"<color=white>{playerName}</color> just spent <color=#87CEFA>{cost} {CurrencyName}</color> to buy a prisoner " +
                        $"with <color=green>100%</color> <color=yellow>{type}</color> blood. Learn more, type <color=green>.buy prisoner help</color>";

                    Helper.BroadcastSystemMessage(broadcastMessage);
                }
                catch (Exception ex)
                {
                    LogPurchase(steamId, playerName, type.ToString(), 0, false, "Spawn callback error: " + ex.Message);
                    reply("<color=red>Prisoner spawned but setup failed.</color>");
                    Core.LogException(ex);
                }
            },
            prisonCellPosition.y
        );
    }

    private static void SetupPurchasedPrisoner(Entity senderUserEntity, Entity prisonerEntity, Entity prisonCellEntity, BloodType type)
    {
        var em = Core.EntityManager;

        if (prisonerEntity.Has<BloodConsumeSource>())
        {
            var blood = em.GetComponentData<BloodConsumeSource>(prisonerEntity);
            blood.UnitBloodType._Value = new PrefabGUID((int)type);
            blood.BloodQuality = 100;
            blood.CanBeConsumed = true;
            em.SetComponentData(prisonerEntity, blood);
        }

        if (prisonerEntity.Has<BehaviourTreeState>())
        {
            var behaviourTreeState = prisonerEntity.Read<BehaviourTreeState>();
            behaviourTreeState.Value = GenericEnemyState.Imprisoned;
            prisonerEntity.Write(behaviourTreeState);
        }

        if (prisonerEntity.Has<BehaviourTreeStateMetadata>())
        {
            var behaviourTreeStateMetadata = prisonerEntity.Read<BehaviourTreeStateMetadata>();
            behaviourTreeStateMetadata.PreviousState = GenericEnemyState.Imprisoned;
            prisonerEntity.Write(behaviourTreeStateMetadata);
        }

        if (!prisonerEntity.Has<Imprisoned>())
            prisonerEntity.Add<Imprisoned>();

        prisonerEntity.Write(new Imprisoned
        {
            PrisonCellEntity = prisonCellEntity
        });

        var prisonCellData = prisonCellEntity.Read<PrisonCell>();
        prisonCellData.ImprisonedEntity = prisonerEntity;
        prisonCellEntity.Write(prisonCellData);

        if (prisonCellEntity.Has<Prisonstation>())
        {
            var prisonStation = prisonCellEntity.Read<Prisonstation>();
            prisonStation.HasPrisoner = true;
            prisonCellEntity.Write(prisonStation);
        }

        BuffService.AddBuff(senderUserEntity, prisonerEntity, ImprisonedBuffPrefab, -1, true);
    }

    private static bool TryFindClosestEmptyOwnedPrisonCell(Entity senderCharacterEntity, float radius, out Entity prisonCellEntity, out float3 prisonCellPosition, out string reason)
    {
        prisonCellEntity = Entity.Null;
        prisonCellPosition = default;
        reason = "No empty owned prison cell found";

        var em = Core.EntityManager;
        if (!em.HasComponent<LocalToWorld>(senderCharacterEntity))
        {
            reason = "Cannot find player position";
            return false;
        }

        if (!em.HasComponent<Team>(senderCharacterEntity))
        {
            reason = "Cannot read player team";
            return false;
        }

        var playerPosition = em.GetComponentData<LocalToWorld>(senderCharacterEntity).Position;
        var playerTeam = em.GetComponentData<Team>(senderCharacterEntity).Value;
        float bestDistanceSq = radius * radius;

        var cells = Helper.GetEntitiesByComponentTypes<PrisonCell, LocalToWorld>();
        try
        {
            foreach (var cell in cells)
            {
                if (!em.Exists(cell) || !em.HasComponent<Team>(cell))
                    continue;

                if (em.GetComponentData<Team>(cell).Value != playerTeam)
                    continue;

                var prisonCellData = em.GetComponentData<PrisonCell>(cell);
                var currentPrisoner = prisonCellData.ImprisonedEntity._Entity;
                if (currentPrisoner != Entity.Null && em.Exists(currentPrisoner))
                    continue;

                var cellPosition = em.GetComponentData<LocalToWorld>(cell).Position;
                var distanceSq = math.distancesq(playerPosition, cellPosition);
                if (distanceSq > bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                prisonCellEntity = cell;
                prisonCellPosition = cellPosition;
            }
        }
        finally
        {
            cells.Dispose();
        }

        return prisonCellEntity != Entity.Null;
    }

    private static bool TrySpendCurrency(Entity characterEntity, int amount, out string reason)
    {
        reason = string.Empty;

        try
        {
            if (amount <= 0)
            {
                reason = "Invalid cost";
                return false;
            }

            var (currencyPrefab, currencyName) = GetCurrency();
            int have = Helper.GetItemCountInInventory(characterEntity, currencyPrefab);
            if (have < amount)
            {
                reason = $"Not enough {currencyName} ({have}/{amount})";
                return false;
            }

            if (!Helper.TryRemoveItemsFromInventory(characterEntity, currencyPrefab, amount))
            {
                reason = "Remove items failed";
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            reason = "Exception: " + e.Message;
            return false;
        }
    }

    private static int GetCost(string bloodTypeName)
    {
        var config = GetConfig();
        foreach (var kv in config.BloodCosts)
        {
            if (string.Equals(kv.Key, bloodTypeName, StringComparison.OrdinalIgnoreCase))
                return kv.Value > 0 ? kv.Value : config.DefaultCost;
        }

        return config.DefaultCost;
    }

    private static ConfigSection GetConfig() => ConfigService.GetPrisonerConfig();

    private static void LogPurchase(ulong steamId, string playerName, string bloodType, int cost, bool success, string reason)
    {
        try
        {
            lock (LOG_LOCK)
            {
                Directory.CreateDirectory(CONFIG_DIR);
                bool newFile = !File.Exists(LOG_FILE);

                using var fs = new FileStream(LOG_FILE, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                if (newFile) sw.WriteLine("server_time,steam_id,player_name,blood_type,cost,success,reason");

                sw.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{steamId},{Csv(playerName)},{Csv(bloodType)},{cost},{(success ? "true" : "false")},{Csv(reason)}");
            }
        }
        catch
        {
        }
    }

    private static string Csv(string s)
    {
        if (s == null)
            return "\"\"";

        return $"\"{s.Replace("\"", "\"\"")}\"";
    }
}
