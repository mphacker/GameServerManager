using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using ValveKeyValue;

namespace GameServerManager
{
    /// <summary>
    /// Checks for Steam game updates using SteamCMD app_info_print.
    /// </summary>
    public class SteamCmdUpdateChecker : IDisposable
    {
        private readonly string _steamCmdPath;
        private readonly ILogger _logger;
        private readonly string _gamePath;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private TimeSpan _currentCheckInterval;
        private readonly TimeSpan _minCheckInterval = TimeSpan.FromMinutes(15);
        private readonly TimeSpan _maxCheckInterval = TimeSpan.FromHours(4);
        private int _consecutiveFailures = 0;
        private const int MAX_FAILURES_BEFORE_BACKOFF = 3;
        private Process? _currentProcess;
        private readonly object _processLock = new object();
        private bool _disposed = false;

        public SteamCmdUpdateChecker(string steamCmdPath, string gamePath, ILogger logger, TimeSpan initialCheckInterval)
        {
            _steamCmdPath = steamCmdPath;
            _gamePath = gamePath;
            _logger = logger;
            _currentCheckInterval = initialCheckInterval;
            
            if (_currentCheckInterval < _minCheckInterval)
            {
                _logger.LogWarning($"Check interval {_currentCheckInterval.TotalMinutes} min is too low. Using minimum: {_minCheckInterval.TotalMinutes} min");
                _currentCheckInterval = _minCheckInterval;
            }
            else if (_currentCheckInterval > _maxCheckInterval)
            {
                _logger.LogWarning($"Check interval {_currentCheckInterval.TotalMinutes} min is too high. Using maximum: {_maxCheckInterval.TotalMinutes} min");
                _currentCheckInterval = _maxCheckInterval;
            }
        }

        public TimeSpan CurrentCheckInterval => _currentCheckInterval;
        public DateTime LastCheckTime => _lastCheckTime;
        public DateTime NextCheckTime => _lastCheckTime == DateTime.MinValue ? DateTime.Now : _lastCheckTime + _currentCheckInterval;
        public bool CanCheckNow => DateTime.Now >= NextCheckTime;

        public int? GetLocalBuildId(string appId)
        {
            try
            {
                var commonDir = Directory.GetParent(_gamePath);
                if (commonDir == null) return null;
                
                var steamappsDir = commonDir.Parent;
                if (steamappsDir == null) return null;
                
                var manifestPath = Path.Combine(steamappsDir.FullName, $"appmanifest_{appId}.acf");
                _logger.LogDebug($"Looking for local build ID in: {manifestPath}");

                if (!File.Exists(manifestPath)) return null;

                var manifestContent = File.ReadAllText(manifestPath);
                var match = Regex.Match(manifestContent, @"""buildid""\s+""(\d+)""", RegexOptions.IgnoreCase);
                
                if (match.Success && int.TryParse(match.Groups[1].Value, out var buildId))
                {
                    _logger.LogInformation($"Local build ID for AppID {appId}: {buildId}");
                    return buildId;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error reading local build ID: {ex.Message}");
                return null;
            }
        }

        public async Task<UpdateCheckResult?> CheckForUpdateAsync(string appId, int? currentBuildId)
        {
            if (_disposed || !CanCheckNow) return null;

            _logger.LogInformation($"[SteamCMD] Checking for updates: AppID {appId}, CurrentBuild {currentBuildId?.ToString() ?? "unknown"}");
            
            try
            {
                var latestBuildId = await GetLatestBuildIdAsync(appId);
                
                if (!latestBuildId.HasValue)
                {
                    _consecutiveFailures++;
                    HandleCheckFailure("Could not parse build ID from SteamCMD output");
                    return new UpdateCheckResult { Success = false, ErrorMessage = "Failed to retrieve build ID", CheckMethod = "SteamCMD" };
                }

                _consecutiveFailures = 0;
                _lastCheckTime = DateTime.Now;

                var updateAvailable = currentBuildId.HasValue && latestBuildId.Value != currentBuildId.Value;
                _logger.LogInformation($"[SteamCMD] AppID {appId}: Current={currentBuildId?.ToString() ?? "unknown"}, Latest={latestBuildId}, UpdateAvailable={updateAvailable}");

                return new UpdateCheckResult
                {
                    Success = true,
                    UpdateAvailable = updateAvailable,
                    CurrentBuildId = currentBuildId ?? 0,
                    NewBuildId = latestBuildId.Value,
                    CheckMethod = "SteamCMD"
                };
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                HandleCheckFailure($"Exception: {ex.Message}");
                return new UpdateCheckResult { Success = false, ErrorMessage = ex.Message, CheckMethod = "SteamCMD" };
            }
        }

        private async Task<int?> GetLatestBuildIdAsync(string appId)
        {
            if (_disposed) return null;

            var args = $"+login anonymous +app_info_update 1 +app_info_print {appId} +quit";
            _logger.LogDebug($"[SteamCMD] Executing: {_steamCmdPath} {args}");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = _steamCmdPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_steamCmdPath) ?? string.Empty
            };

            Process? process = null;
            try
            {
                process = Process.Start(startInfo);
                if (process == null) return null;

                _logger.LogDebug($"[SteamCMD] Process started (PID: {process.Id})");

                lock (_processLock) { _currentProcess = process; }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(90));

                var completedTask = await Task.WhenAny(outputTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning("SteamCMD timeout after 90 seconds");
                    KillProcess(process);
                    return null;
                }

                var output = await outputTask;
                var error = await errorTask;
                await process.WaitForExitAsync();

                _logger.LogDebug($"[SteamCMD] Process completed (ExitCode: {process.ExitCode})");
                _logger.LogDebug($"[SteamCMD] Output length: {output?.Length ?? 0} chars");

                return ParseBuildIdFromOutput(output, appId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SteamCMD] Exception: {ex.Message}");
                return null;
            }
            finally
            {
                lock (_processLock) { _currentProcess = null; }
                process?.Dispose();
            }
        }

        private int? ParseBuildIdFromOutput(string output, string appId)
        {
            if (string.IsNullOrWhiteSpace(output)) return null;

            try
            {
                var appDataStart = output.IndexOf($"\"{appId}\"");
                if (appDataStart == -1) return null;

                var appData = output.Substring(appDataStart);
                var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(appData));
                
                var data = kv.Deserialize(stream);
                if (data == null) return null;

                var buildIdValue = data["depots"]?["branches"]?["public"]?["buildid"];
                if (buildIdValue == null)
                {
                    _logger.LogWarning("[SteamCMD] Could not navigate to depots/branches/public/buildid");
                    return null;
                }

                if (int.TryParse(buildIdValue.ToString(), out var buildId))
                {
                    _logger.LogDebug($"[SteamCMD] Parsed build ID {buildId}");
                    return buildId;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SteamCMD] VDF parsing failed: {ex.Message}");
                
                // Regex fallback
                try
                {
                    var match = Regex.Match(output, @"""buildid""\s+""(\d+)""", RegexOptions.Singleline);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var buildId))
                    {
                        _logger.LogDebug($"[SteamCMD] Parsed build ID {buildId} using regex fallback");
                        return buildId;
                    }
                }
                catch { }
                
                return null;
            }
        }

        private void KillProcess(Process? process)
        {
            if (process == null) return;
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch { }
        }

        private void HandleCheckFailure(string reason)
        {
            _logger.LogWarning($"SteamCMD check failed ({_consecutiveFailures}/{MAX_FAILURES_BEFORE_BACKOFF}): {reason}");

            if (_consecutiveFailures >= MAX_FAILURES_BEFORE_BACKOFF)
            {
                var newInterval = TimeSpan.FromMinutes(_currentCheckInterval.TotalMinutes * 1.5);
                if (newInterval > _maxCheckInterval) newInterval = _maxCheckInterval;

                if (newInterval != _currentCheckInterval)
                {
                    _logger.LogWarning($"Increasing check interval from {_currentCheckInterval.TotalMinutes:F0} to {newInterval.TotalMinutes:F0} minutes");
                    _currentCheckInterval = newInterval;
                }
            }
        }

        public async Task<UpdateCheckResult?> ForceCheckNowAsync(string appId, int? currentBuildId)
        {
            if (_disposed) return null;

            _logger.LogInformation($"[SteamCMD] Force check requested for AppID {appId}");
            
            var originalLastCheckTime = _lastCheckTime;
            _lastCheckTime = DateTime.MinValue.AddYears(1000);
            
            var result = await CheckForUpdateAsync(appId, currentBuildId);
            
            if (_lastCheckTime == DateTime.MinValue.AddYears(1000))
            {
                _lastCheckTime = originalLastCheckTime;
            }
            
            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_processLock)
            {
                if (_currentProcess != null)
                {
                    KillProcess(_currentProcess);
                    _currentProcess = null;
                }
            }
        }
    }

    public class UpdateCheckResult
    {
        public bool Success { get; set; }
        public bool UpdateAvailable { get; set; }
        public int NewBuildId { get; set; }
        public int CurrentBuildId { get; set; }
        public string CheckMethod { get; set; } = "Unknown";
        public string? ErrorMessage { get; set; }
    }
}
