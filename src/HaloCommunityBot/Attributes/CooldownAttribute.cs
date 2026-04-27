using Discord;
using Discord.Interactions;
using System.Collections.Concurrent;

namespace DiscordBot.Attributes;

/// <summary>
/// Adds a per-user cooldown for a command to reduce spam.
/// </summary>
public class CooldownAttribute : PreconditionAttribute
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Cooldowns = new();
    private readonly TimeSpan _duration;

    /// <summary>
    /// Creates a cooldown period in seconds.
    /// </summary>
    public CooldownAttribute(int seconds)
    {
        if (seconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(seconds), "Cooldown duration must be greater than zero.");

        _duration = TimeSpan.FromSeconds(seconds);
    }

    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        var now = DateTimeOffset.UtcNow;
        var commandKey = commandInfo.Name ?? "unknown";
        var key = $"{context.User.Id}:{commandKey}";

        if (Cooldowns.TryGetValue(key, out var expiresAt) && expiresAt > now)
        {
            var remaining = expiresAt - now;
            var remainingSeconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
            return Task.FromResult(PreconditionResult.FromError(
                $"You're using this command too quickly. Try again in {remainingSeconds}s."));
        }

        Cooldowns[key] = now.Add(_duration);
        return Task.FromResult(PreconditionResult.FromSuccess());
    }
}
