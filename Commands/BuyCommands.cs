using System;
using PrisonerBlood.Models;
using PrisonerBlood.Services;
using ProjectM.Network;
using VampireCommandFramework;

namespace PrisonerBlood.Commands;

[CommandGroup("buy")]
internal static class BuyCommands
{
    [Command("prisoner", shortHand: "ps", adminOnly: false, description: "Buy a prisoner with 100% blood quality into a nearby empty prison cell.")]
    public static void BuyPrisoner(ChatCommandContext ctx, string arg = "")
    {
        arg = (arg ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(arg) || arg.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            BuyPrisonerService.ReplyHelp(ctx.Reply);
            return;
        }

        if (!BuyPrisonerService.TryParseBloodTypeStrict(arg, out BloodType bloodType))
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

    [Command("bloodpotion", shortHand: "bp", adminOnly: false, description: "Buy a 100% Blood Merlot potion into your inventory.")]
    public static void BuyBloodPotion(ChatCommandContext ctx, string arg = "")
    {
        arg = (arg ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(arg) || arg.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            BuyBloodPotionService.ReplyHelp(ctx.Reply);
            return;
        }

        if (!BuyBloodPotionService.TryParseBloodTypeStrict(arg, out BloodType bloodType))
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
            ctx.Reply
        );
    }

    [Command("reload", shortHand: "rl", adminOnly: true, description: "Reload buyconfig.json.")]
    public static void Reload(ChatCommandContext ctx)
    {
        ConfigService.Reload();
        ctx.Reply("<color=green>buyconfig.json reloaded.</color>");
    }
}
