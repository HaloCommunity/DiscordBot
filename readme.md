# HaloCommunity Bot

Discord bot for the Halo Community server, built with C# (.NET 9) and [Discord.Net](https://github.com/discord-net/Discord.Net).

## ✨ Features

* **Slash commands** via Discord.Net's `InteractionService`
* **Moderation tools**: ban, kick, mute, warn, clear, purge, slowmode, lock/unlock
* **General utilities**: avatar, userinfo, serverinfo, reminders, fun commands, and more
* **Halo Services status monitor**: polls the [Halo Services Solutions status RSS feed](https://status.haloservicesolutions.com/pages/63ef45da7ee94905308a1a4a/rss) and posts updates to a configured channel
* **Permission-aware error handling**: friendly ephemeral responses when permission checks fail
* **Deployment via GitHub Actions**: CI build gate → SSH deploy to Linux host with systemd

## 🚀 Getting Started

### Prerequisites

* [.NET 9 SDK](https://dotnet.microsoft.com/download)
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
|---|---|
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
|---|---|---|
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
    "StatusMonitor": {
      "Enabled": false,
      "ChannelId": 0,
      "RoleId": 0,
      "FeedUrl": "https://status.haloservicesolutions.com/pages/63ef45da7ee94905308a1a4a/rss",
      "PollIntervalMinutes": 5
    }
  }
}
```

### Status Monitor

Set `StatusMonitor:Enabled` to `true` and configure:

| Setting | Description |
|---|---|
| `ChannelId` | Channel where status updates are posted |
| `RoleId` | Role to mention on status updates (set `0` to disable mentions) |
| `FeedUrl` | RSS feed URL (defaults to Halo Services Solutions) |
| `PollIntervalMinutes` | How often to check for new feed items (default: 5) |

### Status Command

Use the slash command:

* `/status` to post the current status overview publicly in-channel
* `/status private:true` to return the same overview as an ephemeral response only visible to you

### Environment Variables

In production, settings are provided via environment variables using the `HALOCOMMUNITYBOT_` prefix and `__` as the section separator:

```
HALOCOMMUNITYBOT_Bot__Token=your-token-here
HALOCOMMUNITYBOT_Bot__GuildId=1234567890
HALOCOMMUNITYBOT_Bot__StatusMonitor__Enabled=true
HALOCOMMUNITYBOT_Bot__StatusMonitor__ChannelId=1234567890
HALOCOMMUNITYBOT_Bot__StatusMonitor__RoleId=1234567890
```

## 🚢 Deployment

See [`.github/deployment/DEPLOYMENT_SETUP.md`](.github/deployment/DEPLOYMENT_SETUP.md) for full host setup instructions, including:

* Creating the `deployer` service account
* Installing the systemd service unit
* Configuring the `.env` file
* Setting up the required sudoers entries for the GitHub Actions deploy workflow

Deployments are triggered automatically by the `deploy.yml` workflow after a successful CI build on `main`, or manually via `workflow_dispatch`.

## 🔧 Tech Stack

* [.NET 9](https://dotnet.microsoft.com/) / C# 13
* [Discord.Net 3.x](https://github.com/discord-net/Discord.Net)
* `Microsoft.Extensions.Hosting` / `IHostedService`
* Central package management via `Directory.Packages.props`
