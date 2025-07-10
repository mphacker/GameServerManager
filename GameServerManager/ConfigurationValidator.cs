using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GameServerManager
{
    public static class ConfigurationValidator
    {
        public static List<string> Validate(Settings settings, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(settings.SteamCMDPath) || !fileSystem.FileExists(settings.SteamCMDPath))
                errors.Add($"SteamCMDPath is invalid: {settings.SteamCMDPath}");
            if (settings.GameServers == null || settings.GameServers.Count == 0)
                errors.Add("No game servers configured.");
            else
            {
                foreach (var server in settings.GameServers)
                {
                    errors.AddRange(ValidateGameServer(server, fileSystem));
                }
            }
            return errors;
        }

        public static List<string> ValidateGameServer(GameServer gameServer, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(gameServer.Name))
                errors.Add("Game server name is missing.");
            if (string.IsNullOrWhiteSpace(gameServer.ProcessName))
                errors.Add($"Process name is missing for {gameServer.Name}.");
            if (string.IsNullOrWhiteSpace(gameServer.GamePath) || !fileSystem.DirectoryExists(gameServer.GamePath))
                errors.Add($"Game path is invalid for {gameServer.Name}: {gameServer.GamePath}");
            var exePath = Path.Combine(gameServer.GamePath, gameServer.ServerExe);
            if (string.IsNullOrWhiteSpace(gameServer.ServerExe) || !fileSystem.FileExists(exePath))
                errors.Add($"Server executable not found for {gameServer.Name} at {exePath}");
            if (gameServer.AutoBackup && (string.IsNullOrWhiteSpace(gameServer.AutoBackupSource) || string.IsNullOrWhiteSpace(gameServer.AutoBackupDest)))
                errors.Add($"Invalid backup source or destination for {gameServer.Name}");
            // Validate update/backup time formats
            if (!string.IsNullOrWhiteSpace(gameServer.AutoUpdateTime))
            {
                bool valid = false;
                try { NCrontab.CrontabSchedule.Parse(gameServer.AutoUpdateTime); valid = true; }
                catch { valid = DateTime.TryParseExact(gameServer.AutoUpdateTime, new[] { "HH:mm", "hh:mm tt", "H:mm", "h:mm tt" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _); }
                if (!valid)
                    errors.Add($"Invalid AutoUpdateTime for {gameServer.Name}: {gameServer.AutoUpdateTime}");
            }
            if (!string.IsNullOrWhiteSpace(gameServer.AutoBackupTime))
            {
                bool valid = false;
                try { NCrontab.CrontabSchedule.Parse(gameServer.AutoBackupTime); valid = true; }
                catch { valid = DateTime.TryParseExact(gameServer.AutoBackupTime, new[] { "HH:mm", "hh:mm tt", "H:mm", "h:mm tt" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _); }
                if (!valid)
                    errors.Add($"Invalid AutoBackupTime for {gameServer.Name}: {gameServer.AutoBackupTime}");
            }
            if (gameServer.AutoBackup && gameServer.AutoBackupsToKeep < 0)
                errors.Add($"AutoBackupsToKeep must be 0 or greater for {gameServer.Name}");
            return errors;
        }
    }
}
