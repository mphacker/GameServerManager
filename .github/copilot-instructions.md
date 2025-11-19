# GitHub Copilot Custom Instructions for GameServerManager

## Project Overview
- This repository is a C#/.NET 10 command-line utility for managing Steam-based game servers on Windows.
- Key features: server watchdog, auto backup, auto update (with CRON or time scheduling), multi-server support, and email notifications.
- Configuration is via `appsettings.json` or an interactive CLI (`GameServerManager config`).
- Logging is handled by Serilog to file only.
- **UI Framework**: Uses Spectre.Console for rich, modern terminal UI with live updates.

## Coding Guidelines for Copilot
- Use C# 14.0 and .NET 10 features where appropriate.
- Follow the existing code style: use explicit types, prefer `var` only when type is obvious, and use PascalCase for public members.
- All configuration should be compatible with both JSON and CLI flows.
- When adding new features, ensure they are accessible/configurable via both `appsettings.json` and the interactive CLI.
- Use dependency injection for new services/components.
- Logging should use the existing Serilog/Microsoft.Extensions.Logging setup.
- For scheduling, support both CRON and time-of-day formats (see `ServerUpdater.cs` for helpers).
- For notifications, extend the `NotificationManager` and `INotificationProvider` pattern.
- All new code should be thread-safe if it interacts with shared state (see use of `ConcurrentQueue`).
- When editing or adding files, update the README and configuration examples if user-facing changes are made.

## Exception Handling (CRITICAL)
- **All file I/O operations must be wrapped in try-catch blocks** with specific exception types (IOException, UnauthorizedAccessException, JsonException).
- **Timer callbacks must be protected** - wrap entire async methods called from timers in try-catch to prevent app crashes.
- **Process operations must handle Win32Exception and InvalidOperationException** explicitly.
- **Always log exceptions** using Serilog with full details: `_logger.LogError(ex, "Message: {Message}", ex.Message)`.
- **Graceful degradation** - return defaults, skip operations, or retry instead of throwing.
- **Never throw from async void methods** - use Task return types for all async methods.
- **Validate inputs before operations** - check for null, empty strings, file existence, etc.
- See `EXCEPTION_HANDLING_AUDIT.md` for comprehensive patterns and examples.

## UI Architecture
- **Spectre.Console** is used for all UI rendering (both status dashboard and configuration CLI).
- The `ConsoleUI` class in `UI/ConsoleUI.cs` handles the live status dashboard using `AnsiConsole.Live()`.
- **Keyboard input** is handled in a background task in `ConsoleUI.ListenForKeyboard()` - must handle InvalidOperationException when console is redirected.
- The `ConfigurationCLI` class in `UI/ConfigurationCLI.cs` handles interactive configuration with prompts and menus.
- The `InteractiveMenu` class in `UI/InteractiveMenu.cs` handles manual operations accessible via 'M' key.
- **DO NOT** use manual `Console.SetCursorPosition`, `Console.Clear()`, or similar low-level console operations.
- Use Spectre.Console components: `Table`, `Panel`, `Layout`, `SelectionPrompt`, `TextPrompt`, `Status`, etc.
- For colors, use Spectre.Console markup (e.g., `[green]text[/]`, `[red bold]error[/]`) or `Color` enum values.
- UI updates are centralized through `Program.LogWithStatus()` which routes to both file logging and the ConsoleUI.
- **Dashboard loop must have exception handling** with auto-restart capability on errors.

## Interactive Operations
- **Menu system** accessible via 'M' key during dashboard operation (see `InteractiveMenu` class).
- All manual operations (Force Backup, Force Update Check) must:
  - Filter servers by capability (only show servers with relevant features enabled)
  - Provide confirmation prompts before destructive operations
  - Show operation progress with Spectre.Console spinners
  - Handle errors gracefully and allow return to menu
- Manual operations should reuse the same underlying methods as automatic operations (e.g., `BackupServerAsync()`).
- **Expose necessary properties/methods for manual operations** - see `ServerUpdater.GameServer` property and `ForceUpdateCheckNowAsync()` method.

## Concurrency & Threading
- Use `ConcurrentQueue` for thread-safe collections shared across threads (see `ConsoleUI._recentActions`).
- Use `Interlocked` operations for atomic flag checks (see `ServerUpdater._updateInProgress`, `_isCheckingForUpdate`).
- Timer callbacks run on background threads - ensure thread safety when accessing shared state.
- Keyboard listener runs on background thread - handle console I/O exceptions gracefully.
- Use `CancellationToken` for graceful shutdown (see `Program.Main` and `ConsoleUI.StartDashboardAsync`).
- Dispose async resources properly using `IAsyncDisposable` pattern (see `ServerUpdater`, `Watchdog`).

## State Management
- **In-memory state** is tracked in GameServer objects (CurrentBuildId, LastUpdateCheck, LastBackupDate, etc.).
- **Persisted state** is saved to `appsettings.json` via `AppSettingsHelper` methods.
- All state updates must be resilient to file I/O failures - updates are optional enhancements, not critical to operation.
- State dictionaries for manual operations: `_serverUpdaters` and `_watchdogs` in `Program.cs` keyed by server name.
- UI state is ephemeral and rebuilt on each dashboard refresh cycle.

## Directory Structure
- All main code is in the `GameServerManager` project directory.
- UI components are in `GameServerManager/UI/` subdirectory.
- `.github/copilot-instructions.md` contains these instructions.
- `README.MD` provides user-facing documentation and configuration examples.
- `appsettings.json` is the main configuration file. The current contents is just an example configuration.
- `EXCEPTION_HANDLING_AUDIT.md` documents exception handling patterns and standards.
- `MANUAL_OPERATIONS_FEATURE.md` documents the interactive menu system.

## Best Practices
- Always validate configuration before use (see `ConfigurationValidator`).
- Use async/await for I/O-bound operations (e.g., backups, updates).
- **All file I/O must have exception handling** - never assume file operations will succeed.
- **All timer callbacks must be wrapped in try-catch** - unhandled exceptions in timer callbacks crash the app.
- **All process operations must validate paths and handle Win32 exceptions**.
- Prefer composition over inheritance for extensibility.
- Keep the CLI and JSON configuration in sync.
- Write clear, user-friendly log and error messages.
- Use Spectre.Console's built-in validation for user inputs in the CLI.
- When adding new dashboard elements, update the `ConsoleUI.CreateDashboard()` layout.
- When adding new configuration options, update both `ConfigurationCLI.PromptGameServer()` and the Settings model.

## Dependencies
- **Spectre.Console**: Rich terminal UI library - use for all console rendering, menus, prompts, and status indicators
- **Serilog**: Logging framework - use for file logging only (no console output)
- **NCrontab**: CRON expression parsing - use for schedule validation and parsing
- **Microsoft.Extensions.Configuration**: Configuration management
- **Microsoft.Extensions.DependencyInjection**: DI container
- **Microsoft.Extensions.Logging**: Logging abstraction layer over Serilog
- **System.IO.Compression**: ZIP file creation for backups

## Testing Considerations
- **Manual testing scenarios**: File I/O failures, process crashes, network disconnections, invalid configurations.
- **Test exception paths**: Lock files, delete executables, invalid JSON, missing permissions.
- **UI testing**: Console resize, input redirection, rapid key presses.
- **Concurrency testing**: Multiple servers updating/backing up simultaneously.
- **Recovery testing**: Kill processes, delete files, network failures during operations.

## As the Project Evolves
- **Update this file** (`copilot-instructions.md`) with new instructions, patterns, or architectural changes as the project evolves.
- If adding new UI components, follow the Spectre.Console patterns in `UI/ConsoleUI.cs`, `UI/ConfigurationCLI.cs`, and `UI/InteractiveMenu.cs`.
- If adding new configuration options, ensure they work in both JSON and interactive CLI modes.
- **If adding file I/O, process operations, or timer callbacks**, ensure proper exception handling (see Exception Handling section).
- **When adding manual operations**, update `InteractiveMenu.cs` and ensure proper server filtering.
- **Update exception handling documentation** if new patterns or exception types are encountered.
- Reference: https://docs.github.com/en/copilot/customizing-copilot/adding-repository-custom-instructions-for-github-copilot

---

_Last updated: 2025-01-19_
