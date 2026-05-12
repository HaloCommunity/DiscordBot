using Discord;
using Discord.WebSocket;
using DiscordBot.Core.Data;
using DiscordBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Xml.Linq;

namespace DiscordBot.Services;

/// <summary>
/// Background service that polls the Halo Services status RSS feed and posts
/// a Discord embed to a configured channel whenever a new status item appears.
/// </summary>
public class HaloStatusMonitorService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly BotConfig _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HaloStatusMonitorService> _logger;

    public HaloStatusMonitorService(
        DiscordSocketClient client,
        BotConfig config,
        IServiceProvider serviceProvider,
        ILogger<HaloStatusMonitorService> logger)
    {
        _client = client;
        _config = config;
        _serviceProvider = serviceProvider;
        _logger = logger;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HaloCommunityBot/1.0");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var monitorConfig = _config.StatusMonitor;

        if (!monitorConfig.Enabled || monitorConfig.ChannelId == 0)
        {
            _logger.LogInformation("Halo status monitor is disabled or has no channel configured — skipping.");
            return;
        }

        // Wait until the Discord client is fully connected before starting the loop.
        while (_client.ConnectionState != ConnectionState.Connected && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("Halo status monitor started. Polling {FeedUrl} every {Interval} minute(s).",
            monitorConfig.FeedUrl, monitorConfig.PollIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollFeedAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling Halo status feed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(monitorConfig.PollIntervalMinutes), stoppingToken);
        }
    }

    private async Task PollFeedAsync(CancellationToken cancellationToken)
    {
        var xml = await _httpClient.GetStringAsync(_config.StatusMonitor.FeedUrl, cancellationToken);
        var doc = XDocument.Parse(xml);
        var items = doc.Descendants("item").ToList();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HaloCommunityBotContext>();
        var feedKey = $"halo-status:{_config.StatusMonitor.FeedUrl.Trim()}";
        var state = await db.FeedPostStates.FirstOrDefaultAsync(x => x.FeedType == "HaloStatus" && x.SourceId == feedKey, cancellationToken);

        if (state == null)
        {
            state = new FeedPostState
            {
                FeedType = "HaloStatus",
                SourceId = feedKey
            };
            db.FeedPostStates.Add(state);
        }

        if (string.IsNullOrWhiteSpace(state.LastPostedItemId))
        {
            var latestItemId = items.FirstOrDefault() is XElement latestItem ? GetItemId(latestItem) : string.Empty;
            if (!string.IsNullOrWhiteSpace(latestItemId))
            {
                state.LastPostedItemId = latestItemId;
                state.LastCheckedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Halo status monitor initialised — latest feed item {ItemId} stored as baseline.", latestItemId);
            }

            return;
        }

        var pendingItems = GetPendingItems(items, state);
        if (pendingItems.Count == 0)
        {
            state.LastCheckedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        foreach (var item in pendingItems)
        {
            var id = GetItemId(item);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            await PostStatusUpdateAsync(item);

            state.LastPostedItemId = id;
            state.LastCheckedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static List<XElement> GetPendingItems(IReadOnlyList<XElement> items, FeedPostState state)
    {
        if (!string.IsNullOrWhiteSpace(state.LastPostedItemId))
        {
            var lastPostedIndex = items
                .Select((item, index) => new { Item = item, Index = index })
                .FirstOrDefault(entry => string.Equals(GetItemId(entry.Item), state.LastPostedItemId, StringComparison.OrdinalIgnoreCase))
                ?.Index;

            if (lastPostedIndex.HasValue)
            {
                return items.Take(lastPostedIndex.Value).Reverse().ToList();
            }
        }

        if (state.LastCheckedAt.HasValue)
        {
            return items
                .Where(item => TryParsePubDate(item, out var publishedAt) && publishedAt > state.LastCheckedAt.Value)
                .Reverse()
                .ToList();
        }

        return new List<XElement>();
    }

    private static string GetItemId(XElement item)
        => item.Element("guid")?.Value?.Trim()
            ?? item.Element("link")?.Value?.Trim()
            ?? string.Empty;

    private static bool TryParsePubDate(XElement item, out DateTimeOffset publishedAt)
    {
        var pubDateStr = item.Element("pubDate")?.Value;
        if (!string.IsNullOrWhiteSpace(pubDateStr) && DateTimeOffset.TryParse(pubDateStr, out var parsed))
        {
            publishedAt = parsed;
            return true;
        }

        publishedAt = DateTimeOffset.MinValue;
        return false;
    }

    private async Task PostStatusUpdateAsync(XElement item)
    {
        if (_client.GetChannel(_config.StatusMonitor.ChannelId) is not IMessageChannel channel)
        {
            _logger.LogWarning("Status monitor channel {ChannelId} was not found or is not a text channel.",
                _config.StatusMonitor.ChannelId);
            return;
        }

        var title = item.Element("title")?.Value?.Trim() ?? "Halo Services Status Update";
        var rawDescription = item.Element("description")?.Value ?? string.Empty;
        var link = item.Element("link")?.Value?.Trim() ?? string.Empty;
        var pubDateStr = item.Element("pubDate")?.Value;

        DateTimeOffset pubDate = DateTimeOffset.UtcNow;
        if (!string.IsNullOrEmpty(pubDateStr) && DateTimeOffset.TryParse(pubDateStr, out var parsed))
            pubDate = parsed;

        // Strip HTML tags and decode HTML entities from the description.
        var description = HaloStatusFormatting.StripHtmlAndDecode(rawDescription);

        var (color, emoji) = HaloStatusFormatting.DetermineStatusAppearance(title);

        var embed = new EmbedBuilder()
            .WithTitle($"{emoji} {title}")
            .WithColor(color)
            .WithTimestamp(pubDate)
            .WithFooter("Halo Services Status • status.haloservicesolutions.com");

        if (!string.IsNullOrEmpty(description))
            embed.WithDescription(description);

        if (!string.IsNullOrEmpty(link))
            embed.WithUrl(link);

        string? mentionText = null;
        if (_config.StatusMonitor.RoleId != 0)
            mentionText = $"<@&{_config.StatusMonitor.RoleId}>";

        await channel.SendMessageAsync(text: mentionText, embed: embed.Build());
        _logger.LogInformation("Posted Halo status update: {Title}", title);
    }

    public override void Dispose()
    {
        _httpClient.Dispose();
        base.Dispose();
    }
}
