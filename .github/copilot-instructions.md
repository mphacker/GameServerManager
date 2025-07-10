# GitHub Copilot Custom Instructions for GameServerManager

## Project Overview
- This repository is a C#/.NET 9 command-line utility for managing Steam-based game servers on Windows.
- Key features: server watchdog, auto backup, auto update (with CRON or time scheduling), multi-server support, and email notifications.
- Configuration is via `appsettings.json` or an interactive CLI (`GameServerManager config`).
- Logging is handled by Serilog to file only.

## Coding Guidelines for Copilot
- Use C# 13.0 and .NET 9 features where appropriate.
- Follow the existing code style: use explicit types, prefer `var` only when type is obvious, and use PascalCase for public members.
- All configuration should be compatible with both JSON and CLI flows.
- When adding new features, ensure they are accessible/configurable via both `appsettings.json` and the CLI.
- Use dependency injection for new services/components.
- Logging should use the existing Serilog/Microsoft.Extensions.Logging setup.
- For scheduling, support both CRON and time-of-day formats (see `Program.cs` for helpers).
- For notifications, extend the `NotificationManager` and `INotificationProvider` pattern.
- All new code should be thread-safe if it interacts with shared state (see use of `ConcurrentQueue`).
- When editing or adding files, update the README and configuration examples if user-facing changes are made.

## Directory Structure
- All main code is in the `GameServerManager` project directory.
- `.github/copilot-instructions.md` contains these instructions.
- `README.MD` provides user-facing documentation and configuration examples.
- `appsettings.json` is the main configuration file. The current contents is just an example configuration.

## Best Practices
- Always validate configuration before use (see `ConfigurationValidator`).
- Use async/await for I/O-bound operations (e.g., backups, updates).
- Prefer composition over inheritance for extensibility.
- Keep the CLI and JSON configuration in sync.
- Write clear, user-friendly log and error messages.

## As the Project Evolves
- **Update this file** (`copilot-instructions.md`) with new instructions, patterns, or architectural changes as the project evolves.
- Reference: https://docs.github.com/en/copilot/customizing-copilot/adding-repository-custom-instructions-for-github-copilot

---

_Last updated: 2024-06_
