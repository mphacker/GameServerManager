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
            
            try
            {
                return await Task.Run(() =>
                    _processWrapper.GetProcessesByName(processName).Any(p => !p.HasExited)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if process {processName} is running: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StopProcessAsync(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            try
            {
                var process = _processWrapper.GetProcessesByName(processName).FirstOrDefault();
                if (process == null || process.HasExited)
                    return true; // Already stopped

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
            catch (InvalidOperationException ioEx)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"[Error] Invalid operation stopping process {processName}: {ioEx.Message}");
                return false;
            }
            catch (System.ComponentModel.Win32Exception w32Ex)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"[Error] Win32 error stopping process {processName}: {w32Ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"[Error] Unexpected error stopping process {processName}: {ex.Message}");
                _logger.LogError(ex, $"Full exception details for stopping {processName}");
                return false;
            }
        }

        public async Task KillProcessAsync(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            try
            {
                var process = _processWrapper.GetProcessesByName(processName).FirstOrDefault();
                if (process == null || process.HasExited)
                    return; // Already dead

                Program.LogWithStatus(_logger, LogLevel.Warning, $"[Kill] Forcefully terminating {processName}...");
                await Task.Run(() => process.Kill());
                Program.LogWithStatus(_logger, LogLevel.Information, $"[OK] Process {processName} terminated.");
            }
            catch (InvalidOperationException ioEx)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"[Error] Process {processName} already exited: {ioEx.Message}");
            }
            catch (System.ComponentModel.Win32Exception w32Ex)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"[Error] Win32 error killing process {processName}: {w32Ex.Message}");
            }
            catch (Exception ex)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"[Error] Unexpected error killing process {processName}: {ex.Message}");
                _logger.LogError(ex, $"Full exception details for killing {processName}");
            }
        }

        public async Task StartProcessAsync(GameServer gameServer)
        {
            try
            {
                if (gameServer == null)
                {
                    _logger.LogError("GameServer parameter is null in StartProcessAsync");
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(gameServer.GamePath) || string.IsNullOrWhiteSpace(gameServer.ServerExe))
                {
                    Program.LogWithStatus(_logger, LogLevel.Error, $"GamePath or ServerExe is null or empty for {gameServer?.Name}");
                    return;
                }
                
                var exePath = System.IO.Path.Combine(gameServer.GamePath, gameServer.ServerExe);
                if (!System.IO.File.Exists(exePath))
                {
                    Program.LogWithStatus(_logger, LogLevel.Error, $"Server executable not found at {exePath} for {gameServer.Name}");
                    return;
                }
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = gameServer.ServerArgs ?? string.Empty,
                    WorkingDirectory = gameServer.GamePath,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                
                await Task.Run(() =>
                {
                    _processWrapper.StartProcess(startInfo);
                });
                
                Program.LogWithStatus(_logger, LogLevel.Information, $"Started process for {gameServer.Name}");
            }
            catch (System.ComponentModel.Win32Exception w32Ex)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"Win32 error starting {gameServer?.Name}: {w32Ex.Message}");
            }
            catch (System.IO.FileNotFoundException fnfEx)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"File not found starting {gameServer?.Name}: {fnfEx.Message}");
            }
            catch (Exception ex)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"Unexpected error starting {gameServer?.Name}: {ex.Message}");
                _logger.LogError(ex, $"Full exception details for starting {gameServer?.Name}");
            }
        }
    }
}
