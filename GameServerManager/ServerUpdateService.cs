using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GameServerManager
{
    public class ServerUpdateService
    {
        private readonly ILogger _logger;
        public ServerUpdateService(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<bool> UpdateAsync(GameServer gameServer, string steamCmdPath)
        {
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
            _logger.LogInformation("Running SteamCMD at {Path} with arguments: {Args}", steamCmdPath, steamCmdArgs);
            try
            {
                using var updateProcess = new Process { StartInfo = startInfo };
                updateProcess.OutputDataReceived += (sender, e) => { if (e.Data != null) _logger.LogInformation("SteamCMD Output: {Data}", e.Data); };
                updateProcess.ErrorDataReceived += (sender, e) => { if (e.Data != null) _logger.LogError("SteamCMD Error: {Data}", e.Data); };
                updateProcess.Start();
                updateProcess.BeginOutputReadLine();
                updateProcess.BeginErrorReadLine();
                await updateProcess.WaitForExitAsync();
                if (updateProcess.ExitCode == 0)
                {
                    _logger.LogInformation("Update completed successfully for {ServerName}.", gameServer.Name);
                    return true;
                }
                else
                {
                    _logger.LogError("Update failed for {ServerName}. Exit code: {ExitCode}", gameServer.Name, updateProcess.ExitCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception running SteamCMD for {ServerName}", gameServer.Name);
                return false;
            }
        }
    }
}
