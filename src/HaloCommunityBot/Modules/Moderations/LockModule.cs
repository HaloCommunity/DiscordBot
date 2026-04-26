using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordBot.Modules.Moderations;
public class LockModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("lock", "Lock this channel")]
    [RequireUserPermission(GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageChannels)]
    public async Task LockAsync()
    {
        var channel = (SocketTextChannel)Context.Channel;
        var everyone = channel.Guild.EveryoneRole;

        await channel.AddPermissionOverwriteAsync(everyone, new OverwritePermissions(sendMessages: PermValue.Deny));
        await RespondAsync("Channel has been locked 🔒");
    }

    [SlashCommand("unlock", "Unlock this channel")]
    [RequireUserPermission(GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageChannels)]
    public async Task UnlockAsync()
    {
        var channel = (SocketTextChannel)Context.Channel;
        var everyone = channel.Guild.EveryoneRole;

        await channel.AddPermissionOverwriteAsync(everyone, new OverwritePermissions(sendMessages: PermValue.Allow));
        await RespondAsync("Channel has been unlocked 🔓");
    }

}
