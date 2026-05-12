namespace DiscordBot.Services;

public static class YoutubeChannelReferenceParser
{
    /// <summary>
    /// Normalizes a YouTube channel reference to a channel ID.
    /// Supports: UCxxxxxx (ID), @handle (handle), URL, or channel name.
    /// </summary>
    public static bool TryNormalize(string input, out string channelId)
    {
        channelId = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();

        // Handle @mention format
        if (trimmed.StartsWith("@"))
        {
            channelId = trimmed.Substring(1).Trim();
            return !string.IsNullOrWhiteSpace(channelId);
        }

        // Handle URLs
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var queryParts = uri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => Uri.UnescapeDataString(parts[0]), parts => Uri.UnescapeDataString(parts[1]), StringComparer.OrdinalIgnoreCase);

            if (queryParts.TryGetValue("channel_id", out var queryChannelId) && !string.IsNullOrWhiteSpace(queryChannelId))
            {
                channelId = queryChannelId.Trim();
                return true;
            }

            var pathSegments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (pathSegments.Length >= 2 && pathSegments[0].Equals("channel", StringComparison.OrdinalIgnoreCase))
            {
                channelId = pathSegments[1];
                return true;
            }
        }

        // Treat as ID or handle
        channelId = trimmed;
        return true;
    }
}
