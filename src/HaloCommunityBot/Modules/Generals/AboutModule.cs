using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;

namespace DiscordBot.Modules.Generals;

public class AboutModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;
    private readonly DiscordBotService _botService;

    public AboutModule(DiscordSocketClient client, DiscordBotService botService)
    {
        _client = client;
        _botService = botService;
    }

    [SlashCommand("about", "About this bot")]
    public async Task AboutAsync()
    {
        var botUser = _client.CurrentUser;
        var uptime = DateTime.UtcNow - _botService.StartTime;

        var embed = new EmbedBuilder()
            .WithTitle("🤖 About this bot")
            .WithThumbnailUrl(botUser.GetAvatarUrl() ?? botUser.GetDefaultAvatarUrl())
            .AddField("Name", botUser.Username, true)
            .AddField("ID", botUser.Id, true)
            .AddField("Created date", botUser.CreatedAt.ToString("dd/MM/yyyy"), true)
            .AddField("Status", _client.Status.ToString(), true)
            .AddField("Uptime", $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s")
            .WithFooter($"Requested by {Context.User.Username}")
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed);
    }
}
