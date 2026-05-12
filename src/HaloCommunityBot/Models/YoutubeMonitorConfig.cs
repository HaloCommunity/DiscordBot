namespace DiscordBot.Models;

public class YoutubeMonitorConfig
{
    public bool Enabled { get; set; } = false;

    public ulong ForumChannelId { get; set; }

    public int PollIntervalMinutes { get; set; } = 15;

    public string DefaultPostTitleTemplate { get; set; } = "[{ChannelName}] {VideoTitle}";

    public List<YoutubeChannelConfig> Channels { get; set; } = new();

    /// <summary>
    /// Validates the YouTube monitor configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
    public void Validate()
    {
        if (Enabled)
        {
            if (ForumChannelId == 0)
                throw new InvalidOperationException("YoutubeMonitorConfig: When enabled, ForumChannelId must be a valid Discord forum channel ID (non-zero). Current value: 0. Check HALOCOMMUNITYBOT_Bot__YoutubeMonitor__ForumChannelId environment variable. Must be a single channel ID, not 'guildId/channelId'.");

            if (string.IsNullOrWhiteSpace(DefaultPostTitleTemplate))
                throw new InvalidOperationException("YoutubeMonitorConfig: DefaultPostTitleTemplate cannot be empty.");

            if (PollIntervalMinutes <= 0)
                throw new InvalidOperationException($"YoutubeMonitorConfig: PollIntervalMinutes must be greater than 0. Current value: {PollIntervalMinutes}.");
        }
    }
}

public class YoutubeChannelConfig
{
    public string ChannelId { get; set; } = string.Empty;

    public string? ChannelName { get; set; }

    public string? PostTitleTemplate { get; set; }

    /// <summary>
    /// Optional list of keywords to filter videos by title (case-insensitive).
    /// If provided, only videos with titles containing at least one keyword will be posted.
    /// If empty or null, all videos are posted.
    /// </summary>
    public List<string> KeywordFilters { get; set; } = new();
}
