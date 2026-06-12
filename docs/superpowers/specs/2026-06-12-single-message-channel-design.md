# Single Message Per User Channel Enforcement

**Date:** 2026-06-12
**Bots:** HaloCommunityBot, HuduCommunityBot, PandaBot
**Status:** Approved

## Overview

Enforce a rule that each user may post only one message (ever) in designated channels. Moderators can reset individual users to allow them to post again, toggle enforcement on/off at runtime, and list who has already posted.

## Configuration

Channel registration lives in `appsettings.json` (or environment variables). This defines which channels are *eligible* for enforcement — it does not activate enforcement on its own.

```json
"SingleMessage": {
  "Channels": [
    { "ChannelId": 123456789012345678, "ScanHistoryOnEnable": true },
    { "ChannelId": 987654321098765432, "ScanHistoryOnEnable": false }
  ]
}
```

`ScanHistoryOnEnable`: when `true`, the bot fetches up to 100 messages of history from the channel when enforcement is first enabled and pre-populates records for any users already found there (bots excluded).

## Database Schema

Two new EF Core tables added via a single migration per bot.

### `SingleMessageChannelState`

| Column | Type | Notes |
|---|---|---|
| `ChannelId` | `ulong` (PK) | Discord channel snowflake |
| `IsEnabled` | `bool` | Runtime toggle, default `false` |
| `CreatedAt` | `datetime` | |
| `UpdatedAt` | `datetime` | |

### `SingleMessageRecord`

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` (PK, auto-increment) | |
| `ChannelId` | `ulong` | FK → `SingleMessageChannelState.ChannelId` |
| `UserId` | `ulong` | Discord user snowflake |
| `MessageId` | `ulong` | Snowflake of the user's first (allowed) message |
| `PostedAt` | `datetime` | |

Unique index on `(ChannelId, UserId)` — one record per user per channel.

## Architecture

### `SingleMessageService` (singleton)

Owns all business logic. Injected into:
- `DiscordBotService` — for `MessageReceived` event handling
- `SingleMessageModule` — for slash command implementations

Constructor dependencies: `IDbContextFactory<{Bot}Context>`, `IConfiguration`, `ILogger<SingleMessageService>`, `DiscordSocketClient` (for history scanning).

### `SingleMessageModule` (Discord.NET interaction module)

Thin slash command group `/singlemessage`. All four subcommands delegate immediately to `SingleMessageService` and respond ephemerally.

### `DiscordBotService`

One new subscription in the constructor:
```csharp
_client.MessageReceived += _singleMessageService.HandleMessageAsync;
```

## Message Enforcement Flow

Called on every `MessageReceived` event:

1. Return early if the author is a bot or the message is not in a guild text channel.
2. Check if the channel ID appears in the config `SingleMessage:Channels` list — return early if not (zero DB hits for unregistered channels).
3. Load `SingleMessageChannelState` for the channel — return early if not found or `IsEnabled = false`.
4. Check for a `SingleMessageRecord` for `(ChannelId, UserId)`:
   - **No record** → insert a new record (recording `MessageId` and `PostedAt`) and allow the message.
   - **Record exists** → delete the message and send an ephemeral reply: *"This channel only allows one message per user. Your original message has been kept."*

## Slash Commands

All commands require `ManageChannels`. All responses are ephemeral.

### `/singlemessage enable [channel]`

1. Verify the target channel is in config — respond with an error if not.
2. Upsert `SingleMessageChannelState` with `IsEnabled = true`, updating `UpdatedAt`.
3. If `ScanHistoryOnEnable = true` for the channel: fetch up to 100 messages of history, insert `SingleMessageRecord` rows for each unique non-bot user not already recorded.
4. Respond: *"Single-message enforcement enabled for #channel-name."* (include count of users pre-populated from history if scan was performed).

### `/singlemessage disable [channel]`

1. Set `IsEnabled = false` in `SingleMessageChannelState`, updating `UpdatedAt`.
2. Leave all `SingleMessageRecord` rows intact (history is preserved for if re-enabled).
3. Respond: *"Single-message enforcement disabled for #channel-name. Existing records retained."*

### `/singlemessage reset-user <user> [channel]`

1. Default `channel` to the current channel if not provided.
2. Verify the channel is registered in config.
3. Delete the `SingleMessageRecord` for `(ChannelId, UserId)` if it exists.
4. Respond: *"@user has been reset in #channel-name and may post again."* (or *"No record found for @user in #channel-name."* if nothing to delete).

### `/singlemessage list [channel]`

1. Default `channel` to the current channel if not provided.
2. Query all `SingleMessageRecord` rows for the channel ordered by `PostedAt` ascending.
3. Render as an embed: each entry shows user mention, link to their message, and relative timestamp.
4. If zero records: *"No posts recorded in #channel-name yet."*
5. Paginate at 25 entries if needed (embed field limit).

## File Structure

### HaloCommunityBot

```
src/HaloCommunityBot/
  Models/
    SingleMessageChannelConfig.cs     ← config POCO
    SingleMessageChannelState.cs      ← EF entity
    SingleMessageRecord.cs            ← EF entity
  Services/
    SingleMessageService.cs
  Modules/
    Moderations/
      SingleMessageModule.cs
  Migrations/
    YYYYMMDDHHMMSS_AddSingleMessage.cs
```

### HuduCommunityBot

Same structure as HaloCommunityBot, namespace `DiscordBot`, DB context `HuduCommunityBotContext`.

### PandaBot

```
src/PandaBot/
  Models/
    SingleMessageChannelConfig.cs
    SingleMessageChannelState.cs
    SingleMessageRecord.cs
  Services/
    SingleMessageService.cs
  Modules/
    Moderation/                        ← singular, matching existing convention
      SingleMessageModule.cs
  Migrations/
    YYYYMMDDHHMMSS_AddSingleMessage.cs
```

Namespace: `PandaBot`, DB context: `PandaBotContext`.

## Cross-Cutting Notes

- The config POCO (`SingleMessageChannelConfig`) and the channel ID lookup are resolved once at startup and cached in the service to avoid re-parsing config on every message event.
- History scanning on enable is best-effort: the Discord API may return fewer than 100 messages for new or low-traffic channels, which is fine.
- The `MessageId` stored in `SingleMessageRecord` is used in the `/singlemessage list` command to produce a direct message link (`https://discord.com/channels/{guildId}/{channelId}/{messageId}`).
- Bot messages and webhook messages are never tracked.
