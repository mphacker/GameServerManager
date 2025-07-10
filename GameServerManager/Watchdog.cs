using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GameServerManager
{
    public class Watchdog
    {
        private readonly GameServer _gameServer;
        private readonly string _steamCmdPath;
        private readonly System.Timers.Timer _timer;
        private readonly ServerUpdater _updater;
        private readonly ILogger<Watchdog> _logger;
        private readonly NotificationManager? _notificationManager;
        private bool _startupCheckDone = false;

        public Watchdog(GameServer gameServer, string steamCmdPath, ILogger<Watchdog> logger, ILogger<ServerUpdater> updaterLogger, NotificationManager? notificationManager = null)
        {
            _gameServer = gameServer;
            _steamCmdPath = steamCmdPath;
            _logger = logger;
            _timer = new System.Timers.Timer(30000); // Check every 30 seconds
            _timer.Elapsed += async (sender, e) => await CheckProcessAsync();
            _updater = new ServerUpdater(_gameServer, _steamCmdPath, updaterLogger, notificationManager);
            _notificationManager = notificationManager;
        }

        public void Start()
        {
            Program.LogWithStatus(_logger, LogLevel.Information, $"Watchdog started for {_gameServer.Name}");
            _timer.AutoReset = true;
            _timer.Enabled = true;
            _timer.Start();
        }

        public void Stop()
        {
            Program.LogWithStatus(_logger, LogLevel.Information, $"Watchdog stopped for {_gameServer.Name}");
            _timer.Stop();
        }

        private async Task CheckProcessAsync()
        {
            // On first check after startup, trigger immediate backup/update if needed
            if (!_startupCheckDone)
            {
                _startupCheckDone = true;
                if (_gameServer.AutoBackup && !_gameServer.LastBackupDate.HasValue)
                {
                    await _updater.BackupServerAsync();
                }
                if (_gameServer.AutoUpdate && !_gameServer.LastUpdateDate.HasValue)
                {
                    await _updater.UpdateServerAsync();
                }
            }
            Program.LogWithStatus(_logger, LogLevel.Information, $"Checking process for {_gameServer.Name}");
            if (_gameServer.AutoUpdate)
            {
                if (await _updater.IsTimeToUpdateServerAsync())
                {
                    await _updater.UpdateServerAsync();
                }
            }
            if (_gameServer.AutoBackup)
            {
                if (await _updater.IsTimeToBackupServerAsync())
                {
                    await _updater.BackupServerAsync();
                }
            }
            if (_updater.UpdateInProgress)
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

        private void StartGameServer()
        {
            Program.LogWithStatus(_logger, LogLevel.Information, $"Watchdog is restarting process for {_gameServer.Name}");
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = System.IO.Path.Combine(_gameServer.GamePath, _gameServer.ServerExe),
                    Arguments = _gameServer.ServerArgs,
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
            catch (Exception ex)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"Error starting or restarting {_gameServer.Name}: {ex.Message}");
                _notificationManager?.NotifyError($"Error starting/restarting {_gameServer.Name}", ex.ToString());
            }
        }
    }
}
