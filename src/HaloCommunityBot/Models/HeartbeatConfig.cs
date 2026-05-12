namespace DiscordBot.Models;

public class HeartbeatConfig
{
    public bool Enabled { get; set; }
    public string PushUrl { get; set; } = string.Empty;
    public int IntervalSeconds { get; set; } = 60;
    public int StartupDelaySeconds { get; set; } = 15;
    public int TimeoutSeconds { get; set; } = 10;
}
