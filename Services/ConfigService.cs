using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PrisonerBlood.Models;

namespace PrisonerBlood.Services;

internal static class ConfigService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string BUY_CONFIG_FILE = Path.Combine(CONFIG_DIR, BUY_CONFIG_FILE_NAME);
    private static readonly string SELL_CONFIG_FILE = Path.Combine(CONFIG_DIR, SELL_CONFIG_FILE_NAME);

    public const string BUY_CONFIG_FILE_NAME = "buyconfig.json";
    public const string SELL_CONFIG_FILE_NAME = "sellconfig.json";

    private static readonly object IO_LOCK = new();

    private static readonly BuyConfigRoot _defaultBuyRoot = CreateDefaultBuyRoot();
    private static readonly SellConfigRoot _defaultSellRoot = CreateDefaultSellRoot();

    private static DateTime _buylastWrite = DateTime.MinValue;
    private static BuyConfigRoot _buyroot = new();

    private static DateTime _sellLastWrite = DateTime.MinValue;
    private static SellConfigRoot _sellRoot = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    public static void Initialize() 
    {
        LoadBuy(force: true);
        LoadSell(force: true);
    }
    
    public static void Reload() 
    {
        LoadBuy(force: true);
        LoadSell(force: true);
    }

    public static BuySection GetBuyPrisonerConfig()
    {
        LoadBuy(force: false);
        lock (IO_LOCK)
            return NormalizeBuy(_buyroot.Prisoner, _defaultBuyRoot.Prisoner);
    }

    public static BuySection GetBuyBloodPotionConfig()
    {
        LoadBuy(force: false);
        lock (IO_LOCK)
            return NormalizeBuy(_buyroot.BloodPotion, _defaultBuyRoot.BloodPotion);
    }

    public static SellSection GetSellPrisonerConfig()
    {
        LoadSell(force: false);
        lock (IO_LOCK)
            return NormalizeSell(_sellRoot.Prisoner, _defaultSellRoot.Prisoner);
    }

    private static void LoadBuy(bool force)
    {
        lock (IO_LOCK)
        {
            try
            {
                Directory.CreateDirectory(CONFIG_DIR);

                if (!File.Exists(BUY_CONFIG_FILE))
                {
                    _buyroot = _defaultBuyRoot;
                    File.WriteAllText(BUY_CONFIG_FILE, JsonSerializer.Serialize(_buyroot, JsonOptions));
                    _buylastWrite = File.GetLastWriteTime(BUY_CONFIG_FILE);
                    return;
                }

                var writeTime = File.GetLastWriteTime(BUY_CONFIG_FILE);
                if (!force && writeTime <= _buylastWrite)
                    return;

                var json = File.ReadAllText(BUY_CONFIG_FILE);
                _buyroot = JsonSerializer.Deserialize<BuyConfigRoot>(json, JsonOptions) ?? _defaultBuyRoot;
                _buylastWrite = writeTime;
            }
            catch (Exception e)
            {
                Core.Log.LogError($"[ConfigService] Failed to load {BUY_CONFIG_FILE_NAME}: {e}");
                _buyroot = _defaultBuyRoot;
            }
        }
    }

    private static void LoadSell(bool force)
    {
        lock (IO_LOCK)
        {
            try
            {
                Directory.CreateDirectory(CONFIG_DIR);

                if (!File.Exists(SELL_CONFIG_FILE))
                {
                    _sellRoot = _defaultSellRoot;
                    File.WriteAllText(SELL_CONFIG_FILE, JsonSerializer.Serialize(_sellRoot, JsonOptions));
                    _sellLastWrite = File.GetLastWriteTime(SELL_CONFIG_FILE);
                    return;
                }

                var writeTime = File.GetLastWriteTime(SELL_CONFIG_FILE);
                if (!force && writeTime <= _sellLastWrite)
                    return;

                var json = File.ReadAllText(SELL_CONFIG_FILE);
                _sellRoot = JsonSerializer.Deserialize<SellConfigRoot>(json, JsonOptions) ?? _defaultSellRoot;                
                _sellLastWrite = writeTime;
            }
            catch (Exception e)
            {
                Core.Log.LogError($"[ConfigService] Failed to load {SELL_CONFIG_FILE_NAME}: {e}");
                _sellRoot = _defaultSellRoot;
            }
        }
    }
        
    private static BuySection NormalizeBuy(BuySection config, BuySection defaults)
    {
        config ??= new BuySection();
        bool hasUserCosts = config.BloodCosts != null;
        var costs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (hasUserCosts)
        {
            foreach (var kv in config.BloodCosts)
                if (kv.Value > 0) costs[kv.Key.Trim()] = kv.Value;
        }
        return new BuySection {
            Enabled = config.Enabled,
            CurrencyPrefab = config.CurrencyPrefab != 0 ? config.CurrencyPrefab : defaults.CurrencyPrefab,
            CurrencyName = string.IsNullOrWhiteSpace(config.CurrencyName) ? defaults.CurrencyName : config.CurrencyName.Trim(),
            DefaultCost = config.DefaultCost > 0 ? config.DefaultCost : defaults.DefaultCost,
            BloodCosts = hasUserCosts ? costs : new Dictionary<string, int>(defaults.BloodCosts, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static SellSection NormalizeSell(SellSection config, SellSection defaults)
    {
        config ??= new SellSection();
        bool hasUserPrices = config.BloodPrices != null;
        var prices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (hasUserPrices)
        {
            foreach (var kv in config.BloodPrices)
                if (kv.Value > 0) prices[kv.Key.Trim()] = kv.Value;
        }
        return new SellSection {
            Enabled = config.Enabled,
            MinSellableQuality = config.MinSellableQuality > 0 ? config.MinSellableQuality : defaults.MinSellableQuality,
            CurrencyPrefab = config.CurrencyPrefab != 0 ? config.CurrencyPrefab : defaults.CurrencyPrefab,
            CurrencyName = string.IsNullOrWhiteSpace(config.CurrencyName) ? defaults.CurrencyName : config.CurrencyName.Trim(),
            DefaultPrice = config.DefaultPrice > 0 ? config.DefaultPrice : defaults.DefaultPrice,
            BloodPrices = hasUserPrices ? prices : new Dictionary<string, int>(defaults.BloodPrices, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static BuyConfigRoot CreateDefaultBuyRoot()
    {
        return new BuyConfigRoot
        {
            Prisoner = new BuySection
            {
                Enabled = true,
                CurrencyPrefab = 576389135,
                CurrencyName = "Greater Stygian Shards",
                DefaultCost = 5000,
                BloodCosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Worker", 4000 },
                    { "Creature", 4200 },
                    { "Mutant", 4500 },
                    { "Corrupted", 4800 },
                    { "Draculin", 5000 },
                    { "Warrior", 5200 },
                    { "Rogue", 5500 },
                    { "Brute", 5700 },
                    { "Scholar", 6000 }
                }
            },
            BloodPotion = new BuySection
            {
                Enabled = true,
                CurrencyPrefab = 576389135,
                CurrencyName = "Greater Stygian Shards",
                DefaultCost = 500,
                BloodCosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Worker", 300 },
                    { "Creature", 350 },
                    { "Mutant", 400 },
                    { "Corrupted", 450 },
                    { "Draculin", 500 },
                    { "Warrior", 550 },
                    { "Rogue", 600 },
                    { "Brute", 650 },
                    { "Scholar", 700 }
                }
            }
        };
    }

    private static SellConfigRoot CreateDefaultSellRoot()
    {
        return new SellConfigRoot
        {
            Prisoner = new SellSection
            {
                Enabled = true,
                MinSellableQuality = 80f,
                CurrencyPrefab = 576389135,
                CurrencyName = "Greater Stygian Shards",
                DefaultPrice = 2500,
                BloodPrices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Worker", 2000 },
                    { "Creature", 2100 },
                    { "Mutant", 2250 },
                    { "Corrupted", 2400 },
                    { "Draculin", 2500 },
                    { "Warrior", 2600 },
                    { "Rogue", 2750 },
                    { "Brute", 2850 },
                    { "Scholar", 3000 }
                }
            }
        };
    }
}
