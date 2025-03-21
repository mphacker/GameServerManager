using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServerManager
{
    public class Watchdog
    {
        private GameServer _gameServer;
        private string _steamCmdPath = string.Empty;
        private System.Timers.Timer _timer;
        ServerUpdater updater;
        public Watchdog(GameServer gameServer, string steamCmdPath)
        {
            _gameServer = gameServer;
            _steamCmdPath = steamCmdPath;
            _timer = new System.Timers.Timer(30000); // Check every 30 seconds
            _timer.Elapsed += (sender, e) => CheckProcess();
            // Check if it's time to update the server
            updater = new ServerUpdater(_gameServer, _steamCmdPath);
        }

        public void Start()
        {
            // Start the watchdog timer
            // This is where you would implement the logic to monitor the game server process
            // and restart it if necessary.
            Utils.Log($"Watchdog started for {_gameServer.Name}");
            _timer.AutoReset = true;
            _timer.Enabled = true;
            _timer.Start();
        }

        public void Stop()
        {
            // Stop the watchdog timer
            // This is where you would implement the logic to stop monitoring the game server process.
            Utils.Log($"Watchdog stopped for {_gameServer.Name}");
            _timer.Stop();

        }

        private void CheckProcess()
        {
            //Is AutoUpdate enabled?
            if (_gameServer.AutoUpdate)
            {
                if (updater.IsTimeToUpdateServer())
                {
                    updater.UpdateServer();
                }
            }


            if (updater.UpdateInProgres)
            {
                Utils.Log($"Watchdog - Update in progress for {_gameServer.Name}. Skipping process check.");
                return;
            }

            // Check if the game server process is running
            // If not, restart it
            // This is where you would implement the logic to check the game server process.
            Utils.Log($"Watchdog - Checking process for {_gameServer.Name}");
            var process = System.Diagnostics.Process.GetProcessesByName(_gameServer.ProcessName).FirstOrDefault();
            if (process == null || process.HasExited)
            {
                Utils.Log($"Watchdog - Process for {_gameServer.Name} not found. Restarting...");
                StartGameServer();
            }
            else
            {
                Utils.Log($"Watchdog - Process for {_gameServer.Name} is running.");
            }
        }

        private void StartGameServer()
        {
            // Restart the game server process
            // This is where you would implement the logic to restart the game server process.
            Utils.Log($"Watchdog is restarting process for {_gameServer.Name}");
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
                Utils.Log($"Watchdog - Process for {_gameServer.Name} started.");
            }
        }
    }
}
