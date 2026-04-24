using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PrisonerBlood.Models;
using ProjectM;
using ProjectM.Shared; 
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PrisonerBlood.Services;

internal static class SellPrisonerService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string LOG_FILE = Path.Combine(CONFIG_DIR, "sellprisoner_log.csv");
    private static readonly object LOG_LOCK = new();

    private const float PrisonCellSearchRadius = 2f;

    public static void Initialize() 
    {
        ConfigService.Initialize();
        InitializeLogFile();
    }

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

    public static (int defaultPrice, Dictionary<string, int> prices) GetPriceSnapshot()
    {
        var config = GetConfig();
        return (config.DefaultPrice, new Dictionary<string, int>(config.BloodPrices, StringComparer.OrdinalIgnoreCase));
    }

    public static void ReplyHelp(Action<string> reply, string warningLine = null)
    {
        var (defaultPrice, prices) = GetPriceSnapshot();
        var config = GetConfig();

        if (!string.IsNullOrWhiteSpace(warningLine))
            reply(warningLine);


        if (!prices.TryGetValue("Rogue", out int roguePrice))
        {
            roguePrice = defaultPrice;
        }

        int examplePrice = (int)(roguePrice * 0.86f);
        
        reply("<color=yellow>Command:</color> <color=green>.sell prisoner</color> or <color=green>.sell ps</color>");
        reply($"<color=yellow>Quality:</color> {config.MinSellableQuality:0}-100%.");
        reply($"<color=yellow>Price:</color> Scales with the prisoner's blood quality.");
        reply($"<color=yellow>Example:</color> rogue <color=white>86%</color> blood quality = <color=white>86%</color> of {roguePrice} = <color=green>{examplePrice}</color>");
        
        var sb = new StringBuilder();
        sb.AppendLine($"<color=yellow>Blood types</color> : <color=yellow>Price</color> <color=#87CEFA>({CurrencyName})</color>");

        var all = Helper.AllowedBloodTypes
            .Select(bt =>
            {
                var name = bt.ToString();
                bool hasOverride = prices.TryGetValue(name, out var jsonCost);
                int price = hasOverride ? jsonCost : defaultPrice;

                return new 
                { 
                    Name = name, 
                    Price = price,
                    IsDefault = !hasOverride 
                };
            })
            .OrderBy(x => x.Price)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var parts = all
            .Select(x => $"{x.Name} : {x.Price}")
            .ToList();

        const int chunkSize = 3;
        
        foreach (var chunk in parts.Chunk(chunkSize))
        {
            sb.AppendLine(string.Join(" <color=white>|</color> ", chunk));
        }
        
        reply(sb.ToString().TrimEnd());
        reply($"<color=yellow>Stand within {PrisonCellSearchRadius:0.0}m of the prisoner you want to sell.</color>");
    }

    public static void SellPrisoner(Entity senderCharacterEntity, ulong steamId, string playerName, Action<string> reply)
    {
        if (!IsEnabled())
        {
            reply("<color=yellow>Sell Prisoner:</color> <color=red>Disabled.</color>");
            return;
        }

        var em = Core.EntityManager;
        if (senderCharacterEntity == Entity.Null || !em.Exists(senderCharacterEntity))
        {
            reply("<color=red>Character not ready.</color>");
            return;
        }

        if (Helper.IsInCombat(senderCharacterEntity))
        {
            reply("<color=red>You cannot sell a prisoner while in combat.</color>");
            return;
        }

        if (!TryFindClosestOccupiedOwnedPrisonCell(senderCharacterEntity, PrisonCellSearchRadius, out var prisonCellEntity, out var prisonerEntity, out string cellLogReason, out string cellReplyMessage))
        {
            LogSale(steamId, playerName, "unknown", 0f, 0, false, cellLogReason);
            reply(cellReplyMessage);
            return;
        }

        var config = GetConfig();
        if (!TryGetValidBloodData(prisonerEntity, config.MinSellableQuality, out string bloodTypeName, out float bloodQuality, out string bloodLogReason, out string bloodReplyMessage))
        {
            LogSale(steamId, playerName, "unknown", bloodQuality, 0, false, bloodLogReason);
            reply(bloodReplyMessage);
            return;
        }

        int emptySlots = Helper.GetEmptyInventorySlotsCount(senderCharacterEntity);
        if (emptySlots < 2)
        {
            LogSale(steamId, playerName, bloodTypeName, bloodQuality, 0, false, "not_enough_inventory_slots");
            reply($"<color=yellow>Failed!</color> Not enough inventory slots.");
            return;
        }

        var (currencyPrefab, currencyName) = GetCurrency();
        int sellPrice = GetSellPrice(bloodTypeName, bloodQuality);

        try
        {

            if (em.HasComponent<DropTable>(prisonerEntity)) 
            {
                em.RemoveComponent<DropTable>(prisonerEntity);
            }

            DestroyUtility.Destroy(em, prisonerEntity, DestroyDebugReason.TryRemoveBuff);

            Helper.AddItemToInventory(senderCharacterEntity, currencyPrefab, sellPrice);

            LogSale(steamId, playerName, bloodTypeName, bloodQuality, sellPrice, true, "successful");
            
            reply($"<color=green>Success!</color> You received <color=#87CEFA>{sellPrice} {currencyName}</color> for selling a <color=green>{bloodQuality:0}%</color> <color=yellow>{bloodTypeName}</color> prisoner.");
        }
        catch (Exception ex)
        {
            LogSale(steamId, playerName, bloodTypeName, bloodQuality, 0, false, "error_during_deletion: " + ex.Message);
            reply("<color=red>Error occurred while trying to sell the prisoner.</color>");
            Core.LogException(ex);
        }
    }

    private static bool TryFindClosestOccupiedOwnedPrisonCell(Entity senderCharacterEntity, float radius, out Entity prisonCellEntity, out Entity prisonerEntity, out string cellLogReason, out string cellReplyMessage)
    {
        prisonCellEntity = Entity.Null;
        prisonerEntity = Entity.Null;
        
        cellLogReason = "no_occupied_owned_prison_cell_found";
        cellReplyMessage = $"<color=yellow>Failed!</color> No occupied prison cell you own found within {PrisonCellSearchRadius:0.0}m.";

        var em = Core.EntityManager;
        if (!em.HasComponent<LocalToWorld>(senderCharacterEntity))
        {
            cellLogReason = "cannot_find_player_position";
            cellReplyMessage = "<color=yellow>Failed!</color> Cannot find player position.";
            return false;
        }

        if (!em.HasComponent<Team>(senderCharacterEntity))
        {
            cellLogReason = "cannot_read_player_team";
            cellReplyMessage = "<color=yellow>Failed!</color> Cannot read player team.";
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
                if (currentPrisoner == Entity.Null || !em.Exists(currentPrisoner))
                    continue;

                var cellPosition = em.GetComponentData<LocalToWorld>(cell).Position;
                var distanceSq = math.distancesq(playerPosition, cellPosition);
                if (distanceSq > bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                prisonCellEntity = cell;
                prisonerEntity = currentPrisoner;
            }
        }
        finally
        {
            cells.Dispose();
        }

        bool found = prisonCellEntity != Entity.Null;
        
        if (found)
        {
            cellLogReason = string.Empty;
            cellReplyMessage = string.Empty;
        }

        return found;
    }

    private static bool TryGetValidBloodData(Entity prisonerEntity, float minQuality, out string bloodTypeName, out float bloodQuality, out string bloodLogReason, out string bloodReplyMessage)
    {
        bloodTypeName = "unknown";
        bloodQuality = 0f;
        bloodLogReason = string.Empty;
        bloodReplyMessage = string.Empty;

        var em = Core.EntityManager;
        if (!em.HasComponent<BloodConsumeSource>(prisonerEntity))
        {
            bloodLogReason = "cannot_read_prisoner_data";
            bloodReplyMessage = "<color=red>Failed!</color> Cannot read prisoner data.";
            return false;
        }

        var bloodData = em.GetComponentData<BloodConsumeSource>(prisonerEntity);

        bloodQuality = (float)math.floor(bloodData.BloodQuality);   // math.floor เพื่อตัดเศษทศนิยมของเลือดทิ้ง

        if (bloodQuality < minQuality)
        {
            bloodLogReason = $"blood_quality_too_low_{bloodQuality:0}%";
            bloodReplyMessage = $"<color=yellow>Failed!</color> Cannot sell <color=red>{bloodQuality:0}%</color> blood quality. Minimum quality is <color=green>{minQuality:0}%</color>.";
            return false;
        }

        var bloodGuid = bloodData.UnitBloodType._Value.GuidHash;
        foreach (BloodType bt in Enum.GetValues(typeof(BloodType)))
        {
            if (new PrefabGUID((int)bt).GuidHash == bloodGuid)
            {
                bloodTypeName = bt.ToString();
                break;
            }
        }

        return true;
    }

    private static int GetSellPrice(string bloodTypeName, float bloodQuality)
    {
        var config = GetConfig();
        int sellPrice = config.DefaultPrice;

        foreach (var kv in config.BloodPrices)
        {
            if (string.Equals(kv.Key, bloodTypeName, StringComparison.OrdinalIgnoreCase))
            {
                sellPrice = kv.Value > 0 ? kv.Value : config.DefaultPrice;
                break;
            }
        }

        return math.max(1, (int)(sellPrice * (bloodQuality / 100f)));
    }

    private static SellSection GetConfig() => ConfigService.GetSellPrisonerConfig();

    private static void InitializeLogFile()
    {
        try
        {
            lock (LOG_LOCK)
            {
                Directory.CreateDirectory(CONFIG_DIR);

                if (!File.Exists(LOG_FILE))
                {
                    using var fs = new FileStream(LOG_FILE, FileMode.Create, FileAccess.Write, FileShare.Read);
                    using var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    
                    sw.WriteLine("server_time,steam_id,player_name,blood_type,blood_quality,sell_price,success,reason");
                }
            }
        }
        catch (Exception ex)
        {
            Core.LogException(ex);
        }
    }

    private static void LogSale(ulong steamId, string playerName, string bloodType, float bloodQuality, int sellPrice, bool success, string reason)
    {
        try
        {
            lock (LOG_LOCK)
            {
                using var fs = new FileStream(LOG_FILE, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                sw.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{steamId},{Csv(playerName)},{Csv(bloodType)},{bloodQuality:0},{sellPrice},{(success ? "true" : "false")},{Csv(reason)}");
                
            }
        }
        catch (Exception ex)
        {
            Core.LogException(ex);
        }
    }

    private static string Csv(string s)
    {
        if (s == null)
            return "\"\"";

        return $"\"{s.Replace("\"", "\"\"")}\"";
    }
}
