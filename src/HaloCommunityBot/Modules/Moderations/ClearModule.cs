using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordBot.Modules.Moderations;

public class ClearModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("clear", "Delete messages in the channel")]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    [RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task ClearAsync(
            [Summary("amount", "Number of messages to delete (1-100)")]
            [MinValue(1)]
            [MaxValue(100)]
            int amount)
    {
        // Defer response as deleting messages can take time
        await DeferAsync(ephemeral: true);

        try
        {
            var channel = Context.Channel as ITextChannel;
            if (channel == null)
            {
                await FollowupAsync("❌ This command can only be used in a text channel!", ephemeral: true);
                return;
            }

            if (Context.Guild == null || channel is not IGuildChannel guildChannel)
            {
                await FollowupAsync("❌ Could not resolve guild channel permissions for this command.", ephemeral: true);
                return;
            }

            // Channel-level permission checks are clearer than relying only on command preconditions.
            var botPerms = Context.Guild.CurrentUser.GetPermissions(guildChannel);
            var missingBotPerms = new List<string>();

            if (!botPerms.ViewChannel)
                missingBotPerms.Add("View Channel");
            if (!botPerms.ReadMessageHistory)
                missingBotPerms.Add("Read Message History");
            if (!botPerms.ManageMessages)
                missingBotPerms.Add("Manage Messages");
            if (!botPerms.SendMessages)
                missingBotPerms.Add("Send Messages");

            if (missingBotPerms.Count > 0)
            {
                await FollowupAsync(
                    $"❌ I don't have enough permissions in #{guildChannel.Name}.\n" +
                    $"Missing: {string.Join(", ", missingBotPerms)}",
                    ephemeral: true);
                return;
            }

            if (Context.User is IGuildUser invokingUser)
            {
                var userPerms = invokingUser.GetPermissions(guildChannel);
                if (!userPerms.ManageMessages)
                {
                    await FollowupAsync(
                        $"❌ You don't have **Manage Messages** in #{guildChannel.Name}.",
                        ephemeral: true);
                    return;
                }
            }

            // Get messages (excluding slash command message)
            var messages = await channel.GetMessagesAsync(amount, CacheMode.AllowDownload).FlattenAsync();

            if (!messages.Any())
            {
                await FollowupAsync("❌ No messages found to delete!", ephemeral: true);
                return;
            }

            // Filter deletable messages and track why messages are skipped.
            var deletableMessages = new List<IMessage>();
            var now = DateTimeOffset.UtcNow;
            int skippedOld = 0;
            int skippedType = 0;

            foreach (var message in messages)
            {
                // A message is deletable only when it is under 14 days old and is a normal/reply message.
                var isRecent = (now - message.Timestamp).TotalDays < 14;
                var isSupportedType = message.Type == MessageType.Default || message.Type == MessageType.Reply;

                if (isRecent && isSupportedType)
                {
                    deletableMessages.Add(message);
                }
                else if (!isRecent)
                {
                    skippedOld++;
                }
                else
                {
                    skippedType++;
                }
            }

            if (!deletableMessages.Any())
            {
                await FollowupAsync(
                    $"❌ No messages could be deleted from the last {messages.Count()} checked.\n" +
                    $"• Older than 14 days: {skippedOld}\n" +
                    $"• Unsupported/system message types: {skippedType}",
                    ephemeral: true);
                return;
            }

            int successfulDeletes = 0;
            int failedDeletes = 0;

            // Delete each message safely
            if (deletableMessages.Count == 1)
            {
                try
                {
                    await deletableMessages[0].DeleteAsync();
                    successfulDeletes = 1;
                }
                catch (Discord.Net.HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownMessage)
                {
                    failedDeletes = 1;
                }
            }
            else
            {
                // Split into batches to avoid rate limit
                var batches = deletableMessages.Chunk(100); // Discord limits 100 messages per batch

                foreach (var batch in batches)
                {
                    try
                    {
                        // Check if the message exists before deleting
                        var validMessages = new List<IMessage>();
                        foreach (var msg in batch)
                        {
                            try
                            {
                                // Try to get the message to check if it still exists
                                var checkMsg = await channel.GetMessageAsync(msg.Id);
                                if (checkMsg != null)
                                {
                                    validMessages.Add(msg);
                                }
                            }
                            catch
                            {
                                failedDeletes++;
                            }
                        }

                        if (validMessages.Count > 1)
                        {
                            await channel.DeleteMessagesAsync(validMessages);
                            successfulDeletes += validMessages.Count;
                        }
                        else if (validMessages.Count == 1)
                        {
                            await validMessages[0].DeleteAsync();
                            successfulDeletes += 1;
                        }

                        // Short delay to avoid rate limit
                        if (batches.Count() > 1)
                            await Task.Delay(1000);
                    }
                    catch (Discord.Net.HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownMessage)
                    {
                        failedDeletes += batch.Count();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting batch: {ex.Message}");
                        failedDeletes += batch.Count();
                    }
                }
            }

            // Create response message
            var responseMessage =
                $"✅ Deleted {successfulDeletes} messages from the last {messages.Count()} checked.";
            if (skippedOld > 0 || skippedType > 0)
            {
                responseMessage +=
                    $"\nℹ️ Skipped {skippedOld} old and {skippedType} unsupported/system messages.";
            }
            if (failedDeletes > 0)
            {
                responseMessage += $"\n⚠️ Failed to delete {failedDeletes} messages (may have been deleted or are system messages).";
            }

            await FollowupAsync(responseMessage, ephemeral: true);
        }
        catch (Discord.Net.HttpException ex) when (ex.DiscordCode == DiscordErrorCode.MissingPermissions)
        {
            await FollowupAsync("❌ Bot does not have permission to delete messages in this channel!", ephemeral: true);
        }
        catch (Discord.Net.HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownMessage)
        {
            await FollowupAsync("❌ Some messages do not exist or have been deleted!", ephemeral: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ClearAsync: {ex}");
            await FollowupAsync($"❌ An unexpected error occurred: {ex.Message}", ephemeral: true);
        }
    }
}
