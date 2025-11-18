namespace GameServerManager;

using NCrontab;
using System.Globalization;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles update detection and backup operations for a game server.
/// Uses SteamCMD for periodic update checking with adaptive rate limiting.
/// </summary>
public class ServerUpdater : IAsyncDisposable
{
    private readonly GameServer _gameServer;
    private readonly string _steamCmdPath;
    private readonly ILogger<ServerUpdater> _logger;
    private readonly ServerUpdateService _updateService;
    private readonly ServerBackupService _backupService;
    private readonly ProcessManager _processManager;
    private readonly SteamCmdUpdateChecker? _updateChecker;
    private readonly Timer _timer;
    private DateTime _lastBackupDate;
    private int _updateInProgress = 0; // 0 = false, 1 = true
    private volatile bool _disposed;
    private bool _pendingBackup = false;
    private int _isCheckingForUpdate = 0; // Changed to int for Interlocked operations
    private bool _hasPerformedStartupCheck = false;

    /// <summary>
    /// Indicates if an update or backup is currently in progress.
    /// </summary>
    public bool UpdateInProgress => Interlocked.CompareExchange(ref _updateInProgress, 0, 0) == 1;

    /// <summary>
    /// Indicates if currently checking for updates (SteamCMD running in background).
    /// </summary>
    public bool IsCheckingForUpdate => Interlocked.CompareExchange(ref _isCheckingForUpdate, 0, 0) == 1;

    /// <summary>
    /// Gets the last time an update check was performed.
    /// </summary>
    public DateTime? LastUpdateCheck => _updateChecker?.LastCheckTime == DateTime.MinValue ? null : _updateChecker?.LastCheckTime;

    /// <summary>
    /// Gets the next time an update check will occur.
    /// </summary>
    public DateTime? NextUpdateCheck => _updateChecker?.NextCheckTime;

    /// <summary>
    /// Gets the current update check interval being used.
    /// </summary>
    public TimeSpan? CurrentCheckInterval => _updateChecker?.CurrentCheckInterval;

    /// <summary>
    /// Notification manager for sending notifications.
    /// </summary>
    public NotificationManager? NotificationManager { get; set; }

    public ServerUpdater(GameServer gameServer, string steamCmdPath, ILogger<ServerUpdater> logger, 
        NotificationManager? notificationManager = null, int updateCheckIntervalMinutes = 30)
    {
        _gameServer = gameServer;
        _steamCmdPath = steamCmdPath;
        _logger = logger;
        _updateService = new(_logger);
        _backupService = new(_logger);
        _processManager = new(_logger);
        
        // Initialize update checker ONLY for Steam games with AutoUpdate enabled and valid SteamAppId
        if (_gameServer.AutoUpdate && !string.IsNullOrWhiteSpace(_gameServer.SteamAppId))
        {
            var checkInterval = TimeSpan.FromMinutes(updateCheckIntervalMinutes);
            _updateChecker = new SteamCmdUpdateChecker(steamCmdPath, _gameServer.GamePath, logger, checkInterval);
            _logger.LogInformation($"Update checker initialized for {_gameServer.Name} (SteamAppId: {_gameServer.SteamAppId}) with {updateCheckIntervalMinutes} minute interval");
            
            // Read the actual local build ID if not already set
            if (!_gameServer.CurrentBuildId.HasValue)
            {
                var localBuildId = _updateChecker.GetLocalBuildId(_gameServer.SteamAppId);
                if (localBuildId.HasValue)
                {
                    _gameServer.CurrentBuildId = localBuildId.Value;
                    AppSettingsHelper.UpdateServerBuildId(_gameServer.Name, localBuildId.Value);
                    _logger.LogInformation($"Discovered local build ID for {_gameServer.Name}: {localBuildId.Value}");
                }
            }
        }
        else if (_gameServer.AutoUpdate && string.IsNullOrWhiteSpace(_gameServer.SteamAppId))
        {
            // Log warning if AutoUpdate is enabled but no SteamAppId (non-Steam app)
            _logger.LogWarning($"AutoUpdate is enabled for {_gameServer.Name} but SteamAppId is empty. This appears to be a non-Steam application. AutoUpdate will be skipped.");
        }
        
        _timer = new(async _ => await PeriodicCheckAsync(), null, Timeout.Infinite, Timeout.Infinite);
        
        // Initialize last backup from persisted value
        _lastBackupDate = _gameServer.LastBackupDate ?? DateTime.MinValue;
        NotificationManager = notificationManager;
    }

    /// <summary>
    /// Starts the periodic check timer (runs every minute).
    /// </summary>
    public void Start()
    {
        _logger.LogInformation("Periodic check process started for {ServerName}", _gameServer.Name);
        _timer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1)); // First check after 5 seconds, then every minute
    }

    /// <summary>
    /// Stops the periodic check timer.
    /// </summary>
    public void Stop()
    {
        _logger.LogInformation("Periodic check process stopped for {ServerName}", _gameServer.Name);
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Periodic check for updates and backups.
    /// </summary>
    private async Task PeriodicCheckAsync()
    {
        // Perform startup check immediately (first run only)
        if (!_hasPerformedStartupCheck && _updateChecker != null && !UpdateInProgress && !IsCheckingForUpdate)
        {
            _hasPerformedStartupCheck = true;
            Program.LogWithStatus(_logger, LogLevel.Information, $"[Startup] Performing startup build check for {_gameServer.Name}");
            await CheckForUpdatesAsync(forceCheck: true);
            return; // Exit after startup check to avoid running regular check in same cycle
        }

        // Check for updates (if enabled, not in progress, and enough time has passed)
        if (_updateChecker != null && !UpdateInProgress && !IsCheckingForUpdate && _updateChecker.CanCheckNow)
        {
            await CheckForUpdatesAsync();
        }

        // Check for scheduled backups
        if (!UpdateInProgress && !IsCheckingForUpdate)
        {
            if (_pendingBackup)
            {
                _logger.LogInformation("Pending backup for {ServerName} is being processed.", _gameServer.Name);
                _pendingBackup = false;
                await BackupServerAsync();
                return;
            }

            if (await IsTimeToBackupServerAsync())
            {
                await BackupServerAsync();
            }
        }
    }

    /// <summary>
    /// Checks for updates using SteamCMD (runs in background, doesn't stop the server).
    /// </summary>
    private async Task CheckForUpdatesAsync(bool forceCheck = false)
    {
        if (_updateChecker == null)
            return;

        // Use Interlocked to ensure only one check runs at a time
        if (Interlocked.CompareExchange(ref _isCheckingForUpdate, 1, 0) == 1)
        {
            _logger.LogDebug($"Update check already in progress for {_gameServer.Name}, skipping");
            return;
        }

        try
        {
            Program.LogWithStatus(_logger, LogLevel.Information, $"[Check] Checking for updates: {_gameServer.Name}");

            UpdateCheckResult? result;
            if (forceCheck)
            {
                result = await _updateChecker.ForceCheckNowAsync(_gameServer.SteamAppId, _gameServer.CurrentBuildId);
            }
            else
            {
                result = await _updateChecker.CheckForUpdateAsync(_gameServer.SteamAppId, _gameServer.CurrentBuildId);
            }

            if (result == null)
            {
                // Rate limited or too soon to check
                _logger.LogDebug($"Update check for {_gameServer.Name} returned null (rate limited or too soon)");
                return;
            }

            if (!result.Success)
            {
                _logger.LogWarning($"Update check failed for {_gameServer.Name}: {result.ErrorMessage}");
                return;
            }

            // Update last check time
            _gameServer.LastUpdateCheck = DateTime.Now;
            AppSettingsHelper.UpdateServerLastCheck(_gameServer.Name, DateTime.Now);

            // If we don't have a local build ID yet, this check result becomes our baseline
            if (!_gameServer.CurrentBuildId.HasValue)
            {
                _gameServer.CurrentBuildId = result.NewBuildId;
                AppSettingsHelper.UpdateServerBuildId(_gameServer.Name, result.NewBuildId);
                Program.LogWithStatus(_logger, LogLevel.Information, 
                    $"[OK] {_gameServer.Name}: Steam reports build {result.NewBuildId} (local build ID unknown - assuming up to date)");
                return;
            }

            // Check if update is available
            if (result.UpdateAvailable)
            {
                Program.LogWithStatus(_logger, LogLevel.Warning, 
                    $"[UPDATE] UPDATE AVAILABLE for {_gameServer.Name}: Local Build {result.CurrentBuildId} -> Steam Build {result.NewBuildId}");
                
                // Trigger update
                await UpdateServerAsync(result.NewBuildId);
            }
            else
            {
                Program.LogWithStatus(_logger, LogLevel.Information, 
                    $"[OK] {_gameServer.Name} is up to date (Local: {result.CurrentBuildId}, Steam: {result.NewBuildId})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error checking for updates for {_gameServer.Name}: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isCheckingForUpdate, 0);
        }
    }

    private string GetNextScheduledTime(bool enabled, string schedule, DateTime lastRun, string type)
    {
        if (!enabled)
            return $"no {type}s configured";
        var now = DateTime.Now;
        // Try CRON
        try
        {
            var cron = CrontabSchedule.Parse(schedule);
            var next = cron.GetNextOccurrence(now);
            return next.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch { }
        // Try legacy
        if (DateTime.TryParseExact(schedule, new[] { "HH:mm", "hh:mm tt", "H:mm", "h:mm tt" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var scheduledTime))
        {
            var scheduledToday = new DateTime(now.Year, now.Month, now.Day, scheduledTime.Hour, scheduledTime.Minute, 0);
            if (scheduledToday > now)
                return scheduledToday.ToString("yyyy-MM-dd HH:mm:ss");
            else
                return scheduledToday.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss");
        }
        return $"invalid {type} schedule";
    }

    public ValueTask<bool> IsTimeToBackupServerAsync() => new(CheckBackupConditions());

    private bool CheckBackupConditions()
    {
        if (!_gameServer.AutoBackup)
        {
            return false;
        }
        if (UpdateInProgress || IsCheckingForUpdate)
        {
            return false;
        }
        var now = DateTime.Now;
        var schedule = _gameServer.AutoBackupTime;
        
        // Try CRON parse
        try
        {
            var cron = CrontabSchedule.Parse(schedule);
            DateTime safeLastRun = _lastBackupDate == DateTime.MinValue ? now : _lastBackupDate;
            DateTime prev;
            try
            {
                prev = cron.GetNextOccurrence(safeLastRun.AddSeconds(-1));
            }
            catch
            {
                prev = cron.GetNextOccurrence(now.AddSeconds(-60));
            }
            if (prev > now) prev = cron.GetNextOccurrence(now.AddSeconds(-60));
            
            if (_lastBackupDate < prev && now >= prev)
            {
                _lastBackupDate = prev;
                _logger.LogInformation($"CRON backup time reached for {_gameServer.Name}.");
                return true;
            }
            return false;
        }
        catch { }
        
        // Try legacy time
        if (DateTime.TryParseExact(schedule, new[] { "HH:mm", "hh:mm tt", "H:mm", "h:mm tt" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var scheduledTime))
        {
            var scheduledToday = new DateTime(now.Year, now.Month, now.Day, scheduledTime.Hour, scheduledTime.Minute, 0);
            var scheduledPrev = scheduledToday;
            if (now < scheduledToday)
                scheduledPrev = scheduledToday.AddDays(-1);
            
            if (_lastBackupDate < scheduledPrev && now >= scheduledPrev)
            {
                _lastBackupDate = scheduledPrev;
                _logger.LogInformation($"Legacy backup time reached for {_gameServer.Name}.");
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Performs a server update (stops server, runs SteamCMD, restarts server).
    /// Performs a backup before updating if AutoBackup is enabled.
    /// </summary>
    public async Task<bool> UpdateServerAsync(int? newBuildId = null)
    {
        if (Interlocked.CompareExchange(ref _updateInProgress, 1, 0) == 1)
        {
            Program.LogWithStatus(_logger, LogLevel.Information, $"Update already in progress for {_gameServer.Name}...");
            return false;
        }
        
        Program.LogWithStatus(_logger, LogLevel.Warning, $"[Updating] UPDATING {_gameServer.Name}...");
        
        try
        {
            bool serverWasRunning = await _processManager.IsProcessRunningAsync(_gameServer.ProcessName);
            
            // Step 1: Perform pre-update backup if enabled
            if (_gameServer.AutoBackup)
            {
                Program.LogWithStatus(_logger, LogLevel.Information, $"[Backup] Creating pre-update backup for {_gameServer.Name}...");
                
                // Stop server for backup if it's running and BackupWithoutShutdown is false
                if (serverWasRunning && !_gameServer.BackupWithoutShutdown)
                {
                    Program.LogWithStatus(_logger, LogLevel.Information, $"[Stop] Stopping {_gameServer.Name} for pre-update backup...");
                    if (!await _processManager.StopProcessAsync(_gameServer.ProcessName))
                    {
                        Program.LogWithStatus(_logger, LogLevel.Warning, $"[Warn] Could not gracefully stop {_gameServer.Name}, forcing termination...");
                        await _processManager.KillProcessAsync(_gameServer.ProcessName);
                    }
                }
                
                var backupResult = await _backupService.BackupAsync(_gameServer);
                if (!backupResult)
                {
                    Program.LogWithStatus(_logger, LogLevel.Error, $"[Error] Pre-update backup failed for {_gameServer.Name}. Update aborted.");
                    NotificationManager?.NotifyError($"Pre-update backup failed for {_gameServer.Name}", "Update aborted due to backup failure.");
                    
                    // Restart server if we stopped it
                    if (serverWasRunning && !_gameServer.BackupWithoutShutdown)
                    {
                        await _processManager.StartProcessAsync(_gameServer);
                    }
                    return false;
                }
                
                _lastBackupDate = DateTime.Now;
                _gameServer.LastBackupDate = _lastBackupDate;
                AppSettingsHelper.UpdateServerLastDates(_gameServer.Name, null, _lastBackupDate);
                Program.LogWithStatus(_logger, LogLevel.Information, $"[OK] Pre-update backup completed for {_gameServer.Name}");
            }
            
            // Step 2: Ensure server is stopped for update
            if (serverWasRunning && _gameServer.AutoBackup && _gameServer.BackupWithoutShutdown)
            {
                // Server is still running because we backed up without shutdown
                Program.LogWithStatus(_logger, LogLevel.Information, $"[Stop] Stopping {_gameServer.Name} for update...");
                if (!await _processManager.StopProcessAsync(_gameServer.ProcessName))
                {
                    Program.LogWithStatus(_logger, LogLevel.Warning, $"[Warn] Could not gracefully stop {_gameServer.Name}, forcing termination...");
                    await _processManager.KillProcessAsync(_gameServer.ProcessName);
                }
            }
            else if (serverWasRunning && !_gameServer.AutoBackup)
            {
                // No backup was done, but server needs to be stopped
                Program.LogWithStatus(_logger, LogLevel.Information, $"[Stop] Stopping {_gameServer.Name} for update...");
                if (!await _processManager.StopProcessAsync(_gameServer.ProcessName))
                {
                    Program.LogWithStatus(_logger, LogLevel.Warning, $"[Warn] Could not gracefully stop {_gameServer.Name}, forcing termination...");
                    await _processManager.KillProcessAsync(_gameServer.ProcessName);
                }
            }
            
            // Step 3: Perform update via SteamCMD
            Program.LogWithStatus(_logger, LogLevel.Information, $"[Download] Downloading update for {_gameServer.Name}...");
            var updateResult = await _updateService.UpdateAsync(_gameServer, _steamCmdPath);
            if (!updateResult)
            {
                Program.LogWithStatus(_logger, LogLevel.Error, $"[Error] Update failed for {_gameServer.Name}");
                NotificationManager?.NotifyError($"Update failed for {_gameServer.Name}", $"SteamCMD update failed for {_gameServer.Name}.");
                
                // Restart server if it was running before
                if (serverWasRunning)
                {
                    Program.LogWithStatus(_logger, LogLevel.Information, $"[Start] Restarting {_gameServer.Name} after failed update...");
                    await _processManager.StartProcessAsync(_gameServer);
                }
                return false;
            }
            
            // Step 4: Update build ID and timestamp
            if (newBuildId.HasValue)
            {
                _gameServer.CurrentBuildId = newBuildId.Value;
                AppSettingsHelper.UpdateServerBuildId(_gameServer.Name, newBuildId.Value);
                Program.LogWithStatus(_logger, LogLevel.Information, $"[Update] Updated build ID for {_gameServer.Name} to {newBuildId.Value}");
            }
            
            _gameServer.LastUpdateDate = DateTime.Now;
            AppSettingsHelper.UpdateServerLastDates(_gameServer.Name, _gameServer.LastUpdateDate, null);
            
            // Step 5: Restart server if it was running before update
            if (serverWasRunning)
            {
                Program.LogWithStatus(_logger, LogLevel.Information, $"[Start] Restarting {_gameServer.Name}...");
                await _processManager.StartProcessAsync(_gameServer);
                Program.LogWithStatus(_logger, LogLevel.Information, $"[Wait] Waiting 30 seconds for {_gameServer.Name} to start...");
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            
            Program.LogWithStatus(_logger, LogLevel.Information, $"[OK] Update completed for {_gameServer.Name}");
            return true;
        }
        catch (Exception ex)
        {
            Program.LogWithStatus(_logger, LogLevel.Error, $"[Error] Update error for {_gameServer.Name}: {ex.Message}");
            NotificationManager?.NotifyError($"Exception updating {_gameServer.Name}", ex.ToString());
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
            Program.LogWithStatus(_logger, LogLevel.Information, $"Backup already in progress for {_gameServer.Name}...");
            return false;
        }
        Program.LogWithStatus(_logger, LogLevel.Information, $"[Backup] Backing up {_gameServer.Name}...");
        try
        {
            bool needToRestartServer = false;
            // Stop process if running, unless BackupWithoutShutdown is true
            if (!_gameServer.BackupWithoutShutdown && await _processManager.IsProcessRunningAsync(_gameServer.ProcessName))
            {
                needToRestartServer = true;
                Program.LogWithStatus(_logger, LogLevel.Information, $"Process for {_gameServer.Name} is running. Stopping server.");
                await _processManager.StopProcessAsync(_gameServer.ProcessName);
            }
            // Backup
            if (_gameServer.AutoBackup)
            {
                var backupResult = await _backupService.BackupAsync(_gameServer);
                if (!backupResult)
                {
                    Program.LogWithStatus(_logger, LogLevel.Error, $"Backup failed for {_gameServer.Name}");
                    NotificationManager?.NotifyError($"Backup failed for {_gameServer.Name}", $"Backup failed for {_gameServer.Name}.");
                    return false;
                }
                // Persist last backup date
                _lastBackupDate = DateTime.Now;
                _gameServer.LastBackupDate = _lastBackupDate;
                AppSettingsHelper.UpdateServerLastDates(_gameServer.Name, null, _lastBackupDate);
            }
            // Restart
            if (needToRestartServer)
            {
                Program.LogWithStatus(_logger, LogLevel.Information, $"Restarting process for {_gameServer.Name}");
                await _processManager.StartProcessAsync(_gameServer);
                Program.LogWithStatus(_logger, LogLevel.Information, $"Waiting 30 seconds for {_gameServer.Name} to start.");
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            Program.LogWithStatus(_logger, LogLevel.Information, $"[OK] Backup completed for {_gameServer.Name}");
            return true;
        }
        catch (Exception ex)
        {
            Program.LogWithStatus(_logger, LogLevel.Error, $"Backup error for {_gameServer.Name}: {ex.Message}");
            NotificationManager?.NotifyError($"Exception backing up {_gameServer.Name}", ex.ToString());
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _updateInProgress, 0);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        
        _logger.LogInformation($"Disposing ServerUpdater for {_gameServer.Name}");
        
        // Stop the timer
        await _timer.DisposeAsync();
        
        // Dispose update checker (will kill any running SteamCMD process)
        _updateChecker?.Dispose();
        
        _disposed = true;
        await Task.CompletedTask;
    }
}
