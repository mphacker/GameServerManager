# Game Server Manager

## Overview
Game Server manager is a simple Windows command line utility to help keep Steam dedicated server games backed-up and running. Features include:

- Watchdog - Ensures that the game server is up and running. If it crashes or stops the game server will be restarted.
- Auto Backup - Backup your server's save files directory into a ZIP file and stores it in safe location. You can also specify the number of days to keep a backup.
- Auto Update - Automatically have the game server shutdown and then check for server updates from Steam. Once complete, the server will be automatically restarted.

Game Server Manager supports managing multiple Steam servers at one time. All settings are managed in the appsettings.json file.

Below is an example configuration for the game Icarus:

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

Configuration parameters:

- SteamCMDPath: Full path and filename of the steamcmd command line utility.
- GameServers: An array of individual game servers to monitor. 
- Name:  This is a name to help identify the game the configuration represents. The Name is not used for any actions.
- ProcessName: This is the name of the process that shows up in Task Manager when the server is running. This is required to be accurate in order for the watchdog to properly know when the game server is running.
- GamePath: The path to where the game server is installed.
- ServerExe: The filename for the game server.
- ServerArgs: Optional command line arguments required when launching the game server.
- SteamAppId: The Steam ID for the game server.
- AutoRestart: [true/false] - When true, ensures that the watchdog keeps the game server up and running. If the server unexpectedly exits or crashes it will be started back up.
- AutoUpdate: [true/false] - When true, enables automatic updates of the game server from Steam.
- AutoUpdateBackupTime: Time in HH:MM AM/PM format that indicates when the game server should automatically shutdown and perform backups or updates.
- AutoBackup: [true/false] - When true, enables automatic server shutdown and file backups at the configured time.
- AutoBackupSource: Path to the games save files folder.
- AutoBackupDest: Path where the zipped backup of the save games folder should be placed.
- AutoBackupDaysToKeep: Number of days a backup zip file should be saved. The higher the number, the more disk space is required.

## Logging and Troubleshooting
A log with the filename format of "GameServerManager_yyyy-m-dd.log" will be created each day which will show all of the actions of the Game Server Manager.  These log files are automatically deleted after 10 days.  If you are experiencing any issues, consult the logs to see what the issue may be. Generally issues are related to incorrect configuration of SteamAppId, ProcessName, or file paths.

