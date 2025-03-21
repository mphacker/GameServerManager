using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace GameServerManager
{
    public class ServerUpdater
    {
        private GameServer _gameServer;
        string _steamCmdPath = string.Empty;
        private System.Timers.Timer _timer;
        private DateTime _lastUpdateDate = DateTime.MinValue; // Track the last update date
        public bool UpdateInProgres { get; private set; } = false;

        public ServerUpdater(GameServer gameServer, string steamCmdPath)
        {
            _gameServer = gameServer;
            _steamCmdPath = steamCmdPath;
            _timer = new System.Timers.Timer(60000); // Check every 60 seconds
            _timer.Elapsed += (sender, e) => AutoUpdateCheck();
        }

        public void Start()
        {
            // Start the auto-update timer
            Utils.Log($"ServerUpdater - Auto-update check process started for {_gameServer.Name}");
            _timer.AutoReset = true;
            _timer.Enabled = true;
            _timer.Start();
        }

        public void Stop()
        {
            // Stop the auto-update timer
            Utils.Log($"ServerUpdater - Auto-update check process stopped for {_gameServer.Name}");
            _timer.Stop();
        }

        private void AutoUpdateCheck()
        {
            // Check if it's time to update the server
            if (IsTimeToUpdateServer())
            {
                UpdateServer();
            }
        }

        public bool IsTimeToUpdateServer()
        {
            // Check if the game server is set to auto-update
            if (_gameServer.AutoUpdate)
            {
                //if we are already in the process of updating, return false
                if (UpdateInProgres)
                    return false;

                // Get the current time
                var currentTime = DateTime.Now;
                var currentTimeOfDay = currentTime.TimeOfDay;

                // Ensure we haven't already done an update today
                if (_lastUpdateDate.Date == currentTime.Date)
                {
                    Utils.Log($"ServerUpdater - Already updated {_gameServer.Name} today.");
                    return false; // Already updated today
                }

                // Parse the AutoUpdateTime from the appsettings.json
                var autoUpdateTime = DateTime.ParseExact(_gameServer.AutoUpdateBackupTime, "hh:mm tt", CultureInfo.InvariantCulture);
                var autoUpdateTimeOfDay = autoUpdateTime.TimeOfDay;

                // Define the end of the auto-update time range (5 minutes past the auto-update time)
                var autoUpdateEndTime = autoUpdateTimeOfDay.Add(TimeSpan.FromMinutes(5));

                // Check if the current time is within the auto-update time range 
                if (currentTimeOfDay >= autoUpdateTimeOfDay && currentTimeOfDay <= autoUpdateEndTime)
                {
                    Utils.Log($"ServerUpdater - Auto-update time reached for {_gameServer.Name}.");
                    return true; // Time to update the server
                }
            }
            return false; // No need to update the server
        }

        public bool UpdateServer()
        {
            try
            {
                // Check if the game server is set to auto-update or auto-backup and that we are not already running the updateserver process.
                if ((_gameServer.AutoUpdate || _gameServer.AutoBackup) && !UpdateInProgres)
                {
                    _lastUpdateDate = DateTime.Now; // Update the last update date
                    UpdateInProgres = true;
                    bool needToRestartServer = false;
                    Utils.Log($"ServerUpdater - Updating {_gameServer.Name}...");

                    //check to see if the game server is running
                    var process = System.Diagnostics.Process.GetProcessesByName(_gameServer.ProcessName).FirstOrDefault();
                    if (process != null && !process.HasExited)
                    {
                        needToRestartServer = true; // The server is running and needs to be restarted after the update
                        Utils.Log($"ServerUpdater - Process for {_gameServer.Name} is running. Stopping server.");

                        // Stop the game server process gracefully
                        process.CloseMainWindow(); // This will send a close message to the main window of the process
                        process.WaitForExit(10000); // Wait for 10 seconds for the process to exit

                        if (!process.HasExited)
                        {
                            Utils.Log($"ServerUpdater - Process for {_gameServer.Name} did not exit in time. Killing process.");
                            process.Kill(); // Forcefully kill the process if it didn't exit
                        }

                        Utils.Log($"ServerUpdater - Process for {_gameServer.Name} stopped.");
                    }

                    //If we have AutoBackup enabled, complete the backup process.
                    if (_gameServer.AutoBackup)
                    {
                        var backup = new ServerBackup(_gameServer, _steamCmdPath);
                        if (backup.Backup())
                        {
                            Utils.Log($"ServerUpdater - Backup completed for {_gameServer.Name}.");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Utils.Log($"ServerUpdater - Backup failed for {_gameServer.Name}. Proceeding with update.");
                            Console.ResetColor();
                        }
                    }

                    //Do the game server update if required.
                    if (_gameServer.AutoUpdate)
                    {

                        // Run the SteamCMD command to update the game server
                        var steamCmdArgs = $"+login anonymous +force_install_dir \"{_gameServer.GamePath}\" +app_update {_gameServer.SteamAppId} validate +quit";
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = _steamCmdPath,
                            Arguments = steamCmdArgs,
                            UseShellExecute = false, // Required for redirection
                            RedirectStandardOutput = true, // Redirect standard output
                            RedirectStandardError = true, // Redirect standard error
                            CreateNoWindow = true
                        };

                        // Start the SteamCMD process
                        Utils.Log($"ServerUpdater - Running SteamCMD at {_steamCmdPath} with arguments: {steamCmdArgs}");
                        using (var updateProcess = new System.Diagnostics.Process())
                        {
                            updateProcess.StartInfo = startInfo;
                            updateProcess.OutputDataReceived += (sender, e) => { if (e.Data != null) Utils.Log($"ServerUpdater - SteamCMD Output: {e.Data}"); };
                            updateProcess.ErrorDataReceived += (sender, e) => { if (e.Data != null) Utils.Log($"ServerUpdater - Error: {e.Data}"); };
                            updateProcess.Start();
                            updateProcess.BeginOutputReadLine();
                            updateProcess.BeginErrorReadLine();
                            updateProcess.WaitForExit();
                            if (updateProcess.ExitCode == 0)
                            {
                                Utils.Log($"ServerUpdater - Update completed successfully for {_gameServer.Name}.");
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Utils.Log($"ServerUpdater - Update failed for {_gameServer.Name}. Exit code: {updateProcess.ExitCode}");
                                Console.ResetColor();
                                UpdateInProgres = false; // Reset the update in progress flag
                                return false; // Return false if the update failed
                            }
                        }
                    }

                    // Restart the server if it was running prior to the update
                    if (needToRestartServer)
                    {
                        Utils.Log($"ServerUpdater - restarting process for {_gameServer.Name}");
                        var restartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = System.IO.Path.Combine(_gameServer.GamePath, _gameServer.ServerExe),
                            Arguments = _gameServer.ServerArgs,
                            UseShellExecute = true,
                            CreateNoWindow = false
                        };
                        var rerestartProcess = new System.Diagnostics.Process();
                        rerestartProcess.StartInfo = restartInfo;
                        rerestartProcess.Start();

                        //wait 2 minutes
                        Utils.Log($"ServerUpdater - Waiting 30 seconds for {_gameServer.Name} to start.");
                        System.Threading.Thread.Sleep(30000);
                        Utils.Log($"ServerUpdater - Waiting completed.");

                    }

                    UpdateInProgres = false; // Reset the update in progress flag
                    return true; // Return true if the update was successful
                }
                else if (UpdateInProgres)
                {
                    Utils.Log($"ServerUpdater - update in progress for {_gameServer.Name}...");
                    return false; // Return false if the update is already in progress
                }
                return false; // Return false if auto-update is not enabled
            }
            catch (Exception ex)
            {
                Utils.Log($"ServerUpdater - Exception occurred while updating {_gameServer.Name}: {ex.Message}");
                UpdateInProgres = false; // Reset the update in progress flag
                return false; // Return false if an exception occurred
            }
        }
    }
}
