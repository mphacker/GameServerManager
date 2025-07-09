using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GameServerManager
{
    public class ServerBackup
    {
        private readonly GameServer _gameServer;
        private readonly string _steamCmdPath = string.Empty;
        private readonly ILogger<ServerBackup> _logger;

        public ServerBackup(GameServer gameServer, string steamCmdPath, ILogger<ServerBackup> logger)
        {
            _gameServer = gameServer;
            _steamCmdPath = steamCmdPath;
            _logger = logger;
        }

        public bool Backup()
        {
            if (_gameServer.AutoBackup)
            {
                // Check if the source and destination paths are valid
                if (string.IsNullOrEmpty(_gameServer.AutoBackupSource) || string.IsNullOrEmpty(_gameServer.AutoBackupDest))
                {
                    _logger.LogWarning("ServerBackup - Invalid backup source or destination for {ServerName}", _gameServer.Name);
                    return false;
                }

                _logger.LogInformation("ServerBackup - Checking process for {ServerName}", _gameServer.Name);
                var process = System.Diagnostics.Process.GetProcessesByName(_gameServer.ProcessName).FirstOrDefault();
                if (process == null || process.HasExited)
                {
                    _logger.LogInformation("ServerBackup - Process for {ServerName} not found. Backup process starting.", _gameServer.Name);
                }
                else
                {
                    _logger.LogInformation("ServerBackup - Process for {ServerName} is running. Backup skipped.", _gameServer.Name);
                    return false;
                }

                //create a backup filename consisting of the game server name and the current date with time to seconds
                string backupFileName = $"{_gameServer.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";

                _logger.LogInformation("ServerBackup - Creating backup for {ServerName} at {Time}", _gameServer.Name, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                //Compress the source directory into a zip file in the destination directory
                string backupFilePath = System.IO.Path.Combine(_gameServer.AutoBackupDest, backupFileName);
                try
                {
                    System.IO.Compression.ZipFile.CreateFromDirectory(_gameServer.AutoBackupSource, backupFilePath);
                    _logger.LogInformation("ServerBackup - Backup created successfully for {ServerName} at {BackupPath}", _gameServer.Name, backupFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ServerBackup - Error creating backup for {ServerName}", _gameServer.Name);
                    return false;
                }

                //Clean up old backups. Only keep the last _gamerServer.AutoBackupDaysToKeep days of backups. Note, the backup folder may contain files from other server backups.
                try
                {
                    _logger.LogInformation("ServerBackup - Cleaning up old backups for {ServerName}", _gameServer.Name);
                    var backupFiles = System.IO.Directory.GetFiles(_gameServer.AutoBackupDest, $"{_gameServer.Name}_*.zip");
                    foreach (var file in backupFiles)
                    {
                        var fileInfo = new System.IO.FileInfo(file);
                        if (fileInfo.CreationTime < DateTime.Now.AddDays(-_gameServer.AutoBackupDaysToKeep))
                        {
                            fileInfo.Delete();
                            _logger.LogInformation("Deleted old backup file: {File}", file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ServerBackup - Error cleaning up old backups for {ServerName}", _gameServer.Name);
                    return false;
                }

                return true;
            }

            return false;
        }
    }
}
