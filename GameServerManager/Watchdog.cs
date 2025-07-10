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
            _logger.LogInformation("Watchdog started for {ServerName}", _gameServer.Name);
            _timer.AutoReset = true;
            _timer.Enabled = true;
            _timer.Start();
        }

        public void Stop()
        {
            _logger.LogInformation("Watchdog stopped for {ServerName}", _gameServer.Name);
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
            Program.AddRecentAction($"Checking process for {_gameServer.Name}");
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
                _logger.LogInformation("Update or backup in progress for {ServerName}. Skipping process check.", _gameServer.Name);
                return;
            }
            _logger.LogInformation("Checking process for {ServerName}", _gameServer.Name);
            var process = System.Diagnostics.Process.GetProcessesByName(_gameServer.ProcessName).FirstOrDefault();
            if (process == null || process.HasExited)
            {
                _logger.LogWarning("Process for {ServerName} not found. Restarting...", _gameServer.Name);
                Program.AddRecentAction($"Process for {_gameServer.Name} not found. Restarting...");
                StartGameServer();
            }
            else
            {
                _logger.LogInformation("Process for {ServerName} is running.", _gameServer.Name);
            }
        }

        private void StartGameServer()
        {
            _logger.LogInformation("Watchdog is restarting process for {ServerName}", _gameServer.Name);
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
                    _logger.LogInformation("Process for {ServerName} started.", _gameServer.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting or restarting {ServerName}", _gameServer.Name);
                _notificationManager?.NotifyError($"Error starting/restarting {_gameServer.Name}", ex.ToString());
            }
        }
    }
}
