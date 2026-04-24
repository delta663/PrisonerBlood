namespace PrisonerBlood.Models;

internal sealed class BuyConfigRoot
{
    public BuySection Prisoner { get; set; } = new();
    public BuySection BloodPotion { get; set; } = new();
}

internal sealed class SellConfigRoot
{
    public SellSection Prisoner { get; set; } = new();
}
