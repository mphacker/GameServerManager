# Game Server Manager

## Overview
Game Server Manager is a Windows command-line utility designed to help manage Steam-based game servers **and non-Steam applications/programs**. It provides features to ensure servers are running, backed up, and updated automatically. Key features include:

- **Watchdog**: Ensures the game server or application is running. If it crashes or stops, it will be restarted automatically.
- **Auto Backup**: Creates ZIP backups of your server's save files and stores them in a specified location. You can configure the number of backups to retain.
- **Auto Update**: Automatically shuts down the server, checks for updates via Steam, and restarts the server after updates are applied (Steam games only).
- **Rich Console UI**: Modern, interactive console interface powered by Spectre.Console with live status updates.
- **Notifications**: Supports email notifications for errors and important events.
- **Non-Steam Support**: Monitor and manage any Windows application or server, not just Steam games.

Game Server Manager supports managing multiple servers and applications simultaneously. Configuration is handled via the `appsettings.json` file or through an interactive CLI configuration utility.

---

## User Interface

### Live Status Dashboard
When you run the application normally, you'll see a beautiful live dashboard with:
- **Real-time server status** - View all enabled servers at a glance
- **Next scheduled events** - See when updates and backups will occur
- **Recent activity log** - Track the last 10 actions performed
- **Error alerts** - Highlighted error messages when issues occur
- **Auto-refresh** - Updates every 2 seconds automatically

The dashboard uses a modern table-based layout with color-coded information:
- ?? Green: Active/Enabled features
- ?? Yellow: Warnings or scheduled events
- ?? Red: Errors
- ?? Blue: Backup operations

### Interactive Configuration CLI
Run the configuration utility with:
```
GameServerManager config
```

Features of the configuration CLI:
- **Interactive prompts** - No need to manually edit JSON files
- **Input validation** - Ensures all values are correct before saving
- **Visual menus** - Easy-to-navigate selection menus
- **Test notifications** - Send test emails to verify your SMTP settings
- **Safe editing** - Option to exit without saving changes

---

## Configuration

### Example Configuration for Steam Game (`appsettings.json`)
Below is an example configuration for the game **Icarus**:
```json
{
  "SteamCMDPath": "D:\\steamcmd\\steamcmd.exe",
  "GameServers": [
    {
      "Name": "Icarus",
      "ProcessName": "IcarusServer-Win64-Shipping",
      "GamePath": "D:\\icarus",
      "ServerExe": "IcarusServer.exe",
      "ServerArgs": "-log -SteamServerName=\"Trez's Icarus Server\" -ResumeProspect",
      "SteamAppId": "2089300",
      "AutoRestart": true,
      "AutoUpdate": true,
      "AutoUpdateTime": "05:30 AM", // Legacy format example
      "AutoBackup": true,
      "AutoBackupTime": "0 5 * * *", // CRON format example (daily at 5:00 AM)
      "AutoBackupSource": "D:\\icarus\\Icarus\\Saved",
      "AutoBackupDest": "E:\\GameBackups\\Icarus\\",
      "AutoBackupsToKeep": 30,
      "BackupWithoutShutdown": false,
      "Enabled": true
    }
  ],
  "Notification": {
    "SmtpHost": "smtp.office365.com",
    "SmtpPort": 587,
    "SmtpUser": "your-email@example.com",
    "SmtpPass": "your-password",
    "Recipient": "contact-person@email.com",
    "EnableEmail": true
  }
}
```

### Example Configuration for Non-Steam Application
Below is an example for monitoring a non-Steam application or custom server:
```json
{
  "SteamCMDPath": "D:\\steamcmd\\steamcmd.exe",
  "GameServers": [
    {
      "Name": "ArchobotLLM",
      "ProcessName": "ArchobotLLMServerV3",
      "GamePath": "D:\\ArchobotLLMV3",
      "ServerExe": "ArchobotLLMServerV3.exe",
      "ServerArgs": "",
      "SteamAppId": "",
      "AutoRestart": true,
      "AutoUpdate": false,
      "AutoUpdateTime": "",
      "AutoBackup": false,
      "AutoBackupTime": "",
      "AutoBackupSource": "",
      "AutoBackupDest": "",
      "AutoBackupsToKeep": 30,
      "BackupWithoutShutdown": false,
      "LastUpdateDate": "",
      "LastBackupDate": "",
      "Enabled": true
    }
  ]
}
```

**Notes for Non-Steam Applications:**
- Set `SteamAppId` to an empty string (`""`)
- Set `AutoUpdate` to `false` (Steam updates won't work for non-Steam apps)
- `SteamCMDPath` is only required if you have at least one Steam-based server with `AutoUpdate` enabled
- You can still use `AutoRestart` for watchdog functionality
- You can still use `AutoBackup` if the application generates save files

### Configuration Parameters
- **SteamCMDPath**: Full path to the SteamCMD executable (e.g., `steamcmd.exe`). Only required if at least one enabled server has `AutoUpdate` set to `true`.
- **GameServers**: An array of game server configurations.
  - **Name**: A descriptive name for the server (used for identification only).
  - **ProcessName**: The name of the server's process as it appears in Task Manager (required for watchdog functionality).
  - **GamePath**: Path to the game server installation directory.
  - **ServerExe**: Filename of the game server executable.
  - **ServerArgs**: Optional command-line arguments for launching the server.
  - **SteamAppId**: Steam application ID for the game server. Leave empty (`""`) for non-Steam applications.
  - **Enabled**: `true`/`false` - Whether this server configuration is active.
  - **AutoRestart**: `true`/`false` - Automatically restart the server if it crashes or stops unexpectedly.
  - **AutoUpdate**: `true`/`false` - Enable automatic updates via Steam. Set to `false` for non-Steam applications.
  - **AutoUpdateTime**: Time for scheduled updates. Supports both legacy and CRON formats:
    - Legacy: `"HH:mm"`, `"hh:mm tt"` (e.g., `"05:30 AM"`, `"17:30"`)
    - CRON: Standard CRON expression (e.g., `"0 5 * * *"` for daily at 5:00 AM)
  - **AutoBackup**: `true`/`false` - Enable automatic backups of server save files.
  - **AutoBackupSource**: Path to the folder containing save files.
  - **AutoBackupDest**: Path to store the zipped backups.
  - **AutoBackupTime**: Time for scheduled backups. Supports both legacy and CRON formats (see above).
  - **AutoBackupsToKeep**: Number of most recent backup files to retain. Older backups will be deleted.
  - **BackupWithoutShutdown**: `true`/`false` - If `true`, backups will be performed without shutting down the dedicated server. If `false`, the server will be stopped before backup and restarted after.
  - **LastUpdateDate**: (Auto-managed) Last time the server was updated.
  - **LastBackupDate**: (Auto-managed) Last time the server was backed up.

- **Notification**: (Optional) Notification settings for email notifications.
  - **SmtpHost**: SMTP server hostname (e.g., `smtp.office365.com` for Office 365).
  - **SmtpPort**: SMTP server port (e.g., 587 for TLS).
  - **SmtpUser**: SMTP username (your email address).
  - **SmtpPass**: SMTP password or app-specific password.
  - **Recipient**: Email address to receive notifications.
  - **EnableEmail**: `true`/`false` - Enable email notifications.

---

## CLI Commands

### Start the Server Manager (Live Dashboard)
```
GameServerManager
```
Starts the application with the live status dashboard. Press **CTRL+C** to exit gracefully.

### Interactive Configuration
```
GameServerManager config
```
Launches the interactive configuration editor with menu-driven prompts.

### Help
```
GameServerManager --help
```
or
```
GameServerManager /?
```
Displays command-line help and usage information.

---

## Running the Application

### First Time Setup
1. **Install .NET 9.0** runtime if not already installed
2. **Download and install SteamCMD** from [https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip](https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip)
3. **Run the configuration CLI**: `GameServerManager config`
4. **Set your SteamCMD path** (e.g., `D:\steamcmd\steamcmd.exe`)
5. **Add your game servers** using the interactive prompts
6. **Configure notifications** (optional but recommended)
7. **Save and exit**

### Normal Operation
Once configured, start the application:
```
GameServerManager
```

The live dashboard will appear, showing:
- All enabled servers
- Last update/backup times
- Next scheduled update/backup times
- Real-time activity log
- Any errors or warnings

The watchdog will continuously monitor your servers, and auto updates/backups will occur as scheduled.

---

## Logging and Troubleshooting
The application generates daily log files in the format `GameServerManager_yyyy-MM-dd.log` in the application directory. Logs include:
- Server start/stop events
- Update operations
- Backup operations
- Errors and warnings
- Configuration validation messages

Log files are automatically deleted after 10 days to save disk space.

### Common Issues
- **Server not restarting**: Check that `ProcessName` matches exactly what appears in Task Manager
- **Updates failing**: Verify `SteamAppId` is correct and `SteamCMDPath` points to a valid `steamcmd.exe`
- **Backups failing**: Ensure `AutoBackupSource` and `AutoBackupDest` paths exist and are accessible
- **Email notifications not working**: Use the "Send Test Notification" option in the config CLI to verify SMTP settings
- **Server starts but crashes immediately**: The server process is started with its `GamePath` as the working directory, so it should find config files and dependencies correctly

---

## Requirements
- **.NET 9.0 Runtime**: [Download here](https://dotnet.microsoft.com/download/dotnet/9.0)
- **SteamCMD**: Required only for Steam-based server updates ([Download here](https://developer.valvesoftware.com/wiki/SteamCMD))
- **Windows OS**: Currently designed for Windows environments

---

## Features Summary
- ? **Watchdog**: Keeps servers running with automatic restart
- ? **Auto Backup**: Scheduled backups with configurable retention
- ? **Auto Update**: Scheduled updates via SteamCMD (Steam games only)
- ? **Multi-Server Support**: Manage multiple servers simultaneously
- ? **Non-Steam Support**: Monitor and restart any Windows application
- ? **Rich Console UI**: Modern, live-updating dashboard with Spectre.Console
- ? **Interactive Configuration**: User-friendly CLI configuration editor
- ? **Flexible Scheduling**: Support for both time-of-day and CRON expressions
- ? **Email Notifications**: Get notified of errors and important events
- ? **Detailed Logging**: Comprehensive logs for troubleshooting

---

## Technical Details

### Architecture
- Built with **.NET 9.0** and C# 13.0
- Uses **Spectre.Console** for rich terminal UI
- **Serilog** for structured logging
- **NCrontab** for CRON expression parsing
- **Dependency Injection** for service management

### Performance
- Efficient UI rendering with automatic diffing (no flicker)
- Minimal CPU usage during monitoring
- Thread-safe concurrent operations
- Async/await throughout for responsive UI

---

## License
This project is provided as-is for managing Steam dedicated servers. Feel free to modify and extend for your needs.











