using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Models;
using DiscordBot.Services;
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

        services.AddSingleton<DiscordBotService>();
        services.AddHostedService<HaloStatusMonitorService>();

        return services;
    }
}
