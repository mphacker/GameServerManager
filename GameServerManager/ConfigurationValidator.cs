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
            if ((gameServer.AutoUpdate || gameServer.AutoBackup) && !DateTime.TryParse(gameServer.AutoUpdateBackupTime, out _))
                errors.Add($"Invalid update/backup time for {gameServer.Name}: {gameServer.AutoUpdateBackupTime}");
            return errors;
        }
    }
}
