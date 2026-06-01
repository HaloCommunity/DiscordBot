using Discord;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Core.Data;
using DiscordBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Xml.Linq;

namespace DiscordBot.Services;

/// <summary>
/// Background service that polls the Halo Services status RSS feed and posts
/// a Discord embed to a configured channel whenever a new status item appears.
/// </summary>
public class HaloStatusMonitorService : BackgroundService
{
    private const int MaxTrackedIncidentThreads = 50;

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
                state.RecentIncidentMessageIds = null;
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

        var incidentMessageMap = DeserializeIncidentMessageMap(state.RecentIncidentMessageIds);

        foreach (var item in pendingItems)
        {
            var id = GetItemId(item);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var title = item.Element("title")?.Value?.Trim() ?? "Halo Services Status Update";
            var incidentKey = GetIncidentKey(item);
            DateTimeOffset observedPublishedAt = DateTimeOffset.UtcNow;
            if (TryParsePubDate(item, out var parsedPublishedAt))
            {
                observedPublishedAt = parsedPublishedAt;
            }

            _logger.LogInformation(
                "Observed Halo status candidate {ItemId} ({Title}) published {PublishedAtUtc}.",
                id,
                title,
                observedPublishedAt.UtcDateTime);

            incidentMessageMap.TryGetValue(incidentKey, out var replyToMessageId);
            var postResult = await PostStatusUpdateAsync(item, replyToMessageId);
            var postedMessageId = postResult.MessageId;

            if (postedMessageId != 0)
            {
                if (!postResult.UsedReply)
                {
                    incidentMessageMap[incidentKey] = postedMessageId;
                    TrimIncidentMessageMap(incidentMessageMap);
                }

                if (IsResolvedUpdate(title))
                {
                    incidentMessageMap.Remove(incidentKey);
                }

                state.RecentIncidentMessageIds = SerializeIncidentMessageMap(incidentMessageMap);
            }

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

    private static string GetIncidentKey(XElement item)
    {
        var link = item.Element("link")?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(link))
            return link;

        var guid = item.Element("guid")?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(guid))
            return guid;

        var title = item.Element("title")?.Value?.Trim();
        return string.IsNullOrWhiteSpace(title) ? string.Empty : title;
    }

    private static Dictionary<string, ulong> DeserializeIncidentMessageMap(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, ulong>>(raw);
            return parsed is null
                ? new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ulong>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? SerializeIncidentMessageMap(Dictionary<string, ulong> map)
    {
        if (map.Count == 0)
            return null;

        return JsonSerializer.Serialize(map);
    }

    private static void TrimIncidentMessageMap(Dictionary<string, ulong> map)
    {
        if (map.Count <= MaxTrackedIncidentThreads)
            return;

        var overflow = map.Count - MaxTrackedIncidentThreads;
        foreach (var key in map.Keys.Take(overflow).ToList())
        {
            map.Remove(key);
        }
    }

    private static bool IsResolvedUpdate(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var normalized = title.Trim().ToLowerInvariant();
        return normalized.Contains("resolved") || normalized.Contains("monitoring");
    }

    private async Task<PostStatusResult> PostStatusUpdateAsync(XElement item, ulong replyToMessageId)
    {
        if (_client.GetChannel(_config.StatusMonitor.ChannelId) is not IMessageChannel channel)
        {
            _logger.LogWarning("Status monitor channel {ChannelId} was not found or is not a text channel.",
                _config.StatusMonitor.ChannelId);
            return PostStatusResult.None;
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
        if (_config.StatusMonitor.RoleId != 0 && replyToMessageId == 0)
            mentionText = $"<@&{_config.StatusMonitor.RoleId}>";

        IUserMessage sent;
        var usedReply = false;

        if (replyToMessageId != 0)
        {
            try
            {
                sent = await channel.SendMessageAsync(
                    embed: embed.Build(),
                    messageReference: new MessageReference(replyToMessageId, _config.StatusMonitor.ChannelId, failIfNotExists: false),
                    allowedMentions: AllowedMentions.None);
                usedReply = true;
            }
            catch (HttpException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to reply to Halo status message {ReplyToMessageId}; posting standalone update instead.",
                    replyToMessageId);

                sent = await channel.SendMessageAsync(embed: embed.Build(), allowedMentions: AllowedMentions.None);
            }
        }
        else
        {
            sent = await channel.SendMessageAsync(text: mentionText, embed: embed.Build());
        }

        var itemId = GetItemId(item);
        _logger.LogInformation("Posted Halo status update {ItemId}: {Title}", itemId, title);
        return new PostStatusResult(sent.Id, usedReply);
    }

    private readonly record struct PostStatusResult(ulong MessageId, bool UsedReply)
    {
        public static PostStatusResult None => new(0, false);
    }

    public override void Dispose()
    {
        _httpClient.Dispose();
        base.Dispose();
    }
}
