using DiscordBot.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Services;

public class HeartbeatMonitorService : BackgroundService
{
    private readonly HttpClient _httpClient;
    private readonly BotConfig _botConfig;
    private readonly ILogger<HeartbeatMonitorService> _logger;

    public HeartbeatMonitorService(HttpClient httpClient, BotConfig botConfig, ILogger<HeartbeatMonitorService> logger)
    {
        _httpClient = httpClient;
        _botConfig = botConfig;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var heartbeat = _botConfig.Heartbeat;

        if (!heartbeat.Enabled)
        {
            _logger.LogInformation("Heartbeat monitor is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(heartbeat.PushUrl))
        {
            _logger.LogWarning("Heartbeat monitor is enabled but PushUrl is empty.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(15, heartbeat.IntervalSeconds));
        var startupDelay = TimeSpan.FromSeconds(Math.Max(0, heartbeat.StartupDelaySeconds));

        if (startupDelay > TimeSpan.Zero)
        {
            await Task.Delay(startupDelay, stoppingToken);
        }

        _logger.LogInformation("Heartbeat monitor started. Pinging every {IntervalSeconds}s.", interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(2, heartbeat.TimeoutSeconds)));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);

                using var response = await _httpClient.GetAsync(heartbeat.PushUrl, linked.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Heartbeat push failed with status code {StatusCode}.", (int)response.StatusCode);
                }
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Heartbeat push timed out after {TimeoutSeconds}s.", Math.Max(2, heartbeat.TimeoutSeconds));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat push failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
