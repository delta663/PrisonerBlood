using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PrisonerBlood.Models;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;

namespace PrisonerBlood.Services;

internal static class BuyBloodPotionService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string LOG_FILE = Path.Combine(CONFIG_DIR, "buybloodpotion_log.csv");
    private static readonly object LOG_LOCK = new();

    private static readonly PrefabGUID BloodPotionPrefab = new(1223264867);

    public static void Initialize() 
    {
        ConfigService.Initialize();
        CheckAndMigrateLogFile();
        InitializeLogFile(); 
    }
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

        reply("<color=yellow>Command:</color> <color=green>.buy bloodpotion <BloodType> (Amount)</color>");
        reply("<color=yellow>Example:</color> <color=green>.buy bloodpotion rogue 1</color> or <color=green>.buy bp rogue</color>");

        var sb = new StringBuilder();
        sb.AppendLine($"<color=yellow>Blood types</color> : <color=yellow>Costs</color> <color=#87CEFA>({CurrencyName})</color>");

        var all = Helper.AllowedBloodTypes
            .Select(bt =>
            {
                var name = bt.ToString();

                bool hasOverride = prices.TryGetValue(name, out var jsonCost);
                int cost = hasOverride ? jsonCost : defaultCost;

                return new
                {
                    Name = name,
                    Cost = cost,
                    IsDefault = !hasOverride
                };
            })
            .OrderBy(x => x.Cost)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var parts = all
            .Select(x => $"{x.Name} : {x.Cost}")
            .ToList();

        const int chunkSize = 3;

        foreach (var chunk in parts.Chunk(chunkSize))
        {
            sb.AppendLine(string.Join(" <color=white>|</color> ", chunk));
        }

        reply(sb.ToString().TrimEnd());
        reply("<color=yellow>Blood Merlot with <color=green>100%</color> blood quality will be added to your inventory.</color>");
    }

    public static void BuyBloodPotion(Entity senderCharacterEntity, ulong steamId, string playerName, BloodType type, int quantity, Action<string> reply)
    {
        if (!IsEnabled())
        {
            reply("<color=yellow>Buy Blood Potion:</color> <color=red>Disabled.</color>");
            return;
        }

        if (quantity < 1) quantity = 1;

        var em = Core.EntityManager;
        if (senderCharacterEntity == Entity.Null || !em.Exists(senderCharacterEntity))
        {
            reply("<color=red>Character not ready.</color>");
            return;
        }

        if (Helper.IsInCombat(senderCharacterEntity))
        {
            reply("<color=red>You cannot buy a blood potion while in combat.</color>");
            return;
        }

        int emptySlots = Helper.GetEmptyInventorySlotsCount(senderCharacterEntity);
        if (emptySlots < quantity)
        {
            LogPurchase(steamId, playerName, type.ToString(), quantity, 0, false, $"not_enough_inventory_slots_{emptySlots}/{quantity}");
            reply($"<color=yellow>Failed!</color> Not enough inventory slots ({emptySlots}/{quantity}).");
            return;
        }

        int unitCost = GetCost(type.ToString());
        int totalCost = unitCost * quantity;
        if (!TrySpendCurrency(senderCharacterEntity, totalCost, out string spendLogReason, out string spendReplyMessage))
        {       
            LogPurchase(steamId, playerName, type.ToString(), quantity, totalCost, false, spendLogReason);
            reply(spendReplyMessage);
            return;
        }

        int successfulItems = 0;
        for (int i = 0; i < quantity; i++)
        {
            var potionEntity = Helper.AddItemToInventory(senderCharacterEntity, BloodPotionPrefab, 1);
        
            if (potionEntity != Entity.Null && em.Exists(potionEntity))
            {
                try
                {
                    var blood = new StoredBlood
                    {
                        BloodQuality = 100f,
                        PrimaryBloodType = new PrefabGUID((int)type)
                    };
                    em.SetComponentData(potionEntity, blood);
                    successfulItems++;
                }
                catch (Exception ex)
                {
                    Core.LogException(ex);
                }
            }
        }

        LogPurchase(steamId, playerName, type.ToString(), successfulItems, totalCost, true, "successful");

        string pluralText = successfulItems > 1 ? "s were" : " was";
        reply($"<color=green>Success!</color> {successfulItems} blood potion{pluralText} added to your inventory.");
    }

    private static bool TrySpendCurrency(Entity characterEntity, int amount, out string spendLogReason, out string spendReplyMessage)
    {
        spendLogReason = string.Empty;
        spendReplyMessage = string.Empty;

        try
        {
            if (amount <= 0)
            {
                spendLogReason = "invalid_cost";
                spendReplyMessage = "<color=yellow>Failed!</color> Invalid cost";
                return false;
            }

            var (currencyPrefab, currencyName) = GetCurrency();
            int have = Helper.GetItemCountInInventory(characterEntity, currencyPrefab);
            if (have < amount)
            {
                spendLogReason = $"not_enough_currency_{have}/{amount}";
                spendReplyMessage = $"<color=yellow>Failed!</color> Not enough {currencyName} ({have}/{amount})";
                return false;
            }

            if (!Helper.TryRemoveItemsFromInventory(characterEntity, currencyPrefab, amount))
            {
                spendLogReason = "remove_items_failed";
                spendReplyMessage = $"<color=red>Failed!</color> Remove items failed";
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            spendLogReason = "exception: " + e.Message;
            spendReplyMessage = "<color=red>Error!</color> An unexpected error occurred while spending currency.";
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

    private static BuySection GetConfig() => ConfigService.GetBuyBloodPotionConfig();

    private static void CheckAndMigrateLogFile()
    {
        try
        {
            lock (LOG_LOCK)
            {
                Directory.CreateDirectory(CONFIG_DIR);

                if (File.Exists(LOG_FILE))
                {
                    string header = null;
                    using (var fsRead = new FileStream(LOG_FILE, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fsRead))
                    {
                        header = sr.ReadLine();
                    }

                    if (!string.IsNullOrEmpty(header) && !header.Contains("quantity"))
                    {
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string backupFileName = Path.Combine(CONFIG_DIR, $"buybloodpotion_log_old_{timestamp}.csv");
                        
                        File.Move(LOG_FILE, backupFileName);
                        
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.LogException(ex);
        }
    }

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
                    
                    sw.WriteLine("server_time,steam_id,player_name,blood_type,quantity,cost,success,reason");
                }
            }
        }
        catch (Exception ex)
        {
            Core.LogException(ex);
        }
    }

    private static void LogPurchase(ulong steamId, string playerName, string bloodType, int quantity, int cost, bool success, string reason)
    {
        try
        {
            lock (LOG_LOCK)
            {
                using var fs = new FileStream(LOG_FILE, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                sw.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{steamId},{Csv(playerName)},{Csv(bloodType)},{quantity},{cost},{(success ? "true" : "false")},{Csv(reason)}");
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
