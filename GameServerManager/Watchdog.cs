using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GameServerManager
{
    public class Watchdog : IAsyncDisposable
    {
        private readonly GameServer _gameServer;
        private readonly string _steamCmdPath;
        private readonly System.Timers.Timer _timer;
        private readonly ServerUpdater _updater;
        private readonly ILogger<Watchdog> _logger;
        private readonly NotificationManager? _notificationManager;
        private bool _startupCheckDone = false;
        private bool _disposed = false;

        /// <summary>
        /// Gets the ServerUpdater instance for this watchdog.
        /// </summary>
        public ServerUpdater Updater => _updater;

        public Watchdog(GameServer gameServer, string steamCmdPath, ILogger<Watchdog> logger, 
            ILogger<ServerUpdater> updaterLogger, NotificationManager? notificationManager = null,
            int updateCheckIntervalMinutes = 30)
        {
            _gameServer = gameServer;
            _steamCmdPath = steamCmdPath;
            _logger = logger;
            _timer = new System.Timers.Timer(30000); // Check every 30 seconds
            _timer.Elapsed += async (sender, e) => await CheckProcessAsync();
            _updater = new ServerUpdater(_gameServer, _steamCmdPath, updaterLogger, notificationManager, 
                updateCheckIntervalMinutes);
            _notificationManager = notificationManager;
        }

        public void Start()
        {
            Program.LogWithStatus(_logger, LogLevel.Information, $"Watchdog started for {_gameServer.Name}");
            _updater.Start();
            _timer.AutoReset = true;
            _timer.Enabled = true;
            _timer.Start();
        }

        public void Stop()
        {
            Program.LogWithStatus(_logger, LogLevel.Information, $"Watchdog stopped for {_gameServer.Name}");
            _timer.Stop();
            _updater.Stop();
        }

        private async Task CheckProcessAsync()
        {
            try
            {
                // On first check after startup, trigger immediate backup if needed
                if (!_startupCheckDone)
                {
                    _startupCheckDone = true;
                    if (_gameServer.AutoBackup && !_gameServer.LastBackupDate.HasValue)
                    {
                        await _updater.BackupServerAsync();
                    }
                }
                
                Program.LogWithStatus(_logger, LogLevel.Information, $"Checking process for {_gameServer.Name}");
                
                // Check for backups
                if (_gameServer.AutoBackup)
                {
                    if (await _updater.IsTimeToBackupServerAsync())
                    {
                        await _updater.BackupServerAsync();
                    }
                }
                
                if (_updater.UpdateInProgress || _updater.IsCheckingForUpdate)
                {
                    Program.LogWithStatus(_logger, LogLevel.Information, $"Update or backup in progress for {_gameServer.Name}. Skipping process check.");
                    return;
                }
                
                var process = System.Diagnostics.Process.GetProcessesByName(_gameServer.ProcessName).FirstOrDefault();
                if (process == null || process.HasExited)
                {
                    Program.LogWithStatus(_logger, LogLevel.Warning, $"Process for {_gameServer.Name} not found. Restarting...");
                    StartGameServer();
                }
                else
                {
                    Program.LogWithStatus(_logger, LogLevel.Information, $"Process for {_gameServer.Name} is running.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unhandled exception in process check for {_gameServer.Name}: {ex.Message}");
            }
        }

        private void StartGameServer()
        {
            try
            {
                Program.LogWithStatus(_logger, LogLevel.Information, $"Watchdog is restarting process for {_gameServer.Name}");
                
                if (string.IsNullOrWhiteSpace(_gameServer.GamePath))
                {
                    Program.LogWithStatus(_logger, LogLevel.Error, $"GamePath is null or empty for {_gameServer.Name}. Cannot start server.");
                    _notificationManager?.NotifyError($"Cannot start {_gameServer.Name}", "GamePath is not configured.");
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(_gameServer.ServerExe))
                {
                    Program.LogWithStatus(_logger, LogLevel.Error, $"ServerExe is null or empty for {_gameServer.Name}. Cannot start server.");
                    _notificationManager?.NotifyError($"Cannot start {_gameServer.Name}", "ServerExe is not configured.");
                    return;
                }
                
                var exePath = System.IO.Path.Combine(_gameServer.GamePath, _gameServer.ServerExe);
                if (!System.IO.File.Exists(exePath))
                {
                    Program.LogWithStatus(_logger, LogLevel.Error, $"Server executable not found at {exePath}");
                    _notificationManager?.NotifyError($"Cannot start {_gameServer.Name}", $"Executable not found: {exePath}");
                    return;
                }
                
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = _gameServer.ServerArgs ?? string.Empty,
                    WorkingDirectory = _gameServer.GamePath,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    Program.LogWithStatus(_logger, LogLevel.Information, $"Process for {_gameServer.Name} started.");
                }
            }
            catch (System.ComponentModel.Win32Exception w32Ex)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"Win32 error starting {_gameServer.Name}: {w32Ex.Message}");
                _notificationManager?.NotifyError($"Error starting {_gameServer.Name}", $"Win32 error: {w32Ex.Message}");
            }
            catch (System.IO.IOException ioEx)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"IO error starting {_gameServer.Name}: {ioEx.Message}");
                _notificationManager?.NotifyError($"Error starting {_gameServer.Name}", $"IO error: {ioEx.Message}");
            }
            catch (Exception ex)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"Unexpected error starting {_gameServer.Name}: {ex.Message}");
                _logger.LogError(ex, $"Full exception details for {_gameServer.Name}");
                _notificationManager?.NotifyError($"Error starting {_gameServer.Name}", ex.ToString());
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            
            _logger.LogInformation($"Disposing Watchdog for {_gameServer.Name}");
            
            Stop();
            _timer?.Dispose();
            await _updater.DisposeAsync();
            
            _disposed = true;
        }
    }
}
