using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PrisonerBlood.Models;

namespace PrisonerBlood.Services;

internal static class ConfigService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string CONFIG_FILE = Path.Combine(CONFIG_DIR, "buyconfig.json");
    private static readonly object IO_LoCK = new();

    private static DateTime _lastWrite = DateTime.MinValue;
    private static ConfigRoot _root = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    public static string ConfigFileName => "buyconfig.json";

    public static void Initialize() => Load(force: true);
    public static void Reload() => Load(force: true);

    public static ConfigSection GetPrisonerConfig()
    {
        Load(force: false);
        lock (IO_LoCK)
            return CloneAndNormalize(_root.Prisoner, CreateDefaultRoot().Prisoner);
    }

    public static ConfigSection GetBloodPotionConfig()
    {
        Load(force: false);
        lock (IO_LoCK)
            return CloneAndNormalize(_root.BloodPotion, CreateDefaultRoot().BloodPotion);
    }

    private static void Load(bool force)
    {
        lock (IO_LoCK)
        {
            try
            {
                Directory.CreateDirectory(CONFIG_DIR);

                if (!File.Exists(CONFIG_FILE))
                {
                    _root = CreateDefaultRoot();
                    File.WriteAllText(CONFIG_FILE, JsonSerializer.Serialize(_root, JsonOptions));
                    _lastWrite = File.GetLastWriteTime(CONFIG_FILE);
                    return;
                }

                var writeTime = File.GetLastWriteTime(CONFIG_FILE);
                if (!force && writeTime <= _lastWrite)
                    return;

                var json = File.ReadAllText(CONFIG_FILE);
                _root = JsonSerializer.Deserialize<ConfigRoot>(json, JsonOptions) ?? CreateDefaultRoot();
                NormalizeRoot(_root);
                _lastWrite = writeTime;
            }
            catch (Exception e)
            {
                Core.Log.LogError($"[ConfigService] Failed to load {ConfigFileName}: {e}");
                _root = CreateDefaultRoot();
            }
        }
    }

    private static void NormalizeRoot(ConfigRoot root)
    {
        var defaults = CreateDefaultRoot();
        root.Prisoner = CloneAndNormalize(root.Prisoner, defaults.Prisoner);
        root.BloodPotion = CloneAndNormalize(root.BloodPotion, defaults.BloodPotion);
    }

    private static ConfigSection CloneAndNormalize(ConfigSection config, ConfigSection defaults)
    {
        config ??= new ConfigSection();
        defaults ??= new ConfigSection();

        var bloodCosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in config.BloodCosts ?? new Dictionary<string, int>())
        {
            if (!string.IsNullOrWhiteSpace(kv.Key) && kv.Value > 0)
                bloodCosts[kv.Key.Trim()] = kv.Value;
        }

        return new ConfigSection
        {
            Enabled = config.Enabled,
            CurrencyPrefab = config.CurrencyPrefab != 0 ? config.CurrencyPrefab : defaults.CurrencyPrefab,
            CurrencyName = string.IsNullOrWhiteSpace(config.CurrencyName) ? defaults.CurrencyName : config.CurrencyName.Trim(),
            DefaultCost = config.DefaultCost > 0 ? config.DefaultCost : defaults.DefaultCost,
            BloodCosts = bloodCosts.Count > 0 ? bloodCosts : new Dictionary<string, int>(defaults.BloodCosts, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static ConfigRoot CreateDefaultRoot()
    {
        return new ConfigRoot
        {
            Prisoner = new ConfigSection
            {
                Enabled = true,
                CurrencyPrefab = 576389135,
                CurrencyName = "Greater Stygian Shards",
                DefaultCost = 5000,
                BloodCosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Worker", 4000 },
                    { "Creature", 4500 },
                    { "Mutant", 4800 },
                    { "Draculin", 4800 },
                    { "Corrupted", 4800 },
                    { "Warrior", 4800 },
                    { "Rogue", 5000 },
                    { "Brute", 5200 },
                    { "Scholar", 5500 }
                }
            },
            BloodPotion = new ConfigSection
            {
                Enabled = true,
                CurrencyPrefab = 576389135,
                CurrencyName = "Greater Stygian Shards",
                DefaultCost = 500,
                BloodCosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Worker", 300 },
                    { "Creature", 400 },
                    { "Mutant", 450 },
                    { "Draculin", 500 },
                    { "Corrupted", 650 },
                    { "Rogue", 650 },
                    { "Warrior", 700 },
                    { "Brute", 750 },
                    { "Scholar", 800 }
                }
            }
        };
    }
}
