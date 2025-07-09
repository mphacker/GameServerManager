using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GameServerManager
{
    public class ServerBackupService
    {
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        public ServerBackupService(ILogger logger, IFileSystem? fileSystem = null)
        {
            _logger = logger;
            _fileSystem = fileSystem ?? new FileSystem();
        }

        public async Task<bool> BackupAsync(GameServer gameServer)
        {
            if (!gameServer.AutoBackup)
                return false;
            if (string.IsNullOrEmpty(gameServer.AutoBackupSource) || string.IsNullOrEmpty(gameServer.AutoBackupDest))
            {
                _logger.LogWarning("Invalid backup source or destination for {ServerName}", gameServer.Name);
                return false;
            }
            var processRunning = System.Diagnostics.Process.GetProcessesByName(gameServer.ProcessName).Any(p => !p.HasExited);
            if (processRunning && !gameServer.BackupWithoutShutdown)
            {
                _logger.LogInformation("Process for {ServerName} is running. Backup skipped.", gameServer.Name);
                return false;
            }
            string backupFileName = $"{gameServer.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            string backupFilePath = Path.Combine(gameServer.AutoBackupDest, backupFileName);
            try
            {
                await Task.Run(() => ZipFile.CreateFromDirectory(gameServer.AutoBackupSource, backupFilePath));
                _logger.LogInformation("Backup created successfully for {ServerName} at {BackupPath}", gameServer.Name, backupFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating backup for {ServerName}", gameServer.Name);
                return false;
            }
            // Clean up old backups: keep only the most recent N backups
            try
            {
                var backupFiles = _fileSystem.GetFiles(gameServer.AutoBackupDest, $"{gameServer.Name}_*.zip");
                var orderedFiles = backupFiles
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();
                if (gameServer.AutoBackupsToKeep > 0 && orderedFiles.Count > gameServer.AutoBackupsToKeep)
                {
                    foreach (var file in orderedFiles.Skip(gameServer.AutoBackupsToKeep))
                    {
                        _fileSystem.DeleteFile(file.FullName);
                        _logger.LogInformation("Deleted old backup file: {File}", file.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old backups for {ServerName}", gameServer.Name);
                return false;
            }
            return true;
        }
    }
}
