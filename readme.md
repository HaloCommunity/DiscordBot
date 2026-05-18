# HaloCommunity Bot

Discord bot for the Halo Community server, built with C# (.NET 10) and [Discord.Net](https://github.com/discord-net/Discord.Net).

## ✨ Features

* **Slash commands** via Discord.Net's `InteractionService`
* **Moderation tools**: ban, kick, mute, warn, clear, purge, slowmode, lock/unlock
* **General utilities**: avatar, userinfo, serverinfo, reminders, fun commands, and more
* **Halo Services status monitor**: polls the [Halo Services Solutions status RSS feed](https://status.haloservicesolutions.com/pages/63ef45da7ee94905308a1a4a/rss) and posts updates to a configured channel
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
    }
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
| `PollIntervalMinutes` | How often to check for new feed items (default: 5) |

### YouTube Monitor

Set `YoutubeMonitor:Enabled` to `true` and configure:

| Setting | Description |
| --- | --- |
| `ForumChannelId` | Discord forum channel ID where new video threads are created |
| `RoleId` | Optional role to mention when a new video is posted (set `0` to disable mentions) |
| `YouTubeDataApiKey` | Optional YouTube Data API key used to resolve plain channel names to channel IDs |
| `PollIntervalMinutes` | Feed polling cadence (default: 15) |
| `DefaultPostTitleTemplate` | Thread title template (supports `{ChannelName}` and `{VideoTitle}`) |
| `Channels` | Optional startup seed list of YouTube channel IDs, @handles, feed URLs, or channel names (channel names require `YouTubeDataApiKey`) |

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
HALOCOMMUNITYBOT_Bot__GuildId=1234567890
HALOCOMMUNITYBOT_Bot__Cooldowns__UserInfo=5
HALOCOMMUNITYBOT_Bot__Cooldowns__Status=15
HALOCOMMUNITYBOT_Bot__StatusMonitor__Enabled=true
HALOCOMMUNITYBOT_Bot__StatusMonitor__ChannelId=1234567890
HALOCOMMUNITYBOT_Bot__StatusMonitor__RoleId=1234567890
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__Enabled=true
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__ForumChannelId=1234567890
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__RoleId=1234567890
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__YouTubeDataApiKey=your-youtube-data-api-key
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__PollIntervalMinutes=15
HALOCOMMUNITYBOT_Bot__YoutubeMonitor__DefaultPostTitleTemplate=[{ChannelName}] {VideoTitle}
HALOCOMMUNITYBOT_Bot__Heartbeat__Enabled=true
HALOCOMMUNITYBOT_Bot__Heartbeat__PushUrl=https://kuma.example.com/api/push/xxxxx
HALOCOMMUNITYBOT_Bot__Heartbeat__IntervalSeconds=60
HALOCOMMUNITYBOT_Bot__Heartbeat__StartupDelaySeconds=15
HALOCOMMUNITYBOT_Bot__Heartbeat__TimeoutSeconds=10
```

### GitHub Secrets (Deploy Workflow)

If you deploy with `.github/workflows/deploy.yml`, configure these repository secrets and they will be written into the runtime `.env` file on host:

| GitHub Secret | Runtime Environment Variable |
| --- | --- |
| `DEPLOY_SSH_KEY` | Used by GitHub Actions SSH setup to connect to the host |
| `DEPLOY_HOST` | Used by GitHub Actions SSH/rsync/scp target host |
| `DISCORD_TOKEN` | `HALOCOMMUNITYBOT_Bot__Token` |
| `STATUS_MONITOR_ENABLED` | `HALOCOMMUNITYBOT_Bot__StatusMonitor__Enabled` |
| `STATUS_MONITOR_CHANNEL_ID` | `HALOCOMMUNITYBOT_Bot__StatusMonitor__ChannelId` |
| `STATUS_MONITOR_ROLE_ID` | `HALOCOMMUNITYBOT_Bot__StatusMonitor__RoleId` |
| `YOUTUBE_MONITOR_ENABLED` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__Enabled` |
| `YOUTUBE_FORUM_CHANNEL_ID` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__ForumChannelId` |
| `YOUTUBE_MONITOR_ROLE_ID` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__RoleId` |
| `YOUTUBE_DATA_API_KEY` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__YouTubeDataApiKey` |
| `YOUTUBE_POLL_INTERVAL_MINUTES` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__PollIntervalMinutes` |
| `YOUTUBE_DEFAULT_POST_TITLE_TEMPLATE` | `HALOCOMMUNITYBOT_Bot__YoutubeMonitor__DefaultPostTitleTemplate` |
| `HEARTBEAT_ENABLED` | `HALOCOMMUNITYBOT_Bot__Heartbeat__Enabled` |
| `HEARTBEAT_PUSH_URL` | `HALOCOMMUNITYBOT_Bot__Heartbeat__PushUrl` |
| `HEARTBEAT_INTERVAL_SECONDS` | `HALOCOMMUNITYBOT_Bot__Heartbeat__IntervalSeconds` |
| `HEARTBEAT_STARTUP_DELAY_SECONDS` | `HALOCOMMUNITYBOT_Bot__Heartbeat__StartupDelaySeconds` |
| `HEARTBEAT_TIMEOUT_SECONDS` | `HALOCOMMUNITYBOT_Bot__Heartbeat__TimeoutSeconds` |

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
