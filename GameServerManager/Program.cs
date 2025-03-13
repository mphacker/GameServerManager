using Microsoft.Extensions.Configuration;
using System.Threading; // Add this using directive

namespace GameServerManager
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Game Server Manger starting up...");

            // Load appsettings.json into AppSettings model
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var appSettings = configuration.Get<Settings>() ?? new Settings();

            // Check if SteamCMD path is pointing to a valid file
            if (!File.Exists(appSettings.SteamCMDPath))
            {
                Console.WriteLine($"SteamCMD path is invalid: {appSettings.SteamCMDPath}");
                return;
            }

            // Check if any game servers are configured
            if (appSettings.GameServers is null || appSettings.GameServers.Count == 0)
            {
                Console.WriteLine("No game servers configured in appsettings.json.");
                return;
            }

            //ensure that the game servers are valid
            foreach (var gameServer in appSettings.GameServers)
            {
                if (string.IsNullOrEmpty(gameServer.Name) || string.IsNullOrEmpty(gameServer.ProcessName))
                {
                    Console.WriteLine($"Invalid game server configuration. Name or process name is missing: {gameServer.Name}");
                    return;
                }

                //The full path to the game server is the GamePath and ServerExe combined.  Verify that the file exists
                var fullPath = Path.Combine(gameServer.GamePath, gameServer.ServerExe);
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"Game server {gameServer.Name} executable not found at: {fullPath}");
                    return;
                }

                //check if the game server backup paths are valid
                if (gameServer.AutoBackup &&(string.IsNullOrEmpty(gameServer.AutoBackupSource) || string.IsNullOrEmpty(gameServer.AutoBackupDest)))
                {
                    Console.WriteLine($"Invalid game server backup source or destination for {gameServer.Name}");
                    return;
                }

                //check if the game server update time is valid date time
                if ((gameServer.AutoUpdate || gameServer.AutoBackup) && !DateTime.TryParse(gameServer.AutoUpdateBackupTime, out _))
                {
                    Console.WriteLine($"Invalid game server update/backup time for {gameServer.Name}");
                    return;
                }

                //ensure that the game server path does not have a trailing \
                if (gameServer.GamePath.EndsWith("\\"))
                {
                    gameServer.GamePath = gameServer.AutoBackupSource.TrimEnd('\\');
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: Game path for {gameServer.Name} has a trailing backslash. Removing it.");
                    Console.ResetColor();
                }

            }

            // Setup watchdog for each game server that has AutoRestart enabled
            var watchdogs = new List<Watchdog>();
            var updaters = new List<ServerUpdater>();
            foreach (var gameServer in appSettings.GameServers)
            {
                // Watchdog will handle process monitoring of servers.
                // If the game server is also set for AutoUpdate, the watchdog will also handle the update process.
                if (gameServer.AutoRestart)
                {
                    var watchdog = new Watchdog(gameServer, appSettings.SteamCMDPath);
                    watchdogs.Add(watchdog);
                    watchdog.Start();
                }
                else if (gameServer.AutoUpdate || gameServer.AutoBackup)
                {
                    // This is for games that are not covered by watchdog but still need to be updated and/or backed up.
                    var updater = new ServerUpdater(gameServer, appSettings.SteamCMDPath);
                    updaters.Add(updater);
                    updater.Start();
                }
            }

            // Keep the program running without using CPU cycles
            var resetEvent = new ManualResetEvent(false);
            resetEvent.WaitOne();
        }
    }
}
