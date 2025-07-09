# Game Server Manager

## Overview
Game Server Manager is a Windows command-line utility designed to help manage Steam dedicated server games. It provides features to ensure servers are running, backed up, and updated automatically. Key features include:

- **Watchdog**: Ensures the game server is running. If the server crashes or stops, it will be restarted automatically.
- **Auto Backup**: Creates ZIP backups of your server's save files and stores them in a specified location. You can configure the number of days to retain backups.
- **Auto Update**: Automatically shuts down the server, checks for updates via Steam, and restarts the server after updates are applied.

Game Server Manager supports managing multiple Steam servers simultaneously. Configuration is handled via the `appsettings.json` file or through a CLI configuration utility.

---

## Configuration

### Example Configuration (`appsettings.json`)
Below is an example configuration for the game **Icarus**:
```
{
  "SteamCMDPath": "D:\\steamcmd\\steamcmd.exe",
  "GameServers": [
    {
      "Name": "Icarus",
      "ProcessName": "IcarusServer-Win64-Shipping",
      "GamePath": "D:\\icarus",
      "ServerExe": "IcarusServer.exe",
      "ServerArgs": "-log -SteamServerName=\"My Icarus Server\" -ResumeProspect",
      "SteamAppId": "2089300",
      "AutoRestart": true,
      "AutoUpdate": true,
      "AutoUpdateTime": "05:30 AM", // Legacy format example
      "AutoBackup": true,
      "AutoBackupSource": "D:\\icarus\\Icarus\\Saved",
      "AutoBackupDest": "E:\\GameBackups\\Icarus\\",
      "AutoBackupTime": "30 5 * * *", // CRON format example (5:30 AM every day)
      "AutoBackupsToKeep": 30,
      "BackupWithoutShutdown": false
    }
  ]
}
```
### Configuration Parameters
- **SteamCMDPath**: Full path to the SteamCMD executable (e.g., `steamcmd.exe`).
- **GameServers**: An array of game server configurations.
  - **Name**: A descriptive name for the server (used for identification only).
  - **ProcessName**: The name of the server's process as it appears in Task Manager (required for watchdog functionality).
  - **GamePath**: Path to the game server installation directory.
  - **ServerExe**: Filename of the game server executable.
  - **ServerArgs**: Optional command-line arguments for launching the server.
  - **SteamAppId**: Steam application ID for the game server.
  - **AutoRestart**: `true`/`false` - Automatically restart the server if it crashes or stops unexpectedly.
  - **AutoUpdate**: `true`/`false` - Enable automatic updates via Steam.
  - **AutoUpdateTime**: Time for scheduled updates. Supports both legacy and CRON formats:
    - Legacy: `"HH:mm"`, `"hh:mm tt"` (e.g., `"05:30 AM"`, `"17:30"`)
    - CRON: Standard CRON expression (e.g., `"30 5 * * *"` for 5:30 AM daily)
  - **AutoBackup**: `true`/`false` - Enable automatic backups of server save files.
  - **AutoBackupSource**: Path to the folder containing save files.
  - **AutoBackupDest**: Path to store the zipped backups.
  - **AutoBackupTime**: Time for scheduled backups. Supports both legacy and CRON formats (see above).
  - **AutoBackupsToKeep**: Number of most recent backup files to retain. Older backups will be deleted.
  - **BackupWithoutShutdown**: `true`/`false` - If `true`, backups will be performed without shutting down the dedicated server. If `false`, the server will be stopped before backup and restarted after.

---

## CLI Configuration
You can configure the application using the CLI instead of manually editing the `appsettings.json` file. Run the application with the `config` argument:
GameServerManager config
Follow the prompts to set up your configuration interactively.

---

## Running the Application
Once the `appsettings.json` file is configured (manually or via CLI), start the application by running:
GameServerManager.exe
The watchdog process will begin monitoring the servers. Auto updates and backups will occur as scheduled.

---

## Logging and Troubleshooting
The application generates daily log files in the format `GameServerManager_yyyy-MM-dd.log`. Logs include details of all actions performed by the application. Log files are automatically deleted after 10 days.

If you encounter issues, check the logs for errors related to configuration (e.g., incorrect `SteamAppId`, `ProcessName`, or file paths).

---

## Requirements
- **.NET 9.0**: Ensure the runtime is installed.
- **SteamCMD**: Required for server updates.

---

## Features Summary
- **Watchdog**: Keeps servers running.
- **Auto Backup**: Scheduled backups with retention management.
- **Auto Update**: Scheduled updates via Steam.
- **Multi-Server Support**: Manage multiple servers simultaneously.
- **Logging**: Detailed logs for troubleshooting.

---






