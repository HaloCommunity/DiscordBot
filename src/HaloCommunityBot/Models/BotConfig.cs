namespace DiscordBot.Models;

public class BotConfig
{
    public string Token { get; set; } = string.Empty;
    public string Prefix { get; set; } = "@";
    public ulong? GuildId { get; set; }
    public List<ulong> AllowedFunChannels { get; set; } = new List<ulong>();
    public Dictionary<string, int> Cooldowns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public StatusMonitorConfig StatusMonitor { get; set; } = new();
}
