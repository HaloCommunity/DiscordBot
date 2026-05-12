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
        var botConfig = configuration.GetSection("Bot").Get<BotConfig>() ?? new BotConfig();
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
