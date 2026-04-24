using System.Collections.Generic;

namespace PrisonerBlood.Models;

internal sealed class BuySection
{
    public bool Enabled { get; set; } = true;
    public int CurrencyPrefab { get; set; }
    public string CurrencyName { get; set; } = string.Empty;
    public int DefaultCost { get; set; }
    public Dictionary<string, int> BloodCosts { get; set; } = new();
}

internal sealed class SellSection
{
    public bool Enabled { get; set; } = true;
    public float MinSellableQuality { get; set; }
    public int CurrencyPrefab { get; set; }
    public string CurrencyName { get; set; } = string.Empty;
    public int DefaultPrice { get; set; }
    public Dictionary<string, int> BloodPrices { get; set; } = new();
}
