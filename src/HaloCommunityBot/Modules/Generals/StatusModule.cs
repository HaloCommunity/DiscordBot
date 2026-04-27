using Discord;
using Discord.Interactions;
using DiscordBot.Models;
using DiscordBot.Services;
using System.Xml.Linq;

namespace DiscordBot.Modules.Generals;

public class StatusModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BotConfig _config;

    public StatusModule(BotConfig config)
    {
        _config = config;
    }

    [SlashCommand("status", "Show current Halo services status overview")]
    public async Task StatusAsync(
        [Summary("private", "Return the status only to you (ephemeral)")] bool @private = false)
    {
        await DeferAsync(ephemeral: @private);

        var feedUrl = string.IsNullOrWhiteSpace(_config.StatusMonitor.FeedUrl)
            ? "https://status.haloservicesolutions.com/pages/63ef45da7ee94905308a1a4a/rss"
            : _config.StatusMonitor.FeedUrl;

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HaloCommunityBot/1.0");

            var xml = await httpClient.GetStringAsync(feedUrl);
            var doc = XDocument.Parse(xml);
            var items = doc.Descendants("item").Take(5).ToList();

            if (items.Count == 0)
            {
                await FollowupAsync("No status items are currently available from the feed.", ephemeral: @private);
                return;
            }

            var latestItem = items[0];
            var latestTitle = latestItem.Element("title")?.Value?.Trim() ?? "Halo Services Status";
            var latestDescription = HaloStatusFormatting.StripHtmlAndDecode(latestItem.Element("description")?.Value ?? string.Empty, 1024);
            var latestLink = latestItem.Element("link")?.Value?.Trim() ?? string.Empty;

            var (color, emoji) = HaloStatusFormatting.DetermineStatusAppearance(latestTitle);

            var lines = new List<string>();
            foreach (var item in items)
            {
                var title = item.Element("title")?.Value?.Trim() ?? "Status update";
                var link = item.Element("link")?.Value?.Trim() ?? string.Empty;
                var (_, itemEmoji) = HaloStatusFormatting.DetermineStatusAppearance(title);

                if (!string.IsNullOrEmpty(link))
                    lines.Add($"{itemEmoji} [{title}]({link})");
                else
                    lines.Add($"{itemEmoji} {title}");
            }

            var embed = new EmbedBuilder()
                .WithTitle($"{emoji} Halo Services Status Overview")
                .WithColor(color)
                .WithUrl("https://status.haloservicesolutions.com")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithFooter("Source: status.haloservicesolutions.com")
                .AddField("Latest Update", string.IsNullOrWhiteSpace(latestLink) ? latestTitle : $"[{latestTitle}]({latestLink})", false)
                .AddField("Recent Items", string.Join("\n", lines), false);

            if (!string.IsNullOrWhiteSpace(latestDescription))
                embed.AddField("Latest Details", latestDescription, false);

            await FollowupAsync(embed: embed.Build(), ephemeral: @private);
        }
        catch (Exception)
        {
            await FollowupAsync($"Unable to retrieve status feed from {feedUrl} right now. Please try again in a moment.", ephemeral: @private);
        }
    }
}
