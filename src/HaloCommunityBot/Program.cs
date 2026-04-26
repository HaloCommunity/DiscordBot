using DiscordBot.Extensions;
using DiscordBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables("HALOCOMMUNITYBOT_");
                config.AddUserSecrets<Program>(optional: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddDiscordBot(context.Configuration);
            })
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

var botService = host.Services.GetRequiredService<DiscordBotService>();

try
{
    await botService.StartAsync();
    await host.StartAsync();
    await host.WaitForShutdownAsync();
}
finally
{
    await botService.StopAsync();
    await host.StopAsync();
}
