using NCrontab;
using System.Globalization;
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
        private DateTime _lastBackupDate = DateTime.MinValue;
        private DateTime _lastUpdateDate = DateTime.MinValue;
        private int _updateInProgress = 0; // 0 = false, 1 = true
        private bool _disposed;
        private bool _pendingUpdate = false;
        private bool _pendingBackup = false;

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
            // Only allow one operation at a time
            if (UpdateInProgress)
            {
                _logger.LogInformation("Update or backup already in progress for {ServerName}. Skipping scheduled operations.", _gameServer.Name);
                // If either is due, set pending flags
                if (await IsTimeToUpdateServerAsync()) _pendingUpdate = true;
                if (await IsTimeToBackupServerAsync()) _pendingBackup = true;
                return;
            }

            // If a pending update or backup exists, run it first
            if (_pendingUpdate)
            {
                _logger.LogInformation("Pending update for {ServerName} is being processed.", _gameServer.Name);
                _pendingUpdate = false;
                await UpdateServerAsync();
                // After update, check if backup is now due and not running
                if (!_pendingBackup && await IsTimeToBackupServerAsync())
                {
                    _pendingBackup = true;
                }
                if (_pendingBackup)
                {
                    await AutoUpdateCheckAsync();
                }
                return;
            }
            if (_pendingBackup)
            {
                _logger.LogInformation("Pending backup for {ServerName} is being processed.", _gameServer.Name);
                _pendingBackup = false;
                await BackupServerAsync();
                // After backup, check if update is now due and not running
                if (!_pendingUpdate && await IsTimeToUpdateServerAsync())
                {
                    _pendingUpdate = true;
                }
                if (_pendingUpdate)
                {
                    await AutoUpdateCheckAsync();
                }
                return;
            }

            // Prioritize update over backup if both are scheduled at the same time
            if (await IsTimeToUpdateServerAsync())
            {
                await UpdateServerAsync();
                // After update, check if backup is now due and not running
                if (await IsTimeToBackupServerAsync())
                {
                    _pendingBackup = true;
                    await AutoUpdateCheckAsync();
                }
                return;
            }
            if (await IsTimeToBackupServerAsync())
            {
                await BackupServerAsync();
                // After backup, check if update is now due and not running
                if (await IsTimeToUpdateServerAsync())
                {
                    _pendingUpdate = true;
                    await AutoUpdateCheckAsync();
                }
                return;
            }
        }

        public async Task<bool> IsTimeToUpdateServerAsync()
        {
            return await Task.FromResult(CheckUpdateConditions(_gameServer.AutoUpdate, _gameServer.AutoUpdateTime, ref _lastUpdateDate, "update"));
        }

        public async Task<bool> IsTimeToBackupServerAsync()
        {
            return await Task.FromResult(CheckUpdateConditions(_gameServer.AutoBackup, _gameServer.AutoBackupTime, ref _lastBackupDate, "backup"));
        }

        private bool CheckUpdateConditions(bool enabled, string schedule, ref DateTime lastRun, string type)
        {
            if (!enabled)
                return false;
            if (UpdateInProgress)
                return false;
            var now = DateTime.Now;
            bool cronValid = false;
            bool legacyValid = false;
            bool shouldRun = false;

            // Try CRON parse only
            NCrontab.CrontabSchedule? cron = null;
            try
            {
                cron = NCrontab.CrontabSchedule.Parse(schedule);
                cronValid = true;
            }
            catch
            {
                cronValid = false;
            }
            // If CRON is valid, do the scheduling logic (outside try/catch)
            if (cronValid && cron != null)
            {
                // Prevent DateTime.MinValue subtraction
                DateTime safeLastRun = lastRun == DateTime.MinValue ? now : lastRun;
                DateTime prev;
                try
                {
                    prev = cron.GetNextOccurrence(safeLastRun.AddSeconds(-1));
                }
                catch
                {
                    prev = cron.GetNextOccurrence(now.AddSeconds(-60));
                }
                if (prev > now) prev = cron.GetNextOccurrence(now.AddSeconds(-60)); // fallback
                if (lastRun < prev && now >= prev)
                {
                    lastRun = prev;
                    _logger.LogInformation($"CRON {type} time reached for {{ServerName}}.", _gameServer.Name);
                    shouldRun = true;
                }
            }

            // Try legacy time
            if (DateTime.TryParseExact(schedule, new[] { "HH:mm", "hh:mm tt", "H:mm", "h:mm tt" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var scheduledTime))
            {
                legacyValid = true;
                var scheduledToday = new DateTime(now.Year, now.Month, now.Day, scheduledTime.Hour, scheduledTime.Minute, 0);
                var scheduledPrev = scheduledToday;
                if (now < scheduledToday)
                    scheduledPrev = scheduledToday.AddDays(-1);
                if (lastRun < scheduledPrev && now >= scheduledPrev)
                {
                    lastRun = scheduledPrev;
                    _logger.LogInformation($"Legacy {type} time reached for {{ServerName}}.", _gameServer.Name);
                    shouldRun = true;
                }
            }

            // Only log a warning if neither CRON nor legacy time format is valid
            if (!cronValid && !legacyValid && !string.IsNullOrWhiteSpace(schedule))
            {
                _logger.LogWarning($"Invalid {type} time format for {{ServerName}}: {{Schedule}}", _gameServer.Name, schedule);
            }
            return shouldRun;
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
                bool needToRestartServer = false;
                _logger.LogInformation("Updating {ServerName}...", _gameServer.Name);
                // Stop process if running
                if (await _processManager.IsProcessRunningAsync(_gameServer.ProcessName))
                {
                    needToRestartServer = true;
                    _logger.LogInformation("Process for {ServerName} is running. Stopping server.", _gameServer.Name);
                    await _processManager.StopProcessAsync(_gameServer.ProcessName);
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

        public async Task<bool> BackupServerAsync()
        {
            if (Interlocked.CompareExchange(ref _updateInProgress, 1, 0) == 1)
            {
                _logger.LogInformation("Backup already in progress for {ServerName}...", _gameServer.Name);
                return false;
            }
            try
            {
                bool needToRestartServer = false;
                _logger.LogInformation("Backing up {ServerName}...", _gameServer.Name);
                // Stop process if running, unless BackupWithoutShutdown is true
                if (!_gameServer.BackupWithoutShutdown && await _processManager.IsProcessRunningAsync(_gameServer.ProcessName))
                {
                    needToRestartServer = true;
                    _logger.LogInformation("Process for {ServerName} is running. Stopping server.", _gameServer.Name);
                    await _processManager.StopProcessAsync(_gameServer.ProcessName);
                }
                // Backup
                if (_gameServer.AutoBackup)
                {
                    var backupResult = await _backupService.BackupAsync(_gameServer);
                    if (!backupResult)
                    {
                        _logger.LogError("Backup failed for {ServerName}.", _gameServer.Name);
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
                _logger.LogError(ex, "Exception occurred while backing up {ServerName}", _gameServer.Name);
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
