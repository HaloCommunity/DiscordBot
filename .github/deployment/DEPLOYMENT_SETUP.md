# HaloCommunityBot Deployment Setup Guide

This guide covers setting up HaloCommunityBot on a DigitalOcean droplet (or any Linux host).

## Prerequisites

* Linux host (Ubuntu 20.04+)
* SSH access with sudo privileges
* SSH key pair for deployment

## Initial Setup (One-time)

### 1. Create Deployment User

```bash
sudo useradd -m -s /bin/bash deployer
sudo usermod -aG sudo deployer
```

### 2. Setup Directory Structure

```bash
sudo mkdir -p /opt/halocommunitybot
sudo chown deployer:deployer /opt/halocommunitybot
```

### 3. Create `.env` File

The deployment workflow automatically creates/updates this file with secrets from GitHub Actions.

Manual creation (if needed):

```bash
sudo tee /opt/halocommunitybot/.env > /dev/null << 'EOF'
HALOCOMMUNITYBOT_Bot__Token=your_discord_token_here
HALOCOMMUNITYBOT_Bot__StatusMonitor__Enabled=true
HALOCOMMUNITYBOT_Bot__StatusMonitor__ChannelId=your_channel_id_here
HALOCOMMUNITYBOT_Bot__StatusMonitor__RoleId=your_role_id_here
EOF

sudo chmod 600 /opt/halocommunitybot/.env
```

Environment variable names use the `HALOCOMMUNITYBOT_` prefix and `__` (double-underscore) as the config section separator:

| Variable | Config key | Purpose |
| --- | --- | --- |
| `HALOCOMMUNITYBOT_Bot__Token` | `Bot:Token` | Discord bot token |
| `HALOCOMMUNITYBOT_Bot__StatusMonitor__Enabled` | `Bot:StatusMonitor:Enabled` | Enable status RSS monitor |
| `HALOCOMMUNITYBOT_Bot__StatusMonitor__ChannelId` | `Bot:StatusMonitor:ChannelId` | Channel to post status updates |
| `HALOCOMMUNITYBOT_Bot__StatusMonitor__RoleId` | `Bot:StatusMonitor:RoleId` | Optional role to mention for status updates |

### 4. Install .NET Runtime

```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0 --runtime aspnetcore --install-dir /usr/local/dotnet
sudo ln -sf /usr/local/dotnet/dotnet /usr/bin/dotnet
```

### 5. Install Systemd Service

```bash
sudo cp halocommunitybot.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable halocommunitybot.service
```

### 6. Allow Non-Interactive Deploy Commands

The GitHub Actions deploy workflow runs `sudo` over SSH without a TTY/password prompt.
Grant `deployer` passwordless access to only the commands needed by the workflow:

```bash
sudo tee /etc/sudoers.d/halocommunitybot-deploy > /dev/null << 'EOF'
deployer ALL=(root) NOPASSWD: /bin/systemctl start halocommunitybot.service, /bin/systemctl stop halocommunitybot.service, /bin/systemctl status halocommunitybot.service, /bin/chown, /bin/chmod, /usr/bin/tee, /bin/mkdir
EOF
sudo chmod 440 /etc/sudoers.d/halocommunitybot-deploy
sudo visudo -cf /etc/sudoers.d/halocommunitybot-deploy
```

## Deployment Workflow

The GitHub Actions workflow (`deploy.yml`) handles:

1. Building the project
2. Publishing a Release build
3. Uploading files via SSH/rsync
4. Writing the `.env` file from GitHub secrets
5. Managing the systemd service (stop → deploy → start)
6. Checking service status post-deploy

### Required GitHub Secrets

Set these in your repository settings (Settings → Secrets and variables → Actions):

| Secret | Purpose |
| ------ | ------- |
| `DEPLOY_SSH_KEY` | Private SSH key for the deployer user |
| `DEPLOY_HOST` | IP/hostname of your server |
| `DISCORD_TOKEN` | Discord bot token |
| `STATUS_MONITOR_CHANNEL_ID` | Discord channel ID for status updates (optional) |
| `STATUS_MONITOR_ROLE_ID` | Discord role ID to mention in status updates (optional) |

## Managing the Service

After initial setup, manage the bot with:

```bash
# Check status
sudo systemctl status halocommunitybot.service

# View logs
sudo journalctl -u halocommunitybot.service -f

# Restart manually
sudo systemctl restart halocommunitybot.service

# Stop
sudo systemctl stop halocommunitybot.service

# Start
sudo systemctl start halocommunitybot.service
```

## Updating Environment Variables

### Option 1: Via Deployment

Update the workflow in `.github/workflows/deploy.yml` and push to main branch. All keys in the generated `.env` must use the `HALOCOMMUNITYBOT_` prefix to be picked up by the app.

### Option 2: Manual SSH

```bash
ssh deployer@your-host
sudo nano /opt/halocommunitybot/.env
# Edit the file, save and exit
sudo systemctl restart halocommunitybot.service
```

## Troubleshooting

### Service won't start

```bash
sudo journalctl -u halocommunitybot.service -n 50
```

### .env file not found

```bash
ls -la /opt/halocommunitybot/.env
# Should show: -rw------- (600 permissions)
```

### Permission denied errors

```bash
sudo chown -R deployer:deployer /opt/halocommunitybot
```

### Check if .NET is installed

```bash
dotnet --version
```

## Database

SQLite database is stored at `/opt/halocommunitybot/halocommunitybot.db`. This persists between deployments.

To backup:

```bash
cp /opt/halocommunitybot/halocommunitybot.db /opt/halocommunitybot/halocommunitybot.db.backup
```
