namespace GameServerManager;

using GameServerManager.UI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Spectre.Console;
using ILogger = Microsoft.Extensions.Logging.ILogger;

internal class Program
{
    private static List<GameServer> _gameServers = new();
    private static ConsoleUI? _consoleUI;

    /// <summary>
    /// Application entry point. Handles startup, configuration, DI, and CLI.
    /// </summary>
    static async Task Main(string[] args)
    {
        ConfigureLogging();
        
        if (HandleCliCommands(args))
            return;

        IConfigurationRoot? configuration = null;
        Settings? appSettings = null;
        
        try
        {
            configuration = LoadConfiguration();
            appSettings = configuration.Get<Settings>() ?? new();
        }
        catch (Exception ex)
        {
            DisplayConfigurationLoadError(ex);
            return;
        }

        var serviceProvider = ConfigureDependencyInjection(configuration);
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        _gameServers = appSettings.GameServers ?? new List<GameServer>();
        var notificationManager = serviceProvider.GetRequiredService<NotificationManager>();

        LogWithStatus(logger, LogLevel.Information, "Game Server Manager starting up...");

        if (!ValidateAndLogConfiguration(appSettings, logger))
        {
            // Display errors and wait for user acknowledgment
            DisplayValidationErrorsAndExit(appSettings);
            return;
        }

        CleanGamePaths(appSettings, logger);
        LogConfiguredServers(appSettings, logger);

        // Initialize Spectre.Console UI
        _consoleUI = new ConsoleUI();
        
        // Set up cancellation for graceful shutdown
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            _consoleUI?.StopDashboard();
            cts.Cancel();
        };

        StartWatchdogsAndUpdaters(appSettings, serviceProvider, notificationManager, logger);

        // Start the live dashboard
        await _consoleUI.StartDashboardAsync(_gameServers);
    }

    private static void ConfigureLogging()
    {
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(AppContext.BaseDirectory, "GameServerManager_.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 10,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
        Serilog.Log.Logger = serilogLogger;
    }

    private static bool HandleCliCommands(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            var configCli = new ConfigurationCLI();
            configCli.Run();
            return true;
        }
        
        if (args.Length > 0 && (args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) || args[0].Equals("/?")))
        {
            ShowHelp();
            return true;
        }
        
        return false;
    }

    private static void ShowHelp()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("Game Server Manager").Color(Color.Cyan1));
        AnsiConsole.WriteLine();
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue);
        
        table.AddColumn("[bold]Command[/]");
        table.AddColumn("[bold]Description[/]");
        
        table.AddRow("[green]config[/]", "Launch interactive configuration CLI");
        table.AddRow("[green]--help, /?[/]", "Show this help message");
        table.AddRow("[dim](none)[/]", "Start the server manager with live status dashboard");
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static ServiceProvider ConfigureDependencyInjection(IConfiguration configuration)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(configuration);
        serviceCollection.AddLogging(configure =>
        {
            configure.AddSerilog();
            configure.SetMinimumLevel(LogLevel.Information);
        });
        serviceCollection.AddSingleton<ILogger<ServerUpdater>, Logger<ServerUpdater>>();
        serviceCollection.AddSingleton<ILogger<Watchdog>, Logger<Watchdog>>();
        serviceCollection.AddSingleton<NotificationManager>();
        return serviceCollection.BuildServiceProvider();
    }

    private static IConfigurationRoot LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();
    }

    /// <summary>
    /// Centralized logging that integrates with the Spectre.Console UI.
    /// </summary>
    public static void LogWithStatus(ILogger? logger, LogLevel level, string message)
    {
        switch (level)
        {
            case LogLevel.Information:
                logger?.LogInformation(message);
                _consoleUI?.AddRecentAction(message);
                break;
            case LogLevel.Warning:
                logger?.LogWarning(message);
                _consoleUI?.AddRecentAction($"[yellow]⚠[/] {message}");
                break;
            case LogLevel.Error:
                logger?.LogError(message);
                _consoleUI?.LogError(message);
                break;
            case LogLevel.Critical:
                logger?.LogCritical(message);
                _consoleUI?.LogError($"[bold]{message}[/]");
                break;
            default:
                logger?.Log(level, message);
                break;
        }
    }

    private static bool ValidateAndLogConfiguration(Settings appSettings, ILogger logger)
    {
        var validationErrors = ConfigurationValidator.Validate(appSettings);
        if (validationErrors.Count > 0 || appSettings.GameServers == null)
        {
            foreach (var error in validationErrors)
            {
                logger.LogError(error);
            }
            return false;
        }
        return true;
    }

    private static void DisplayValidationErrorsAndExit(Settings appSettings)
    {
        AnsiConsole.Clear();
        
        // Display header
        var panel = new Panel(new FigletText("Configuration Error").Color(Color.Red))
            .BorderColor(Color.Red)
            .Padding(1, 1);
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        // Get validation errors
        var validationErrors = ConfigurationValidator.Validate(appSettings);
        
        if (validationErrors.Count == 0 && appSettings.GameServers == null)
        {
            validationErrors.Add("No game servers configured in appsettings.json");
        }

        // Display error table
        var errorTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Red)
            .AddColumn(new TableColumn("[bold red]Validation Errors[/]").Centered());

        foreach (var error in validationErrors)
        {
            errorTable.AddRow($"[red]✗[/] {error}");
        }

        AnsiConsole.Write(errorTable);
        AnsiConsole.WriteLine();

        // Display help text
        var helpPanel = new Panel(
            "[yellow]Please fix the configuration errors above.[/]\n\n" +
            "You can:\n" +
            "  • Run [green]GameServerManager config[/] to use the interactive configuration tool\n" +
            "  • Manually edit [cyan]appsettings.json[/] in the application directory\n" +
            "  • Run [green]GameServerManager --help[/] for more information"
        )
        .Header("[bold yellow]⚠ Next Steps[/]")
        .BorderColor(Color.Yellow)
        .Padding(1, 1);
        
        AnsiConsole.Write(helpPanel);
        AnsiConsole.WriteLine();

        // Log file location
        AnsiConsole.MarkupLine($"[dim]Detailed logs are available at: {Path.Combine(AppContext.BaseDirectory, "GameServerManager_*.log")}[/]");
        AnsiConsole.WriteLine();

        // Wait for user acknowledgment
        AnsiConsole.MarkupLine("[bold]Press any key to exit...[/]");
        Console.ReadKey(true);
    }

    private static void DisplayConfigurationLoadError(Exception ex)
    {
        AnsiConsole.Clear();
        
        // Display header
        var panel = new Panel(new FigletText("Fatal Error").Color(Color.Red))
            .BorderColor(Color.Red)
            .Padding(1, 1);
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        // Display error message
        var errorPanel = new Panel(
            $"[bold red]Failed to load appsettings.json[/]\n\n" +
            $"[yellow]Error:[/] {ex.Message}\n\n" +
            $"[dim]Exception Type:[/] {ex.GetType().Name}"
        )
        .Header("[bold red]Configuration Load Error[/]")
        .BorderColor(Color.Red)
        .Padding(1, 1);
        
        AnsiConsole.Write(errorPanel);
        AnsiConsole.WriteLine();

        // Display help text
        var helpPanel = new Panel(
            "[yellow]Common causes:[/]\n" +
            "  • Invalid JSON syntax in appsettings.json\n" +
            "  • Missing appsettings.json file\n" +
            "  • File permissions issue\n" +
            "  • Corrupted configuration file\n\n" +
            "[yellow]How to fix:[/]\n" +
            "  • Check the file exists in: [cyan]" + AppContext.BaseDirectory + "[/]\n" +
            "  • Validate JSON syntax using a JSON validator\n" +
            "  • Run [green]GameServerManager config[/] to recreate the configuration\n" +
            "  • Restore from a backup if available"
        )
        .Header("[bold yellow]⚠ Troubleshooting[/]")
        .BorderColor(Color.Yellow)
        .Padding(1, 1);
        
        AnsiConsole.Write(helpPanel);
        AnsiConsole.WriteLine();

        // Show full exception in a collapsible tree (if user wants details)
        if (ex.InnerException != null || !string.IsNullOrEmpty(ex.StackTrace))
        {
            AnsiConsole.MarkupLine("[dim]Full error details:[/]");
            var tree = new Tree("[red]Exception Details[/]");
            tree.AddNode($"[yellow]Message:[/] {ex.Message}");
            if (ex.InnerException != null)
            {
                tree.AddNode($"[yellow]Inner Exception:[/] {ex.InnerException.Message}");
            }
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                var stackNode = tree.AddNode("[yellow]Stack Trace[/]");
                var stackLines = ex.StackTrace.Split('\n').Take(5);
                foreach (var line in stackLines)
                {
                    stackNode.AddNode($"[dim]{line.Trim()}[/]");
                }
            }
            AnsiConsole.Write(tree);
            AnsiConsole.WriteLine();
        }

        // Log file location
        AnsiConsole.MarkupLine($"[dim]Logs saved to: {Path.Combine(AppContext.BaseDirectory, "GameServerManager_*.log")}[/]");
        AnsiConsole.WriteLine();

        // Wait for user acknowledgment
        AnsiConsole.MarkupLine("[bold]Press any key to exit...[/]");
        Console.ReadKey(true);
    }

    private static void CleanGamePaths(Settings appSettings, ILogger logger)
    {
        if (appSettings.GameServers != null)
            foreach (var gameServer in appSettings.GameServers)
            {
                if (gameServer.GamePath.EndsWith("\\"))
                {
                    gameServer.GamePath = gameServer.GamePath.TrimEnd('\\');
                    LogWithStatus(logger, LogLevel.Warning, $"Game path for {gameServer.Name} has a trailing backslash. Removing it.");
                }
            }
    }

    private static void LogConfiguredServers(Settings appSettings, ILogger logger)
    {
        logger.LogInformation("Configured game servers:");
        if (appSettings.GameServers != null)
            foreach (var gameServer in appSettings.GameServers)
            {
                logger.LogInformation($"  {gameServer.Name}: Enabled={gameServer.Enabled}, AutoRestart={gameServer.AutoRestart}");
            }
    }

    private static void StartWatchdogsAndUpdaters(Settings appSettings, ServiceProvider serviceProvider, NotificationManager notificationManager, ILogger logger)
    {
        var watchdogs = new List<Watchdog>();
        var updaters = new List<ServerUpdater>();
        
        if (appSettings.GameServers != null)
            foreach (var gameServer in appSettings.GameServers)
            {
                if (!gameServer.Enabled)
                {
                    logger.LogInformation($"Skipping disabled server: {gameServer.Name}");
                    continue;
                }

                if (gameServer.AutoRestart)
                {
                    var watchdogLogger = serviceProvider.GetRequiredService<ILogger<Watchdog>>();
                    var updaterLogger = serviceProvider.GetRequiredService<ILogger<ServerUpdater>>();
                    var watchdog = new Watchdog(gameServer, appSettings.SteamCMDPath, watchdogLogger, updaterLogger, notificationManager);
                    watchdogs.Add(watchdog);
                    watchdog.Start();
                }
                else if (gameServer.AutoUpdate || gameServer.AutoBackup)
                {
                    var updaterLogger = serviceProvider.GetRequiredService<ILogger<ServerUpdater>>();
                    var updater = new ServerUpdater(gameServer, appSettings.SteamCMDPath, updaterLogger, notificationManager);
                    updaters.Add(updater);
                    updater.Start();
                }
            }
    }
}
