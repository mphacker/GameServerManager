using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;
using Serilog;

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
    }
}
