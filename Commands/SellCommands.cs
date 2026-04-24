using System;
using PrisonerBlood.Services;
using ProjectM.Network;
using VampireCommandFramework;

namespace PrisonerBlood.Commands;

[CommandGroup("sell")]
internal static class SellCommands
{
    [Command("prisoner", shortHand: "ps", adminOnly: false, description: "Sell a prisoner from a nearby prison cell.")]
    public static void SellPrisoner(ChatCommandContext ctx, string args = "")
    {
        args = (args ?? string.Empty).Trim();

        if (args.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            SellPrisonerService.ReplyHelp(ctx.Reply);
            return;
        }

        var user = ctx.Event.SenderUserEntity.Read<User>();
        SellPrisonerService.SellPrisoner(
            ctx.Event.SenderCharacterEntity,
            user.PlatformId,
            user.CharacterName.ToString(),
            ctx.Reply
        );
    }

    [Command("reload", shortHand: "rl", adminOnly: true, description: "Reload sellconfig.json and buyconfig.json.")]
    public static void Reload(ChatCommandContext ctx)
    {
        ConfigService.Reload();
        ctx.Reply($"<color=green>{ConfigService.SELL_CONFIG_FILE_NAME} and {ConfigService.BUY_CONFIG_FILE_NAME} reloaded.</color>");
    }
}
