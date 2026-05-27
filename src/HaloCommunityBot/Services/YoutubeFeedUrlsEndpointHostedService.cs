using System.Net;
using System.Text;
using System.Text.Json;
using DiscordBot.Core.Data;
using DiscordBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Services;

internal sealed class YoutubeFeedUrlsEndpointHostedService : BackgroundService
{
    private const string EndpointPath = "/observability/youtube-feed-urls";

    private readonly int _port;
    private readonly IServiceProvider _serviceProvider;
    private readonly BotConfig _botConfig;
    private readonly ILogger<YoutubeFeedUrlsEndpointHostedService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private HttpListener? _listener;

    public YoutubeFeedUrlsEndpointHostedService(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        BotConfig botConfig,
        ILogger<YoutubeFeedUrlsEndpointHostedService> logger)
    {
        _port = configuration.GetValue<int>("Metrics:FeedUrlsPort", 9191);
        _serviceProvider = serviceProvider;
        _botConfig = botConfig;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{_port}/");
        _listener.Start();

        _logger.LogInformation("YouTube feed URL endpoint started on port {Port} at {Path}.", _port, EndpointPath);

        while (!stoppingToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            _ = Task.Run(() => ProcessRequestAsync(context, stoppingToken), stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        if (_listener is not null)
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }

            _listener.Close();
            _listener = null;
        }

        return base.StopAsync(cancellationToken);
    }

    private async Task ProcessRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var response = context.Response;

        if (!HttpMethodsMatchGet(context.Request.HttpMethod) || !PathMatches(context.Request.RawUrl))
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.Close();
            return;
        }

        try
        {
            var payload = await BuildPayloadAsync(cancellationToken);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, _jsonOptions));

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, cancellationToken);
            response.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serve YouTube feed URL endpoint response.");

            if (response.OutputStream.CanWrite)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Close();
            }
        }
    }

    private async Task<YoutubeFeedUrlsPayload> BuildPayloadAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HaloCommunityBotContext>();

        var settings = await db.YoutubeMonitorSettings
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var channels = await db.YoutubeTrackedChannels
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.ChannelName)
            .ToListAsync(cancellationToken);

        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unresolved = new HashSet<string>(StringComparer.Ordinal);

        foreach (var channel in channels)
        {
            var original = channel.ChannelId?.Trim();
            if (string.IsNullOrWhiteSpace(original))
            {
                continue;
            }

            if (!YoutubeChannelReferenceParser.TryNormalize(original, out var normalized) || string.IsNullOrWhiteSpace(normalized))
            {
                unresolved.Add(original);
                continue;
            }

            if (normalized.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) &&
                    uri.AbsolutePath.Contains("/feeds/videos.xml", StringComparison.OrdinalIgnoreCase))
                {
                    urls.Add(normalized);
                    continue;
                }

                unresolved.Add(original);
                continue;
            }

            if (normalized.StartsWith("UC", StringComparison.OrdinalIgnoreCase) && normalized.Length >= 20)
            {
                urls.Add($"https://www.youtube.com/feeds/videos.xml?channel_id={normalized}");
                continue;
            }

            unresolved.Add(original);
        }

        return new YoutubeFeedUrlsPayload(
            Service: "halo-bot",
            GeneratedAtUtc: DateTime.UtcNow,
            YoutubeMonitorEnabled: settings?.Enabled ?? _botConfig.YoutubeMonitor.Enabled,
            ConfiguredChannelCount: channels.Count,
            FeedUrls: urls.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            UnresolvedReferences: unresolved.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static bool HttpMethodsMatchGet(string? method)
        => string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);

    private static bool PathMatches(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        return rawUrl.Equals(EndpointPath, StringComparison.OrdinalIgnoreCase)
            || rawUrl.StartsWith(EndpointPath + "?", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record YoutubeFeedUrlsPayload(
        string Service,
        DateTime GeneratedAtUtc,
        bool YoutubeMonitorEnabled,
        int ConfiguredChannelCount,
        string[] FeedUrls,
        string[] UnresolvedReferences);
}
