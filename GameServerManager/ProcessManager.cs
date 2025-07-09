using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GameServerManager
{
    public class ProcessManager
    {
        private readonly ILogger _logger;
        public ProcessManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> IsProcessRunningAsync(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;
            return await Task.Run(() =>
                Process.GetProcessesByName(processName).Any(p => !p.HasExited)
            );
        }

        public async Task StopProcessAsync(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;
            var process = Process.GetProcessesByName(processName).FirstOrDefault();
            if (process != null && !process.HasExited)
            {
                try
                {
                    process.CloseMainWindow();
                    await Task.Run(() => process.WaitForExit(10000));
                    if (!process.HasExited)
                    {
                        _logger.LogWarning("Process {ProcessName} did not exit in time. Killing process.", processName);
                        process.Kill();
                    }
                    _logger.LogInformation("Process {ProcessName} stopped.", processName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping process {ProcessName}", processName);
                }
            }
        }

        public async Task StartProcessAsync(GameServer gameServer)
        {
            if (gameServer == null)
                throw new ArgumentNullException(nameof(gameServer));
            if (string.IsNullOrWhiteSpace(gameServer.GamePath) || string.IsNullOrWhiteSpace(gameServer.ServerExe))
            {
                _logger.LogError("GamePath or ServerExe is null or empty for {ServerName}", gameServer?.Name);
                return;
            }
            var startInfo = new ProcessStartInfo
            {
                FileName = System.IO.Path.Combine(gameServer.GamePath, gameServer.ServerExe),
                Arguments = gameServer.ServerArgs ?? string.Empty,
                UseShellExecute = true,
                CreateNoWindow = false
            };
            await Task.Run(() =>
            {
                using var process = new Process { StartInfo = startInfo };
                process.Start();
            });
        }
    }
}
