using System.Collections.Generic;

namespace PrisonerBlood.Models;

internal sealed class ConfigSection
{
    public bool Enabled { get; set; } = true;
    public int CurrencyPrefab { get; set; }
    public string CurrencyName { get; set; } = string.Empty;
    public int DefaultCost { get; set; }
    public Dictionary<string, int> BloodCosts { get; set; } = new();
}