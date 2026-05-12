using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Models;
using DiscordBot.Core.Data;
using DiscordBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Extensions;

/// <summary>
/// Extension methods for registering Discord bot services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Discord bot configuration, client, and related services into the DI container.
    /// </summary>
    public static IServiceCollection AddDiscordBot(this IServiceCollection services, IConfiguration configuration)
    {
        var botSection = configuration.GetSection("Bot");
        BotConfig botConfig;
        try
        {
            botConfig = botSection.Get<BotConfig>() ?? new BotConfig();
        }
        catch (Exception ex)
        {
            // Include only non-sensitive values to help diagnose invalid env var bindings.
            var diagnostics = string.Join(
                ", ",
                [
                    $"Bot:GuildId='{botSection["GuildId"] ?? "<null>"}'",
                    $"Bot:StatusMonitor:Enabled='{botSection["StatusMonitor:Enabled"] ?? "<null>"}'",
                    $"Bot:StatusMonitor:ChannelId='{botSection["StatusMonitor:ChannelId"] ?? "<null>"}'",
                    $"Bot:StatusMonitor:RoleId='{botSection["StatusMonitor:RoleId"] ?? "<null>"}'",
                    $"Bot:StatusMonitor:PollIntervalMinutes='{botSection["StatusMonitor:PollIntervalMinutes"] ?? "<null>"}'",
                    $"Bot:YoutubeMonitor:Enabled='{botSection["YoutubeMonitor:Enabled"] ?? "<null>"}'",
                    $"Bot:YoutubeMonitor:ForumChannelId='{botSection["YoutubeMonitor:ForumChannelId"] ?? "<null>"}'",
                    $"Bot:YoutubeMonitor:PollIntervalMinutes='{botSection["YoutubeMonitor:PollIntervalMinutes"] ?? "<null>"}'",
                    $"Bot:Heartbeat:Enabled='{botSection["Heartbeat:Enabled"] ?? "<null>"}'",
                    $"Bot:Heartbeat:IntervalSeconds='{botSection["Heartbeat:IntervalSeconds"] ?? "<null>"}'",
                    $"Bot:Heartbeat:StartupDelaySeconds='{botSection["Heartbeat:StartupDelaySeconds"] ?? "<null>"}'",
                    $"Bot:Heartbeat:TimeoutSeconds='{botSection["Heartbeat:TimeoutSeconds"] ?? "<null>"}'"
                ]);

            throw new InvalidOperationException(
                $"Failed to bind 'Bot' configuration. Check numeric/boolean environment values. {diagnostics}",
                ex);
        }

        services.AddSingleton(botConfig);

        var socketConfig = new DiscordSocketConfig
        {
            AlwaysDownloadUsers = true,
            GatewayIntents = GatewayIntents.All,
            LogLevel = LogSeverity.Info
        };
        services.AddSingleton(socketConfig);
        services.AddSingleton<DiscordSocketClient>();

        services.AddSingleton(x =>
        {
            var client = x.GetRequiredService<DiscordSocketClient>();
            return new InteractionService(client);
        });

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=./halocommunitybot.db";
        services.AddDbContext<HaloCommunityBotContext>(options => options.UseSqlite(connectionString));

        services.AddSingleton<DiscordBotService>();
        services.AddHttpClient<HeartbeatMonitorService>();
        services.AddHostedService<HaloStatusMonitorService>();
        services.AddHostedService<YoutubeMonitorService>();
        services.AddHostedService<HeartbeatMonitorService>();

        return services;
    }
}
