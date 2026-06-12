# Single Message Per User Channel Enforcement — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce a one-message-per-user rule in designated Discord channels across HaloCommunityBot, HuduCommunityBot, and PandaBot, with slash commands to enable/disable per channel, list who has posted, and reset individual users.

**Architecture:** A `SingleMessageService` singleton owns all business logic — it subscribes to `MessageReceived` events via `DiscordBotService`, checks an in-memory set of registered channel IDs (from config), queries/writes to two new EF Core tables, and deletes violating messages. A `SingleMessageModule` provides four ephemeral slash commands that delegate entirely to the service. Configuration declares eligible channels in `appsettings.json`; the DB stores runtime enable/disable state and per-user posted records.

**Tech Stack:** .NET 10, Discord.NET 3.19.1, EF Core 10 (SQLite), xunit 2.9.3, `IServiceScopeFactory` for safe scoped-DB access from singleton service.

---

## Repo Paths

| Bot | Repo root | Namespace | DB context |
|---|---|---|---|
| HaloCommunityBot | `j:/Projects/HaloCommunity/bot` | `DiscordBot` | `HaloCommunityBotContext` |
| HuduCommunityBot | `j:/Projects/HuduCommunity/bot` | `DiscordBot` | `HuduCommunityBotContext` |
| PandaBot | `j:/Projects/Pandamonium/bot` | `DiscordBot` / `PandaBot` (see notes) | `PandaBotContext` |

**PandaBot namespace note:** Service files, module files, and extension methods use the `DiscordBot.*` namespace (matching existing conventions), but model files and the context use the `PandaBot.*` namespace. Config root section is `SingleMessage` at the top level (same as all three bots).

---

## File Map

### HaloCommunityBot (`src/HaloCommunityBot/`)

| Action | Path |
|---|---|
| Create | `Models/SingleMessageChannelConfig.cs` |
| Create | `Models/SingleMessageChannelState.cs` |
| Create | `Models/SingleMessageRecord.cs` |
| Modify | `Core/Data/HaloCommunityBotContext.cs` |
| Create | `Migrations/20260612120000_AddSingleMessage.cs` |
| Modify | `Migrations/HaloCommunityBotContextModelSnapshot.cs` |
| Create | `Services/SingleMessageService.cs` |
| Create | `Modules/Moderations/SingleMessageModule.cs` |
| Modify | `Extensions/ServiceCollectionExtensions.cs` |
| Modify | `Services/DiscordBotService.cs` |
| Modify | `appsettings.json` |
| Create | `tests/HaloCommunityBot.Tests/HaloCommunityBot.Tests.csproj` |
| Create | `tests/HaloCommunityBot.Tests/SingleMessageServiceTests.cs` |
| Modify | `HaloCommunityBot.sln` |

### HuduCommunityBot (`src/HuduCommunityBot/`)

Same structure as Halo; context is `HuduCommunityBotContext`, test project already exists at `tests/HuduCommunityBot.Tests/`.

| Action | Path |
|---|---|
| Create | `Models/SingleMessageChannelConfig.cs` |
| Create | `Models/SingleMessageChannelState.cs` |
| Create | `Models/SingleMessageRecord.cs` |
| Modify | `Core/Data/HuduCommunityBotContext.cs` |
| Create | `Migrations/20260612120000_AddSingleMessage.cs` |
| Modify | `Migrations/HuduCommunityBotContextModelSnapshot.cs` |
| Create | `Services/SingleMessageService.cs` |
| Create | `Modules/Moderations/SingleMessageModule.cs` |
| Modify | `Extensions/ServiceCollectionExtensions.cs` |
| Modify | `Services/DiscordBotService.cs` |
| Modify | `appsettings.json` |
| Create | `tests/HuduCommunityBot.Tests/SingleMessageServiceTests.cs` |

### PandaBot (`src/PandaBot/`)

Moderation folder is `Moderation` (singular). Models use `PandaBot.Models` namespace. Context is `PandaBotContext`.

| Action | Path |
|---|---|
| Create | `Models/SingleMessageChannelConfig.cs` |
| Create | `Models/SingleMessageChannelState.cs` |
| Create | `Models/SingleMessageRecord.cs` |
| Modify | `Core/Data/PandaBotContext.cs` |
| Create | `Migrations/20260612120000_AddSingleMessage.cs` |
| Modify | `Migrations/PandaBotContextModelSnapshot.cs` |
| Create | `Services/SingleMessageService.cs` |
| Create | `Modules/Moderation/SingleMessageModule.cs` |
| Modify | `Extensions/ServiceCollectionExtensions.cs` |
| Modify | `Services/DiscordBotService.cs` |
| Modify | `appsettings.json` |
| Create | `tests/PandaBot.Tests/PandaBot.Tests.csproj` |
| Create | `tests/PandaBot.Tests/SingleMessageServiceTests.cs` |
| Modify | `PandaBot.sln` |

---

## PART 1 — HaloCommunityBot

### Task 1: Models

**Files:**
- Create: `j:/Projects/HaloCommunity/bot/src/HaloCommunityBot/Models/SingleMessageChannelConfig.cs`
- Create: `j:/Projects/HaloCommunity/bot/src/HaloCommunityBot/Models/SingleMessageChannelState.cs`
- Create: `j:/Projects/HaloCommunity/bot/src/HaloCommunityBot/Models/SingleMessageRecord.cs`

- [ ] **Step 1: Create config POCO**

`src/HaloCommunityBot/Models/SingleMessageChannelConfig.cs`:

```csharp
namespace DiscordBot.Models;

public class SingleMessageChannelConfig
{
    public ulong ChannelId { get; set; }
    public bool ScanHistoryOnEnable { get; set; } = false;
}
```

- [ ] **Step 2: Create EF entity — channel state**

`src/HaloCommunityBot/Models/SingleMessageChannelState.cs`:

```csharp
namespace DiscordBot.Models;

public class SingleMessageChannelState
{
    public ulong ChannelId { get; set; }
    public bool IsEnabled { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 3: Create EF entity — posted record**

`src/HaloCommunityBot/Models/SingleMessageRecord.cs`:

```csharp
namespace DiscordBot.Models;

public class SingleMessageRecord
{
    public int Id { get; set; }
    public ulong ChannelId { get; set; }
    public ulong UserId { get; set; }
    public ulong MessageId { get; set; }
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;

    public SingleMessageChannelState? Channel { get; set; }
}
```

- [ ] **Step 4: Commit**

```bash
cd j:/Projects/HaloCommunity/bot
git add src/HaloCommunityBot/Models/SingleMessageChannelConfig.cs src/HaloCommunityBot/Models/SingleMessageChannelState.cs src/HaloCommunityBot/Models/SingleMessageRecord.cs
git commit -m "feat: add SingleMessage model classes for HaloCommunityBot"
```

---

### Task 2: Database Context + Migration

**Files:**
- Modify: `j:/Projects/HaloCommunity/bot/src/HaloCommunityBot/Core/Data/HaloCommunityBotContext.cs`
- Create: `j:/Projects/HaloCommunity/bot/src/HaloCommunityBot/Migrations/20260612120000_AddSingleMessage.cs`
- Modify: `j:/Projects/HaloCommunity/bot/src/HaloCommunityBot/Migrations/HaloCommunityBotContextModelSnapshot.cs`

- [ ] **Step 1: Register DbSets and configure model in context**

In `src/HaloCommunityBot/Core/Data/HaloCommunityBotContext.cs`, add the two new `DbSet` properties and their `OnModelCreating` configuration. The final file should look like:

```csharp
using DiscordBot.Models;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Core.Data;

public class HaloCommunityBotContext : DbContext
{
    public HaloCommunityBotContext(DbContextOptions<HaloCommunityBotContext> options) : base(options)
    {
    }

    public DbSet<FeedPostState> FeedPostStates => Set<FeedPostState>();
    public DbSet<YoutubeMonitorSettings> YoutubeMonitorSettings => Set<YoutubeMonitorSettings>();
    public DbSet<YoutubeTrackedChannel> YoutubeTrackedChannels => Set<YoutubeTrackedChannel>();
    public DbSet<SingleMessageChannelState> SingleMessageChannelStates => Set<SingleMessageChannelState>();
    public DbSet<SingleMessageRecord> SingleMessageRecords => Set<SingleMessageRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FeedPostState>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.FeedType, x.SourceId }).IsUnique();
            entity.Property(x => x.FeedType).IsRequired();
            entity.Property(x => x.SourceId).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<YoutubeMonitorSettings>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Enabled).HasDefaultValue(false);
            entity.Property(x => x.PollIntervalMinutes).HasDefaultValue(15);
            entity.Property(x => x.DefaultPostTitleTemplate).HasDefaultValue("[{ChannelName}] {VideoTitle}");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<YoutubeTrackedChannel>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ChannelId).IsUnique();
            entity.Property(x => x.ChannelId).IsRequired();
            entity.Property(x => x.ChannelName).IsRequired();
            entity.Property(x => x.KeywordFilters).IsRequired(false);
            entity.Property(x => x.IsEnabled).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<SingleMessageChannelState>(entity =>
        {
            entity.HasKey(x => x.ChannelId);
            entity.Property(x => x.IsEnabled).HasDefaultValue(false);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<SingleMessageRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ChannelId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Channel)
                .WithMany()
                .HasForeignKey(x => x.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.PostedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}
```

- [ ] **Step 2: Generate the EF migration**

Run from the solution root:

```bash
cd j:/Projects/HaloCommunity/bot
dotnet ef migrations add AddSingleMessage --project src/HaloCommunityBot --output-dir Migrations
```

Expected output:
```
Build started...
Build succeeded.
Done. To undo this action, use 'ef migrations remove'
```

Two files will be created/updated:
- `src/HaloCommunityBot/Migrations/20260612XXXXXX_AddSingleMessage.cs`
- `src/HaloCommunityBot/Migrations/HaloCommunityBotContextModelSnapshot.cs` (updated)

- [ ] **Step 3: Verify the generated migration creates both tables**

Open the generated `Migrations/20260612XXXXXX_AddSingleMessage.cs` and confirm it contains `migrationBuilder.CreateTable` calls for both `SingleMessageChannelStates` and `SingleMessageRecords`, and that `Down()` drops both tables. The `SingleMessageRecords` table should have a foreign key to `SingleMessageChannelStates`.

- [ ] **Step 4: Verify the migration applies cleanly**

```bash
cd j:/Projects/HaloCommunity/bot
dotnet ef database update --project src/HaloCommunityBot
```

Expected: `Applying migration '20260612XXXXXX_AddSingleMessage'.` followed by success.

- [ ] **Step 5: Commit**

```bash
cd j:/Projects/HaloCommunity/bot
git add src/HaloCommunityBot/Core/Data/HaloCommunityBotContext.cs src/HaloCommunityBot/Migrations/
git commit -m "feat: add SingleMessage EF migration for HaloCommunityBot"
```

---

### Task 3: SingleMessageService

**Files:**
- Create: `j:/Projects/HaloCommunity/bot/src/HaloCommunityBot/Services/SingleMessageService.cs`

- [ ] **Step 1: Create the service file**

`src/HaloCommunityBot/Services/SingleMessageService.cs`:

```csharp
using Discord;
using Discord.WebSocket;
using DiscordBot.Core.Data;
using DiscordBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Services;

public class SingleMessageService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<SingleMessageService> _logger;
    private readonly HashSet<ulong> _registeredChannelIds;
    private readonly Dictionary<ulong, SingleMessageChannelConfig> _channelConfigs;

    public SingleMessageService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        DiscordSocketClient client,
        ILogger<SingleMessageService> logger)
    {
        _scopeFactory = scopeFactory;
        _client = client;
        _logger = logger;

        var configs = configuration
            .GetSection("SingleMessage:Channels")
            .Get<List<SingleMessageChannelConfig>>() ?? [];

        _channelConfigs = configs.ToDictionary(c => c.ChannelId);
        _registeredChannelIds = [.. _channelConfigs.Keys];
    }

    public bool IsRegisteredChannel(ulong channelId) => _registeredChannelIds.Contains(channelId);

    public async Task HandleMessageAsync(SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage message) return;
        if (message.Author.IsBot) return;
        if (message.Channel is not SocketTextChannel channel) return;

        var channelId = channel.Id;
        if (!_registeredChannelIds.Contains(channelId)) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HaloCommunityBotContext>();

        var state = await db.SingleMessageChannelStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ChannelId == channelId);

        if (state is null || !state.IsEnabled) return;

        var userId = message.Author.Id;
        var existing = await db.SingleMessageRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ChannelId == channelId && r.UserId == userId);

        if (existing is null)
        {
            db.SingleMessageRecords.Add(new SingleMessageRecord
            {
                ChannelId = channelId,
                UserId = userId,
                MessageId = message.Id,
                PostedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return;
        }

        try
        {
            await message.DeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete duplicate message {MessageId} in channel {ChannelId}", message.Id, channelId);
            return;
        }

        try
        {
            var notification = await channel.SendMessageAsync(
                $"<@{userId}> This channel only allows one message per user. Your original message has been kept.");
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                try { await notification.DeleteAsync(); } catch { }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send enforcement notification in channel {ChannelId}", channelId);
        }
    }

    public async Task<string> EnableChannelAsync(ulong channelId, ulong guildId)
    {
        if (!_channelConfigs.TryGetValue(channelId, out var config))
            return $"❌ <#{channelId}> is not registered in the bot's configuration. Add it to `SingleMessage:Channels` in appsettings.json first.";

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HaloCommunityBotContext>();

        var state = await db.SingleMessageChannelStates.FindAsync(channelId);
        if (state is null)
        {
            state = new SingleMessageChannelState { ChannelId = channelId, IsEnabled = true };
            db.SingleMessageChannelStates.Add(state);
        }
        else
        {
            state.IsEnabled = true;
            state.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        int prePopulated = 0;
        if (config.ScanHistoryOnEnable)
        {
            prePopulated = await ScanHistoryAsync(db, channelId, guildId);
        }

        var suffix = prePopulated > 0
            ? $" {prePopulated} existing user(s) pre-populated from message history."
            : string.Empty;

        return $"✅ Single-message enforcement enabled for <#{channelId}>.{suffix}";
    }

    public async Task<string> DisableChannelAsync(ulong channelId)
    {
        if (!_registeredChannelIds.Contains(channelId))
            return $"❌ <#{channelId}> is not registered in the bot's configuration.";

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HaloCommunityBotContext>();

        var state = await db.SingleMessageChannelStates.FindAsync(channelId);
        if (state is null)
            return $"ℹ️ <#{channelId}> has no active enforcement state to disable.";

        state.IsEnabled = false;
        state.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return $"✅ Single-message enforcement disabled for <#{channelId}>. Existing records retained.";
    }

    public async Task<string> ResetUserAsync(ulong channelId, ulong userId, string userMention)
    {
        if (!_registeredChannelIds.Contains(channelId))
            return $"❌ <#{channelId}> is not registered in the bot's configuration.";

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HaloCommunityBotContext>();

        var record = await db.SingleMessageRecords
            .FirstOrDefaultAsync(r => r.ChannelId == channelId && r.UserId == userId);

        if (record is null)
            return $"ℹ️ No record found for {userMention} in <#{channelId}>.";

        db.SingleMessageRecords.Remove(record);
        await db.SaveChangesAsync();

        return $"✅ {userMention} has been reset in <#{channelId}> and may post again.";
    }

    public async Task<IReadOnlyList<SingleMessageRecord>> ListPostedUsersAsync(ulong channelId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HaloCommunityBotContext>();

        return await db.SingleMessageRecords
            .AsNoTracking()
            .Where(r => r.ChannelId == channelId)
            .OrderBy(r => r.PostedAt)
            .ToListAsync();
    }

    private async Task<int> ScanHistoryAsync(HaloCommunityBotContext db, ulong channelId, ulong guildId)
    {
        var guild = _client.GetGuild(guildId);
        if (guild?.GetChannel(channelId) is not ITextChannel textChannel)
        {
            _logger.LogWarning("Could not resolve channel {ChannelId} in guild {GuildId} for history scan", channelId, guildId);
            return 0;
        }

        var existingUserIds = await db.SingleMessageRecords
            .Where(r => r.ChannelId == channelId)
            .Select(r => r.UserId)
            .ToHashSetAsync();

        var messages = await textChannel.GetMessagesAsync(100).FlattenAsync();
        var newRecords = messages
            .Where(m => !m.Author.IsBot && !existingUserIds.Contains(m.Author.Id))
            .GroupBy(m => m.Author.Id)
            .Select(g => g.OrderBy(m => m.Timestamp).First())
            .Select(m => new SingleMessageRecord
            {
                ChannelId = channelId,
                UserId = m.Author.Id,
                MessageId = m.Id,
                PostedAt = m.Timestamp.UtcDateTime
            })
            .ToList();

        if (newRecords.Count > 0)
        {
            db.SingleMessageRecords.AddRange(newRecords);
            await db.SaveChangesAsync();
        }

        return newRecords.Count;
    }
}
```

- [ ] **Step 2: Build to verify no compilation errors**

```bash
cd j:/Projects/HaloCommunity/bot
dotnet build src/HaloCommunityBot/HaloCommunityBot.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd j:/Projects/HaloCommunity/bot
git add src/HaloCommunityBot/Services/SingleMessageService.cs
git commit -m "feat: add SingleMessageService for HaloCommunityBot"
```

---

### Task 4: Slash Command Module

**Files:**
- Create: `j:/Projects/HaloCommunity/bot/src/HaloCommunityBot/Modules/Moderations/SingleMessageModule.cs`

- [ ] **Step 1: Create the module**

`src/HaloCommunityBot/Modules/Moderations/SingleMessageModule.cs`:

```csharp
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;

namespace DiscordBot.Modules.Moderations;

[Group("singlemessage", "Manage single-message-per-user channel enforcement")]
[RequireUserPermission(GuildPermission.ManageChannels)]
public class SingleMessageModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly SingleMessageService _service;

    public SingleMessageModule(SingleMessageService service)
    {
        _service = service;
    }

    [SlashCommand("enable", "Enable single-message enforcement for a channel")]
    public async Task EnableAsync(
        [Summary("channel", "Channel to enable (defaults to current)")] SocketTextChannel? channel = null)
    {
        var target = channel ?? (SocketTextChannel)Context.Channel;
        var result = await _service.EnableChannelAsync(target.Id, Context.Guild.Id);
        await RespondAsync(result, ephemeral: true);
    }

    [SlashCommand("disable", "Disable single-message enforcement for a channel")]
    public async Task DisableAsync(
        [Summary("channel", "Channel to disable (defaults to current)")] SocketTextChannel? channel = null)
    {
        var target = channel ?? (SocketTextChannel)Context.Channel;
        var result = await _service.DisableChannelAsync(target.Id);
        await RespondAsync(result, ephemeral: true);
    }

    [SlashCommand("reset-user", "Allow a user to post again in a single-message channel")]
    public async Task ResetUserAsync(
        [Summary("user", "User to reset")] SocketGuildUser user,
        [Summary("channel", "Channel to reset in (defaults to current)")] SocketTextChannel? channel = null)
    {
        var target = channel ?? (SocketTextChannel)Context.Channel;
        var result = await _service.ResetUserAsync(target.Id, user.Id, user.Mention);
        await RespondAsync(result, ephemeral: true);
    }

    [SlashCommand("list", "List users who have posted in a single-message channel")]
    public async Task ListAsync(
        [Summary("channel", "Channel to list (defaults to current)")] SocketTextChannel? channel = null)
    {
        var target = channel ?? (SocketTextChannel)Context.Channel;

        if (!_service.IsRegisteredChannel(target.Id))
        {
            await RespondAsync($"❌ <#{target.Id}> is not registered as a single-message channel.", ephemeral: true);
            return;
        }

        var records = await _service.ListPostedUsersAsync(target.Id);

        if (records.Count == 0)
        {
            await RespondAsync($"ℹ️ No posts recorded in <#{target.Id}> yet.", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle($"Posted users in #{target.Name}")
            .WithColor(Color.Blue)
            .WithFooter($"{records.Count} user(s) total");

        foreach (var record in records.Take(25))
        {
            var messageLink = $"https://discord.com/channels/{Context.Guild.Id}/{record.ChannelId}/{record.MessageId}";
            embed.AddField(
                $"<@{record.UserId}>",
                $"[View message]({messageLink}) — <t:{new DateTimeOffset(record.PostedAt).ToUnixTimeSeconds()}:R>");
        }

        if (records.Count > 25)
            embed.WithDescription($"Showing first 25 of {records.Count} users.");

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
cd j:/Projects/HaloCommunity/bot
dotnet build src/HaloCommunityBot/HaloCommunityBot.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd j:/Projects/HaloCommunity/bot
git add src/HaloCommunityBot/Modules/Moderations/SingleMessageModule.cs
git commit -m "feat: add SingleMessageModule slash commands for HaloCommunityBot"
```

---

### Task 5: DI Registration, Event Wiring, and Config

**Files:**
- Modify: `j:/Projects/HaloCommunity/bot/src/HaloCommunityBot/Extensions/ServiceCollectionExtensions.cs`
- Modify: `j:/Projects/HaloCommunity/bot/src/HaloCommunityBot/Services/DiscordBotService.cs`
- Modify: `j:/Projects/HaloCommunity/bot/src/HaloCommunityBot/appsettings.json`

- [ ] **Step 1: Register SingleMessageService in DI**

In `src/HaloCommunityBot/Extensions/ServiceCollectionExtensions.cs`, add the singleton registration directly after the `services.AddSingleton<DiscordBotService>();` line:

```csharp
services.AddSingleton<DiscordBotService>();
services.AddSingleton<SingleMessageService>();  // ← add this line
```

Also add the using at the top if not already present — the service is in the same `DiscordBot.Services` namespace so no additional using is needed.

- [ ] **Step 2: Wire MessageReceived event in DiscordBotService**

In `src/HaloCommunityBot/Services/DiscordBotService.cs`:

Add `SingleMessageService` as a constructor parameter and store it in a field:

```csharp
private readonly SingleMessageService _singleMessageService;

public DiscordBotService(
    DiscordSocketClient client,
    InteractionService interactionService,
    IServiceProvider services,
    BotConfig config,
    ILogger<DiscordBotService> logger,
    SingleMessageService singleMessageService)   // ← add this parameter
{
    _client = client;
    _interactionService = interactionService;
    _services = services;
    _config = config;
    _logger = logger;
    _singleMessageService = singleMessageService;  // ← add this line

    _client.Log += LogAsync;
    _client.Ready += ReadyAsync;
    _client.Connected += ConnectedAsync;
    _client.Disconnected += DisconnectedAsync;
    _client.InteractionCreated += HandleInteractionAsync;
    _client.GuildAvailable += GuildAvailableAsync;
    _client.MessageReceived += _singleMessageService.HandleMessageAsync;  // ← add this line

    _interactionService.Log += LogAsync;
    _interactionService.SlashCommandExecuted += SlashCommandExecutedAsync;
}
```

- [ ] **Step 3: Add SingleMessage config section to appsettings.json**

In `src/HaloCommunityBot/appsettings.json`, add a `SingleMessage` section at the top level (alongside `Bot`, `Logging`, `ConnectionStrings`, `Metrics`):

```json
"SingleMessage": {
  "Channels": []
}
```

The array is empty by default. Server admins add channel IDs here when they want to designate channels.

- [ ] **Step 4: Build the full project**

```bash
cd j:/Projects/HaloCommunity/bot
dotnet build src/HaloCommunityBot/HaloCommunityBot.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
cd j:/Projects/HaloCommunity/bot
git add src/HaloCommunityBot/Extensions/ServiceCollectionExtensions.cs src/HaloCommunityBot/Services/DiscordBotService.cs src/HaloCommunityBot/appsettings.json
git commit -m "feat: wire SingleMessageService into DI and MessageReceived for HaloCommunityBot"
```

---

### Task 6: Tests for HaloCommunityBot

**Files:**
- Create: `j:/Projects/HaloCommunity/bot/tests/HaloCommunityBot.Tests/HaloCommunityBot.Tests.csproj`
- Create: `j:/Projects/HaloCommunity/bot/tests/HaloCommunityBot.Tests/SingleMessageServiceTests.cs`
- Modify: `j:/Projects/HaloCommunity/bot/HaloCommunityBot.sln` (add project reference)

- [ ] **Step 1: Create the test project file**

`tests/HaloCommunityBot.Tests/HaloCommunityBot.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.6.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.8" />
    <PackageReference Include="coverlet.collector" Version="10.0.1">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/HaloCommunityBot/HaloCommunityBot.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add the test project to the solution**

```bash
cd j:/Projects/HaloCommunity/bot
dotnet sln add tests/HaloCommunityBot.Tests/HaloCommunityBot.Tests.csproj
```

Expected: `Project 'tests/HaloCommunityBot.Tests/HaloCommunityBot.Tests.csproj' added to the solution.`

- [ ] **Step 3: Write the failing tests**

`tests/HaloCommunityBot.Tests/SingleMessageServiceTests.cs`:

```csharp
using DiscordBot.Core.Data;
using DiscordBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HaloCommunityBot.Tests;

public sealed class SingleMessageServiceTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly HaloCommunityBotContext _db;

    public SingleMessageServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<HaloCommunityBotContext>(o =>
            o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        _provider = services.BuildServiceProvider();
        _db = _provider.GetRequiredService<HaloCommunityBotContext>();
    }

    public void Dispose() => _provider.Dispose();

    private static IConfiguration BuildConfig(params (ulong channelId, bool scanHistory)[] channels)
    {
        var dict = new Dictionary<string, string?>();
        for (int i = 0; i < channels.Length; i++)
        {
            dict[$"SingleMessage:Channels:{i}:ChannelId"] = channels[i].channelId.ToString();
            dict[$"SingleMessage:Channels:{i}:ScanHistoryOnEnable"] = channels[i].scanHistory.ToString();
        }
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private HaloCommunityBotContext CreateFreshDb()
    {
        return _provider.GetRequiredService<HaloCommunityBotContext>();
    }

    [Fact]
    public async Task EnableChannelAsync_UnregisteredChannel_ReturnsError()
    {
        var config = BuildConfig((111UL, false));
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        var service = new DiscordBot.Services.SingleMessageService(
            scopeFactory, config, null!, NullLogger<DiscordBot.Services.SingleMessageService>.Instance);

        var result = await service.EnableChannelAsync(999UL, 1UL);

        Assert.Contains("❌", result, StringComparison.Ordinal);
        Assert.Contains("not registered", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnableChannelAsync_RegisteredChannel_SetsEnabledInDb()
    {
        const ulong channelId = 111UL;
        var config = BuildConfig((channelId, false));
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        var service = new DiscordBot.Services.SingleMessageService(
            scopeFactory, config, null!, NullLogger<DiscordBot.Services.SingleMessageService>.Instance);

        var result = await service.EnableChannelAsync(channelId, 1UL);

        Assert.Contains("✅", result, StringComparison.Ordinal);
        var db = CreateFreshDb();
        var state = await db.SingleMessageChannelStates.FindAsync(channelId);
        Assert.NotNull(state);
        Assert.True(state.IsEnabled);
    }

    [Fact]
    public async Task DisableChannelAsync_EnabledChannel_SetsDisabledInDb()
    {
        const ulong channelId = 222UL;
        var config = BuildConfig((channelId, false));
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        var service = new DiscordBot.Services.SingleMessageService(
            scopeFactory, config, null!, NullLogger<DiscordBot.Services.SingleMessageService>.Instance);

        await service.EnableChannelAsync(channelId, 1UL);
        var result = await service.DisableChannelAsync(channelId);

        Assert.Contains("✅", result, StringComparison.Ordinal);
        var db = CreateFreshDb();
        var state = await db.SingleMessageChannelStates.FindAsync(channelId);
        Assert.NotNull(state);
        Assert.False(state.IsEnabled);
    }

    [Fact]
    public async Task DisableChannelAsync_PreservesExistingRecords()
    {
        const ulong channelId = 333UL;
        var config = BuildConfig((channelId, false));
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        var service = new DiscordBot.Services.SingleMessageService(
            scopeFactory, config, null!, NullLogger<DiscordBot.Services.SingleMessageService>.Instance);

        await service.EnableChannelAsync(channelId, 1UL);
        _db.SingleMessageRecords.Add(new SingleMessageRecord { ChannelId = channelId, UserId = 42UL, MessageId = 99UL, PostedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        await service.DisableChannelAsync(channelId);

        var db = CreateFreshDb();
        var count = await db.SingleMessageRecords.CountAsync(r => r.ChannelId == channelId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ResetUserAsync_ExistingRecord_DeletesRecord()
    {
        const ulong channelId = 444UL;
        const ulong userId = 55UL;
        var config = BuildConfig((channelId, false));
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        var service = new DiscordBot.Services.SingleMessageService(
            scopeFactory, config, null!, NullLogger<DiscordBot.Services.SingleMessageService>.Instance);

        await service.EnableChannelAsync(channelId, 1UL);
        _db.SingleMessageRecords.Add(new SingleMessageRecord { ChannelId = channelId, UserId = userId, MessageId = 1UL, PostedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var result = await service.ResetUserAsync(channelId, userId, $"<@{userId}>");

        Assert.Contains("✅", result, StringComparison.Ordinal);
        var db = CreateFreshDb();
        var exists = await db.SingleMessageRecords.AnyAsync(r => r.ChannelId == channelId && r.UserId == userId);
        Assert.False(exists);
    }

    [Fact]
    public async Task ResetUserAsync_NoRecord_ReturnsInfoMessage()
    {
        const ulong channelId = 555UL;
        var config = BuildConfig((channelId, false));
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        var service = new DiscordBot.Services.SingleMessageService(
            scopeFactory, config, null!, NullLogger<DiscordBot.Services.SingleMessageService>.Instance);

        var result = await service.ResetUserAsync(channelId, 99UL, "<@99>");

        Assert.Contains("ℹ️", result, StringComparison.Ordinal);
        Assert.Contains("No record found", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListPostedUsersAsync_ReturnsRecordsOrderedByPostedAt()
    {
        const ulong channelId = 666UL;
        var config = BuildConfig((channelId, false));
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        var service = new DiscordBot.Services.SingleMessageService(
            scopeFactory, config, null!, NullLogger<DiscordBot.Services.SingleMessageService>.Instance);

        await service.EnableChannelAsync(channelId, 1UL);
        var now = DateTime.UtcNow;
        _db.SingleMessageRecords.AddRange(
            new SingleMessageRecord { ChannelId = channelId, UserId = 1UL, MessageId = 10UL, PostedAt = now.AddMinutes(-5) },
            new SingleMessageRecord { ChannelId = channelId, UserId = 2UL, MessageId = 11UL, PostedAt = now.AddMinutes(-2) },
            new SingleMessageRecord { ChannelId = channelId, UserId = 3UL, MessageId = 12UL, PostedAt = now }
        );
        await _db.SaveChangesAsync();

        var records = await service.ListPostedUsersAsync(channelId);

        Assert.Equal(3, records.Count);
        Assert.Equal(1UL, records[0].UserId);
        Assert.Equal(2UL, records[1].UserId);
        Assert.Equal(3UL, records[2].UserId);
    }

    [Fact]
    public void IsRegisteredChannel_ReturnsTrueForConfiguredChannel()
    {
        const ulong channelId = 777UL;
        var config = BuildConfig((channelId, false));
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        var service = new DiscordBot.Services.SingleMessageService(
            scopeFactory, config, null!, NullLogger<DiscordBot.Services.SingleMessageService>.Instance);

        Assert.True(service.IsRegisteredChannel(channelId));
        Assert.False(service.IsRegisteredChannel(888UL));
    }
}
```

- [ ] **Step 4: Run the failing tests to confirm they compile and run**

```bash
cd j:/Projects/HaloCommunity/bot
dotnet test tests/HaloCommunityBot.Tests/HaloCommunityBot.Tests.csproj --no-build -v normal
```

If build errors occur, fix compilation issues first, then re-run. Expected once service is in place: all tests pass.

- [ ] **Step 5: Run all tests to confirm no regressions**

```bash
cd j:/Projects/HaloCommunity/bot
dotnet test
```

Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
cd j:/Projects/HaloCommunity/bot
git add tests/HaloCommunityBot.Tests/ HaloCommunityBot.sln
git commit -m "test: add SingleMessageService tests for HaloCommunityBot"
```

---

## PART 2 — HuduCommunityBot

All tasks mirror Part 1 exactly. Differences are called out per task.

### Task 7: Models

**Files:**
- Create: `j:/Projects/HuduCommunity/bot/src/HuduCommunityBot/Models/SingleMessageChannelConfig.cs`
- Create: `j:/Projects/HuduCommunity/bot/src/HuduCommunityBot/Models/SingleMessageChannelState.cs`
- Create: `j:/Projects/HuduCommunity/bot/src/HuduCommunityBot/Models/SingleMessageRecord.cs`

- [ ] **Step 1: Create config POCO**

`src/HuduCommunityBot/Models/SingleMessageChannelConfig.cs` — identical content to Halo Task 1 Step 1.

- [ ] **Step 2: Create EF entity — channel state**

`src/HuduCommunityBot/Models/SingleMessageChannelState.cs` — identical content to Halo Task 1 Step 2.

- [ ] **Step 3: Create EF entity — posted record**

`src/HuduCommunityBot/Models/SingleMessageRecord.cs` — identical content to Halo Task 1 Step 3.

- [ ] **Step 4: Commit**

```bash
cd j:/Projects/HuduCommunity/bot
git add src/HuduCommunityBot/Models/SingleMessageChannelConfig.cs src/HuduCommunityBot/Models/SingleMessageChannelState.cs src/HuduCommunityBot/Models/SingleMessageRecord.cs
git commit -m "feat: add SingleMessage model classes for HuduCommunityBot"
```

---

### Task 8: Database Context + Migration

**Files:**
- Modify: `j:/Projects/HuduCommunity/bot/src/HuduCommunityBot/Core/Data/HuduCommunityBotContext.cs`
- Create + Modify: `j:/Projects/HuduCommunity/bot/src/HuduCommunityBot/Migrations/`

- [ ] **Step 1: Register DbSets and configure model**

`src/HuduCommunityBot/Core/Data/HuduCommunityBotContext.cs` — apply the same changes as Halo Task 2 Step 1, substituting `HuduCommunityBotContext` for `HaloCommunityBotContext`. The `DbSet` properties and `OnModelCreating` configuration for `SingleMessageChannelState` and `SingleMessageRecord` are identical.

- [ ] **Step 2: Generate migration**

```bash
cd j:/Projects/HuduCommunity/bot
dotnet ef migrations add AddSingleMessage --project src/HuduCommunityBot --output-dir Migrations
```

- [ ] **Step 3: Verify migration content** — same check as Halo Task 2 Step 3.

- [ ] **Step 4: Verify migration applies**

```bash
cd j:/Projects/HuduCommunity/bot
dotnet ef database update --project src/HuduCommunityBot
```

- [ ] **Step 5: Commit**

```bash
cd j:/Projects/HuduCommunity/bot
git add src/HuduCommunityBot/Core/Data/HuduCommunityBotContext.cs src/HuduCommunityBot/Migrations/
git commit -m "feat: add SingleMessage EF migration for HuduCommunityBot"
```

---

### Task 9: SingleMessageService for HuduCommunityBot

**Files:**
- Create: `j:/Projects/HuduCommunity/bot/src/HuduCommunityBot/Services/SingleMessageService.cs`

- [ ] **Step 1: Create the service**

Copy the service from Halo Task 3, with these substitutions:
- Replace `HaloCommunityBotContext` with `HuduCommunityBotContext` in every `GetRequiredService<>` call — appears in `HandleMessageAsync`, `EnableChannelAsync`, `DisableChannelAsync`, `ResetUserAsync`, `ListPostedUsersAsync`, and `ScanHistoryAsync` (six occurrences total)
- The namespace `DiscordBot.Services` and all other types remain identical

- [ ] **Step 2: Build to verify**

```bash
cd j:/Projects/HuduCommunity/bot
dotnet build src/HuduCommunityBot/HuduCommunityBot.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd j:/Projects/HuduCommunity/bot
git add src/HuduCommunityBot/Services/SingleMessageService.cs
git commit -m "feat: add SingleMessageService for HuduCommunityBot"
```

---

### Task 10: Module, DI, Config, and Tests for HuduCommunityBot

**Files:**
- Create: `j:/Projects/HuduCommunity/bot/src/HuduCommunityBot/Modules/Moderations/SingleMessageModule.cs`
- Modify: `j:/Projects/HuduCommunity/bot/src/HuduCommunityBot/Extensions/ServiceCollectionExtensions.cs`
- Modify: `j:/Projects/HuduCommunity/bot/src/HuduCommunityBot/Services/DiscordBotService.cs`
- Modify: `j:/Projects/HuduCommunity/bot/src/HuduCommunityBot/appsettings.json`
- Create: `j:/Projects/HuduCommunity/bot/tests/HuduCommunityBot.Tests/SingleMessageServiceTests.cs`

- [ ] **Step 1: Create the module**

`src/HuduCommunityBot/Modules/Moderations/SingleMessageModule.cs` — identical content to Halo Task 4 Step 1 (same namespace `DiscordBot.Modules.Moderations`, no changes needed).

- [ ] **Step 2: Register service in DI**

`src/HuduCommunityBot/Extensions/ServiceCollectionExtensions.cs` — add `services.AddSingleton<SingleMessageService>();` immediately after `services.AddSingleton<DiscordBotService>();`, same as Halo Task 5 Step 1.

- [ ] **Step 3: Wire event in DiscordBotService**

`src/HuduCommunityBot/Services/DiscordBotService.cs` — apply the same constructor changes as Halo Task 5 Step 2: add `SingleMessageService singleMessageService` parameter, store in `_singleMessageService` field, subscribe `_client.MessageReceived += _singleMessageService.HandleMessageAsync;`.

- [ ] **Step 4: Add config section**

`src/HuduCommunityBot/appsettings.json` — add `"SingleMessage": { "Channels": [] }` at the top level.

- [ ] **Step 5: Build**

```bash
cd j:/Projects/HuduCommunity/bot
dotnet build src/HuduCommunityBot/HuduCommunityBot.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Write tests**

`tests/HuduCommunityBot.Tests/SingleMessageServiceTests.cs` — copy from Halo Task 6 Step 3, substituting:
- `HaloCommunityBotContext` → `HuduCommunityBotContext` (in the `ServiceCollection` registration and all direct `_db` usages)
- Namespace declaration: `namespace HuduCommunityBot.Tests;`

- [ ] **Step 7: Run tests**

```bash
cd j:/Projects/HuduCommunity/bot
dotnet test
```

Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
cd j:/Projects/HuduCommunity/bot
git add src/HuduCommunityBot/Modules/Moderations/SingleMessageModule.cs src/HuduCommunityBot/Extensions/ServiceCollectionExtensions.cs src/HuduCommunityBot/Services/DiscordBotService.cs src/HuduCommunityBot/appsettings.json tests/HuduCommunityBot.Tests/SingleMessageServiceTests.cs
git commit -m "feat: wire SingleMessage commands, service and tests for HuduCommunityBot"
```

---

## PART 3 — PandaBot

Key differences from Halo/Hudu:
- Model namespace is `PandaBot.Models` (not `DiscordBot.Models`)
- DB context is `PandaBotContext` (from `PandaBot.Core.Data`)
- Moderation module folder is `Moderation` (singular)
- No existing `PandaBot.Tests` project — must create it and add to solution

### Task 11: Models

**Files:**
- Create: `j:/Projects/Pandamonium/bot/src/PandaBot/Models/SingleMessageChannelConfig.cs`
- Create: `j:/Projects/Pandamonium/bot/src/PandaBot/Models/SingleMessageChannelState.cs`
- Create: `j:/Projects/Pandamonium/bot/src/PandaBot/Models/SingleMessageRecord.cs`

- [ ] **Step 1: Create config POCO**

`src/PandaBot/Models/SingleMessageChannelConfig.cs`:

```csharp
namespace PandaBot.Models;

public class SingleMessageChannelConfig
{
    public ulong ChannelId { get; set; }
    public bool ScanHistoryOnEnable { get; set; } = false;
}
```

- [ ] **Step 2: Create EF entity — channel state**

`src/PandaBot/Models/SingleMessageChannelState.cs`:

```csharp
namespace PandaBot.Models;

public class SingleMessageChannelState
{
    public ulong ChannelId { get; set; }
    public bool IsEnabled { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 3: Create EF entity — posted record**

`src/PandaBot/Models/SingleMessageRecord.cs`:

```csharp
namespace PandaBot.Models;

public class SingleMessageRecord
{
    public int Id { get; set; }
    public ulong ChannelId { get; set; }
    public ulong UserId { get; set; }
    public ulong MessageId { get; set; }
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;

    public SingleMessageChannelState? Channel { get; set; }
}
```

- [ ] **Step 4: Commit**

```bash
cd j:/Projects/Pandamonium/bot
git add src/PandaBot/Models/SingleMessageChannelConfig.cs src/PandaBot/Models/SingleMessageChannelState.cs src/PandaBot/Models/SingleMessageRecord.cs
git commit -m "feat: add SingleMessage model classes for PandaBot"
```

---

### Task 12: Database Context + Migration

**Files:**
- Modify: `j:/Projects/Pandamonium/bot/src/PandaBot/Core/Data/PandaBotContext.cs`
- Create + Modify: `j:/Projects/Pandamonium/bot/src/PandaBot/Migrations/`

- [ ] **Step 1: Add DbSets and configure model**

In `src/PandaBot/Core/Data/PandaBotContext.cs`, add the using and the two new DbSet properties:

```csharp
using PandaBot.Models;  // add this using (alongside existing ones)
```

Add properties:
```csharp
public DbSet<SingleMessageChannelState> SingleMessageChannelStates { get; set; }
public DbSet<SingleMessageRecord> SingleMessageRecords { get; set; }
```

Add to `OnModelCreating` (append inside the method, after the last existing `modelBuilder.Entity` block):

```csharp
modelBuilder.Entity<SingleMessageChannelState>(entity =>
{
    entity.HasKey(x => x.ChannelId);
    entity.Property(x => x.IsEnabled).HasDefaultValue(false);
    entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
});

modelBuilder.Entity<SingleMessageRecord>(entity =>
{
    entity.HasKey(x => x.Id);
    entity.HasIndex(x => new { x.ChannelId, x.UserId }).IsUnique();
    entity.HasOne(x => x.Channel)
        .WithMany()
        .HasForeignKey(x => x.ChannelId)
        .OnDelete(DeleteBehavior.Cascade);
    entity.Property(x => x.PostedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
});
```

- [ ] **Step 2: Generate migration**

```bash
cd j:/Projects/Pandamonium/bot
dotnet ef migrations add AddSingleMessage --project src/PandaBot --output-dir Migrations
```

- [ ] **Step 3: Verify migration content** — same checks as Halo Task 2 Step 3.

- [ ] **Step 4: Verify migration applies**

```bash
cd j:/Projects/Pandamonium/bot
dotnet ef database update --project src/PandaBot
```

- [ ] **Step 5: Commit**

```bash
cd j:/Projects/Pandamonium/bot
git add src/PandaBot/Core/Data/PandaBotContext.cs src/PandaBot/Migrations/
git commit -m "feat: add SingleMessage EF migration for PandaBot"
```

---

### Task 13: SingleMessageService for PandaBot

**Files:**
- Create: `j:/Projects/Pandamonium/bot/src/PandaBot/Services/SingleMessageService.cs`

- [ ] **Step 1: Create the service**

`src/PandaBot/Services/SingleMessageService.cs` — copy from Halo Task 3, with these substitutions:
- `using DiscordBot.Core.Data;` → `using PandaBot.Core.Data;`
- `using DiscordBot.Models;` → `using PandaBot.Models;`
- `HaloCommunityBotContext` → `PandaBotContext` (all occurrences in `GetRequiredService<>` calls and the `ScanHistoryAsync` method signature)
- Namespace remains `DiscordBot.Services` (matches existing PandaBot service convention)
- `ILogger<SingleMessageService>` remains unchanged

- [ ] **Step 2: Build to verify**

```bash
cd j:/Projects/Pandamonium/bot
dotnet build src/PandaBot/PandaBot.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd j:/Projects/Pandamonium/bot
git add src/PandaBot/Services/SingleMessageService.cs
git commit -m "feat: add SingleMessageService for PandaBot"
```

---

### Task 14: Module, DI, Config, and Tests for PandaBot

**Files:**
- Create: `j:/Projects/Pandamonium/bot/src/PandaBot/Modules/Moderation/SingleMessageModule.cs`
- Modify: `j:/Projects/Pandamonium/bot/src/PandaBot/Extensions/ServiceCollectionExtensions.cs`
- Modify: `j:/Projects/Pandamonium/bot/src/PandaBot/Services/DiscordBotService.cs`
- Modify: `j:/Projects/Pandamonium/bot/src/PandaBot/appsettings.json`
- Create: `j:/Projects/Pandamonium/bot/tests/PandaBot.Tests/PandaBot.Tests.csproj`
- Create: `j:/Projects/Pandamonium/bot/tests/PandaBot.Tests/SingleMessageServiceTests.cs`
- Modify: `j:/Projects/Pandamonium/bot/PandaBot.sln`

- [ ] **Step 1: Create the module**

`src/PandaBot/Modules/Moderation/SingleMessageModule.cs` — identical content to Halo Task 4 Step 1. Namespace is `DiscordBot.Modules.Moderations` — **do not change to `Moderation`**; the namespace doesn't need to match the folder name, and all other moderation modules in PandaBot also use the same namespace.

  > Verify by checking e.g. `src/PandaBot/Modules/Moderation/BanModule.cs` namespace — use whatever namespace that file declares.

- [ ] **Step 2: Register service in DI**

`src/PandaBot/Extensions/ServiceCollectionExtensions.cs` — add the following using at the top if not present:

```csharp
using DiscordBot.Services;
```

Then add `services.AddSingleton<SingleMessageService>();` immediately after `services.AddSingleton<DiscordBotService>();`.

- [ ] **Step 3: Wire event in DiscordBotService**

`src/PandaBot/Services/DiscordBotService.cs` — apply the same constructor changes as Halo Task 5 Step 2.

- [ ] **Step 4: Add config section**

`src/PandaBot/appsettings.json` — add `"SingleMessage": { "Channels": [] }` at the top level (alongside `Discord`, `Logging`, `ConnectionStrings`, `AshesForge`, etc.).

- [ ] **Step 5: Build**

```bash
cd j:/Projects/Pandamonium/bot
dotnet build src/PandaBot/PandaBot.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Create test project**

`tests/PandaBot.Tests/PandaBot.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.6.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.8" />
    <PackageReference Include="coverlet.collector" Version="10.0.1">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/PandaBot/PandaBot.csproj" />
  </ItemGroup>
</Project>
```

```bash
cd j:/Projects/Pandamonium/bot
dotnet sln add tests/PandaBot.Tests/PandaBot.Tests.csproj
```

- [ ] **Step 7: Write tests**

`tests/PandaBot.Tests/SingleMessageServiceTests.cs` — copy from Halo Task 6 Step 3, substituting:
- `HaloCommunityBotContext` → `PandaBotContext`
- `using DiscordBot.Core.Data;` → `using PandaBot.Core.Data;`
- `using DiscordBot.Models;` → `using PandaBot.Models;`
- Namespace declaration: `namespace PandaBot.Tests;`

- [ ] **Step 8: Run tests**

```bash
cd j:/Projects/Pandamonium/bot
dotnet test
```

Expected: All tests pass.

- [ ] **Step 9: Commit**

```bash
cd j:/Projects/Pandamonium/bot
git add src/PandaBot/Modules/Moderation/SingleMessageModule.cs src/PandaBot/Extensions/ServiceCollectionExtensions.cs src/PandaBot/Services/DiscordBotService.cs src/PandaBot/appsettings.json tests/PandaBot.Tests/ PandaBot.sln
git commit -m "feat: wire SingleMessage commands, service and tests for PandaBot"
```

---

## Final Checks

- [ ] All three bots build cleanly: `dotnet build` in each repo root
- [ ] All three test suites pass: `dotnet test` in each repo root
- [ ] Run each bot locally, register a test channel in appsettings, run `/singlemessage enable`, post two messages as a non-bot user, confirm the second is deleted and the notification appears
- [ ] Run `/singlemessage list` and verify the first message link is correct
- [ ] Run `/singlemessage reset-user` and confirm the user can post again
- [ ] Run `/singlemessage disable` and confirm a second message is no longer deleted
