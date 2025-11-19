using Spectre.Console;
using Microsoft.Extensions.Logging;

namespace GameServerManager.UI;

/// <summary>
/// Handles interactive menu operations for manual server management.
/// </summary>
public class InteractiveMenu
{
    private readonly Dictionary<string, ServerUpdater> _serverUpdaters;
    private readonly Dictionary<string, Watchdog> _watchdogs;
    private readonly ILogger? _logger;

    public InteractiveMenu(
        Dictionary<string, ServerUpdater> serverUpdaters, 
        Dictionary<string, Watchdog> watchdogs,
        ILogger? logger = null)
    {
        _serverUpdaters = serverUpdaters;
        _watchdogs = watchdogs;
        _logger = logger;
    }

    /// <summary>
    /// Shows the main interactive menu and handles user selection.
    /// </summary>
    /// <returns>True if user wants to return to dashboard, false to exit app</returns>
    public async Task<bool> ShowMainMenuAsync()
    {
        try
        {
            AnsiConsole.Clear();
            
            // Display header
            var header = new Panel(new FigletText("Manual Operations").Color(Color.Yellow))
                .BorderColor(Color.Yellow)
                .Padding(1, 0);
            AnsiConsole.Write(header);
            AnsiConsole.WriteLine();

            // Show menu options
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]What would you like to do?[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "Force Backup",
                        "Force Update Check",
                        "Return to Dashboard",
                        "Exit Application"
                    }));

            switch (choice)
            {
                case "Force Backup":
                    await ForceBackupAsync();
                    return true;
                
                case "Force Update Check":
                    await ForceUpdateCheckAsync();
                    return true;
                
                case "Return to Dashboard":
                    return true;
                
                case "Exit Application":
                    return false;
                
                default:
                    return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in main menu: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]Error displaying menu: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[dim]Press any key to return to dashboard...[/]");
            Console.ReadKey(true);
            return true; // Return to dashboard on error
        }
    }

    /// <summary>
    /// Shows a list of servers with AutoBackup enabled and forces a backup on selected server.
    /// </summary>
    private async Task ForceBackupAsync()
    {
        try
        {
            AnsiConsole.Clear();
            
            // Get servers with AutoBackup enabled (from both standalone updaters and watchdogs)
            var backupServers = new List<string>();
            
            // Add from standalone updaters
            backupServers.AddRange(_serverUpdaters
                .Where(kvp => kvp.Value != null && kvp.Value.GameServer.AutoBackup)
                .Select(kvp => kvp.Key));
            
            // Add from watchdogs
            backupServers.AddRange(_watchdogs
                .Where(kvp => kvp.Value != null && kvp.Value.Updater.GameServer.AutoBackup)
                .Select(kvp => kvp.Key));
            
            backupServers = backupServers.Distinct().OrderBy(s => s).ToList();

            if (backupServers.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No servers with AutoBackup enabled found.[/]");
                AnsiConsole.MarkupLine("[dim]Press any key to return to menu...[/]");
                Console.ReadKey(true);
                return;
            }

            // Add cancel option
            backupServers.Add("[dim]<< Cancel[/]");

            var selectedServer = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[blue]Select server to backup:[/]")
                    .PageSize(15)
                    .AddChoices(backupServers));

            if (selectedServer == "[dim]<< Cancel[/]")
            {
                return;
            }

            // Confirm action
            if (!AnsiConsole.Confirm($"[yellow]Force backup for {selectedServer}?[/]"))
            {
                return;
            }

            // Execute backup
            AnsiConsole.MarkupLine($"\n[blue]Starting backup for {selectedServer}...[/]");
            
            var updater = GetUpdater(selectedServer);
            if (updater != null)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync($"Backing up {selectedServer}...", async ctx =>
                    {
                        var result = await updater.BackupServerAsync();
                        
                        if (result)
                        {
                            AnsiConsole.MarkupLine($"[green]? Backup completed successfully for {selectedServer}[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[red]? Backup failed for {selectedServer}[/]");
                        }
                    });
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]? Could not find updater for {selectedServer}[/]");
            }

            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in force backup: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]Error during backup operation: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey(true);
        }
    }

    /// <summary>
    /// Shows a list of servers with AutoUpdate enabled and forces an update check on selected server.
    /// </summary>
    private async Task ForceUpdateCheckAsync()
    {
        try
        {
            AnsiConsole.Clear();
            
            // Get servers with AutoUpdate enabled (from both standalone updaters and watchdogs)
            var updateServers = new List<string>();
            
            // Add from standalone updaters
            updateServers.AddRange(_serverUpdaters
                .Where(kvp => kvp.Value != null && 
                             kvp.Value.GameServer.AutoUpdate && 
                             !string.IsNullOrWhiteSpace(kvp.Value.GameServer.SteamAppId))
                .Select(kvp => kvp.Key));
            
            // Add from watchdogs
            updateServers.AddRange(_watchdogs
                .Where(kvp => kvp.Value != null && 
                             kvp.Value.Updater.GameServer.AutoUpdate && 
                             !string.IsNullOrWhiteSpace(kvp.Value.Updater.GameServer.SteamAppId))
                .Select(kvp => kvp.Key));
            
            updateServers = updateServers.Distinct().OrderBy(s => s).ToList();

            if (updateServers.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No servers with AutoUpdate enabled found.[/]");
                AnsiConsole.MarkupLine("[dim]Press any key to return to menu...[/]");
                Console.ReadKey(true);
                return;
            }

            // Add cancel option
            updateServers.Add("[dim]<< Cancel[/]");

            var selectedServer = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]Select server to check for updates:[/]")
                    .PageSize(15)
                    .AddChoices(updateServers));

            if (selectedServer == "[dim]<< Cancel[/]")
            {
                return;
            }

            // Confirm action
            if (!AnsiConsole.Confirm($"[yellow]Force update check for {selectedServer}?[/]"))
            {
                return;
            }

            // Execute update check
            AnsiConsole.MarkupLine($"\n[yellow]Checking for updates for {selectedServer}...[/]");
            
            var updater = GetUpdater(selectedServer);
            if (updater != null)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync($"Checking {selectedServer}...", async ctx =>
                    {
                        await updater.ForceUpdateCheckNowAsync();
                        AnsiConsole.MarkupLine($"[green]? Update check completed for {selectedServer}[/]");
                        AnsiConsole.MarkupLine($"[dim]Check the dashboard or logs for results.[/]");
                    });
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]? Could not find updater for {selectedServer}[/]");
            }

            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in force update check: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]Error during update check operation: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey(true);
        }
    }

    /// <summary>
    /// Gets the ServerUpdater for a given server name (from standalone updaters or watchdogs).
    /// </summary>
    private ServerUpdater? GetUpdater(string serverName)
    {
        // Try standalone updater first
        if (_serverUpdaters.TryGetValue(serverName, out var updater))
        {
            return updater;
        }
        
        // Try watchdog
        if (_watchdogs.TryGetValue(serverName, out var watchdog))
        {
            return watchdog.Updater;
        }
        
        return null;
    }

    /// <summary>
    /// Checks if a server has AutoBackup enabled by finding its GameServer instance.
    /// </summary>
    private bool IsBackupEnabled(string serverName)
    {
        var gameServer = GetGameServer(serverName);
        return gameServer?.AutoBackup ?? false;
    }

    /// <summary>
    /// Checks if a server has AutoUpdate enabled by finding its GameServer instance.
    /// </summary>
    private bool IsUpdateEnabled(string serverName)
    {
        var gameServer = GetGameServer(serverName);
        return (gameServer?.AutoUpdate ?? false) && !string.IsNullOrWhiteSpace(gameServer?.SteamAppId);
    }

    /// <summary>
    /// Gets the GameServer instance for a given server name.
    /// </summary>
    private GameServer? GetGameServer(string serverName)
    {
        // Get from updater
        var updater = GetUpdater(serverName);
        return updater?.GameServer;
    }
}
