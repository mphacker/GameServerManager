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

```json
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
      "AutoUpdateBackupTime": "05:30 AM",
      "AutoBackup": true,
      "AutoBackupSource": "D:\\icarus\\Icarus\\Saved",
      "AutoBackupDest": "E:\\GameBackups\\Icarus\\",
      "AutoBackupDaysToKeep": 30
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
  - **AutoUpdateBackupTime**: Time in `HH:MM AM/PM` format for scheduled backups and updates.
  - **AutoBackup**: `true`/`false` - Enable automatic backups of server save files.
  - **AutoBackupSource**: Path to the folder containing save files.
  - **AutoBackupDest**: Path to store the zipped backups.
  - **AutoBackupDaysToKeep**: Number of days to retain backup files.

---

## CLI Configuration
You can configure the application using the CLI instead of manually editing the `appsettings.json` file. Run the application with the `config` argument:

```bash
GameServerManager config
```

Follow the prompts to set up your configuration interactively.

---

## Running the Application
Once the `appsettings.json` file is configured (manually or via CLI), start the application by running:

```bash
GameServerManager.exe
```

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



