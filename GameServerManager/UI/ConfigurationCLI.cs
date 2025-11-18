using Spectre.Console;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace GameServerManager.UI;

/// <summary>
/// Interactive configuration CLI using Spectre.Console.
/// </summary>
public class ConfigurationCLI
{
    private readonly string _configPath;

    public ConfigurationCLI()
    {
        _configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
    }

    public void Run()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("Game Server Manager").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[dim]Configuration Editor[/]\n");

        Settings settings;
        NotificationSettings notificationSettings;

        // Load existing configuration
        if (File.Exists(_configPath))
        {
            var json = File.ReadAllText(_configPath);
            var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.TryGetProperty("Notification", out var notifElem))
            {
                notificationSettings = JsonSerializer.Deserialize<NotificationSettings>(
                    notifElem.GetRawText(), 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new NotificationSettings();
            }
            else
            {
                notificationSettings = new NotificationSettings();
            }

            settings = JsonSerializer.Deserialize<Settings>(json, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Settings();
        }
        else
        {
            settings = new Settings { GameServers = new List<GameServer>() };
            notificationSettings = new NotificationSettings();
        }

        settings.GameServers ??= new List<GameServer>();

        // Main menu loop
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[cyan]GameServerManager Configuration[/]").RuleStyle("grey").LeftJustified());
            AnsiConsole.WriteLine();

            var choices = new List<string>
            {
                "Set SteamCMD Path",
                "Add Game Server",
                "Edit Game Server",
                "Remove Game Server",
                "List Game Servers",
                "Configure Notifications"
            };

            // Add test notification option if enabled
            bool anyNotificationEnabled = notificationSettings.EnableEmail;
            if (anyNotificationEnabled)
                choices.Add("Send Test Notification");

            choices.Add("Save and Exit");
            choices.Add("Exit without Saving");

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]What would you like to do?[/]")
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                    .AddChoices(choices));

            switch (choice)
            {
                case "Set SteamCMD Path":
                    SetSteamCMDPath(settings);
                    break;
                case "Add Game Server":
                    settings.GameServers.Add(PromptGameServer());
                    break;
                case "Edit Game Server":
                    EditGameServer(settings.GameServers);
                    break;
                case "Remove Game Server":
                    RemoveGameServer(settings.GameServers);
                    break;
                case "List Game Servers":
                    ListGameServers(settings.GameServers);
                    AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
                    Console.ReadKey(true);
                    break;
                case "Configure Notifications":
                    ConfigureNotifications(notificationSettings);
                    break;
                case "Send Test Notification":
                    SendTestNotification(notificationSettings);
                    break;
                case "Save and Exit":
                    SaveConfiguration(settings, notificationSettings);
                    AnsiConsole.MarkupLine("\n[green][[OK]] Configuration saved successfully![/]");
                    Thread.Sleep(1000);
                    return;
                case "Exit without Saving":
                    if (AnsiConsole.Confirm("[red]Are you sure you want to exit without saving?[/]"))
                    {
                        AnsiConsole.MarkupLine("[grey]Exiting without saving...[/]");
                        Thread.Sleep(500);
                        return;
                    }
                    break;
            }
        }
    }

    private void SetSteamCMDPath(Settings settings)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[cyan]SteamCMD Path[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();

        if (!string.IsNullOrWhiteSpace(settings.SteamCMDPath))
            AnsiConsole.MarkupLine($"[grey]Current:[/] {settings.SteamCMDPath}");

        var newPath = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]SteamCMD path[/]:")
                .DefaultValue(settings.SteamCMDPath)
                .AllowEmpty());

        if (!string.IsNullOrWhiteSpace(newPath))
            settings.SteamCMDPath = newPath;
    }

    private GameServer PromptGameServer(GameServer? existing = null)
    {
        var gs = existing ?? new GameServer
        {
            AutoRestart = true,
            AutoUpdate = true,
            AutoBackup = false,
            AutoBackupsToKeep = 30,
            BackupWithoutShutdown = false,
            Enabled = true
        };

        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule(
            existing == null ? "[cyan]Add New Game Server[/]" : $"[cyan]Edit: {gs.Name}[/]")
            .RuleStyle("grey")
            .LeftJustified());
        AnsiConsole.WriteLine();

        gs.Name = AnsiConsole.Ask("Server [green]Name[/]:", gs.Name);
        gs.ProcessName = AnsiConsole.Ask("Process [green]Name[/]:", gs.ProcessName);
        gs.GamePath = AnsiConsole.Ask("Game [green]Path[/]:", gs.GamePath);
        gs.ServerExe = AnsiConsole.Ask("Server [green]Executable[/]:", gs.ServerExe);
        gs.ServerArgs = AnsiConsole.Ask("Server [green]Arguments[/]:", gs.ServerArgs);
        gs.SteamAppId = AnsiConsole.Ask("Steam [green]App ID[/]:", gs.SteamAppId);
        
        gs.Enabled = AnsiConsole.Confirm("[green]Enabled[/]?", gs.Enabled);
        gs.AutoRestart = AnsiConsole.Confirm("[green]Auto-Restart[/]?", gs.AutoRestart);
        gs.AutoUpdate = AnsiConsole.Confirm("[green]Auto-Update[/]?", gs.AutoUpdate);
        
        gs.AutoBackup = AnsiConsole.Confirm("[green]Auto-Backup[/]?", gs.AutoBackup);
        
        if (gs.AutoBackup)
        {
            gs.AutoBackupSource = AnsiConsole.Ask("Backup [green]Source Path[/]:", gs.AutoBackupSource);
            gs.AutoBackupDest = AnsiConsole.Ask("Backup [green]Destination Path[/]:", gs.AutoBackupDest);
            
            gs.AutoBackupTime = AnsiConsole.Prompt(
                new TextPrompt<string>("Auto-Backup [green]Schedule[/] (time or CRON):")
                    .DefaultValue(gs.AutoBackupTime)
                    .Validate(schedule => IsValidTimeOrCron(schedule)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]Invalid time/CRON format[/]")));
            
            gs.AutoBackupsToKeep = AnsiConsole.Ask("Backups to [green]Keep[/]:", gs.AutoBackupsToKeep);
            gs.BackupWithoutShutdown = AnsiConsole.Confirm("[green]Backup Without Shutdown[/]?", gs.BackupWithoutShutdown);
        }

        AnsiConsole.MarkupLine("\n[green]? Server configuration completed[/]");
        Thread.Sleep(1000);
        
        return gs;
    }

    private void EditGameServer(List<GameServer> servers)
    {
        if (servers.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No servers to edit.[/]");
            Thread.Sleep(1500);
            return;
        }

        var serverChoices = servers.Select((s, i) => $"{i + 1}. {s.Name} ({s.GamePath})").ToList();
        serverChoices.Add("<< Back");

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select a server to edit:[/]")
                .PageSize(10)
                .AddChoices(serverChoices));

        if (choice == "<< Back")
            return;

        var idx = serverChoices.IndexOf(choice);
        if (idx >= 0 && idx < servers.Count)
        {
            servers[idx] = PromptGameServer(servers[idx]);
        }
    }

    private void RemoveGameServer(List<GameServer> servers)
    {
        if (servers.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No servers to remove.[/]");
            Thread.Sleep(1500);
            return;
        }

        var serverChoices = servers.Select((s, i) => $"{i + 1}. {s.Name} ({s.GamePath})").ToList();
        serverChoices.Add("<< Back");

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select a server to remove:[/]")
                .PageSize(10)
                .AddChoices(serverChoices));

        if (choice == "<< Back")
            return;

        var idx = serverChoices.IndexOf(choice);
        if (idx >= 0 && idx < servers.Count)
        {
            if (AnsiConsole.Confirm($"[red]Remove '{servers[idx].Name}'?[/]"))
            {
                servers.RemoveAt(idx);
                AnsiConsole.MarkupLine("[green][[OK]] Server removed[/]");
                Thread.Sleep(1000);
            }
        }
    }

    private void ListGameServers(List<GameServer> servers)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[cyan]Configured Servers[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();

        if (servers.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No servers configured.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue);

        table.AddColumn("[bold]#[/]");
        table.AddColumn("[bold]Name[/]");
        table.AddColumn("[bold]Path[/]");
        table.AddColumn("[bold]App ID[/]");
        table.AddColumn("[bold]Enabled[/]");
        table.AddColumn("[bold]Auto-Restart[/]");
        table.AddColumn("[bold]Auto-Update[/]");
        table.AddColumn("[bold]Auto-Backup[/]");

        for (int i = 0; i < servers.Count; i++)
        {
            var s = servers[i];
            table.AddRow(
                $"[grey]{i + 1}[/]",
                $"[green]{s.Name}[/]",
                $"[dim]{s.GamePath}[/]",
                $"[cyan]{s.SteamAppId}[/]",
                s.Enabled ? "[green]Yes[/]" : "[red]No[/]",
                s.AutoRestart ? "[green]Yes[/]" : "[grey]No[/]",
                s.AutoUpdate ? "[green]Yes[/]" : "[grey]No[/]",
                s.AutoBackup ? $"[green]Yes[/] [dim]({s.AutoBackupTime})[/]" : "[grey]No[/]"
            );
        }

        AnsiConsole.Write(table);
    }

    private void ConfigureNotifications(NotificationSettings settings)
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[cyan]Notification Configuration[/]").RuleStyle("grey").LeftJustified());
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]Select notification type:[/]")
                    .AddChoices("Email Notifications", "<< Back"));

            if (choice == "<< Back")
                return;

            ConfigureEmailNotifications(settings);
        }
    }

    private void ConfigureEmailNotifications(NotificationSettings settings)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[cyan]Email Notification Settings[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();

        settings.EnableEmail = AnsiConsole.Confirm("[green]Enable Email Notifications[/]?", settings.EnableEmail);
        
        if (settings.EnableEmail)
        {
            settings.SmtpHost = AnsiConsole.Ask("SMTP [green]Host[/]:", settings.SmtpHost);
            settings.SmtpPort = AnsiConsole.Ask("SMTP [green]Port[/]:", settings.SmtpPort);
            settings.SmtpUser = AnsiConsole.Ask("SMTP [green]Username[/]:", settings.SmtpUser);
            settings.SmtpPass = AnsiConsole.Prompt(
                new TextPrompt<string>("SMTP [green]Password[/]:")
                    .Secret()
                    .DefaultValue(settings.SmtpPass)
                    .AllowEmpty());
            settings.Recipient = AnsiConsole.Ask("Recipient [green]Email[/]:", settings.Recipient);
        }

        AnsiConsole.MarkupLine("\n[green][[OK]] Email settings saved[/]");
        Thread.Sleep(1000);
    }

    private void SendTestNotification(NotificationSettings settings)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[cyan]Send Test Notification[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();

        if (!settings.EnableEmail || string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            AnsiConsole.MarkupLine("[red]Email notifications are not properly configured.[/]");
            Thread.Sleep(2000);
            return;
        }

        AnsiConsole.Status()
            .Start("Sending test email...", ctx =>
            {
                try
                {
                    var provider = new SMTPEmailNotificationProvider(
                        new ConfigurationBuilder()
                            .AddInMemoryCollection(new List<KeyValuePair<string, string?>>
                            {
                                new("Notification:SmtpHost", settings.SmtpHost),
                                new("Notification:SmtpPort", settings.SmtpPort.ToString()),
                                new("Notification:SmtpUser", settings.SmtpUser),
                                new("Notification:SmtpPass", settings.SmtpPass),
                                new("Notification:Recipient", settings.Recipient)
                            })
                            .Build());

                    provider.Notify("Test Notification", "This is a test email from GameServerManager.");
                    
                    AnsiConsole.MarkupLine("[green][[OK]] Test email sent successfully![/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red][[X]] Failed to send test email: {ex.Message}[/]");
                }
            });

        Thread.Sleep(2000);
    }

    private void SaveConfiguration(Settings settings, NotificationSettings notificationSettings)
    {
        AnsiConsole.Status()
            .Start("Saving configuration...", ctx =>
            {
                var merged = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                var mergedDoc = JsonDocument.Parse(merged);
                
                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                
                writer.WriteStartObject();
                foreach (var prop in mergedDoc.RootElement.EnumerateObject())
                    prop.WriteTo(writer);
                
                writer.WritePropertyName("Notification");
                JsonSerializer.Serialize(writer, notificationSettings, new JsonSerializerOptions { WriteIndented = true });
                writer.WriteEndObject();
                writer.Flush();
                
                File.WriteAllText(_configPath, Encoding.UTF8.GetString(stream.ToArray()));
            });
    }

    private bool IsValidTimeOrCron(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Try CRON
        try
        {
            NCrontab.CrontabSchedule.Parse(value);
            return true;
        }
        catch { }

        // Try time
        return DateTime.TryParseExact(value,
            new[] { "HH:mm", "hh:mm tt", "H:mm", "h:mm tt" },
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out _);
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
}
