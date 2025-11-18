namespace GameServerManager;

using System;
using System.Collections.Generic;

/// <summary>
/// Application settings for GameServerManager.
/// </summary>
public class Settings
{
    public string SteamCMDPath { get; set; } = string.Empty;
    public int UpdateCheckIntervalMinutes { get; set; } = 30; // Default: check every 30 minutes
    public List<GameServer>? GameServers { get; set; }
}

/// <summary>
/// Represents a game server configuration.
/// </summary>
public class GameServer
{
    public string Name { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string GamePath { get; set; } = string.Empty;
    public string ServerExe { get; set; } = string.Empty;
    public string ServerArgs { get; set; } = string.Empty;
    public string SteamAppId { get; set; } = string.Empty;
    public bool AutoRestart { get; set; } = false;
    public bool AutoUpdate { get; set; } = false;
    public int? CurrentBuildId { get; set; } // Track current build ID for update detection
    public DateTime? LastUpdateCheck { get; set; } // Last time we checked for updates
    public bool AutoBackup { get; set; } = false;
    public string AutoBackupTime { get; set; } = "05:30 AM";
    public string AutoBackupSource { get; set; } = string.Empty;
    public string AutoBackupDest { get; set; } = string.Empty;
    public int AutoBackupsToKeep { get; set; } = 30;
    public bool BackupWithoutShutdown { get; set; } = false;
    public DateTime? LastUpdateDate { get; set; }
    public DateTime? LastBackupDate { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Abstraction for file system operations.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string[] GetFiles(string path, string searchPattern);
    void DeleteFile(string path);
}

public class FileSystem : IFileSystem
{
    public bool FileExists(string path) => System.IO.File.Exists(path);
    public bool DirectoryExists(string path) => System.IO.Directory.Exists(path);
    public string[] GetFiles(string path, string searchPattern) => System.IO.Directory.GetFiles(path, searchPattern);
    public void DeleteFile(string path) => System.IO.File.Delete(path);
}
