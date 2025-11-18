using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GameServerManager
{
    public interface IProcessProxy
    {
        bool HasExited { get; }
        void CloseMainWindow();
        void WaitForExit(int milliseconds);
        void Kill();
    }

    public class ProcessProxy : IProcessProxy
    {
        private readonly Process _process;
        public ProcessProxy(Process process) => _process = process;
        public bool HasExited => _process.HasExited;
        public void CloseMainWindow() => _process.CloseMainWindow();
        public void WaitForExit(int milliseconds) => _process.WaitForExit(milliseconds);
        public void Kill() => _process.Kill();
    }

    public interface IProcessWrapper
    {
        IProcessProxy[] GetProcessesByName(string processName);
        IProcessProxy StartProcess(ProcessStartInfo startInfo);
    }

    public class ProcessWrapper : IProcessWrapper
    {
        public IProcessProxy[] GetProcessesByName(string processName) =>
            Process.GetProcessesByName(processName).Select(p => new ProcessProxy(p)).ToArray();
        public IProcessProxy StartProcess(ProcessStartInfo startInfo)
        {
            var process = new Process { StartInfo = startInfo };
            process.Start();
            return new ProcessProxy(process);
        }
    }

    public interface IProcessManager
    {
        Task<bool> IsProcessRunningAsync(string processName);
        Task<bool> StopProcessAsync(string processName);
        Task KillProcessAsync(string processName);
        Task StartProcessAsync(GameServer gameServer);
    }

    public class ProcessManager : IProcessManager
    {
        private readonly ILogger _logger;
        private readonly IProcessWrapper _processWrapper;
        public ProcessManager(ILogger logger, IProcessWrapper? processWrapper = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _processWrapper = processWrapper ?? new ProcessWrapper();
        }

        public async Task<bool> IsProcessRunningAsync(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;
            return await Task.Run(() =>
                _processWrapper.GetProcessesByName(processName).Any(p => !p.HasExited)
            );
        }

        public async Task<bool> StopProcessAsync(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            var process = _processWrapper.GetProcessesByName(processName).FirstOrDefault();
            if (process == null || process.HasExited)
                return true; // Already stopped

            try
            {
                _logger.LogInformation($"Attempting graceful shutdown of {processName}...");
                process.CloseMainWindow();

                // Wait up to 30 seconds for graceful shutdown
                var exited = await Task.Run(() =>
                {
                    process.WaitForExit(30000);
                    return process.HasExited;
                });

                if (exited)
                {
                    Program.LogWithStatus(_logger, LogLevel.Information, $"[OK] Process {processName} stopped gracefully.");
                    return true;
                }
                else
                {
                    Program.LogWithStatus(_logger, LogLevel.Warning, $"[Warn] Process {processName} did not respond to graceful shutdown after 30 seconds.");
                    return false; // Could not stop gracefully
                }
            }
            catch (Exception ex)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"[Error] Error stopping process {processName}: {ex.Message}");
                return false;
            }
        }

        public async Task KillProcessAsync(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            var process = _processWrapper.GetProcessesByName(processName).FirstOrDefault();
            if (process == null || process.HasExited)
                return; // Already dead

            try
            {
                Program.LogWithStatus(_logger, LogLevel.Warning, $"[Kill] Forcefully terminating {processName}...");
                await Task.Run(() => process.Kill());
                Program.LogWithStatus(_logger, LogLevel.Information, $"[OK] Process {processName} terminated.");
            }
            catch (Exception ex)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"[Error] Error killing process {processName}: {ex.Message}");
            }
        }

        public async Task StartProcessAsync(GameServer gameServer)
        {
            if (gameServer == null)
                throw new ArgumentNullException(nameof(gameServer));
            if (string.IsNullOrWhiteSpace(gameServer.GamePath) || string.IsNullOrWhiteSpace(gameServer.ServerExe))
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"GamePath or ServerExe is null or empty for {gameServer?.Name}");
                return;
            }
            var startInfo = new ProcessStartInfo
            {
                FileName = System.IO.Path.Combine(gameServer.GamePath, gameServer.ServerExe),
                Arguments = gameServer.ServerArgs ?? string.Empty,
                WorkingDirectory = gameServer.GamePath,
                UseShellExecute = true,
                CreateNoWindow = false
            };
            await Task.Run(() =>
            {
                _processWrapper.StartProcess(startInfo);
            });
        }
    }
}
