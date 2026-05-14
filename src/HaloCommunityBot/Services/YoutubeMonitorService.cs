using System.Globalization;
using System.Text.RegularExpressions;
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
    private readonly YoutubeChannelSearchService _channelSearchService;
    private readonly ILogger<YoutubeMonitorService> _logger;

    public YoutubeMonitorService(
        DiscordSocketClient client,
        BotConfig config,
        IServiceProvider serviceProvider,
        YoutubeChannelSearchService channelSearchService,
        ILogger<YoutubeMonitorService> logger)
    {
        _client = client;
        _config = config;
        _serviceProvider = serviceProvider;
        _channelSearchService = channelSearchService;
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

        var settings = await db.YoutubeMonitorSettings
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (settings == null || !settings.Enabled || settings.ForumChannelId == 0)
        {
            _logger.LogInformation("YouTube monitor is disabled or not configured — skipping.");
            return;
        }

        var channels = await db.YoutubeTrackedChannels
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.ChannelName)
            .ToListAsync(cancellationToken);

        if (channels.Count == 0)
        {
            _logger.LogInformation("YouTube monitor has no tracked channels configured — skipping.");
            return;
        }

        var normalizedChannels = new List<YoutubeTrackedChannel>(channels.Count);
        var invalidChannels = new List<string>();
        var normalizedUpdated = 0;

        foreach (var trackedChannel in channels)
        {
            var originalReference = trackedChannel.ChannelId;

            if (!YoutubeChannelReferenceParser.TryNormalize(originalReference, out var normalizedReference) ||
                string.IsNullOrWhiteSpace(normalizedReference))
            {
                var searchResult = await _channelSearchService.SearchAsync(originalReference, cancellationToken);
                if (searchResult == null)
                {
                    trackedChannel.IsEnabled = false;
                    trackedChannel.UpdatedAt = DateTime.UtcNow;
                    invalidChannels.Add(originalReference);
                    continue;
                }

                normalizedReference = searchResult.ChannelId;
                trackedChannel.ChannelId = searchResult.ChannelId;

                if (string.IsNullOrWhiteSpace(trackedChannel.ChannelName) ||
                    string.Equals(trackedChannel.ChannelName.Trim(), originalReference.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    trackedChannel.ChannelName = searchResult.ChannelName;
                }

                trackedChannel.UpdatedAt = DateTime.UtcNow;
                normalizedUpdated++;
            }

            // Reject placeholder/invalid UC IDs so they do not spam feed polling warnings.
            if (normalizedReference.StartsWith("UC", StringComparison.OrdinalIgnoreCase) &&
                !LooksLikeYoutubeChannelId(normalizedReference))
            {
                trackedChannel.IsEnabled = false;
                trackedChannel.UpdatedAt = DateTime.UtcNow;
                invalidChannels.Add(trackedChannel.ChannelId);
                continue;
            }

            if (!string.Equals(trackedChannel.ChannelId, normalizedReference, StringComparison.Ordinal))
            {
                trackedChannel.ChannelId = normalizedReference;
                trackedChannel.UpdatedAt = DateTime.UtcNow;
                normalizedUpdated++;
            }

            normalizedChannels.Add(trackedChannel);
        }

        if (invalidChannels.Count > 0 || normalizedUpdated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        if (invalidChannels.Count > 0)
        {
            _logger.LogWarning(
                "YouTube monitor disabled {InvalidCount} invalid tracked channel(s): {Channels}",
                invalidChannels.Count,
                string.Join(", ", invalidChannels));
        }

        if (normalizedChannels.Count == 0)
        {
            _logger.LogInformation("YouTube monitor has no valid tracked channels configured — skipping.");
            return;
        }

        _logger.LogInformation("YouTube monitor started. Polling {Count} valid channel(s) every {Interval} minute(s).", normalizedChannels.Count, settings.PollIntervalMinutes);

        if (_client.GetChannel(settings.ForumChannelId) is not IForumChannel forumChannel)
        {
            _logger.LogWarning("Configured YouTube forum channel {ChannelId} was not found or is not a forum channel.", settings.ForumChannelId);
            return;
        }

        foreach (var youtubeChannel in normalizedChannels)
        {
            await PollChannelAsync(db, forumChannel, settings, youtubeChannel, cancellationToken);
        }
    }

    private async Task<int> GetPollIntervalAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HaloCommunityBotContext>();
        await EnsureSeededAsync(db, cancellationToken);

        var settings = await db.YoutubeMonitorSettings
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
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

                var channelReference = channel.ChannelId.Trim();
                var channelName = string.IsNullOrWhiteSpace(channel.ChannelName) ? channelReference : channel.ChannelName.Trim();

                if (!YoutubeChannelReferenceParser.TryNormalize(channelReference, out var normalizedReference) ||
                    string.IsNullOrWhiteSpace(normalizedReference))
                {
                    var searchResult = await _channelSearchService.SearchAsync(channelReference, cancellationToken);
                    if (searchResult == null)
                    {
                        _logger.LogWarning("Skipping configured YouTube seed channel because it could not be resolved: {ChannelReference}", channelReference);
                        continue;
                    }

                    normalizedReference = searchResult.ChannelId;
                    channelName = string.IsNullOrWhiteSpace(channel.ChannelName) ? searchResult.ChannelName : channelName;
                }

                db.YoutubeTrackedChannels.Add(new YoutubeTrackedChannel
                {
                    ChannelId = normalizedReference,
                    ChannelName = channelName,
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
        if (!YoutubeChannelReferenceParser.TryNormalize(youtubeChannel.ChannelId, out var normalizedReference))
        {
            _logger.LogWarning(
                "Skipping tracked YouTube channel because the stored reference is not a supported YouTube channel ID, @handle, or feed URL. DbId={DbId}, ChannelId={ChannelId}, ChannelName={ChannelName}",
                youtubeChannel.Id,
                youtubeChannel.ChannelId,
                youtubeChannel.ChannelName ?? "(null)");
            return;
        }

        var feed = await LoadFeedAsync(normalizedReference, cancellationToken);
        if (feed == null)
        {
            _logger.LogDebug(
                "Skipping tracked YouTube channel due to feed load failure. DbId={DbId}, ChannelId={ChannelId}, ChannelName={ChannelName}",
                youtubeChannel.Id,
                youtubeChannel.ChannelId,
                youtubeChannel.ChannelName ?? "(null)");
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
            var mentionPrefix = _config.YoutubeMonitor.RoleId != 0
                ? $"<@&{_config.YoutubeMonitor.RoleId}>\n"
                : string.Empty;
            var body = $"{mentionPrefix}New video from **{channelName}**\n{video.Url}";

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
                        video.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        video.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
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

    private async Task<YouTubeFeed?> LoadFeedAsync(string channelReference, CancellationToken cancellationToken)
    {
        var normalizedReference = channelReference.Trim();
        var feedUrls = new List<string>();
        var attemptFailures = new List<string>();

        try
        {
            // Allow direct feed URLs.
            if (Uri.TryCreate(normalizedReference, UriKind.Absolute, out var parsedUri) &&
                parsedUri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) &&
                parsedUri.AbsolutePath.Contains("/feeds/videos.xml", StringComparison.OrdinalIgnoreCase))
            {
                feedUrls.Add(parsedUri.ToString());
            }

            // channel/<UC...> URL support.
            if (TryExtractChannelIdFromUrl(normalizedReference, out var extractedChannelId))
            {
                feedUrls.Add(BuildChannelFeedUrl(extractedChannelId));
            }

            // Direct UC channel id support.
            if (LooksLikeYoutubeChannelId(normalizedReference))
            {
                feedUrls.Add(BuildChannelFeedUrl(normalizedReference));
            }

            // Handle/user support (for records created with /youtube add-channel @handle or plain handle).
            var handleOrUser = normalizedReference.TrimStart('@');
            if (!string.IsNullOrWhiteSpace(handleOrUser) && !LooksLikeYoutubeChannelId(handleOrUser))
            {
                var resolvedChannelId = await TryResolveChannelIdFromHandleAsync(handleOrUser, cancellationToken);
                if (!string.IsNullOrWhiteSpace(resolvedChannelId))
                {
                    feedUrls.Add(BuildChannelFeedUrl(resolvedChannelId));
                }

                // Legacy username feed fallback.
                feedUrls.Add($"https://www.youtube.com/feeds/videos.xml?user={Uri.EscapeDataString(handleOrUser)}");

                // Some existing records store non-UC values in ChannelId, keep this fallback for compatibility.
                feedUrls.Add(BuildChannelFeedUrl(handleOrUser));
            }

            // Final fallback for any remaining input.
            if (feedUrls.Count == 0)
            {
                feedUrls.Add(BuildChannelFeedUrl(normalizedReference));
            }

            foreach (var url in feedUrls.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    using var response = await _httpClient.GetAsync(url, cancellationToken);
                    var payload = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        attemptFailures.Add($"{url} => HTTP {(int)response.StatusCode}");
                        continue;
                    }

                    if (!LooksLikeXml(payload))
                    {
                        attemptFailures.Add($"{url} => non-XML response");
                        continue;
                    }

                    var doc = XDocument.Parse(payload);

                    var authorElement = doc.Root?.Element(AtomNamespace + "author");
                    var channelName = authorElement?.Element(AtomNamespace + "name")?.Value?.Trim()
                        ?? doc.Root?.Element(AtomNamespace + "title")?.Value?.Trim()
                        ?? normalizedReference;

                    var videos = doc.Descendants(AtomNamespace + "entry")
                        .Select(ParseVideoEntry)
                        .Where(video => video != null)
                        .Select(video => video!)
                        .ToList();

                    return new YouTubeFeed(channelName, videos);
                }
                catch (Exception ex)
                {
                    attemptFailures.Add($"{url} => {ex.GetType().Name}: {ex.Message}");
                }
            }

            var attemptsSummary = attemptFailures.Count == 0
                ? "(no attempts)"
                : string.Join(" | ", attemptFailures.Take(5));

            _logger.LogWarning("Failed to load YouTube feed for channel {ChannelReference}. Attempts: {AttemptsSummary}", normalizedReference, attemptsSummary);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load YouTube feed for channel {ChannelReference}.", normalizedReference);
            return null;
        }
    }

    private static bool LooksLikeYoutubeChannelId(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("UC", StringComparison.OrdinalIgnoreCase) && trimmed.Length >= 20;
    }

    private static string BuildChannelFeedUrl(string channelIdOrReference)
    {
        return $"https://www.youtube.com/feeds/videos.xml?channel_id={Uri.EscapeDataString(channelIdOrReference.Trim())}";
    }

    private static bool LooksLikeXml(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var trimmed = payload.TrimStart();
        return trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("<feed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractChannelIdFromUrl(string input, out string channelId)
    {
        channelId = string.Empty;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) ||
            !uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 2 && segments[0].Equals("channel", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(segments[1]))
        {
            channelId = segments[1].Trim();
            return true;
        }

        return false;
    }

    private async Task<string?> TryResolveChannelIdFromHandleAsync(string handleOrUser, CancellationToken cancellationToken)
    {
        try
        {
            var handle = handleOrUser.TrimStart('@').Trim();
            if (string.IsNullOrWhiteSpace(handle))
            {
                return null;
            }

            var profileUrl = $"https://www.youtube.com/@{Uri.EscapeDataString(handle)}";
            using var response = await _httpClient.GetAsync(profileUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            var match = Regex.Match(html, "\"externalId\":\"(?<id>UC[0-9A-Za-z_-]{20,})\"", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                match = Regex.Match(html, "\"channelId\":\"(?<id>UC[0-9A-Za-z_-]{20,})\"", RegexOptions.IgnoreCase);
            }

            return match.Success ? match.Groups["id"].Value : null;
        }
        catch
        {
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
        var description = entry.Element(AtomNamespace + "summary")?.Value?.Trim() ?? string.Empty;
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

        return new YouTubeVideoEntry(videoId, title, link, publishedAt, description);
    }

    private sealed record YouTubeFeed(string ChannelName, IReadOnlyList<YouTubeVideoEntry> Videos);

    private sealed record YouTubeVideoEntry(string VideoId, string Title, string Url, DateTime PublishedAt, string Description = "");

    public override void Dispose()
    {
        _httpClient.Dispose();
        base.Dispose();
    }
}
