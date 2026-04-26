using Discord;
using Discord.WebSocket;
using DiscordBot.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DiscordBot.Services;

/// <summary>
/// Background service that polls the Halo Services status RSS feed and posts
/// a Discord embed to a configured channel whenever a new status item appears.
/// </summary>
public partial class HaloStatusMonitorService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly BotConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HaloStatusMonitorService> _logger;
    private readonly HashSet<string> _seenGuids = new(StringComparer.Ordinal);
    private bool _initialized;

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    public HaloStatusMonitorService(
        DiscordSocketClient client,
        BotConfig config,
        ILogger<HaloStatusMonitorService> logger)
    {
        _client = client;
        _config = config;
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

        if (!_initialized)
        {
            // Seed the seen-set with everything currently in the feed so we don't
            // flood the channel on the first run after a bot restart.
            foreach (var item in items)
            {
                var id = GetItemId(item);
                if (!string.IsNullOrEmpty(id))
                    _seenGuids.Add(id);
            }

            _initialized = true;
            _logger.LogInformation("Halo status monitor initialised — {Count} existing feed item(s) marked as seen.", _seenGuids.Count);
            return;
        }

        // RSS feeds are newest-first; reverse so we post in chronological order.
        foreach (var item in Enumerable.Reverse(items))
        {
            var id = GetItemId(item);
            if (string.IsNullOrEmpty(id) || !_seenGuids.Add(id))
                continue;

            await PostStatusUpdateAsync(item);
        }
    }

    private static string GetItemId(XElement item)
        => item.Element("guid")?.Value?.Trim()
            ?? item.Element("link")?.Value?.Trim()
            ?? string.Empty;

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
        var description = WebUtility.HtmlDecode(HtmlTagRegex().Replace(rawDescription, string.Empty)).Trim();
        if (description.Length > 2048)
            description = string.Concat(description.AsSpan(0, 2045), "...");

        var (color, emoji) = DetermineStatusAppearance(title);

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

    /// <summary>
    /// Determines the embed colour and emoji based on keywords in the item title.
    /// Statuspage.io prefixes titles with the current state, e.g. "[Investigating]".
    /// </summary>
    private static (Color color, string emoji) DetermineStatusAppearance(string title)
    {
        var lower = title.ToLowerInvariant();

        return lower switch
        {
            _ when lower.Contains("resolved") || lower.Contains("completed") || lower.Contains("postmortem")
                => (Color.Green, "✅"),
            _ when lower.Contains("investigating") || lower.Contains("outage")
                => (Color.Red, "🔴"),
            _ when lower.Contains("identified")
                => (new Color(0xFF6B35), "🟠"),
            _ when lower.Contains("monitoring")
                => (Color.Gold, "👀"),
            _ when lower.Contains("in progress")
                => (Color.Orange, "⚙️"),
            _ when lower.Contains("maintenance") || lower.Contains("scheduled")
                => (new Color(0x5865F2), "🔧"),
            _ when lower.Contains("degraded") || lower.Contains("partial")
                => (Color.Orange, "⚠️"),
            _ => (Color.LightOrange, "ℹ️")
        };
    }

    public override void Dispose()
    {
        _httpClient.Dispose();
        base.Dispose();
    }
}
