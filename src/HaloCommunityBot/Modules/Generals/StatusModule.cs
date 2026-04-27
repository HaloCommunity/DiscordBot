using Discord;
using Discord.Interactions;
using DiscordBot.Models;
using DiscordBot.Services;
using System.Text.Json;

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

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HaloCommunityBot/1.0");

            var statusJson = await httpClient.GetStringAsync($"{statusPageBaseUrl}/api/v2/status.json");
            var unresolvedJson = await httpClient.GetStringAsync($"{statusPageBaseUrl}/api/v2/incidents/unresolved.json");
            var incidentsJson = await httpClient.GetStringAsync($"{statusPageBaseUrl}/api/v2/incidents.json");

            using var statusDoc = JsonDocument.Parse(statusJson);
            using var unresolvedDoc = JsonDocument.Parse(unresolvedJson);
            using var incidentsDoc = JsonDocument.Parse(incidentsJson);

            var indicator = GetNestedString(statusDoc.RootElement, "status", "indicator") ?? "none";
            var description = GetNestedString(statusDoc.RootElement, "status", "description") ?? "Unknown";

            var (color, emoji) = GetOverallAppearance(indicator);
            var indicatorLabel = GetIndicatorLabel(indicator);

            var activeIncident = GetFirstIncident(unresolvedDoc.RootElement);
            var incidentToShow = activeIncident ?? GetFirstIncident(incidentsDoc.RootElement);
            var incidentIsActive = activeIncident.HasValue;

            var embed = new EmbedBuilder()
                .WithTitle($"{emoji} Halo Services Status Overview")
                .WithColor(color)
                .WithUrl(statusPageBaseUrl)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithFooter($"Source: {new Uri(statusPageBaseUrl).Host}")
                .AddField("Current Status", $"{emoji} {description}\nIndicator: `{indicator}` ({indicatorLabel})", false);

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
            await FollowupAsync($"Unable to retrieve status data from {statusPageBaseUrl} right now. Please try again in a moment.", ephemeral: @private);
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

    private static string? GetNestedString(JsonElement root, string parentName, string propertyName)
    {
        if (!root.TryGetProperty(parentName, out var parent))
            return null;

        if (!parent.TryGetProperty(propertyName, out var value))
            return null;

        return value.GetString();
    }

    private static StatusIncident? GetFirstIncident(JsonElement root)
    {
        if (!root.TryGetProperty("incidents", out var incidents) || incidents.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var incident in incidents.EnumerateArray())
        {
            var name = incident.TryGetProperty("name", out var nameEl)
                ? nameEl.GetString() ?? "Incident"
                : "Incident";

            var shortLink = incident.TryGetProperty("shortlink", out var shortLinkEl)
                ? shortLinkEl.GetString()
                : null;

            DateTimeOffset? updatedAt = null;
            if (incident.TryGetProperty("updated_at", out var updatedAtEl)
                && DateTimeOffset.TryParse(updatedAtEl.GetString(), out var parsedUpdatedAt))
            {
                updatedAt = parsedUpdatedAt;
            }

            string? summary = null;
            if (incident.TryGetProperty("incident_updates", out var updates)
                && updates.ValueKind == JsonValueKind.Array)
            {
                foreach (var update in updates.EnumerateArray())
                {
                    if (!update.TryGetProperty("body", out var bodyEl))
                        continue;

                    var body = bodyEl.GetString() ?? string.Empty;
                    summary = HaloStatusFormatting.StripHtmlAndDecode(body, 1024);
                    if (!string.IsNullOrWhiteSpace(summary))
                        break;
                }
            }

            return new StatusIncident(name, shortLink, updatedAt, summary);
        }

        return null;
    }

    private static (Color color, string emoji) GetOverallAppearance(string indicator)
    {
        var normalized = indicator?.ToLowerInvariant() ?? "unknown";
        return normalized switch
        {
            "none" => (Color.Green, "✅"),
            "minor" => (Color.Gold, "⚠️"),
            "major" => (Color.Orange, "🟠"),
            "critical" => (Color.Red, "🔴"),
            "maintenance" => (new Color(0x5865F2), "🔧"),
            _ => (Color.LightGrey, "ℹ️")
        };
    }

    private static string GetIndicatorLabel(string indicator)
    {
        var normalized = indicator?.ToLowerInvariant() ?? "unknown";
        return normalized switch
        {
            "none" => "Operational",
            "minor" => "Minor service disruption",
            "major" => "Major service disruption",
            "critical" => "Critical outage",
            "maintenance" => "Maintenance",
            _ => "Unknown"
        };
    }

    private readonly record struct StatusIncident(
        string Name,
        string? Url,
        DateTimeOffset? UpdatedAt,
        string? Summary);
}
