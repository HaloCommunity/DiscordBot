using Discord;
using System.Net;
using System.Text.RegularExpressions;

namespace DiscordBot.Services;

internal static partial class HaloStatusFormatting
{
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    internal static string StripHtmlAndDecode(string rawDescription, int maxLength = 2048)
    {
        var description = WebUtility.HtmlDecode(HtmlTagRegex().Replace(rawDescription ?? string.Empty, string.Empty)).Trim();

        if (description.Length > maxLength)
            return string.Concat(description.AsSpan(0, maxLength - 3), "...");

        return description;
    }

    /// <summary>
    /// Determines the embed colour and emoji based on keywords in the status item title.
    /// </summary>
    internal static (Color color, string emoji) DetermineStatusAppearance(string title)
    {
        var lower = (title ?? string.Empty).ToLowerInvariant();

        return lower switch
        {
            _ when lower.Contains("resolved") || lower.Contains("completed") || lower.Contains("postmortem")
                => (Color.Green, "✅"),
            _ when lower.Contains("investigating") || lower.Contains("outage")
                => (Color.Red, "🔴"),
            _ when lower.Contains("identified")
                => (new Color(0xFF6B35), "🟠"),
            _ when lower.Contains("monitoring")
                => (Color.Gold, "👀"),
            _ when lower.Contains("in progress")
                => (Color.Orange, "⚙️"),
            _ when lower.Contains("maintenance") || lower.Contains("scheduled")
                => (new Color(0x5865F2), "🔧"),
            _ when lower.Contains("degraded") || lower.Contains("partial")
                => (Color.Orange, "⚠️"),
            _ => (Color.LightOrange, "ℹ️")
        };
    }
}
