using System;
using PrisonerBlood.Models;
using PrisonerBlood.Services;
using ProjectM.Network;
using VampireCommandFramework;

namespace PrisonerBlood.Commands;

[CommandGroup("buy")]
internal static class BuyCommands
{
    [Command("prisoner", shortHand: "ps", adminOnly: false, description: "Buy a prisoner with 100% blood quality and spawn it in a nearby empty prison cell.")]
    public static void BuyPrisoner(ChatCommandContext ctx, string bloodTypeArg = "")
    {
        bloodTypeArg = (bloodTypeArg ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(bloodTypeArg) || bloodTypeArg.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            BuyPrisonerService.ReplyHelp(ctx.Reply);
            return;
        }

        if (!BuyPrisonerService.TryParseBloodTypeStrict(bloodTypeArg, out BloodType bloodType))
        {
            BuyPrisonerService.ReplyHelp(ctx.Reply, "<color=yellow>Invalid blood type.</color>");
            return;
        }

        var user = ctx.Event.SenderUserEntity.Read<User>();
        BuyPrisonerService.BuyPrisoner(
            ctx.Event.SenderUserEntity,
            ctx.Event.SenderCharacterEntity,
            user.PlatformId,
            user.CharacterName.ToString(),
            bloodType,
            ctx.Reply
        );
    }

    [Command("bloodpotion", shortHand: "bp", adminOnly: false, description: "Buy a 100% Blood Merlot potion.")]
    public static void BuyBloodPotion(ChatCommandContext ctx, string bloodTypeArg = "", int quantity = 1)
    {
        bloodTypeArg = (bloodTypeArg ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(bloodTypeArg) || bloodTypeArg.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            BuyBloodPotionService.ReplyHelp(ctx.Reply);
            return;
        }

        if (!BuyBloodPotionService.TryParseBloodTypeStrict(bloodTypeArg, out BloodType bloodType))
        {
            BuyBloodPotionService.ReplyHelp(ctx.Reply, "<color=yellow>Invalid blood type.</color>");
            return;
        }

        var user = ctx.Event.SenderUserEntity.Read<User>();
        
        BuyBloodPotionService.BuyBloodPotion(
            ctx.Event.SenderCharacterEntity,
            user.PlatformId,
            user.CharacterName.ToString(),
            bloodType,
            quantity,
            ctx.Reply
        );
    }

    [Command("reload", shortHand: "rl", adminOnly: true, description: "Reload buyconfig.json and sellconfig.json.")]
    public static void Reload(ChatCommandContext ctx)
    {
        ConfigService.Reload();
        ctx.Reply($"<color=green>{ConfigService.BUY_CONFIG_FILE_NAME} and {ConfigService.SELL_CONFIG_FILE_NAME} reloaded.</color>");
    }
}
