using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServerManager
{
    public class ServerBackup
    {
        private GameServer _gameServer;
        private string _steamCmdPath = string.Empty;

        public ServerBackup(GameServer gameServer, string steamCmdPath) 
        {
            _gameServer = gameServer;
            _steamCmdPath = steamCmdPath;
        }

        public bool Backup()
        {
            if (_gameServer.AutoBackup)
            {
                // Check if the source and destination paths are valid
                if (string.IsNullOrEmpty(_gameServer.AutoBackupSource) || string.IsNullOrEmpty(_gameServer.AutoBackupDest))
                {
                    Utils.Log($"ServerBackup - Invalid backup source or destination for {_gameServer.Name}");
                    return false;
                }

                Utils.Log($"ServerBackup - Checking process for {_gameServer.Name}");
                var process = System.Diagnostics.Process.GetProcessesByName(_gameServer.ProcessName).FirstOrDefault();
                if (process == null || process.HasExited)
                {
                    Utils.Log($"ServerBackup - Process for {_gameServer.Name} not found. Backup process starting.");
                }
                else
                {
                    Utils.Log($"ServerBackup - Process for {_gameServer.Name} is running. Backup skipped.");
                    return false;
                }

                //create a backup filename consisting of the game server name and the current date with time to seconds
                string backupFileName = $"{_gameServer.Name}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.zip";

                Utils.Log($"ServerBackup - Creating backup for {_gameServer.Name} at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
                //Compress the source directory into a zip file in the destination directory
                string backupFilePath = System.IO.Path.Combine(_gameServer.AutoBackupDest, backupFileName);
                try
                {
                    System.IO.Compression.ZipFile.CreateFromDirectory(_gameServer.AutoBackupSource, backupFilePath);
                    Utils.Log($"ServerBackup - Backup created successfully for {_gameServer.Name} at {backupFilePath}");
                }
                catch (Exception ex)
                {
                    Utils.Log($"ServerBackup - Error creating backup for {_gameServer.Name}: {ex.Message}");
                    return false;
                }

                //Clean up old backups. Only keep the last _gamerServer.AutoBackupDaysToKeep days of backups. Note, the backup folder may contain files from other server backups.
                try
                {
                    Utils.Log($"ServerBackup - Cleaning up old backups for {_gameServer.Name}");
                    var backupFiles = System.IO.Directory.GetFiles(_gameServer.AutoBackupDest, $"{_gameServer.Name}_*.zip");
                    foreach (var file in backupFiles)
                    {
                        var fileInfo = new System.IO.FileInfo(file);
                        if (fileInfo.CreationTime < DateTime.Now.AddDays(-_gameServer.AutoBackupDaysToKeep))
                        {
                            fileInfo.Delete();
                            Utils.Log($"Deleted old backup file: {file}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utils.Log($"ServerBackup - Error cleaning up old backups for {_gameServer.Name}: {ex.Message}");
                    return false;
                }


                return true;
            }

            return false;
        }
    }
}
