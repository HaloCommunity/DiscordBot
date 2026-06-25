# HaloCommunity Bot

Discord bot for the Halo Community server, built with C# (.NET 10) and [Discord.Net](https://github.com/discord-net/Discord.Net).

## ✨ Features

* **Slash commands** via Discord.Net's `InteractionService`
* **Moderation tools**: ban, kick, mute, warn, clear, purge, slowmode, lock/unlock
* **General utilities**: avatar, userinfo, serverinfo, reminders, fun commands, and more
* **Halo Services status monitor**: polls the [Halo Services Solutions status RSS feed](https://status.haloservicesolutions.com/pages/63ef45da7ee94905308a1a4a/rss) and posts updates to a configured channel
* **Single-message channel enforcement**: restricts designated channels to one message per user, with slash commands to enable/disable enforcement and reset individual users
* **Moderation action logging**: posts a rich embed to a configured forum channel for every moderation action (ban, unban, kick, mute, unmute, warn, clear, purge, lock/unlock, slowmode, and automated single-message deletions)
* **Cross-channel spam detection**: flags users who post identical messages across multiple channels within a configurable time window, alerting moderators with interactive ban/dismiss buttons
* **Permission-aware error handling**: friendly ephemeral responses when permission checks fail
* **Deployment via GitHub Actions**: CI build gate → SSH deploy to Linux host with systemd

## 🚀 Getting Started

### Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* A Discord bot token ([How to create a bot](https://discord.com/developers/applications))

### Local Development

1. **Clone the repository:**

   ```bash
   git clone https://github.com/homotechsual/CommunityDiscordBot.git
   cd CommunityDiscordBot
   ```

2. **Configure the bot** using one of:

   * `src/HaloCommunityBot/appsettings.Development.json` (gitignored)
   * .NET User Secrets: `dotnet user-secrets set "Bot:Token" "your-token-here" --project src/HaloCommunityBot`

3. **Build and run:**

   ```bash
   dotnet run --project src/HaloCommunityBot
   ```

   In `Debug` builds, slash commands are registered to the guild specified by `Bot:GuildId` for instant availability. Release builds register commands globally.

### Required Bot Permissions

The bot requires the following permissions (the invite URL should include these):

* Read Messages / View Channels
* Send Messages
* Embed Links
* Manage Messages
* Kick Members
* Ban Members
* Moderate Members (for timeout/mute)
* Manage Channels (for lock/slowmode)
* Create Public Threads (for moderation log forum posts)

> **Note:** The **Message Content** privileged intent must be enabled in the [Discord Developer Portal](https://discord.com/developers/applications) for the single-message enforcement and cross-channel spam detection features to function. Restart the bot after enabling it — no token refresh is required.

## 📖 Commands

### General

| Command | Description |
| --- | --- |
| `/about` | Shows bot information and uptime |
| `/avatar [user]` | Displays a user's avatar |
| `/fun` | Random fun commands |
| `/help` | Lists all available commands |
| `/ping` | Shows bot latency |
| `/remind <time> <message>` | Sets a reminder |
| `/serverinfo` | Shows server information |
| `/status [private]` | Shows Halo services status overview (public by default, optional private response) |
| `/userinfo [user]` | Shows information about a user |

### Moderation

| Command | Required User Permission | Required Bot Permission |
| --- | --- | --- |
| `/ban <user> [reason]` | Ban Members | Ban Members |
| `/unban <userid>` | Ban Members | Ban Members |
| `/kick <user> [reason]` | Kick Members | Kick Members |
| `/mute <user> <duration> [reason]` | Moderate Members | Moderate Members |
| `/unmute <user>` | Moderate Members | Moderate Members |
| `/warn <user> <reason>` | Kick Members | Kick Members |
| `/warnings <user>` | Manage Messages | — |
| `/clear <amount>` | Manage Messages | Manage Messages |
| `/purge_user <user> <amount>` | Manage Messages | Manage Messages |
| `/lock [channel]` | Manage Channels | Manage Channels |
| `/unlock [channel]` | Manage Channels | Manage Channels |
| `/slowmode <seconds>` | Manage Channels | Manage Channels |

> **Note:** `/warn` auto-kicks a user after 3 accumulated warnings.

### Single-Message Enforcement

These commands require the **Manage Channels** permission.

| Command | Description |
| --- | --- |
| `/singlemessage enable [channel]` | Enable single-message enforcement for a channel (defaults to current channel) |
| `/singlemessage disable [channel]` | Disable enforcement for a channel; existing records are retained |
| `/singlemessage reset-user <user> [channel]` | Remove a user's record so they may post again |
| `/singlemessage list [channel]` | List all users who have posted in the channel, with links to their messages |

## ⚙️ Configuration

All settings live under the `Bot` key in `appsettings.json`:

```json
{
  "Bot": {
    "Token": "",
    "Prefix": "!",
    "GuildId": 0,
    "AllowPrefixCommands": false,
    "AllowedFunChannels": [],
    "Cooldowns": {
      "UserInfo": 5,
      "Status": 15
    },
    "StatusMonitor": {
      "Enabled": false,
      "ChannelId": 0,
      "RoleId": 0,
      "FeedUrl": "https://status.haloservicesolutions.com/pages/63ef45da7ee94905308a1a4a/rss",
      "PollIntervalMinutes": 5
    },
    "YoutubeMonitor": {
      "Enabled": false,
      "ForumChannelId": 0,
      "PollIntervalMinutes": 15,
      "DefaultPostTitleTemplate": "[{ChannelName}] {VideoTitle}",
      "Channels": []
    },
    "Heartbeat": {
      "Enabled": false,
      "PushUrl": "",
      "IntervalSeconds": 60,
      "StartupDelaySeconds": 15,
      "TimeoutSeconds": 10
    },
    "SingleMessage": {
      "Channels": []
    }
  },
  "ModerationLog": {
    "ForumChannelId": 0,
    "ModeratorRoleId": 0
  },
  "CrossChannelSpam": {
    "Enabled": false,
    "TimeWindowSeconds": 30,
    "MinimumChannelCount": 3,
    "DeleteMessages": true,
    "TimeoutOnDetection": true
  }
}
```

### Status Monitor

Set `StatusMonitor:Enabled` to `true` and configure:

| Setting | Description |
| --- | --- |
| `ChannelId` | Channel where status updates are posted |
| `RoleId` | Role to mention on status updates (set `0` to disable mentions) |
| `FeedUrl` | RSS feed URL (defaults to Halo Services Solutions) |
| `PollIntervalMinutes` | How often to check for new feed items (code default: 1, appsettings template: 5) |

### YouTube Monitor

Set `YoutubeMonitor:Enabled` to `true` and configure:

| Setting | Description |
| --- | --- |
| `ForumChannelId` | Discord forum channel ID where new video threads are created |
| `RoleId` | Optional role to mention when a new video is posted (set `0` to disable mentions) |
| `YouTubeDataApiKey` | Optional YouTube Data API key used to resolve plain channel names to channel IDs |
| `PollIntervalMinutes` | Feed polling cadence (default: 15) |
| `DefaultPostTitleTemplate` | Thread title template; see placeholder table below |
| `Channels` | Optional startup seed list of YouTube channel IDs, @handles, feed URLs, or channel names (channel names require `YouTubeDataApiKey`) |

#### YouTube Title Template Variables

The following placeholders are available in both `DefaultPostTitleTemplate` and per-channel `PostTitleTemplate` (for tracked channels):

| Variable | Meaning | Example Value |
| --- | --- | --- |
| `{ChannelName}` | Display name of the YouTube channel | `Halo Community` |
| `{ChannelId}` | Tracked channel reference (YouTube channel ID) | `UC1234567890abcdef` |
| `{VideoTitle}` | Title of the YouTube video | `Halo Infinite Season 6` |
| `{VideoId}` | YouTube video ID | `dQw4w9WgXcQ` |
| `{VideoUrl}` | Full YouTube watch URL | `https://www.youtube.com/watch?v=dQw4w9WgXcQ` |
| `{PublishedDate}` | Video publish date in UTC (`yyyy-MM-dd`) | `2026-06-14` |
| `{PublishedAtUtc}` | Video publish timestamp in UTC (`yyyy-MM-dd HH:mm:ss UTC`) | `2026-06-14 17:00:00 UTC` |
| `{PublishedAtDiscord}` | Discord formatted timestamp (`<t:unix:f>`) | `<t:1779227925:f>` |
| `{PublishedAtDiscordRelative}` | Discord relative timestamp (`<t:unix:R>`) | `<t:1779227925:R>` |
| `{VideoDescription}` | YouTube video description text | `Season 6 gameplay overview...` |
| `{RoleMention}` | Mention text for configured monitor role, or empty when unset | `<@&1234567890>` |

Notes:

* Placeholder names are case-insensitive.
* Unknown placeholders are left as-is.
* If a template is empty, the monitor falls back to: `[{ChannelName}] {VideoTitle}`.
* Escaped newlines in templates are supported (`\\n`, `\\r\\n`, `\\r`) and converted to real line breaks at runtime. This is useful for one-line environment variable values.
* Post titles are truncated to 100 characters (Discord's forum post title limit).

Examples:

* `[{ChannelName}] {VideoTitle}`
* `{PublishedAtDiscordRelative} | {VideoTitle}`
* `New upload from {ChannelName}: {VideoTitle}`

### Single-Message Channels

Register channels that should allow only one message per user. Channels must be listed here before `/singlemessage enable` will accept them.

```json
{
  "SingleMessage": {
    "Channels": [
      { "ChannelId": 1234567890123456789, "ScanHistoryOnEnable": false }
    ]
  }
}
```

| Setting | Description |
| --- | --- |
| `ChannelId` | Discord channel ID to register for single-message enforcement |
| `ScanHistoryOnEnable` | When `true`, scans the last 100 messages on enable to pre-populate existing posters (default: `false`) |

Note: `SingleMessage:Channels` is an array and is best managed in `appsettings.json` rather than environment variables.

### Moderation Action Logging

All moderation actions are logged as rich embeds to a Discord forum channel. Each embed shows the action type, the target user, the moderator, and the reason.

> **Note:** `ModerationLog` is a root-level config section, not nested under `Bot`.

| Setting | Description |
| --- | --- |
| `ForumChannelId` | Forum channel ID where moderation log threads are created (`0` = disabled) |
| `ModeratorRoleId` | Optional role to mention in log posts (`0` = no mention) |

### Cross-Channel Spam Detection

Detects users who send identical messages across multiple channels within a short time window. When triggered, a spam alert is posted to the moderation log forum channel with **Ban** and **Dismiss** buttons for moderators. Requires the **Message Content** privileged intent (see [Required Bot Permissions](#required-bot-permissions)).

> **Note:** `CrossChannelSpam` is a root-level config section, not nested under `Bot`.

| Setting | Description |
| --- | --- |
| `Enabled` | Enable cross-channel spam detection (default: `false`) |
| `TimeWindowSeconds` | Sliding window duration in seconds (default: `30`) |
| `MinimumChannelCount` | Minimum number of distinct channels before a detection fires (default: `3`) |
| `DeleteMessages` | Delete detected spam messages (requires Manage Messages). Default: `true` |
| `TimeoutOnDetection` | Apply a 28-day timeout to the spammer (requires Moderate Members). Default: `true` |

### Uptime Heartbeat

Set `Heartbeat:Enabled` to `true` and configure:

| Setting | Description |
| --- | --- |
| `PushUrl` | Uptime Kuma push monitor URL (for example `/api/push/<token>`) |
| `IntervalSeconds` | Heartbeat cadence in seconds (minimum enforced: 15) |
| `StartupDelaySeconds` | Delay after bot startup before first heartbeat |
| `TimeoutSeconds` | HTTP timeout for heartbeat push |

### Status Command

Use the slash command:

* `/status` to post the current status overview publicly in-channel
* `/status private:true` to return the same overview as an ephemeral response only visible to you

### Cooldowns

Cooldowns are configured per command under `Bot:Cooldowns` (in seconds).
Code-level defaults are still used when no config override is provided.

Example:

```json
{
  "Bot": {
    "Cooldowns": {
      "UserInfo": 5,
      "Status": 15
    }
  }
}
```

### Environment Variables

In production, settings are provided via environment variables using the `HALOCOMMUNITYBOT_` prefix and `__` as the section separator:

```text
HALOCOMMUNITYBOT_Bot__Token=your-token-here
HALOCOMMUNITYBOT_Bot__Prefix=!
HALOCOMMUNITYBOT_Bot__GuildId=1234567890
HALOCOMMUNITYBOT_Bot__AllowPrefixCommands=false
HALOCOMMUNITYBOT_Bot__Cooldowns__UserInfo=5
HALOCOMMUNITYBOT_Bot__Cooldowns__Status=15
HALOCOMMUNITYBOT_Bot__StatusMonitor__Enabled=true
HALOCOMMUNITYBOT_Bot__StatusMonitor__ChannelId=1234567890
HALOCOMMUNITYBOT_Bot__StatusMonitor__RoleId=1234567890
HALOCOMMUNITYBOT_Bot__StatusMonitor__FeedUrl=https://status.haloservicesolutions.com/pages/63ef45da7ee94905308a1a4a/rss
HALOCOMMUNITYBOT_Bot__StatusMonitor__PollIntervalMinutes=5
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__Enabled=true
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__ForumChannelId=1234567890
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__RoleId=1234567890
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__YouTubeDataApiKey=your-youtube-data-api-key
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__PollIntervalMinutes=15
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__RecentVideoCacheSize=50
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__DefaultPostTitleTemplate=[{ChannelName}] {VideoTitle}
HALOCOMMUNITYBOT_Bot__Heartbeat__Enabled=true
HALOCOMMUNITYBOT_Bot__Heartbeat__PushUrl=https://kuma.example.com/api/push/xxxxx
HALOCOMMUNITYBOT_Bot__Heartbeat__IntervalSeconds=60
HALOCOMMUNITYBOT_Bot__Heartbeat__StartupDelaySeconds=15
HALOCOMMUNITYBOT_Bot__Heartbeat__TimeoutSeconds=10
HALOCOMMUNITYBOT_ModerationLog__ForumChannelId=1234567890
HALOCOMMUNITYBOT_ModerationLog__ModeratorRoleId=1234567890
HALOCOMMUNITYBOT_CrossChannelSpam__Enabled=false
HALOCOMMUNITYBOT_CrossChannelSpam__TimeWindowSeconds=30
HALOCOMMUNITYBOT_CrossChannelSpam__MinimumChannelCount=3
HALOCOMMUNITYBOT_CrossChannelSpam__DeleteMessages=true
HALOCOMMUNITYBOT_CrossChannelSpam__TimeoutOnDetection=true
```

#### Moderation Exemptions Configuration

```bash
# Linux/Mac
export HALOCOMMUNITYBOT_ModerationExemptions__ExemptUserIds__0=1234567890
export HALOCOMMUNITYBOT_ModerationExemptions__ExemptRoleIds__0=1234567890

# Windows PowerShell
$env:HALOCOMMUNITYBOT_ModerationExemptions__ExemptUserIds__0="1234567890"
$env:HALOCOMMUNITYBOT_ModerationExemptions__ExemptRoleIds__0="1234567890"

# Windows CMD
set HALOCOMMUNITYBOT_ModerationExemptions__ExemptUserIds__0=1234567890
set HALOCOMMUNITYBOT_ModerationExemptions__ExemptRoleIds__0=1234567890
```

#### Command Access Configuration

```bash
# Disable all fun commands (meme, 8ball, roll, joke, say)
HALOCOMMUNITYBOT_CommandAccess__DisableAllFunCommands=true

# Disable specific commands globally
HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__0=about
HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__1=avatar
HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__2=help
HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__3=ping
HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__4=remind
HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__5=serverinfo
HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__6=userinfo

# Restrict commands to allowed channel IDs
HALOCOMMUNITYBOT_CommandAccess__RestrictedChannels__about__0=1234567890
HALOCOMMUNITYBOT_CommandAccess__RestrictedChannels__help__0=1234567890
HALOCOMMUNITYBOT_CommandAccess__RestrictedChannels__help__1=2345678901
```

Example list binding for allowed fun channels:

```text
HALOCOMMUNITYBOT_Bot__AllowedFunChannels__0=1075755533048492082
HALOCOMMUNITYBOT_Bot__AllowedFunChannels__1=123456789012345678
```

### GitHub Secrets (Deploy Workflow)

If you deploy with `.github/workflows/deploy.yml`, configure these repository secrets and they will be written into the runtime `.env` file on host:

| GitHub Secret | Runtime Environment Variable |
| --- | --- |
| `DEPLOY_SSH_KEY` | Used by GitHub Actions SSH setup to connect to the host |
| `DEPLOY_HOST` | Used by GitHub Actions SSH/rsync/scp target host |
| `DISCORD_TOKEN` | `HALOCOMMUNITYBOT_Bot__Token` |
| `BOT_PREFIX` | `HALOCOMMUNITYBOT_Bot__Prefix` |
| `GUILD_ID` | `HALOCOMMUNITYBOT_Bot__GuildId` |
| `ALLOW_PREFIX_COMMANDS` | `HALOCOMMUNITYBOT_Bot__AllowPrefixCommands` |
| `STATUS_MONITOR_ENABLED` | `HALOCOMMUNITYBOT_Bot__StatusMonitor__Enabled` |
| `STATUS_MONITOR_CHANNEL_ID` | `HALOCOMMUNITYBOT_Bot__StatusMonitor__ChannelId` |
| `STATUS_MONITOR_ROLE_ID` | `HALOCOMMUNITYBOT_Bot__StatusMonitor__RoleId` |
| `STATUS_MONITOR_FEED_URL` | `HALOCOMMUNITYBOT_Bot__StatusMonitor__FeedUrl` |
| `STATUS_MONITOR_POLL_INTERVAL_MINUTES` | `HALOCOMMUNITYBOT_Bot__StatusMonitor__PollIntervalMinutes` |
| `YOUTUBE_MONITOR_ENABLED` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__Enabled` |
| `YOUTUBE_FORUM_CHANNEL_ID` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__ForumChannelId` |
| `YOUTUBE_MONITOR_ROLE_ID` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__RoleId` |
| `YOUTUBE_DATA_API_KEY` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__YouTubeDataApiKey` |
| `YOUTUBE_POLL_INTERVAL_MINUTES` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__PollIntervalMinutes` |
| `YOUTUBE_RECENT_VIDEO_CACHE_SIZE` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__RecentVideoCacheSize` |
| `YOUTUBE_DEFAULT_POST_TITLE_TEMPLATE` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__DefaultPostTitleTemplate` |
| `HEARTBEAT_ENABLED` | `HALOCOMMUNITYBOT_Bot__Heartbeat__Enabled` |
| `HEARTBEAT_PUSH_URL` | `HALOCOMMUNITYBOT_Bot__Heartbeat__PushUrl` |
| `HEARTBEAT_INTERVAL_SECONDS` | `HALOCOMMUNITYBOT_Bot__Heartbeat__IntervalSeconds` |
| `HEARTBEAT_STARTUP_DELAY_SECONDS` | `HALOCOMMUNITYBOT_Bot__Heartbeat__StartupDelaySeconds` |
| `HEARTBEAT_TIMEOUT_SECONDS` | `HALOCOMMUNITYBOT_Bot__Heartbeat__TimeoutSeconds` |
| `MODERATION_LOG_FORUM_CHANNEL_ID` | `HALOCOMMUNITYBOT_ModerationLog__ForumChannelId` |
| `MODERATION_LOG_MODERATOR_ROLE_ID` | `HALOCOMMUNITYBOT_ModerationLog__ModeratorRoleId` |
| `MODERATION_LOG_EVENT_AUDIT_ENABLED` | `HALOCOMMUNITYBOT_ModerationLog__EventAuditEnabled` |
| `MODERATION_LOG_EVENT_AUDIT_CHANNEL_ID` | `HALOCOMMUNITYBOT_ModerationLog__EventAuditChannelId` |
| `MODERATION_LOG_EVENT_AUDIT_LOG_MESSAGE_DELETES` | `HALOCOMMUNITYBOT_ModerationLog__LogMessageDeletes` |
| `MODERATION_LOG_EVENT_AUDIT_LOG_MEMBER_LEAVES` | `HALOCOMMUNITYBOT_ModerationLog__LogMemberLeaves` |
| `MODERATION_LOG_EVENT_AUDIT_LOG_MEMBER_JOINS` | `HALOCOMMUNITYBOT_ModerationLog__LogMemberJoins` |
| `MODERATION_LOG_IGNORED_USER_ID_0` | `HALOCOMMUNITYBOT_ModerationLog__IgnoredUserIds__0` |
| `MODERATION_LOG_IGNORED_USER_ID_1` | `HALOCOMMUNITYBOT_ModerationLog__IgnoredUserIds__1` |
| `MODERATION_LOG_AUDIT_LOG_LOOKBACK_SECONDS` | `HALOCOMMUNITYBOT_ModerationLog__AuditLogLookbackSeconds` |
| `CROSS_CHANNEL_SPAM_ENABLED` | `HALOCOMMUNITYBOT_CrossChannelSpam__Enabled` |
| `CROSS_CHANNEL_SPAM_TIME_WINDOW_SECONDS` | `HALOCOMMUNITYBOT_CrossChannelSpam__TimeWindowSeconds` |
| `CROSS_CHANNEL_SPAM_MINIMUM_CHANNEL_COUNT` | `HALOCOMMUNITYBOT_CrossChannelSpam__MinimumChannelCount` |
| `CROSS_CHANNEL_SPAM_DELETE_MESSAGES` | `HALOCOMMUNITYBOT_CrossChannelSpam__DeleteMessages` |
| `CROSS_CHANNEL_SPAM_TIMEOUT_ON_DETECTION` | `HALOCOMMUNITYBOT_CrossChannelSpam__TimeoutOnDetection` |
| `MODERATION_EXEMPT_USER_ID_0` | `HALOCOMMUNITYBOT_ModerationExemptions__ExemptUserIds__0` |
| `MODERATION_EXEMPT_ROLE_ID_0` | `HALOCOMMUNITYBOT_ModerationExemptions__ExemptRoleIds__0` |
| `COMMAND_ACCESS_DISABLE_ALL_FUN_COMMANDS` | `HALOCOMMUNITYBOT_CommandAccess__DisableAllFunCommands` |
| `COMMAND_ACCESS_DISABLED_COMMAND_0` | `HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__0` |
| `COMMAND_ACCESS_DISABLED_COMMAND_1` | `HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__1` |
| `COMMAND_ACCESS_DISABLED_COMMAND_2` | `HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__2` |
| `COMMAND_ACCESS_DISABLED_COMMAND_3` | `HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__3` |
| `COMMAND_ACCESS_DISABLED_COMMAND_4` | `HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__4` |
| `COMMAND_ACCESS_DISABLED_COMMAND_5` | `HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__5` |
| `COMMAND_ACCESS_DISABLED_COMMAND_6` | `HALOCOMMUNITYBOT_CommandAccess__DisabledCommands__6` |

Note: `YoutubeMonitor:Channels` is best managed through `/youtube add` and persisted in SQLite, instead of storing an array in secrets.

## 🚢 Deployment

See [`.github/deployment/DEPLOYMENT_SETUP.md`](.github/deployment/DEPLOYMENT_SETUP.md) for full host setup instructions, including:

* Creating the `deployer` service account
* Installing the systemd service unit
* Configuring the `.env` file
* Setting up the required sudoers entries for the GitHub Actions deploy workflow

Deployments are triggered automatically by the `deploy.yml` workflow after a successful CI build on `main`, or manually via `workflow_dispatch`.

## 🏷️ Versioning

HaloCommunityBot now uses a `VersionManager` tool to keep `src/HaloCommunityBot/HaloCommunityBot.csproj` and `CHANGELOG.md` in sync.

1. Build the tool:

```bash
dotnet build tools/VersionManager/VersionManager.csproj -c Release
```

1. Optional commit analysis:

```bash
dotnet artifacts/bin/VersionManager/release/VersionManager.dll check-commits
```

1. Bump version:

```bash
dotnet artifacts/bin/VersionManager/release/VersionManager.dll bump --version X.Y.Z --type patch --message "Your description"
```

1. Validate consistency:

```bash
dotnet artifacts/bin/VersionManager/release/VersionManager.dll validate
```

There is also a helper script:

```bash
./Bump-Version.ps1 -Version X.Y.Z -Type patch -Message "Your description"
```

## 🔧 Tech Stack

* [.NET 10](https://dotnet.microsoft.com/) / C# 13
* [Discord.Net 3.x](https://github.com/discord-net/Discord.Net)
* `Microsoft.Extensions.Hosting` / `IHostedService`
* Central package management via `Directory.Packages.props`
