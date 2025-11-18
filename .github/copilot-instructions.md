# GitHub Copilot Custom Instructions for GameServerManager

## Project Overview
- This repository is a C#/.NET 9 command-line utility for managing Steam-based game servers on Windows.
- Key features: server watchdog, auto backup, auto update (with CRON or time scheduling), multi-server support, and email notifications.
- Configuration is via `appsettings.json` or an interactive CLI (`GameServerManager config`).
- Logging is handled by Serilog to file only.
- **UI Framework**: Uses Spectre.Console for rich, modern terminal UI with live updates.

## Coding Guidelines for Copilot
- Use C# 13.0 and .NET 9 features where appropriate.
- Follow the existing code style: use explicit types, prefer `var` only when type is obvious, and use PascalCase for public members.
- All configuration should be compatible with both JSON and CLI flows.
- When adding new features, ensure they are accessible/configurable via both `appsettings.json` and the interactive CLI.
- Use dependency injection for new services/components.
- Logging should use the existing Serilog/Microsoft.Extensions.Logging setup.
- For scheduling, support both CRON and time-of-day formats (see `ServerUpdater.cs` for helpers).
- For notifications, extend the `NotificationManager` and `INotificationProvider` pattern.
- All new code should be thread-safe if it interacts with shared state (see use of `ConcurrentQueue`).
- When editing or adding files, update the README and configuration examples if user-facing changes are made.

## UI Architecture
- **Spectre.Console** is used for all UI rendering (both status dashboard and configuration CLI).
- The `ConsoleUI` class in `UI/ConsoleUI.cs` handles the live status dashboard using `AnsiConsole.Live()`.
- The `ConfigurationCLI` class in `UI/ConfigurationCLI.cs` handles interactive configuration with prompts and menus.
- **DO NOT** use manual `Console.SetCursorPosition`, `Console.Clear()`, or similar low-level console operations.
- Use Spectre.Console components: `Table`, `Panel`, `Layout`, `SelectionPrompt`, `TextPrompt`, etc.
- For colors, use Spectre.Console markup (e.g., `[green]text[/]`, `[red bold]error[/]`) or `Color` enum values.
- UI updates are centralized through `Program.LogWithStatus()` which routes to both file logging and the ConsoleUI.

## Directory Structure
- All main code is in the `GameServerManager` project directory.
- UI components are in `GameServerManager/UI/` subdirectory.
- `.github/copilot-instructions.md` contains these instructions.
- `README.MD` provides user-facing documentation and configuration examples.
- `appsettings.json` is the main configuration file. The current contents is just an example configuration.

## Best Practices
- Always validate configuration before use (see `ConfigurationValidator`).
- Use async/await for I/O-bound operations (e.g., backups, updates).
- Prefer composition over inheritance for extensibility.
- Keep the CLI and JSON configuration in sync.
- Write clear, user-friendly log and error messages.
- Use Spectre.Console's built-in validation for user inputs in the CLI.
- When adding new dashboard elements, update the `ConsoleUI.CreateDashboard()` layout.
- When adding new configuration options, update both `ConfigurationCLI.PromptGameServer()` and the Settings model.

## Dependencies
- **Spectre.Console**: Rich terminal UI library - use for all console rendering
- **Serilog**: Logging framework - use for file logging only
- **NCrontab**: CRON expression parsing - use for schedule validation and parsing
- **Microsoft.Extensions.Configuration**: Configuration management
- **Microsoft.Extensions.DependencyInjection**: DI container
- **Microsoft.Extensions.Logging**: Logging abstraction

## As the Project Evolves
- **Update this file** (`copilot-instructions.md`) with new instructions, patterns, or architectural changes as the project evolves.
- If adding new UI components, follow the Spectre.Console patterns in `UI/ConsoleUI.cs` and `UI/ConfigurationCLI.cs`.
- If adding new configuration options, ensure they work in both JSON and interactive CLI modes.
- Reference: https://docs.github.com/en/copilot/customizing-copilot/adding-repository-custom-instructions-for-github-copilot

---

_Last updated: 2025-01-14_
