using System.Globalization;
using System.Xml.Linq;
using Discord;
using Discord.WebSocket;
using DiscordBot.Core.Data;
using DiscordBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Services;

/// <summary>
/// Polls configured YouTube channels and posts new uploads into a forum channel.
/// </summary>
public class YoutubeMonitorService : BackgroundService
{
    private static readonly XNamespace AtomNamespace = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace YoutubeNamespace = "http://www.youtube.com/xml/schemas/2015";

    private readonly DiscordSocketClient _client;
    private readonly BotConfig _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly HttpClient _httpClient;
    private readonly ILogger<YoutubeMonitorService> _logger;

    public YoutubeMonitorService(
        DiscordSocketClient client,
        BotConfig config,
        IServiceProvider serviceProvider,
        ILogger<YoutubeMonitorService> logger)
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
        while (_client.ConnectionState != ConnectionState.Connected && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollChannelsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling YouTube feeds.");
            }

            var delay = await GetPollIntervalAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(delay), stoppingToken);
        }
    }

    private async Task PollChannelsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HaloCommunityBotContext>();

        await EnsureSeededAsync(db, cancellationToken);

        var settings = await db.YoutubeMonitorSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (settings == null || !settings.Enabled || settings.ForumChannelId == 0)
        {
            _logger.LogInformation("YouTube monitor is disabled or not configured — skipping.");
            return;
        }

        var channels = await db.YoutubeTrackedChannels.AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.ChannelName)
            .ToListAsync(cancellationToken);

        if (channels.Count == 0)
        {
            _logger.LogInformation("YouTube monitor has no tracked channels configured — skipping.");
            return;
        }

        _logger.LogInformation("YouTube monitor started. Polling {Count} channel(s) every {Interval} minute(s).", channels.Count, settings.PollIntervalMinutes);

        if (_client.GetChannel(settings.ForumChannelId) is not IForumChannel forumChannel)
        {
            _logger.LogWarning("Configured YouTube forum channel {ChannelId} was not found or is not a forum channel.", settings.ForumChannelId);
            return;
        }

        foreach (var youtubeChannel in channels.Where(c => !string.IsNullOrWhiteSpace(c.ChannelId)))
        {
            await PollChannelAsync(db, forumChannel, settings, youtubeChannel, cancellationToken);
        }
    }

    private async Task<int> GetPollIntervalAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HaloCommunityBotContext>();
        await EnsureSeededAsync(db, cancellationToken);

        var settings = await db.YoutubeMonitorSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return settings?.PollIntervalMinutes > 0 ? settings.PollIntervalMinutes : 15;
    }

    private async Task EnsureSeededAsync(HaloCommunityBotContext db, CancellationToken cancellationToken)
    {
        if (!await db.YoutubeMonitorSettings.AnyAsync(cancellationToken))
        {
            var youtubeConfig = _config.YoutubeMonitor;
            db.YoutubeMonitorSettings.Add(new YoutubeMonitorSettings
            {
                Enabled = youtubeConfig.Enabled,
                ForumChannelId = youtubeConfig.ForumChannelId,
                PollIntervalMinutes = youtubeConfig.PollIntervalMinutes,
                DefaultPostTitleTemplate = string.IsNullOrWhiteSpace(youtubeConfig.DefaultPostTitleTemplate)
                    ? "[{ChannelName}] {VideoTitle}"
                    : youtubeConfig.DefaultPostTitleTemplate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (!await db.YoutubeTrackedChannels.AnyAsync(cancellationToken) && _config.YoutubeMonitor.Channels.Count > 0)
        {
            foreach (var channel in _config.YoutubeMonitor.Channels.Where(c => !string.IsNullOrWhiteSpace(c.ChannelId)))
            {
                var keywords = channel.KeywordFilters?.Count > 0
                    ? string.Join(";", channel.KeywordFilters.Select(k => k.Trim()))
                    : null;

                db.YoutubeTrackedChannels.Add(new YoutubeTrackedChannel
                {
                    ChannelId = channel.ChannelId.Trim(),
                    ChannelName = string.IsNullOrWhiteSpace(channel.ChannelName) ? channel.ChannelId.Trim() : channel.ChannelName.Trim(),
                    PostTitleTemplate = string.IsNullOrWhiteSpace(channel.PostTitleTemplate) ? null : channel.PostTitleTemplate.Trim(),
                    KeywordFilters = keywords,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PollChannelAsync(
        HaloCommunityBotContext db,
        IForumChannel forumChannel,
        YoutubeMonitorSettings settings,
        YoutubeTrackedChannel youtubeChannel,
        CancellationToken cancellationToken)
    {
        var feed = await LoadFeedAsync(youtubeChannel.ChannelId, cancellationToken);
        if (feed == null)
        {
            return;
        }

        var channelName = !string.IsNullOrWhiteSpace(youtubeChannel.ChannelName)
            ? youtubeChannel.ChannelName!.Trim()
            : feed.ChannelName;

        var stateKey = youtubeChannel.ChannelId.Trim();
        var state = await db.FeedPostStates.FirstOrDefaultAsync(x => x.FeedType == "YouTube" && x.SourceId == stateKey, cancellationToken);

        if (state == null)
        {
            state = new FeedPostState
            {
                FeedType = "YouTube",
                SourceId = stateKey
            };
            db.FeedPostStates.Add(state);
        }

        if (string.IsNullOrWhiteSpace(state.LastPostedItemId))
        {
            var latestVideoId = feed.Videos.FirstOrDefault()?.VideoId;
            if (!string.IsNullOrWhiteSpace(latestVideoId))
            {
                state.LastPostedItemId = latestVideoId;
                state.LastCheckedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("YouTube channel {ChannelId} initialised — latest video {VideoId} stored as baseline.", youtubeChannel.ChannelId, latestVideoId);
            }

            return;
        }

        var pendingVideos = GetPendingVideos(feed.Videos, state, youtubeChannel.KeywordFilters);
        if (pendingVideos.Count == 0)
        {
            state.LastCheckedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var tagName = NormalizeForumTagName(channelName);
        var forumTag = await EnsureForumTagAsync(forumChannel, tagName);
        if (forumTag == null)
        {
            _logger.LogWarning("Could not resolve or create forum tag {TagName} for YouTube channel {ChannelId}.", tagName, youtubeChannel.ChannelId);
            return;
        }

        var resolvedForumTag = (ForumTag)forumTag;

        foreach (var video in pendingVideos)
        {
            var postTitle = BuildPostTitle(settings, youtubeChannel, channelName, video.Title);
            var body = $"New video from **{channelName}**\n{video.Url}";

            try
            {
                await forumChannel.CreatePostAsync(postTitle, text: body, tags: new ForumTag[] { resolvedForumTag });
                state.LastPostedItemId = video.VideoId;
                state.LastCheckedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Posted YouTube video {VideoId} from {ChannelName} to forum channel {ForumChannelId}.", video.VideoId, channelName, forumChannel.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post YouTube video {VideoId} from {ChannelName}.", video.VideoId, channelName);
                break;
            }
        }
    }

    private static List<YouTubeVideoEntry> GetPendingVideos(IReadOnlyList<YouTubeVideoEntry> videos, FeedPostState state, string? keywordFilters = null)
    {
        var pending = new List<YouTubeVideoEntry>();
        
        if (!string.IsNullOrWhiteSpace(state.LastPostedItemId))
        {
            var lastPostedIndex = videos
                .Select((video, index) => new { Video = video, Index = index })
                .FirstOrDefault(entry => string.Equals(entry.Video.VideoId, state.LastPostedItemId, StringComparison.OrdinalIgnoreCase))
                ?.Index;

            if (lastPostedIndex.HasValue)
            {
                pending = videos.Take(lastPostedIndex.Value).Reverse().ToList();
            }
        }
        else if (state.LastCheckedAt.HasValue)
        {
            pending = videos
                .Where(video => video.PublishedAt > state.LastCheckedAt.Value)
                .Reverse()
                .ToList();
        }

        // Apply keyword filters if configured
        if (!string.IsNullOrWhiteSpace(keywordFilters))
        {
            var keywords = keywordFilters
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList();

            if (keywords.Count > 0)
            {
                pending = pending
                    .Where(video => keywords.Any(keyword => 
                        video.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
        }

        return pending;
    }

    private async Task<ForumTag?> EnsureForumTagAsync(IForumChannel forumChannel, string tagName)
    {
        var existingTag = forumChannel.Tags.FirstOrDefault(tag => tag.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
        if (existingTag != null)
        {
            return existingTag;
        }

        var updatedTags = forumChannel.Tags.Select(tag => tag.ToForumTagBuilder()).ToList();
        updatedTags.Add(new ForumTagBuilder(tagName, id: null, isModerated: false));

        await forumChannel.ModifyAsync(properties => properties.Tags = Optional.Create<IEnumerable<IForumTag>>(updatedTags.Cast<IForumTag>().ToList()));

        existingTag = forumChannel.Tags.FirstOrDefault(tag => tag.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
        return existingTag;
    }

    private static string BuildPostTitle(YoutubeMonitorSettings settings, YoutubeTrackedChannel youtubeChannel, string channelName, string videoTitle)
    {
        var template = string.IsNullOrWhiteSpace(youtubeChannel.PostTitleTemplate)
            ? settings.DefaultPostTitleTemplate
            : youtubeChannel.PostTitleTemplate!;

        if (!string.IsNullOrWhiteSpace(template))
        {
            return template
                .Replace("{ChannelName}", channelName, StringComparison.OrdinalIgnoreCase)
                .Replace("{VideoTitle}", videoTitle, StringComparison.OrdinalIgnoreCase);
        }

        return $"[{channelName}] {videoTitle}";
    }

    private static string NormalizeForumTagName(string channelName)
    {
        var trimmed = channelName.Trim();
        return trimmed.Length <= 20 ? trimmed : trimmed[..20].Trim();
    }

    private async Task<YouTubeFeed?> LoadFeedAsync(string channelId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://www.youtube.com/feeds/videos.xml?channel_id={Uri.EscapeDataString(channelId)}";
            var xml = await _httpClient.GetStringAsync(url, cancellationToken);
            var doc = XDocument.Parse(xml);

            var authorElement = doc.Root?.Element(AtomNamespace + "author");
            var channelName = authorElement?.Element(AtomNamespace + "name")?.Value?.Trim()
                ?? doc.Root?.Element(AtomNamespace + "title")?.Value?.Trim()
                ?? channelId;

            var videos = doc.Descendants(AtomNamespace + "entry")
                .Select(ParseVideoEntry)
                .Where(video => video != null)
                .Select(video => video!)
                .ToList();

            return new YouTubeFeed(channelName, videos);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load YouTube feed for channel {ChannelId}.", channelId);
            return null;
        }
    }

    private static YouTubeVideoEntry? ParseVideoEntry(XElement entry)
    {
        var videoId = entry.Element(YoutubeNamespace + "videoId")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return null;
        }

        var title = entry.Element(AtomNamespace + "title")?.Value?.Trim() ?? videoId;
        var link = entry.Elements(AtomNamespace + "link")
            .FirstOrDefault(x => string.Equals(x.Attribute("rel")?.Value, "alternate", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("href")?.Value?.Trim()
            ?? $"https://www.youtube.com/watch?v={videoId}";

        var publishedText = entry.Element(AtomNamespace + "published")?.Value;
        var publishedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(publishedText) && DateTime.TryParse(publishedText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedPublishedAt))
        {
            publishedAt = parsedPublishedAt;
        }

        return new YouTubeVideoEntry(videoId, title, link, publishedAt);
    }

    private sealed record YouTubeFeed(string ChannelName, IReadOnlyList<YouTubeVideoEntry> Videos);

    private sealed record YouTubeVideoEntry(string VideoId, string Title, string Url, DateTime PublishedAt);

    public override void Dispose()
    {
        _httpClient.Dispose();
        base.Dispose();
    }
}
