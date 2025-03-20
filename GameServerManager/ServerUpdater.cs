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
        bool updateInProgres = false;

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
            Utils.Log($"Auto-update check process started for {_gameServer.Name}");
            _timer.AutoReset = true;
            _timer.Enabled = true;
            _timer.Start();
        }

        public void Stop()
        {
            // Stop the auto-update timer
            Utils.Log($"Auto-update check process stopped for {_gameServer.Name}");
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
                if (updateInProgres)
                    return false;

                // Get the current time
                var currentTime = DateTime.Now;
                var currentTimeOfDay = currentTime.TimeOfDay;

                // Ensure we haven't already done an update today
                if (_lastUpdateDate.Date == currentTime.Date)
                {
                    Utils.Log($"Already updated {_gameServer.Name} today.");
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
                    Utils.Log($"Auto-update time reached for {_gameServer.Name}.");
                    return true; // Time to update the server
                }
            }
            return false; // No need to update the server
        }

        public bool UpdateServer()
        {
            // Check if the game server is set to auto-update or auto-backup and that we are not already running the updateserver process.
            if ((_gameServer.AutoUpdate || _gameServer.AutoBackup) && !updateInProgres)
            {
                _lastUpdateDate = DateTime.Now; // Update the last update date
                updateInProgres = true;
                bool needToRestartServer = false;
                Utils.Log($"Updating {_gameServer.Name}...");

                //check to see if the game server is running
                var process = System.Diagnostics.Process.GetProcessesByName(_gameServer.ProcessName).FirstOrDefault();
                if (process != null && !process.HasExited)
                {
                    needToRestartServer = true; // The server is running and needs to be restarted after the update
                    Utils.Log($"Process for {_gameServer.Name} is running. Stopping server.");

                    // Stop the game server process gracefully
                    process.CloseMainWindow(); // This will send a close message to the main window of the process
                    process.WaitForExit(10000); // Wait for 10 seconds for the process to exit

                    if (!process.HasExited)
                    {
                        Utils.Log($"Process for {_gameServer.Name} did not exit in time. Killing process.");
                        process.Kill(); // Forcefully kill the process if it didn't exit
                    }

                    Utils.Log($"Process for {_gameServer.Name} stopped.");
                }

                //If we have AutoBackup enabled, complete the backup process.
                if (_gameServer.AutoBackup)
                {
                    var backup = new ServerBackup(_gameServer, _steamCmdPath);
                    if (backup.Backup())
                    {
                        Utils.Log($"Backup completed for {_gameServer.Name}.");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Utils.Log($"Backup failed for {_gameServer.Name}. Proceeding with update.");
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
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };

                    // Start the SteamCMD process
                    Utils.Log($"Running SteamCMD at {_steamCmdPath} with arguments: {steamCmdArgs}");
                    using (var updateProcess = new System.Diagnostics.Process())
                    {
                        updateProcess.StartInfo = startInfo;
                        updateProcess.Start();
                        updateProcess.WaitForExit();
                    }
                }

                // Restart the server if it was running prior to the update
                if (needToRestartServer)
                {
                    Utils.Log($"Restarting process for {_gameServer.Name}");
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

                    //wait 20 seconds
                    Thread.Sleep(20000);
                }

                updateInProgres = false; // Reset the update in progress flag
                return true; // Return true if the update was successful
            }
            return false; // Return false if auto-update is not enabled
        }
    }
}
