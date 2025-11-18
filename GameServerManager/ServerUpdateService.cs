using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GameServerManager
{
    public class ServerUpdateService
    {
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        public ServerUpdateService(ILogger logger, IFileSystem? fileSystem = null)
        {
            _logger = logger;
            _fileSystem = fileSystem ?? new FileSystem();
        }

        public async Task<bool> UpdateAsync(GameServer gameServer, string steamCmdPath)
        {
            // Skip update if SteamAppId is empty (non-Steam game)
            if (string.IsNullOrWhiteSpace(gameServer.SteamAppId))
            {
                Program.LogWithStatus(_logger, LogLevel.Warning, $"Skipping update for {gameServer.Name} - no SteamAppId configured (non-Steam game).");
                return true;
            }
            
            if (!_fileSystem.FileExists(steamCmdPath))
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"SteamCMD path does not exist: {steamCmdPath}");
                return false;
            }
            var steamCmdArgs = $"+login anonymous +force_install_dir \"{gameServer.GamePath}\" +app_update {gameServer.SteamAppId} validate +quit";
            var startInfo = new ProcessStartInfo
            {
                FileName = steamCmdPath,
                Arguments = steamCmdArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            Program.LogWithStatus(_logger, LogLevel.Information, $"Running SteamCMD at {steamCmdPath} with arguments: {steamCmdArgs}");
            try
            {
                using var updateProcess = new Process { StartInfo = startInfo };
                updateProcess.OutputDataReceived += (sender, e) => { if (e.Data != null) Program.LogWithStatus(_logger, LogLevel.Information, $"SteamCMD Output: {e.Data}"); };
                updateProcess.ErrorDataReceived += (sender, e) => { if (e.Data != null) Program.LogWithStatus(_logger, LogLevel.Error, $"SteamCMD Error: {e.Data}"); };
                updateProcess.Start();
                updateProcess.BeginOutputReadLine();
                updateProcess.BeginErrorReadLine();
                await updateProcess.WaitForExitAsync();
                if (updateProcess.ExitCode == 0)
                {
                    Program.LogWithStatus(_logger, LogLevel.Information, $"Update completed successfully for {gameServer.Name}.");
                    return true;
                }
                else
                {
                    Program.LogWithStatus(_logger, LogLevel.Error, $"Update failed for {gameServer.Name}. Exit code: {updateProcess.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"Exception running SteamCMD for {gameServer.Name}: {ex.Message}");
                return false;
            }
        }
    }
}
