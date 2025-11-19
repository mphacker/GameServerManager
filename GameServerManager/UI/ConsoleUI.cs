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
    private volatile bool _menuRequested = false;

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
        
        // Start keyboard listener task
        var keyboardTask = Task.Run(() => ListenForKeyboard());
        
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
    /// Listens for keyboard input in a background task.
    /// </summary>
    private void ListenForKeyboard()
    {
        try
        {
            while (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.M)
                        {
                            RequestMenu();
                        }
                    }
                    Thread.Sleep(100);
                }
                catch (InvalidOperationException)
                {
                    // Console input is redirected or not available, stop listening
                    break;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Debug(ex, "Error in keyboard listener: {Message}", ex.Message);
                    Thread.Sleep(1000); // Back off on error
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Fatal error in keyboard listener: {Message}", ex.Message);
        }
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
        try
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
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error creating dashboard: {Message}", ex.Message);
            
            // Return a minimal error display
            var errorLayout = new Layout("Root");
            errorLayout.Update(new Panel($"[red]Dashboard Error: {ex.Message}[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Red));
            return errorLayout;
        }
    }

    private Panel CreateHeader()
    {
        var headerContent = new Rows(
            new Markup("[cyan bold]Game Server Manager[/] | Press [yellow]CTRL+C[/] to exit | Press [yellow]M[/] for menu"),
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
            return new Panel($"[red bold][[!]] Error:[/] {lastError}")
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

        // Build status string with last check and next check
        var statusParts = new List<string>();

        // Calculate time since last check and time until next check
        DateTime? lastCheck = server.LastUpdateCheck;
        DateTime? nextCheck = server.NextUpdateCheck;
        DateTime now = DateTime.Now;

        // Part 1: Time since last check
        if (lastCheck.HasValue)
        {
            var timeSinceCheck = now - lastCheck.Value;
            
            if (timeSinceCheck.TotalMinutes < 60)
            {
                statusParts.Add($"[cyan]{(int)timeSinceCheck.TotalMinutes}m ago[/]");
            }
            else if (timeSinceCheck.TotalHours < 24)
            {
                statusParts.Add($"[yellow]{(int)timeSinceCheck.TotalHours}h ago[/]");
            }
            else
            {
                statusParts.Add($"[red]{(int)timeSinceCheck.TotalDays}d ago[/]");
            }

            // Part 2: Next check time (show remaining time)
            if (nextCheck.HasValue)
            {
                var timeUntilCheck = nextCheck.Value - now;
                
                if (timeUntilCheck.TotalMinutes <= 0)
                {
                    // Check is due or overdue
                    statusParts.Add("[dim](checking...)[/]");
                }
                else if (timeUntilCheck.TotalMinutes < 60)
                {
                    // Show remaining minutes in a way that makes the interval clear
                    int remainingMinutes = (int)Math.Ceiling(timeUntilCheck.TotalMinutes);
                    statusParts.Add($"[dim](in {remainingMinutes}m)[/]");
                }
                else if (timeUntilCheck.TotalHours < 24)
                {
                    int remainingHours = (int)Math.Ceiling(timeUntilCheck.TotalHours);
                    statusParts.Add($"[dim](in {remainingHours}h)[/]");
                }
                else
                {
                    statusParts.Add($"[dim]({nextCheck.Value:MM-dd HH:mm})[/]");
                }
            }
        }
        else
        {
            // No LastUpdateCheck yet
            if (nextCheck.HasValue)
            {
                var timeUntilCheck = nextCheck.Value - now;
                
                if (timeUntilCheck.TotalMinutes <= 0)
                {
                    statusParts.Add("[dim]checking...[/]");
                }
                else if (timeUntilCheck.TotalMinutes < 60)
                {
                    int remainingMinutes = (int)Math.Ceiling(timeUntilCheck.TotalMinutes);
                    statusParts.Add($"[dim]pending (in {remainingMinutes}m)[/]");
                }
                else
                {
                    statusParts.Add("[dim]pending...[/]");
                }
            }
            else if (server.CurrentBuildId.HasValue)
            {
                statusParts.Add("[dim]pending...[/]");
            }
            else
            {
                statusParts.Add("[dim]pending[/]");
            }
        }

        return string.Join(" ", statusParts);
    }

    public async ValueTask DisposeAsync()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Requests the menu to be shown (sets a flag that the dashboard loop will detect).
    /// </summary>
    public void RequestMenu()
    {
        _menuRequested = true;
        StopDashboard();
    }

    /// <summary>
    /// Checks if menu was requested and resets the flag.
    /// </summary>
    public bool IsMenuRequested()
    {
        if (_menuRequested)
        {
            _menuRequested = false;
            return true;
        }
        return false;
    }
}
