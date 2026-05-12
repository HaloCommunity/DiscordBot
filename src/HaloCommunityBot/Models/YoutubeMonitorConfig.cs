namespace DiscordBot.Models;

public class YoutubeMonitorConfig
{
    public bool Enabled { get; set; } = false;

    public ulong ForumChannelId { get; set; }

    public int PollIntervalMinutes { get; set; } = 15;

    public string DefaultPostTitleTemplate { get; set; } = "[{ChannelName}] {VideoTitle}";

    public List<YoutubeChannelConfig> Channels { get; set; } = new();
}

public class YoutubeChannelConfig
{
    public string ChannelId { get; set; } = string.Empty;

    public string? ChannelName { get; set; }

    public string? PostTitleTemplate { get; set; }
}
