namespace PrisonerBlood.Models;

internal sealed class ConfigRoot
{
    public ConfigSection Prisoner { get; set; } = new();
    public ConfigSection BloodPotion { get; set; } = new();
}
