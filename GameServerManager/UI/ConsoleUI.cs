using Spectre.Console;
using Spectre.Console.Rendering;
using System.Collections.Concurrent;

namespace GameServerManager.UI;

/// <summary>
/// Handles all console UI rendering using Spectre.Console.
/// </summary>
public class ConsoleUI : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> _recentActions = new();
    private readonly ConcurrentQueue<string> _errorQueue = new();
    private List<GameServer> _gameServers = new();
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Adds a recent action to the activity log (keeps last 10).
    /// </summary>
    public void AddRecentAction(string action)
    {
        // Escape square brackets that aren't Spectre.Console markup
        // Replace [Check], [OK], [Error], etc. with escaped versions
        action = action
            .Replace("[Check]", "[[Check]]")
            .Replace("[OK]", "[[OK]]")
            .Replace("[Error]", "[[Error]]")
            .Replace("[Startup]", "[[Startup]]")
            .Replace("[UPDATE]", "[[UPDATE]]")
            .Replace("[Updating]", "[[Updating]]")
            .Replace("[Backup]", "[[Backup]]")
            .Replace("[Stop]", "[[Stop]]")
            .Replace("[Warn]", "[[Warn]]")
            .Replace("[Start]", "[[Start]]")
            .Replace("[Wait]", "[[Wait]]")
            .Replace("[Download]", "[[Download]]")
            .Replace("[Update]", "[[Update]]")
            .Replace("[Kill]", "[[Kill]]");
        
        var entry = $"[dim]{DateTime.Now:yyyy-MM-dd HH:mm:ss}[/] {action}";
        _recentActions.Enqueue(entry);
        while (_recentActions.Count > 10)
            _recentActions.TryDequeue(out _);
    }

    /// <summary>
    /// Logs an error message (keeps only the most recent error).
    /// </summary>
    public void LogError(string message)
    {
        while (_errorQueue.TryDequeue(out _)) { }
        var entry = $"[red]{DateTime.Now:yyyy-MM-dd HH:mm:ss}[/] {message}";
        _errorQueue.Enqueue(entry);
    }

    /// <summary>
    /// Starts the live status dashboard.
    /// </summary>
    public async Task StartDashboardAsync(List<GameServer> gameServers)
    {
        _gameServers = gameServers;
        _cancellationTokenSource = new CancellationTokenSource();

        Console.Clear();
        
        await AnsiConsole.Live(CreateDashboard())
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        ctx.UpdateTarget(CreateDashboard());
                        await Task.Delay(2000, _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
    }

    /// <summary>
    /// Stops the dashboard rendering.
    /// </summary>
    public void StopDashboard()
    {
        _cancellationTokenSource?.Cancel();
    }

    private Layout CreateDashboard()
    {
        // Calculate available space for dynamic content
        var consoleHeight = Console.WindowHeight;
        var headerHeight = 3;
        var hasError = _errorQueue.TryPeek(out _);
        var errorHeight = hasError ? 3 : 0;
        
        var enabledServersCount = _gameServers.Count(s => s.Enabled);
        var estimatedServerHeight = Math.Max(5, enabledServersCount + 4); // Table header + borders + rows
        
        // Calculate remaining space for activity
        var usedHeight = headerHeight + estimatedServerHeight + errorHeight;
        var remainingHeight = Math.Max(7, consoleHeight - usedHeight - 1); // -1 for buffer
        var maxActivityLines = Math.Max(3, Math.Min(10, remainingHeight - 3)); // -3 for panel borders/header
        
        // Build a vertical stack of panels
        var panels = new List<IRenderable>
        {
            CreateHeader(),
            CreateServersPanel(),
            CreateActivityPanel(maxActivityLines)
        };
        
        if (hasError)
        {
            panels.Add(CreateErrorPanel());
        }
        
        // Create a grid to stack panels vertically
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadLeft(0).PadRight(0));
        
        foreach (var panel in panels)
        {
            grid.AddRow(panel);
        }
        
        // Wrap in a single layout
        var layout = new Layout("Root");
        layout.Update(grid);
        
        return layout;
    }

    private Panel CreateHeader()
    {
        var headerContent = new Rows(
            new Markup("[cyan bold]Game Server Manager[/] | Press [yellow]CTRL+C[/] to exit"),
            new Markup("[dim]Flags: [green]R[/]=Restart [yellow]U[/]=Update [blue]B[/]=Backup[/]")
        );
        
        return new Panel(headerContent)
            .Border(BoxBorder.Double)
            .BorderColor(Color.Cyan1);
    }

    private Panel CreateServersPanel()
    {
        var enabledServers = _gameServers.Where(s => s.Enabled).ToList();

        if (enabledServers.Count == 0)
        {
            return new Panel("[yellow]No enabled servers configured[/]")
                .Header("[bold]Servers[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Blue);
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddColumn(new TableColumn("[bold]Server[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Build ID[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Update Check[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Last Update[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Last Backup[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Next Backup[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Flags[/]").Centered());

        foreach (var server in enabledServers)
        {
            var serverName = $"[green bold]{server.Name}[/]";
            
            // Build ID display - only for Steam games
            var buildId = GetBuildIdDisplay(server);
            
            // Update check status
            var updateCheckStatus = GetUpdateCheckStatus(server);
            
            var lastUpdate = server.LastUpdateDate?.ToString("MM-dd HH:mm") ?? "[dim]none[/]";
            
            var lastBackup = server.LastBackupDate?.ToString("MM-dd HH:mm") ?? "[dim]none[/]";
            var nextBackup = GetNextScheduledTime(server.AutoBackup, server.AutoBackupTime);
            
            var flags = BuildFlags(server);

            table.AddRow(
                serverName,
                buildId,
                updateCheckStatus,
                lastUpdate,
                lastBackup,
                nextBackup,
                flags
            );
        }

        return new Panel(table)
            .Header("[bold blue]Active Servers[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue);
    }

    private string GetBuildIdDisplay(GameServer server)
    {
        // Non-Steam apps don't have build IDs
        if (string.IsNullOrWhiteSpace(server.SteamAppId))
        {
            return "[dim]N/A[/]";
        }
        
        // Steam games with discovered build ID
        if (server.CurrentBuildId.HasValue)
        {
            return $"[cyan]{server.CurrentBuildId}[/]";
        }
        
        // Steam games with AutoUpdate enabled but build ID not yet discovered
        if (server.AutoUpdate)
        {
            return "[dim]discovering...[/]";
        }
        
        // Steam games with AutoUpdate disabled
        return "[dim]unknown[/]";
    }

    private string BuildFlags(GameServer server)
    {
        var flags = new List<string>();
        if (server.AutoRestart) flags.Add("[green]R[/]");
        if (server.AutoUpdate) flags.Add("[yellow]U[/]");
        if (server.AutoBackup) flags.Add("[blue]B[/]");
        return string.Join(" ", flags);
    }

    private Panel CreateActivityPanel(int maxLines)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap());

        // Get recent actions in reverse order (most recent first)
        var actions = _recentActions.Reverse().Take(maxLines).ToArray();
        
        if (actions.Length == 0)
        {
            grid.AddRow("[dim]No recent activity[/]");
        }
        else
        {
            foreach (var action in actions)
            {
                grid.AddRow(action);
            }
            
            // Show indicator if there are more items not displayed
            var totalActions = _recentActions.Count;
            if (totalActions > maxLines)
            {
                grid.AddRow($"[dim italic]({totalActions - maxLines} older activities hidden)[/]");
            }
        }

        return new Panel(grid)
            .Header("[yellow bold]Recent Activity[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow);
    }

    private Panel CreateErrorPanel()
    {
        if (_errorQueue.TryPeek(out var lastError))
        {
            return new Panel($"[red bold]? Error:[/] {lastError}")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Red);
        }
        
        return new Panel(new Text(""))
            .Border(BoxBorder.None);
    }

    private string GetNextScheduledTime(bool enabled, string schedule)
    {
        if (!enabled || string.IsNullOrWhiteSpace(schedule))
            return "[dim]disabled[/]";

        var now = DateTime.Now;

        // Try CRON
        try
        {
            var cron = NCrontab.CrontabSchedule.Parse(schedule);
            var next = cron.GetNextOccurrence(now);
            return $"[cyan]{next:MM-dd HH:mm}[/]";
        }
        catch { }

        // Try time format
        if (DateTime.TryParseExact(schedule, new[] { "HH:mm", "hh:mm tt", "H:mm", "h:mm tt" },
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var scheduledTime))
        {
            var scheduledToday = new DateTime(now.Year, now.Month, now.Day, scheduledTime.Hour, scheduledTime.Minute, 0);
            if (scheduledToday > now)
                return $"[cyan]{scheduledToday:MM-dd HH:mm}[/]";
            else
                return $"[cyan]{scheduledToday.AddDays(1):MM-dd HH:mm}[/]";
        }

        return "[red]invalid[/]";
    }

    private string GetUpdateCheckStatus(GameServer server)
    {
        // Non-Steam apps or AutoUpdate disabled
        if (!server.AutoUpdate || string.IsNullOrWhiteSpace(server.SteamAppId))
        {
            return "[dim]disabled[/]";
        }

        // Try to get LastUpdateCheck from the server
        if (server.LastUpdateCheck.HasValue)
        {
            var timeSinceCheck = DateTime.Now - server.LastUpdateCheck.Value;
            
            // Show actual time elapsed, not "checking..." after check completes
            if (timeSinceCheck.TotalMinutes < 60)
            {
                return $"[cyan]{(int)timeSinceCheck.TotalMinutes}m ago[/]";
            }
            else if (timeSinceCheck.TotalHours < 24)
            {
                return $"[yellow]{(int)timeSinceCheck.TotalHours}h ago[/]";
            }
            else
            {
                return $"[red]{(int)timeSinceCheck.TotalDays}d ago[/]";
            }
        }
        
        // No LastUpdateCheck yet - either first startup or check failed
        // If we have a build ID, we're probably just waiting for the first check
        if (server.CurrentBuildId.HasValue)
        {
            return "[dim]pending...[/]";
        }
        
        return "[dim]pending[/]";
    }

    public async ValueTask DisposeAsync()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        await Task.CompletedTask;
    }
}
