using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;

namespace DiscordBot.Modules.Moderations;

public class SpamBanModal : IModal
{
    public string Title => "Ban User";

    [InputLabel("Reason")]
    [ModalTextInput("reason", initValue: "Cross-channel spam detected")]
    public string Reason { get; set; } = string.Empty;
}

public class SpamDismissModal : IModal
{
    public string Title => "Dismiss Spam Alert";

    [InputLabel("Note (optional)")]
    [ModalTextInput("note", TextInputStyle.Short, "Optional note for the audit record")]
    public string? Note { get; set; }
}

public class SpamActionModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ModerationLogService _logService;

    public SpamActionModule(ModerationLogService logService)
    {
        _logService = logService;
    }

    [ComponentInteraction("spam_ban:*:*")]
    public async Task BanButtonAsync(string userId, string guildId)
    {
        if (Context.User is not SocketGuildUser actor || !actor.GuildPermissions.BanMembers)
        {
            await RespondAsync("❌ You need the **Ban Members** permission to use this.", ephemeral: true);
            return;
        }

        await RespondWithModalAsync<SpamBanModal>($"spam_ban_modal:{userId}:{guildId}");
    }

    [ModalInteraction("spam_ban_modal:*:*")]
    public async Task BanModalAsync(string userId, string guildId, SpamBanModal modal)
    {
        if (Context.User is not SocketGuildUser actor || !actor.GuildPermissions.BanMembers)
        {
            await RespondAsync("❌ You need the **Ban Members** permission to use this.", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        if (!ulong.TryParse(userId, out var targetUserId))
        {
            await FollowupAsync("❌ Invalid user ID in button data.", ephemeral: true);
            return;
        }

        try
        {
            await Context.Guild.AddBanAsync(targetUserId, reason: modal.Reason);

            var resultEmbed = new EmbedBuilder()
                .WithTitle("🔨 User Banned")
                .WithColor(new Color(0xE74C3C))
                .AddField("Banned by", $"<@{Context.User.Id}>", inline: true)
                .AddField("Reason", modal.Reason)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await _logService.AppendToThreadAsync((IMessageChannel)Context.Channel, resultEmbed);
            await DisableButtonsOnOriginalMessageAsync();
            await FollowupAsync("✅ User banned.", ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"❌ Ban failed: {ex.Message}", ephemeral: true);
        }
    }

    [ComponentInteraction("spam_dismiss:*:*")]
    public async Task DismissButtonAsync(string userId, string guildId)
    {
        if (Context.User is not SocketGuildUser actor || !actor.GuildPermissions.ModerateMembers)
        {
            await RespondAsync("❌ You need the **Timeout Members** permission to use this.", ephemeral: true);
            return;
        }

        await RespondWithModalAsync<SpamDismissModal>($"spam_dismiss_modal:{userId}:{guildId}");
    }

    [ModalInteraction("spam_dismiss_modal:*:*")]
    public async Task DismissModalAsync(string userId, string guildId, SpamDismissModal modal)
    {
        if (Context.User is not SocketGuildUser actor || !actor.GuildPermissions.ModerateMembers)
        {
            await RespondAsync("❌ You need the **Timeout Members** permission to use this.", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        if (!ulong.TryParse(userId, out var targetUserId))
        {
            await FollowupAsync("❌ Invalid user ID in button data.", ephemeral: true);
            return;
        }

        try
        {
            var guildUser = Context.Guild.GetUser(targetUserId);
            if (guildUser is not null)
                await guildUser.RemoveTimeOutAsync();

            var note = string.IsNullOrWhiteSpace(modal.Note) ? "*(none)*" : modal.Note;

            var resultEmbed = new EmbedBuilder()
                .WithTitle("✅ Alert Dismissed")
                .WithColor(new Color(0x95A5A6))
                .AddField("Dismissed by", $"<@{Context.User.Id}>", inline: true)
                .AddField("Note", note)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await _logService.AppendToThreadAsync((IMessageChannel)Context.Channel, resultEmbed);
            await DisableButtonsOnOriginalMessageAsync();
            await FollowupAsync("✅ Alert dismissed and timeout lifted.", ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"❌ Dismiss failed: {ex.Message}", ephemeral: true);
        }
    }

    private async Task DisableButtonsOnOriginalMessageAsync()
    {
        try
        {
            IUserMessage? message = null;

            // Try to get message from SocketMessageComponent (button interaction)
            if (Context.Interaction is SocketMessageComponent component)
            {
                message = component.Message;
            }
            // For modal interactions, the message is not directly accessible
            // This is a limitation of Discord.NET - modals don't preserve the original message reference

            if (message is null)
                return;

            await message.ModifyAsync(m =>
                m.Components = new ComponentBuilder().Build());
        }
        catch (Exception)
        {
            // Non-fatal — buttons may already be gone
        }
    }
}
