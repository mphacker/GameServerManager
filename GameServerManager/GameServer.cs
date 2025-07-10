public class GameServer
{
    public string Name { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string GamePath { get; set; } = string.Empty;
    public string ServerExe { get; set; } = string.Empty;
    public string ServerArgs { get; set; } = string.Empty;
    public string SteamAppId { get; set; } = string.Empty;
    public bool AutoRestart { get; set; }
    public bool AutoUpdate { get; set; }
    public string AutoUpdateTime { get; set; } = string.Empty;
    public bool AutoBackup { get; set; }
    public string AutoBackupTime { get; set; } = string.Empty;
    public string AutoBackupSource { get; set; } = string.Empty;
    public string AutoBackupDest { get; set; } = string.Empty;
    public int AutoBackupsToKeep { get; set; } // Renamed from AutoBackupDaysToKeep
    public bool BackupWithoutShutdown { get; set; } = false;
    // Optionally keep AutoUpdateBackupTime for migration/compatibility
    // public string AutoUpdateBackupTime { get; set; } = string.Empty;

    public DateTime? LastUpdateDate { get; set; }
    public DateTime? LastBackupDate { get; set; }
}
