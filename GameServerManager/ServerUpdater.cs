using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GameServerManager
{
    public class ServerUpdater : IAsyncDisposable
    {
        private readonly GameServer _gameServer;
        private readonly string _steamCmdPath;
        private readonly ILogger<ServerUpdater> _logger;
        private readonly ServerUpdateService _updateService;
        private readonly ServerBackupService _backupService;
        private readonly ProcessManager _processManager;
        private readonly Timer _timer;
        private DateTime _lastUpdateDate = DateTime.MinValue;
        private int _updateInProgress = 0; // 0 = false, 1 = true
        private bool _disposed;

        public bool UpdateInProgress => Interlocked.CompareExchange(ref _updateInProgress, 0, 0) == 1;

        public ServerUpdater(GameServer gameServer, string steamCmdPath, ILogger<ServerUpdater> logger)
        {
            _gameServer = gameServer;
            _steamCmdPath = steamCmdPath;
            _logger = logger;
            _updateService = new ServerUpdateService(_logger);
            _backupService = new ServerBackupService(_logger);
            _processManager = new ProcessManager(_logger);
            _timer = new Timer(async _ => await AutoUpdateCheckAsync(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            _logger.LogInformation("Auto-update check process started for {ServerName}", _gameServer.Name);
            _timer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        public void Stop()
        {
            _logger.LogInformation("Auto-update check process stopped for {ServerName}", _gameServer.Name);
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async Task AutoUpdateCheckAsync()
        {
            if (await IsTimeToUpdateServerAsync())
            {
                await UpdateServerAsync();
            }
        }

        public async Task<bool> IsTimeToUpdateServerAsync()
        {
            return await Task.FromResult(CheckUpdateConditions());
        }

        private bool CheckUpdateConditions()
        {
            if (_gameServer.AutoUpdate)
            {
                if (UpdateInProgress)
                    return false;

                var currentTime = DateTime.Now;
                if (_lastUpdateDate.Date == currentTime.Date)
                {
                    _logger.LogInformation("Already updated {ServerName} today.", _gameServer.Name);
                    return false;
                }

                if (!DateTime.TryParseExact(_gameServer.AutoUpdateBackupTime, "hh:mm tt", null, System.Globalization.DateTimeStyles.None, out var autoUpdateTime))
                {
                    _logger.LogWarning("Invalid AutoUpdateBackupTime for {ServerName}", _gameServer.Name);
                    return false;
                }
                var autoUpdateTimeOfDay = autoUpdateTime.TimeOfDay;
                var currentTimeOfDay = currentTime.TimeOfDay;
                var autoUpdateEndTime = autoUpdateTimeOfDay.Add(TimeSpan.FromMinutes(5));
                if (currentTimeOfDay >= autoUpdateTimeOfDay && currentTimeOfDay <= autoUpdateEndTime)
                {
                    _logger.LogInformation("Auto-update time reached for {ServerName}.", _gameServer.Name);
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> UpdateServerAsync()
        {
            if (Interlocked.CompareExchange(ref _updateInProgress, 1, 0) == 1)
            {
                _logger.LogInformation("Update already in progress for {ServerName}...", _gameServer.Name);
                return false;
            }
            try
            {
                _lastUpdateDate = DateTime.Now;
                bool needToRestartServer = false;
                _logger.LogInformation("Updating {ServerName}...", _gameServer.Name);

                // Stop process if running
                if (await _processManager.IsProcessRunningAsync(_gameServer.ProcessName))
                {
                    needToRestartServer = true;
                    _logger.LogInformation("Process for {ServerName} is running. Stopping server.", _gameServer.Name);
                    await _processManager.StopProcessAsync(_gameServer.ProcessName);
                }

                // Backup
                if (_gameServer.AutoBackup)
                {
                    var backupResult = await _backupService.BackupAsync(_gameServer);
                    if (backupResult)
                        _logger.LogInformation("Backup completed for {ServerName}.", _gameServer.Name);
                    else
                        _logger.LogError("Backup failed for {ServerName}. Proceeding with update.", _gameServer.Name);
                }

                // Update
                if (_gameServer.AutoUpdate)
                {
                    var updateResult = await _updateService.UpdateAsync(_gameServer, _steamCmdPath);
                    if (!updateResult)
                    {
                        _logger.LogError("Update failed for {ServerName}.", _gameServer.Name);
                        return false;
                    }
                }

                // Restart
                if (needToRestartServer)
                {
                    _logger.LogInformation("Restarting process for {ServerName}", _gameServer.Name);
                    await _processManager.StartProcessAsync(_gameServer);
                    _logger.LogInformation("Waiting 30 seconds for {ServerName} to start.", _gameServer.Name);
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while updating {ServerName}", _gameServer.Name);
                return false;
            }
            finally
            {
                Interlocked.Exchange(ref _updateInProgress, 0);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _timer.Dispose();
            _disposed = true;
            await Task.CompletedTask;
        }
    }
}
