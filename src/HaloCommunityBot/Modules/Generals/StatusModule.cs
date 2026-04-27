using Discord;
using Discord.Interactions;
using DiscordBot.Models;
using DiscordBot.Services;
using System.Text.Json;
using System.Xml.Linq;

namespace DiscordBot.Modules.Generals;

public class StatusModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BotConfig _config;

    public StatusModule(BotConfig config)
    {
        _config = config;
    }

    [SlashCommand("status", "Show current Halo services status overview")]
    public async Task StatusAsync(
        [Summary("private", "Return the status only to you (ephemeral)")] bool @private = false)
    {
        await DeferAsync(ephemeral: @private);

        var feedUrl = string.IsNullOrWhiteSpace(_config.StatusMonitor.FeedUrl)
            ? "https://status.haloservicesolutions.com/pages/63ef45da7ee94905308a1a4a/rss"
            : _config.StatusMonitor.FeedUrl;

        var statusPageBaseUrl = GetStatusPageBaseUrl(feedUrl);
        if (string.IsNullOrWhiteSpace(statusPageBaseUrl))
        {
            await FollowupAsync("Status feed URL is invalid in configuration.", ephemeral: @private);
            return;
        }

        if (!TryGetStatusPageId(feedUrl, out var statusPageId))
        {
            await FollowupAsync("Unable to determine Statuspage ID from the configured feed URL.", ephemeral: @private);
            return;
        }

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HaloCommunityBot/1.0");

            var statusJson = await httpClient.GetStringAsync($"https://api.status.io/1.0/status/{statusPageId}");
            using var statusDoc = JsonDocument.Parse(statusJson);

            var overview = ParseStatusOverview(statusDoc.RootElement);
            var mostRecentRssIncident = await TryGetMostRecentIncidentFromRssAsync(httpClient, feedUrl);

            var incidentToShow = overview.ActiveIncident ?? mostRecentRssIncident;
            var incidentIsActive = overview.ActiveIncident.HasValue;

            var (color, emoji) = GetOverallAppearance(overview.StatusText, overview.StatusCode);
            var indicatorLabel = GetIndicatorLabel(overview.StatusText, overview.StatusCode);

            var embed = new EmbedBuilder()
                .WithTitle($"{emoji} Halo Services Status Overview")
                .WithColor(color)
                .WithUrl(statusPageBaseUrl)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithFooter($"Source: {new Uri(statusPageBaseUrl).Host}")
                .AddField("Current Status", $"{emoji} {overview.StatusText}\nIndicator: `{overview.StatusCode?.ToString() ?? "n/a"}` ({indicatorLabel})", false);

            if (overview.UpdatedAt.HasValue)
                embed.AddField("Status Updated", overview.UpdatedAt.Value.ToString("yyyy-MM-dd HH:mm 'UTC'"), false);

            if (incidentToShow.HasValue)
            {
                var incident = incidentToShow.Value;
                var incidentEmoji = incidentIsActive ? "🚨" : "🧾";
                var summaryTitle = incidentIsActive
                    ? $"{incidentEmoji} Active Incident"
                    : $"{incidentEmoji} Most Recent Incident";

                var incidentDateText = incident.UpdatedAt?.ToString("yyyy-MM-dd HH:mm 'UTC'") ?? "Unknown";
                var incidentHeader = string.IsNullOrWhiteSpace(incident.Url)
                    ? incident.Name
                    : $"[{incident.Name}]({incident.Url})";

                embed.AddField(summaryTitle, $"{incidentHeader}\nDate: {incidentDateText}", false);

                if (!string.IsNullOrWhiteSpace(incident.Summary))
                    embed.AddField("📝 Incident Summary", incident.Summary, false);
            }
            else
            {
                embed.AddField("✅ Incident Summary", "No incidents were found in the incident feed.", false);
            }

            await FollowupAsync(embed: embed.Build(), ephemeral: @private);
        }
        catch (HttpRequestException)
        {
            await FollowupAsync("Unable to retrieve public status data right now. Please try again in a moment.", ephemeral: @private);
        }
        catch (JsonException)
        {
            await FollowupAsync("Status API returned an unexpected response format.", ephemeral: @private);
        }
    }

    private static string? GetStatusPageBaseUrl(string feedUrl)
    {
        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri))
            return null;

        var builder = new UriBuilder(uri.Scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port);
        return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static bool TryGetStatusPageId(string feedUrl, out string statusPageId)
    {
        statusPageId = string.Empty;
        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri))
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pagesIndex = Array.FindIndex(segments, s => s.Equals("pages", StringComparison.OrdinalIgnoreCase));
        if (pagesIndex < 0 || pagesIndex + 1 >= segments.Length)
            return false;

        statusPageId = segments[pagesIndex + 1];
        return !string.IsNullOrWhiteSpace(statusPageId);
    }

    private static StatusOverview ParseStatusOverview(JsonElement root)
    {
        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
            return new StatusOverview("Unknown", null, null, null);

        string statusText = "Unknown";
        int? statusCode = null;
        DateTimeOffset? updatedAt = null;

        if (result.TryGetProperty("status_overall", out var statusOverall) && statusOverall.ValueKind == JsonValueKind.Object)
        {
            statusText = TryReadString(statusOverall, "status") ?? statusText;
            statusCode = TryReadInt(statusOverall, "status_code");
            updatedAt = TryReadDate(statusOverall, "updated");
        }

        var activeIncident = TryParseFirstIncident(result);
        return new StatusOverview(statusText, statusCode, updatedAt, activeIncident);
    }

    private static StatusIncident? TryParseFirstIncident(JsonElement result)
    {
        if (!result.TryGetProperty("incidents", out var incidents) || incidents.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var incident in incidents.EnumerateArray())
        {
            var name = TryReadString(incident, "name") ?? "Incident";
            var url = TryReadString(incident, "shortlink")
                ?? TryReadString(incident, "url")
                ?? TryReadString(incident, "link");

            var updated = TryReadDate(incident, "updated")
                ?? TryReadDate(incident, "updated_at")
                ?? TryReadDate(incident, "created")
                ?? TryReadDate(incident, "created_at");

            var summary = TryReadIncidentSummary(incident);
            return new StatusIncident(name, url, updated, summary);
        }

        return null;
    }

    private static async Task<StatusIncident?> TryGetMostRecentIncidentFromRssAsync(HttpClient httpClient, string feedUrl)
    {
        try
        {
            var xml = await httpClient.GetStringAsync(feedUrl);
            var doc = XDocument.Parse(xml);
            var item = doc.Descendants("item").FirstOrDefault();
            if (item is null)
                return null;

            var title = item.Element("title")?.Value?.Trim() ?? "Incident";
            var link = item.Element("link")?.Value?.Trim();
            var summaryRaw = item.Element("description")?.Value ?? string.Empty;
            var summary = HaloStatusFormatting.StripHtmlAndDecode(summaryRaw, 1024);

            DateTimeOffset? updated = null;
            var pubDate = item.Element("pubDate")?.Value;
            if (!string.IsNullOrWhiteSpace(pubDate) && DateTimeOffset.TryParse(pubDate, out var parsed))
                updated = parsed;

            return new StatusIncident(title, link, updated, summary);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadIncidentSummary(JsonElement incident)
    {
        if (!incident.TryGetProperty("updates", out var updates) || updates.ValueKind != JsonValueKind.Array)
        {
            if (!incident.TryGetProperty("messages", out updates) || updates.ValueKind != JsonValueKind.Array)
            {
                if (!incident.TryGetProperty("incident_updates", out updates) || updates.ValueKind != JsonValueKind.Array)
                    return null;
            }
        }

        foreach (var update in updates.EnumerateArray())
        {
            var raw = TryReadString(update, "body")
                ?? TryReadString(update, "message")
                ?? TryReadString(update, "content")
                ?? TryReadString(update, "details")
                ?? TryReadString(update, "text");

            if (!string.IsNullOrWhiteSpace(raw))
                return HaloStatusFormatting.StripHtmlAndDecode(raw, 1024);
        }

        return null;
    }

    private static string? TryReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();

        if (value.ValueKind == JsonValueKind.Number || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            return value.ToString();

        return null;
    }

    private static int? TryReadInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
            return intValue;

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            return parsed;

        return null;
    }

    private static DateTimeOffset? TryReadDate(JsonElement element, string property)
    {
        var raw = TryReadString(element, property);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (DateTimeOffset.TryParse(raw, out var parsed))
            return parsed;

        return null;
    }

    private static (Color color, string emoji) GetOverallAppearance(string? statusText, int? statusCode)
    {
        if (statusCode.HasValue)
        {
            return statusCode.Value switch
            {
                <= 100 => (Color.Green, "✅"),
                <= 299 => (Color.Gold, "⚠️"),
                <= 399 => (Color.Orange, "🟠"),
                _ => (Color.Red, "🔴")
            };
        }

        var normalized = statusText?.ToLowerInvariant() ?? "unknown";
        return normalized switch
        {
            _ when normalized.Contains("operational") => (Color.Green, "✅"),
            _ when normalized.Contains("minor") => (Color.Gold, "⚠️"),
            _ when normalized.Contains("major") => (Color.Orange, "🟠"),
            _ when normalized.Contains("critical") || normalized.Contains("outage") => (Color.Red, "🔴"),
            _ when normalized.Contains("maintenance") => (new Color(0x5865F2), "🔧"),
            _ => (Color.LightGrey, "ℹ️")
        };
    }

    private static string GetIndicatorLabel(string? statusText, int? statusCode)
    {
        if (statusCode.HasValue)
        {
            return statusCode.Value switch
            {
                <= 100 => "Operational",
                <= 299 => "Minor service disruption",
                <= 399 => "Major service disruption",
                _ => "Critical outage"
            };
        }

        return string.IsNullOrWhiteSpace(statusText) ? "Unknown" : statusText;
    }

    private readonly record struct StatusOverview(
        string StatusText,
        int? StatusCode,
        DateTimeOffset? UpdatedAt,
        StatusIncident? ActiveIncident);

    private readonly record struct StatusIncident(
        string Name,
        string? Url,
        DateTimeOffset? UpdatedAt,
        string? Summary);
}
