using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;
using Serilog;
using System.Text.Json;

namespace GameServerManager
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Configure Serilog for file logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File(
                    path: System.IO.Path.Combine(AppContext.BaseDirectory, "GameServerManager_.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 10,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            // CLI configuration mode
            if (args.Length > 0 && args[0].Equals("config", StringComparison.OrdinalIgnoreCase))
            {
                RunConfigCli();
                return;
            }

            // Setup DI and logging
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Game Server Manager starting up...");

            // Load appsettings.json into AppSettings model
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();
            var appSettings = configuration.Get<Settings>() ?? new Settings();

            // Validate configuration
            var validationErrors = ConfigurationValidator.Validate(appSettings);
            if (validationErrors.Count > 0 || appSettings.GameServers == null)
            {
                foreach (var error in validationErrors)
                    logger.LogError(error);
                return;
            }

            // Remove trailing backslash from GamePath
            foreach (var gameServer in appSettings.GameServers)
            {
                if (gameServer.GamePath.EndsWith("\\"))
                {
                    gameServer.GamePath = gameServer.GamePath.TrimEnd('\\');
                    logger.LogWarning("Game path for {ServerName} has a trailing backslash. Removing it.", gameServer.Name);
                }
            }

            // Setup watchdog for each game server that has AutoRestart enabled
            var watchdogs = new List<Watchdog>();
            var updaters = new List<ServerUpdater>();
            foreach (var gameServer in appSettings.GameServers)
            {
                if (gameServer.AutoRestart)
                {
                    var watchdogLogger = serviceProvider.GetRequiredService<ILogger<Watchdog>>();
                    var updaterLogger = serviceProvider.GetRequiredService<ILogger<ServerUpdater>>();
                    var watchdog = new Watchdog(gameServer, appSettings.SteamCMDPath, watchdogLogger, updaterLogger);
                    watchdogs.Add(watchdog);
                    watchdog.Start();
                }
                else if (gameServer.AutoUpdate || gameServer.AutoBackup)
                {
                    var updaterLogger = serviceProvider.GetRequiredService<ILogger<ServerUpdater>>();
                    var updater = new ServerUpdater(gameServer, appSettings.SteamCMDPath, updaterLogger);
                    updaters.Add(updater);
                    updater.Start();
                }
            }

            // Keep the program running without using CPU cycles
            var resetEvent = new ManualResetEvent(false);
            resetEvent.WaitOne();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure =>
            {
                configure.AddSerilog();
                configure.SetMinimumLevel(LogLevel.Information);
            });
            services.AddSingleton<ILogger<ServerUpdater>, Logger<ServerUpdater>>();
            services.AddSingleton<ILogger<Watchdog>, Logger<Watchdog>>();
        }

        private static void RunConfigCli()
        {
            string configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            Settings settings;
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                settings = JsonSerializer.Deserialize<Settings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Settings();
            }
            else
            {
                settings = new Settings { GameServers = new List<GameServer>() };
            }
            settings.GameServers ??= new List<GameServer>();

            while (true)
            {
                Console.Clear();
                WriteHeader("GameServerManager Config CLI");
                Console.WriteLine();
                WriteMenuOption("1", "Set SteamCMDPath");
                WriteMenuOption("2", "Add Game Server");
                WriteMenuOption("3", "Edit Game Server");
                WriteMenuOption("4", "Remove Game Server");
                WriteMenuOption("5", "List Game Servers");
                WriteMenuOption("6", "Save and Exit");
                WriteMenuOption("7", "Exit without Saving");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Select an option: ");
                Console.ResetColor();
                var input = Console.ReadLine();
                switch (input)
                {
                    case "1":
                        SetSteamCMDPath(settings);
                        break;
                    case "2":
                        settings.GameServers.Add(PromptGameServer());
                        break;
                    case "3":
                        EditGameServer(settings.GameServers);
                        break;
                    case "4":
                        RemoveGameServer(settings.GameServers);
                        break;
                    case "5":
                        ListGameServers(settings.GameServers, true);
                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();
                        break;
                    case "6":
                        File.WriteAllText(configPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Configuration saved.");
                        Console.ResetColor();
                        return;
                    case "7":
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Exiting without saving.");
                        Console.ResetColor();
                        return;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Invalid option.");
                        Console.ResetColor();
                        break;
                }
            }
        }

        private static void SetSteamCMDPath(Settings settings)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Current SteamCMDPath: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(string.IsNullOrWhiteSpace(settings.SteamCMDPath) ? "(not set)" : settings.SteamCMDPath);
            Console.ResetColor();
            Console.Write("Enter new SteamCMDPath (leave blank to keep current): ");
            var input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
                settings.SteamCMDPath = input;
        }

        private static GameServer PromptGameServer(GameServer? existing = null)
        {
            var gs = existing ?? new GameServer
            {
                AutoRestart = true,
                AutoUpdate = true,
                AutoUpdateBackupTime = "05:30 AM",
                AutoBackup = false,
                AutoBackupDaysToKeep = 30
            };
            Console.WriteLine();
            WriteSectionHeader(existing == null ? "Add New Game Server" : $"Edit Game Server: {gs.Name}");
            gs.Name = PromptRequired("Name", gs.Name);
            gs.ProcessName = PromptRequired("ProcessName", gs.ProcessName);
            gs.GamePath = PromptRequired("GamePath", gs.GamePath);
            gs.ServerExe = PromptRequired("ServerExe", gs.ServerExe);
            gs.ServerArgs = PromptRequired("ServerArgs", gs.ServerArgs);
            gs.SteamAppId = PromptRequired("SteamAppId", gs.SteamAppId);
            gs.AutoRestart = PromptBool("AutoRestart", gs.AutoRestart);
            gs.AutoUpdate = PromptBool("AutoUpdate", gs.AutoUpdate);
            gs.AutoUpdateBackupTime = PromptDefault("AutoUpdateBackupTime", gs.AutoUpdateBackupTime);
            gs.AutoBackup = PromptBool("AutoBackup", gs.AutoBackup);
            gs.AutoBackupSource = PromptDefault("AutoBackupSource", gs.AutoBackupSource);
            gs.AutoBackupDest = PromptDefault("AutoBackupDest", gs.AutoBackupDest);
            gs.AutoBackupDaysToKeep = PromptInt("AutoBackupDaysToKeep", gs.AutoBackupDaysToKeep);
            return gs;
        }

        private static string PromptRequired(string label, string current)
        {
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{label}");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($" [{current}]: ");
                Console.ResetColor();
                var input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    return input;
                if (!string.IsNullOrWhiteSpace(current))
                    return current;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{label} is required.");
                Console.ResetColor();
            }
        }

        private static string PromptDefault(string label, string current)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{label}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($" [{current}]: ");
            Console.ResetColor();
            var input = Console.ReadLine();
            return !string.IsNullOrWhiteSpace(input) ? input : current;
        }

        private static bool PromptBool(string label, bool current)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{label}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($" [{current}]: ");
            Console.ResetColor();
            var input = Console.ReadLine();
            if (bool.TryParse(input, out var b))
                return b;
            return current;
        }

        private static int PromptInt(string label, int current)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{label}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($" [{current}]: ");
            Console.ResetColor();
            var input = Console.ReadLine();
            if (int.TryParse(input, out var i))
                return i;
            return current;
        }

        private static void EditGameServer(List<GameServer> servers)
        {
            if (servers.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No servers to edit.");
                Console.ResetColor();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            ListGameServers(servers, false);
            Console.Write("Enter server number to edit: ");
            if (int.TryParse(Console.ReadLine(), out var idx) && idx > 0 && idx <= servers.Count)
            {
                servers[idx - 1] = PromptGameServer(servers[idx - 1]);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid selection.");
                Console.ResetColor();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }

        private static void RemoveGameServer(List<GameServer> servers)
        {
            if (servers.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No servers to remove.");
                Console.ResetColor();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            ListGameServers(servers, false);
            Console.Write("Enter server number to remove: ");
            if (int.TryParse(Console.ReadLine(), out var idx) && idx > 0 && idx <= servers.Count)
            {
                servers.RemoveAt(idx - 1);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Server removed.");
                Console.ResetColor();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid selection.");
                Console.ResetColor();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }

        private static void ListGameServers(List<GameServer> servers, bool detailed)
        {
            if (servers.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No servers configured.");
                Console.ResetColor();
                return;
            }
            for (int i = 0; i < servers.Count; i++)
            {
                var s = servers[i];
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"{i + 1}. ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"{s.Name} ({s.GamePath})");
                if (detailed)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"   ProcessName: {s.ProcessName}");
                    Console.WriteLine($"   ServerExe: {s.ServerExe}");
                    Console.WriteLine($"   ServerArgs: {s.ServerArgs}");
                    Console.WriteLine($"   SteamAppId: {s.SteamAppId}");
                    Console.WriteLine($"   AutoRestart: {s.AutoRestart}");
                    Console.WriteLine($"   AutoUpdate: {s.AutoUpdate}");
                    Console.WriteLine($"   AutoUpdateBackupTime: {s.AutoUpdateBackupTime}");
                    Console.WriteLine($"   AutoBackup: {s.AutoBackup}");
                    Console.WriteLine($"   AutoBackupSource: {s.AutoBackupSource}");
                    Console.WriteLine($"   AutoBackupDest: {s.AutoBackupDest}");
                    Console.WriteLine($"   AutoBackupDaysToKeep: {s.AutoBackupDaysToKeep}");
                    Console.ResetColor();
                }
            }
            Console.ResetColor();
        }

        private static void WriteHeader(string text)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(new string('=', text.Length + 4));
            Console.WriteLine($"  {text}");
            Console.WriteLine(new string('=', text.Length + 4));
            Console.ResetColor();
        }

        private static void WriteSectionHeader(string text)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n--- {text} ---\n");
            Console.ResetColor();
        }

        private static void WriteMenuOption(string key, string label)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"[{key}] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(label);
            Console.ResetColor();
        }
    }
}
