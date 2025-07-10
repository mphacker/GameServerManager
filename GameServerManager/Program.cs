namespace GameServerManager;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCrontab;
using Serilog;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

internal class Program
{
    // Remove fixed StatusLines/ErrorLines, use dynamic layout
    private static readonly ConcurrentQueue<string> ErrorQueue = new();
    private static readonly ConcurrentQueue<string> RecentActions = new(); // For last 10 actions
    private static List<GameServer> _gameServers = new();
    private static Timer? _statusTimer;
    private static bool _statusFirstDraw = true;

    static void Main(string[] args)
    {
        // Configure Serilog for file logging only
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: System.IO.Path.Combine(AppContext.BaseDirectory, "GameServerManager_.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 10,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
        Serilog.Log.Logger = serilogLogger;

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

        // Load appsettings.json into AppSettings model
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();
        var appSettings = configuration.Get<Settings>() ?? new();
        _gameServers = appSettings.GameServers ?? new List<GameServer>();

        // Initialization extensible notification manager
        var notificationManager = new NotificationManager(configuration);

        // Start status redraw timer
        _statusTimer = new(_ => DrawStatus(), null, 0, 2000);

        logger.LogInformation("Game Server Manager starting up...");
        AddRecentAction("Game Server Manager starting up...");

        // Validate configuration
        var validationErrors = ConfigurationValidator.Validate(appSettings);
        if (validationErrors.Count > 0 || appSettings.GameServers == null)
        {
            foreach (var error in validationErrors)
            {
                logger.LogError(error);
                AddRecentAction($"ERROR: {error}");
            }
            return;
        }

        // Remove trailing backslash from GamePath
        foreach (var gameServer in appSettings.GameServers)
        {
            if (gameServer.GamePath.EndsWith("\\"))
            {
                gameServer.GamePath = gameServer.GamePath.TrimEnd('\\');
                logger.LogWarning("Game path for {ServerName} has a trailing backslash. Removing it.", gameServer.Name);
                AddRecentAction($"Warning: Game path for {gameServer.Name} had a trailing backslash removed.");
            }
        }

        // Log\ configured game servers and their next backup/update times
        logger.LogInformation("Configured game servers:");
        foreach (var gameServer in appSettings.GameServers)
        {
            string nextUpdate = GetNextScheduledTime(gameServer.AutoUpdate, gameServer.AutoUpdateTime);
            string nextBackup = GetNextScheduledTime(gameServer.AutoBackup, gameServer.AutoBackupTime);
            logger.LogInformation("  {ServerName}: Next Update: {NextUpdate}, Next Backup: {NextBackup}", gameServer.Name, nextUpdate, nextBackup);
            AddRecentAction($"Configured {gameServer.Name}: Next Update: {nextUpdate}, Next Backup: {nextBackup}");
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
                var watchdog = new Watchdog(gameServer, appSettings.SteamCMDPath, watchdogLogger, updaterLogger, notificationManager);
                watchdogs.Add(watchdog);
                watchdog.Start();
                AddRecentAction($"Watchdog started for {gameServer.Name}");
            }
            else if (gameServer.AutoUpdate || gameServer.AutoBackup)
            {
                var updaterLogger = serviceProvider.GetRequiredService<ILogger<ServerUpdater>>();
                var updater = new ServerUpdater(gameServer, appSettings.SteamCMDPath, updaterLogger, notificationManager);
                updaters.Add(updater);
                updater.Start();
                AddRecentAction($"Updater started for {gameServer.Name}");
            }
        }

        // Keep the program running without using CPU cycles
        var resetEvent = new ManualResetEvent(false);
        resetEvent.WaitOne();
    }

    // Helper to get next scheduled time as a date/time string
    private static string GetNextScheduledTime(bool enabled, string schedule)
    {
        if (!enabled || string.IsNullOrWhiteSpace(schedule))
            return "none";
        var now = DateTime.Now;
        // Try CRON
        try
        {
            var cron = CrontabSchedule.Parse(schedule);
            var next = cron.GetNextOccurrence(now);
            return next.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch { }
        // Try time
        if (DateTime.TryParseExact(schedule, new[] { "HH:mm", "hh:mm tt", "H:mm", "h:mm tt" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var scheduledTime))
        {
            var scheduledToday = new DateTime(now.Year, now.Month, now.Day, scheduledTime.Hour, scheduledTime.Minute, 0);
            if (scheduledToday > now)
                return scheduledToday.ToString("yyyy-MM-dd HH:mm:ss");
            else
                return scheduledToday.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss");
        }
        return "invalid";
    }

    // Add a recent action (keep only last 10)
    public static void AddRecentAction(string action)
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {action}";
        RecentActions.Enqueue(entry);
        // Trim to last 10 only (thread-safe)
        while (RecentActions.Count > 10)
            RecentActions.TryDequeue(out _);
    }

    public static void Log(string message, bool isError = false)
    {
        if (isError)
        {
            // Only keep the last error
            while (ErrorQueue.TryDequeue(out _)) { }
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            ErrorQueue.Enqueue(entry);
        }
        // No console log, only file log
    }

    private static void DrawStatus()
    {
        lock (Console.Out)
        {
            if (_statusFirstDraw)
            {
                Console.Clear();
                _statusFirstDraw = false;
            }
            int line = 0;
            // Header
            Console.SetCursorPosition(0, line);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("=== Game Server Status | CTRL+C to close app ===".PadRight(Console.WindowWidth - 1));
            Console.ResetColor();
            line++;
            string separator = new string('-', Math.Max(40, Math.Min(Console.WindowWidth - 1, 80)));
            foreach (var server in _gameServers)
            {
                // Separator
                Console.SetCursorPosition(0, line++);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(separator.PadRight(Console.WindowWidth - 1));
                Console.ResetColor();
                // Server Name
                Console.SetCursorPosition(0, line++);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"Server: ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(server.Name);
                Console.ResetColor();
                // Path
                Console.SetCursorPosition(0, line++);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"  Path:         ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(server.GamePath);
                Console.ResetColor();
                // Process
                Console.SetCursorPosition(0, line++);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"  Process:      ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(server.ProcessName);
                Console.ResetColor();
                // Last/Next Update
                string lastUpdate = server.LastUpdateDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "none";
                string nextUpdate = GetNextScheduledTime(server.AutoUpdate, server.AutoUpdateTime);
                Console.SetCursorPosition(0, line++);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"  Last Update:  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(lastUpdate.PadRight(22));
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"Next Update: ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(nextUpdate);
                Console.ResetColor();
                // Last/Next Backup
                string lastBackup = server.LastBackupDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "none";
                string nextBackup = GetNextScheduledTime(server.AutoBackup, server.AutoBackupTime);
                Console.SetCursorPosition(0, line++);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"  Last Backup:  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(lastBackup.PadRight(22));
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"Next Backup: ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(nextBackup);
                Console.ResetColor();
                // Flags
                Console.SetCursorPosition(0, line++);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"  AutoRestart:  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write((server.AutoRestart ? "Yes" : "No").PadRight(8));
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"AutoUpdate: ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write((server.AutoUpdate ? "Yes" : "No").PadRight(8));
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"AutoBackup: ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(server.AutoBackup ? "Yes" : "No");
                Console.ResetColor();
            }
            // Final separator if any servers
            if (_gameServers.Count > 0)
            {
                Console.SetCursorPosition(0, line++);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(separator.PadRight(Console.WindowWidth - 1));
                Console.ResetColor();
            }
            // Show only the last error (if any)
            if (ErrorQueue.TryPeek(out var lastError))
            {
                Console.SetCursorPosition(0, line);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(lastError.PadRight(Console.WindowWidth - 1));
                Console.ResetColor();
                line++;
            }
            // Recent Actions label
            Console.SetCursorPosition(0, line);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Recent Actions:".PadRight(Console.WindowWidth - 1));
            Console.ResetColor();
            line++;
            // Recent Actions (last 10)
            var actions = RecentActions.ToArray();
            int start = Math.Max(0, actions.Length - 10);
            for (int i = 0; i < 10; i++)
            {
                Console.SetCursorPosition(0, line);
                if (i + start < actions.Length)
                    Console.Write(actions[i + start].PadRight(Console.WindowWidth - 1));
                else
                    Console.Write(new string(' ', Console.WindowWidth - 1));
                line++;
            }
            // Pad to clear any old lines from previous output
            for (int i = line; i < Console.WindowHeight; i++)
            {
                Console.SetCursorPosition(0, i);
                Console.Write(new string(' ', Console.WindowWidth - 1));
            }
        }
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
        NotificationSettings? notificationSettings = null;
        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Notification", out var notifElem))
            {
                notificationSettings = JsonSerializer.Deserialize<NotificationSettings>(notifElem.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            }
            settings = JsonSerializer.Deserialize<Settings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Settings();
        }
        else
        {
            settings = new Settings { GameServers = new List<GameServer>() };
            notificationSettings = new NotificationSettings();
        }
        settings.GameServers ??= new List<GameServer>();
        notificationSettings ??= new NotificationSettings();

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
            WriteMenuOption("6", "Configure Notifications");
            // Show test notification option if any notification type is enabled
            bool anyNotificationEnabled = notificationSettings.EnableEmail; // Add more types here if needed
            if (anyNotificationEnabled)
                WriteMenuOption("7", "Send Test Notification");
            WriteMenuOption(anyNotificationEnabled ? "8" : "7", "Save and Exit");
            WriteMenuOption(anyNotificationEnabled ? "9" : "8", "Exit without Saving");
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
                    ConfigureNotifications(notificationSettings);
                    break;
                case "7":
                    if (anyNotificationEnabled)
                    {
                        SendTestNotification(notificationSettings);
                        break;
                    }
                    goto case "8";
                case "8":
                    if (anyNotificationEnabled)
                    {
                        // Save both settings and notificationSettings
                        var merged = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                        var mergedDoc = JsonDocument.Parse(merged);
                        using (var stream = new MemoryStream())
                        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                        {
                            writer.WriteStartObject();
                            foreach (var prop in mergedDoc.RootElement.EnumerateObject())
                                prop.WriteTo(writer);
                            writer.WritePropertyName("Notification");
                            JsonSerializer.Serialize(writer, notificationSettings, new JsonSerializerOptions { WriteIndented = true });
                            writer.WriteEndObject();
                            writer.Flush();
                            File.WriteAllText(configPath, Encoding.UTF8.GetString(stream.ToArray()));
                        }
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Configuration saved.");
                        Console.ResetColor();
                        return;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Configuration saved.");
                        Console.ResetColor();
                        return;
                    }
                case "9":
                    if (anyNotificationEnabled)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Exiting without saving.");
                        Console.ResetColor();
                        return;
                    }
                    goto default;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid option.");
                    Console.ResetColor();
                    break;
            }
        }
    }

    private static void ConfigureNotifications(NotificationSettings notificationSettings)
    {
        while (true)
        {
            Console.Clear();
            WriteHeader("Notification Settings");
            Console.WriteLine("1. Configure Email Notifications");
            Console.WriteLine("2. Back");
            Console.WriteLine();
            Console.Write("Select notification type to configure: ");
            var input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    ConfigureEmailNotifications(notificationSettings);
                    break;
                case "2":
                    return;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid option.");
                    Console.ResetColor();
                    break;
            }
        }
    }

    private static void SendTestNotification(NotificationSettings notificationSettings)
    {
        var available = new List<string>();
        if (notificationSettings.EnableEmail && !string.IsNullOrWhiteSpace(notificationSettings.SmtpHost) && !string.IsNullOrWhiteSpace(notificationSettings.SmtpUser) && !string.IsNullOrWhiteSpace(notificationSettings.SmtpPass) && !string.IsNullOrWhiteSpace(notificationSettings.Recipient))
            available.Add("Email");
        if (available.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("No notification systems are properly configured.");
            Console.ResetColor();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            return;
        }
        Console.WriteLine("Select notification system to test:");
        for (int i = 0; i < available.Count; i++)
            Console.WriteLine($"{i + 1}. {available[i]}");
        Console.Write("Enter choice: ");
        var choice = Console.ReadLine();
        if (!int.TryParse(choice, out int idx) || idx < 1 || idx > available.Count)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Invalid selection.");
            Console.ResetColor();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            return;
        }
        var selected = available[idx - 1];
        try
        {
            if (selected == "Email")
            {
                var provider = new SMTPEmailNotificationProvider(new ConfigurationBuilder()
                    .AddInMemoryCollection(new List<KeyValuePair<string, string?>>
                    {
                        new KeyValuePair<string, string?>("Notification:SmtpHost", notificationSettings.SmtpHost),
                        new KeyValuePair<string, string?>("Notification:SmtpPort", notificationSettings.SmtpPort.ToString()),
                        new KeyValuePair<string, string?>("Notification:SmtpUser", notificationSettings.SmtpUser),
                        new KeyValuePair<string, string?>("Notification:SmtpPass", notificationSettings.SmtpPass),
                        new KeyValuePair<string, string?>("Notification:Recipient", notificationSettings.Recipient)
                    })
                    .Build());
                provider.Notify("Test Notification", "This is a test email notification from GameServerManager CLI.");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Test email sent (check your inbox).");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to send test notification: {ex.Message}");
        }
        finally
        {
            Console.ResetColor();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }

    private static void ConfigureEmailNotifications(NotificationSettings notificationSettings)
    {
        while (true)
        {
            Console.Clear();
            WriteHeader("Email Notification Settings");
            Console.WriteLine($"1. Enable Email Notifications: {(notificationSettings.EnableEmail ? "Yes" : "No")}");
            Console.WriteLine($"2. SMTP Host: {notificationSettings.SmtpHost}");
            Console.WriteLine($"3. SMTP Port: {notificationSettings.SmtpPort}");
            Console.WriteLine($"4. SMTP User: {notificationSettings.SmtpUser}");
            Console.WriteLine($"5. SMTP Pass: {(string.IsNullOrEmpty(notificationSettings.SmtpPass) ? "(not set)" : "(set)")}");
            Console.WriteLine($"6. Recipient: {notificationSettings.Recipient}");
            Console.WriteLine("7. Back");
            Console.WriteLine();
            Console.Write("Select an option: ");
            var input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("Enable Email Notifications (yes/no) [");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(notificationSettings.EnableEmail ? "yes" : "no");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("]: ");
                    Console.ResetColor();
                    var enableInput = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(enableInput))
                    {
                        if (enableInput.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                            enableInput.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) ||
                            enableInput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
                            notificationSettings.EnableEmail = true;
                        else if (enableInput.Trim().Equals("no", StringComparison.OrdinalIgnoreCase) ||
                            enableInput.Trim().Equals("n", StringComparison.OrdinalIgnoreCase) ||
                            enableInput.Trim().Equals("false", StringComparison.OrdinalIgnoreCase))
                            notificationSettings.EnableEmail = false;
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Invalid input. Please enter 'yes' or 'no'.");
                            Console.ResetColor();
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadKey();
                        }
                    }
                    break;
                case "2":
                    notificationSettings.SmtpHost = PromptDefault("SMTP Host", notificationSettings.SmtpHost);
                    break;
                case "3":
                    notificationSettings.SmtpPort = PromptInt("SMTP Port", notificationSettings.SmtpPort);
                    break;
                case "4":
                    notificationSettings.SmtpUser = PromptDefault("SMTP User", notificationSettings.SmtpUser);
                    break;
                case "5":
                    notificationSettings.SmtpPass = PromptDefault("SMTP Pass", notificationSettings.SmtpPass);
                    break;
                case "6":
                    notificationSettings.Recipient = PromptDefault("Recipient", notificationSettings.Recipient);
                    break;
                case "7":
                    return;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid option.");
                    Console.ResetColor();
                    break;
            }
        }
    }

    public class NotificationSettings
    {
        public string SmtpHost { get; set; } = "smtp.office365.com";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUser { get; set; } = string.Empty;
        public string SmtpPass { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        public bool EnableEmail { get; set; } = false;
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
            AutoBackup = false,
            AutoBackupsToKeep = 30,
            BackupWithoutShutdown = false
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
        gs.AutoUpdateTime = PromptTimeOrCron("AutoUpdateTime", gs.AutoUpdateTime);
        gs.AutoBackup = PromptBool("AutoBackup", gs.AutoBackup);
        gs.AutoBackupSource = PromptDefault("AutoBackupSource", gs.AutoBackupSource);
        gs.AutoBackupDest = PromptDefault("AutoBackupDest", gs.AutoBackupDest);
        gs.AutoBackupTime = PromptTimeOrCron("AutoBackupTime", gs.AutoBackupTime);
        gs.AutoBackupsToKeep = PromptInt("AutoBackupsToKeep", gs.AutoBackupsToKeep);
        gs.BackupWithoutShutdown = PromptBool("BackupWithoutShutdown", gs.BackupWithoutShutdown);
        return gs;
    }

    private static string PromptTimeOrCron(string label, string current)
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{label}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($" [{current}]: ");
            Console.ResetColor();
            var input = Console.ReadLine();
            var value = !string.IsNullOrWhiteSpace(input) ? input : current;
            if (IsValidTimeOrCron(value))
                return value;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{label} must be a valid time (e.g. 05:30 AM, 17:30) or CRON (e.g. 0 * * * *).");
            Console.ResetColor();
        }
    }

    private static bool IsValidTimeOrCron(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        // Try CRON
        try { NCrontab.CrontabSchedule.Parse(value); return true; } catch { }
        // Try time
        return DateTime.TryParseExact(value, new[] { "HH:mm", "hh:mm tt", "H:mm", "h:mm tt" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _);
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
                Console.WriteLine($"   AutoBackup: {s.AutoBackup}");
                Console.WriteLine($"   BackupWithoutShutdown: {s.BackupWithoutShutdown}");
                Console.WriteLine($"   AutoUpdateTime: {s.AutoUpdateTime}");
                Console.WriteLine($"   AutoBackupTime: {s.AutoBackupTime}");
                Console.WriteLine($"   AutoBackupSource: {s.AutoBackupSource}");
                Console.WriteLine($"   AutoBackupDest: {s.AutoBackupDest}");
                Console.WriteLine($"   AutoBackupsToKeep: {s.AutoBackupsToKeep}");

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
