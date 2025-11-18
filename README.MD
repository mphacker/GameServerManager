# Game Server Manager

A Windows command-line utility for managing Steam-based game servers and non-Steam applications with automatic monitoring, updates, and backups.

## Key Features

- **Watchdog** - Automatically restarts crashed or stopped servers
- **Auto Update** - Continuous background update detection for Steam games (SteamCMD-based)
- **Auto Backup** - Scheduled backups with configurable retention
- **Rich Console UI** - Live-updating dashboard powered by Spectre.Console
- **Notifications** - Email alerts for errors and important events
- **Multi-Server Support** - Manage multiple servers simultaneously
- **Non-Steam Support** - Monitor and restart any Windows application

---

## Quick Start

### Prerequisites
- **.NET 10.0 Runtime** - [Download here](https://dotnet.microsoft.com/download/dotnet/10.0)
- **SteamCMD** - Required for Steam updates - [Download here](https://developer.valvesoftware.com/wiki/SteamCMD)
- **Windows OS** - Currently Windows-only

### Setup
1. Download and extract GameServerManager
2. Install SteamCMD (e.g., `D:\steamcmd\`)
3. Run interactive configuration: `GameServerManager config`
4. Add your servers and save
5. Start the manager: `GameServerManager`

---

## Configuration

Edit `appsettings.json` or use the interactive CLI (`GameServerManager config`).

### Example Configuration

```json
{
  "SteamCMDPath": "D:\\steamcmd\\steamcmd.exe",
  "UpdateCheckIntervalMinutes": 30,
  "GameServers": [
    {
      "Name": "Enshrouded",
      "ProcessName": "enshrouded_server",
      "GamePath": "D:\\SteamLibrary\\steamapps\\common\\EnshroudedServer",
      "ServerExe": "enshrouded_server.exe",
      "ServerArgs": "",
      "SteamAppId": "2278520",
      "AutoRestart": true,
      "AutoUpdate": true,
      "AutoBackup": true,
      "AutoBackupTime": "06:00 AM",
      "AutoBackupSource": "D:\\SteamLibrary\\steamapps\\common\\EnshroudedServer\\savegame",
      "AutoBackupDest": "E:\\GameBackups\\Enshrouded\\",
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
    "Recipient": "admin@example.com",
    "EnableEmail": true
  }
}
```

### Key Settings

**Global:**
- `SteamCMDPath` - Path to steamcmd.exe (required for Steam games)
- `UpdateCheckIntervalMinutes` - Check frequency (default: 30, min: 15)

**Per Server:**
- `Name` - Server display name
- `ProcessName` - Process name in Task Manager (without .exe)
- `GamePath` - Installation directory
- `ServerExe` - Executable filename
- `SteamAppId` - Steam App ID (empty for non-Steam apps)
- `AutoRestart` - Restart if crashed (default: false)
- `AutoUpdate` - Enable automatic updates (default: false)
- `AutoBackup` - Enable scheduled backups (default: false)
- `AutoBackupTime` - Schedule as `"HH:mm"` or CRON (e.g., `"0 5 * * *"`)
- `BackupWithoutShutdown` - Backup while running (default: false)
- `Enabled` - Enable/disable this server (default: true)

**Auto-managed fields** (don't edit manually):
- `CurrentBuildId`, `LastUpdateCheck`, `LastUpdateDate`, `LastBackupDate`

---

## How It Works

### Auto Update
1. **Startup Check** - Checks for updates 5 seconds after start
2. **Periodic Checks** - Runs every X minutes (configured)
3. **VDF Parsing** - Uses Valve Data Format parser to read SteamCMD output
4. **Build ID Tracking** - Compares local vs. Steam build IDs
5. **Smart Updates** - Only updates when new version is confirmed

Updates are detected within 15-30 minutes of release. Checks run in the background without stopping your server.

### Backup Scheduling
- **Time Format**: `"05:30 AM"`, `"17:30"`
- **CRON Format**: `"0 5 * * *"` (daily 5 AM), `"0 */6 * * *"` (every 6 hours)

Backups are ZIP files with timestamps. Old backups are auto-deleted based on `AutoBackupsToKeep`.

---

## Dashboard UI

```
??????????????????????????????????????????????????????????????
? Game Server Manager | Press CTRL+C to exit                ?
? Flags: R=Restart U=Update B=Backup                        ?
??????????????????????????????????????????????????????????????
? Server      ? Build ID ? Update Check ? Last Update ? ... ?
?????????????????????????????????????????????????????????????
? Enshrouded  ? 20801322 ? 12m ago      ? 11-18 06:00 ? RUB ?
? CustomApp   ?   N/A    ?   disabled   ?    none     ?  R  ?
??????????????????????????????????????????????????????????????

Recent Activity:
  2025-11-18 13:40:47 [OK] Enshrouded is up to date
  2025-11-18 13:40:44 [Check] Checking for updates: Enshrouded
  2025-11-18 13:33:36 [OK] Backup completed for Enshrouded
```

Auto-refreshes every 2 seconds showing:
- Server status, build IDs, check times
- Last 10 activities
- Error alerts

---

## Commands

| Command | Description |
|---------|-------------|
| `GameServerManager` | Start dashboard |
| `GameServerManager config` | Interactive configuration |
| `GameServerManager --help` | Show help |

---

## Troubleshooting

**Server Not Restarting**
- Verify `ProcessName` matches Task Manager exactly (without .exe)
- Check logs for errors

**Updates Not Working**
- Verify `SteamAppId` is correct
- Check `SteamCMDPath` points to valid steamcmd.exe
- Ensure `AutoUpdate` is true
- Review logs for errors

**Backups Failing**
- Verify source/destination paths exist
- Check file permissions
- Ensure sufficient disk space

**Update Checks Not Running**
- Check `UpdateCheckIntervalMinutes` is at least 15
- Verify server is `Enabled: true`
- Ensure `SteamAppId` is not empty for Steam games

---

## Advanced

### Non-Steam Applications
Set `SteamAppId` to empty string and `AutoUpdate` to false. Watchdog and backups work normally.

### Multiple Servers
Add multiple entries in `GameServers` array. Each operates independently.

### Graceful Shutdown
Press **CTRL+C** to exit. SteamCMD processes are terminated; game servers continue running.

---

## Technical Stack

- **.NET 10** / C# 13.0
- **Spectre.Console** - Rich terminal UI
- **Serilog** - Structured logging
- **NCrontab** - CRON parsing
- **ValveKeyValue** - VDF parsing for SteamCMD output
- **SteamCMD** - Steam game updates

---

## Logging

Logs are written to `GameServerManager_yyyy-MM-dd.log` (retained for 10 days).

Includes: server events, update checks, backups, errors, warnings.

---

## Email Notifications

Configure SMTP in `Notification` section for alerts on:
- Update failures
- Backup failures
- Server restart failures
- Critical errors

Test with: `GameServerManager config` ? "Send Test Notification"

---

## License

Free to use and modify. Provided as-is.











