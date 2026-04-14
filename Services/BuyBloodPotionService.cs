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

        reply("<color=yellow>Command:</color> <color=green>.buy bp <BloodType></color>");
        reply("<color=yellow>Example:</color> <color=green>.buy bp rogue</color>");

        var sb = new StringBuilder();
        sb.AppendLine($"<color=yellow>Blood types</color> : <color=yellow>Costs</color> (<color=#87CEFA>{CurrencyName}</color>)");

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
        reply("<color=yellow>You will receive 1 Blood Merlot with <color=green>100%</color> blood quality.</color>");
    }

    public static void BuyBloodPotion(Entity senderCharacterEntity, ulong steamId, string playerName, BloodType type, Action<string> reply)
    {
        if (!IsEnabled())
        {
            reply("<color=yellow>Buy Blood Potion:</color> <color=red>Disabled.</color>");
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
        var (currencyPrefab, _) = GetCurrency();

        if (!TrySpendCurrency(senderCharacterEntity, cost, out var reason))
        {       
            LogPurchase(steamId, playerName, type.ToString(), 0, false, reason);
            reply($"<color=yellow>Unsuccessful!</color> {reason}.");
            return;
        }

        var potionEntity = Helper.AddItemToInventory(senderCharacterEntity, BloodPotionPrefab, 1);
        if (potionEntity == Entity.Null || !em.Exists(potionEntity))
        {
            Helper.AddItemToInventory(senderCharacterEntity, currencyPrefab, cost);
            LogPurchase(steamId, playerName, type.ToString(), 0, false, "Inventory is full");
            reply("<color=yellow>Unsuccessful!</color> Inventory is full.");
            return;
        }

        try
        {
            var blood = new StoredBlood
            {
                BloodQuality = 100f,
                PrimaryBloodType = new PrefabGUID((int)type)
            };

            em.SetComponentData(potionEntity, blood);

            LogPurchase(steamId, playerName, type.ToString(), cost, true, "Successful");
            reply("<color=green>Success!</color> A blood potion was added to your inventory.");

            /*
            var broadcastMessage =
                $"<color=white>{playerName}</color> just bought a <color=green>100%</color> {type} blood potion. " +
                "Learn more, type <color=green>.buy bloodpotion help</color>";

            Helper.BroadcastSystemMessage(broadcastMessage);
            */
        }
        catch (Exception ex)
        {
            LogPurchase(steamId, playerName, type.ToString(), cost, false, "Set stored blood failed: " + ex.Message);
            reply("<color=red>Blood potion was added, but setting the blood data failed.</color>");
            Core.LogException(ex);
        }
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

    private static ConfigSection GetConfig() => ConfigService.GetBloodPotionConfig();

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
