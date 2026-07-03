using Discord;
using DiscordBot.Models;
using DiscordBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HaloCommunityBot.Tests.Services;

public class ModerationLogServiceTests
{
    private static ModerationLogService BuildService(ulong forumChannelId = 0) =>
        new(null!, new ModerationLogConfig { ForumChannelId = forumChannelId }, NullLogger<ModerationLogService>.Instance);

    [Fact]
    public async Task LogActionAsync_WhenForumChannelIdIsZero_DoesNotThrow()
    {
        var service = BuildService(forumChannelId: 0);
        var entry = new ModerationLogEntry(
            ModerationActionType.Ban,
            null,
            123456789UL,
            null,
            "test reason",
            DateTimeOffset.UtcNow);

        var ex = await Record.ExceptionAsync(() => service.LogActionAsync(entry));
        Assert.Null(ex);
    }

    [Fact]
    public async Task LogSpamDetectedAsync_WhenForumChannelIdIsZero_DoesNotThrow()
    {
        var service = BuildService(forumChannelId: 0);

        var ex = await Record.ExceptionAsync(() =>
            service.LogSpamDetectedAsync(null!, [], "text|", []));
        Assert.Null(ex);
    }

    [Fact]
    public async Task AppendToThreadAsync_WhenChannelIsNull_DoesNotThrow()
    {
        var service = BuildService(forumChannelId: 0);
        var embed = new EmbedBuilder().WithTitle("test").Build();

        var ex = await Record.ExceptionAsync(() =>
            service.AppendToThreadAsync(null!, embed));
        Assert.Null(ex);
    }

    [Fact]
    public async Task LogSpamDetectedAsync_WithImageBytes_DoesNotThrow()
    {
        var service = BuildService(forumChannelId: 0);
        var imageBytes = new byte[] { 1, 2, 3, 4 };

        var ex = await Record.ExceptionAsync(() =>
            service.LogSpamDetectedAsync(null!, [], "text|", [], imageBytes, "spam.png"));
        Assert.Null(ex);
    }
}
