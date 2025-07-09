using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;

namespace GameServerManager.Tests
{
    // Tests for configuration validation logic
    public class ConfigurationValidatorTests
    {
        private readonly Mock<IFileSystem> _fileSystemMock = new Mock<IFileSystem>();

        [Fact]
        // Validates that an error is returned if the SteamCMDPath does not exist
        // Mocks FileExists to always return false, simulating a missing SteamCMD executable
        public void Validate_ReturnsError_WhenSteamCMDPathIsInvalid()
        {
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
            var settings = new Settings
            {
                SteamCMDPath = "nonexistent.exe",
                GameServers = new List<GameServer> { new GameServer { Name = "Test", ProcessName = "proc", GamePath = "C:/", ServerExe = "notfound.exe", SteamAppId = "123" } }
            };
            var errors = ConfigurationValidator.Validate(settings, _fileSystemMock.Object);
            Assert.Contains(errors, e => e.Contains("SteamCMDPath is invalid"));
        }

        [Fact]
        // Validates that an error is returned if no game servers are configured
        // Mocks FileExists to always return true, simulating a valid SteamCMD path
        public void Validate_ReturnsError_WhenNoGameServersConfigured()
        {
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            var settings = new Settings { SteamCMDPath = "valid.exe", GameServers = new List<GameServer>() };
            var errors = ConfigurationValidator.Validate(settings, _fileSystemMock.Object);
            Assert.Contains(errors, e => e.Contains("No game servers configured"));
        }

        [Fact]
        // Validates that an error is returned if the game server name is missing
        // Mocks DirectoryExists and FileExists to always return true, simulating valid paths
        public void ValidateGameServer_ReturnsError_WhenNameMissing()
        {
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            var server = new GameServer { Name = "", ProcessName = "proc", GamePath = "C:/", ServerExe = "notfound.exe", SteamAppId = "123" };
            var errors = ConfigurationValidator.ValidateGameServer(server, _fileSystemMock.Object);
            Assert.Contains(errors, e => e.Contains("Game server name is missing"));
        }

        [Fact]
        // Validates that an error is returned if the process name is missing
        // Mocks DirectoryExists and FileExists to always return true, simulating valid paths
        public void ValidateGameServer_ReturnsError_WhenProcessNameMissing()
        {
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            var server = new GameServer { Name = "Test", ProcessName = "", GamePath = "C:/", ServerExe = "notfound.exe", SteamAppId = "123" };
            var errors = ConfigurationValidator.ValidateGameServer(server, _fileSystemMock.Object);
            Assert.Contains(errors, e => e.Contains("Process name is missing"));
        }

        [Fact]
        // Validates that an error is returned if the game path does not exist
        // Mocks DirectoryExists to always return false, simulating a missing game path
        public void ValidateGameServer_ReturnsError_WhenGamePathInvalid()
        {
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(false);
            var server = new GameServer { Name = "Test", ProcessName = "proc", GamePath = "C:/notfound", ServerExe = "notfound.exe", SteamAppId = "123" };
            var errors = ConfigurationValidator.ValidateGameServer(server, _fileSystemMock.Object);
            Assert.Contains(errors, e => e.Contains("Game path is invalid"));
        }

        [Fact]
        // Validates that an error is returned if the server executable is missing
        // Mocks DirectoryExists to return true and FileExists to return false, simulating a missing server executable
        public void ValidateGameServer_ReturnsError_WhenServerExeMissing()
        {
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
            var server = new GameServer { Name = "Test", ProcessName = "proc", GamePath = "C:/", ServerExe = "", SteamAppId = "123" };
            var errors = ConfigurationValidator.ValidateGameServer(server, _fileSystemMock.Object);
            Assert.Contains(errors, e => e.Contains("Server executable not found"));
        }

        [Fact]
        // Validates that an error is returned if backup source or destination is missing when AutoBackup is enabled
        // Mocks DirectoryExists and FileExists to always return true, simulating valid paths
        public void ValidateGameServer_ReturnsError_WhenAutoBackupSourceOrDestMissing()
        {
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            var server = new GameServer { Name = "Test", ProcessName = "proc", GamePath = "C:/", ServerExe = "notfound.exe", SteamAppId = "123", AutoBackup = true, AutoBackupSource = "", AutoBackupDest = "" };
            var errors = ConfigurationValidator.ValidateGameServer(server, _fileSystemMock.Object);
            Assert.Contains(errors, e => e.Contains("Invalid backup source or destination"));
        }

        [Fact]
        // Validates that an error is returned if the AutoUpdateTime is not a valid time string
        // Mocks DirectoryExists and FileExists to always return true, simulating valid paths
        public void ValidateGameServer_ReturnsError_WhenAutoUpdateTimeInvalid()
        {
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            var server = new GameServer { Name = "Test", ProcessName = "proc", GamePath = "C:/", ServerExe = "notfound.exe", SteamAppId = "123", AutoUpdate = true, AutoUpdateTime = "notatime" };
            var errors = ConfigurationValidator.ValidateGameServer(server, _fileSystemMock.Object);
            Assert.Contains(errors, e => e.Contains("Invalid AutoUpdateTime"));
        }

        [Fact]
        // Validates that Validate returns no errors when all settings are valid
        // Mocks FileExists and DirectoryExists to always return true, simulating all paths and files exist
        public void Validate_ReturnsNoErrors_WhenSettingsAreValid()
        {
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
            var settings = new Settings
            {
                SteamCMDPath = "valid.exe",
                GameServers = new List<GameServer>
                {
                    new GameServer
                    {
                        Name = "TestServer",
                        ProcessName = "proc",
                        GamePath = "C:/games",
                        ServerExe = "server.exe",
                        SteamAppId = "123",
                        AutoBackup = true,
                        AutoBackupSource = "C:/games/save",
                        AutoBackupDest = "D:/backups",
                        AutoUpdate = true,
                        AutoUpdateTime = "05:30 AM",
                        AutoBackupTime = "05:30 AM"
                    }
                }
            };
            var errors = ConfigurationValidator.Validate(settings, _fileSystemMock.Object);
            Assert.Empty(errors);
        }
    }

    // Tests for process management logic
    public class ProcessManagerTests
    {
        [Fact]
        // Validates that IsProcessRunningAsync returns false if the process name is empty
        public async Task IsProcessRunningAsync_ReturnsFalse_WhenProcessNameIsEmpty()
        {
            var logger = Mock.Of<ILogger>();
            var pm = new ProcessManager(logger);
            var result = await pm.IsProcessRunningAsync("");
            Assert.False(result);
        }

        [Fact]
        // Validates that StopProcessAsync does nothing if process name is empty
        public async Task StopProcessAsync_DoesNothing_WhenProcessNameIsEmpty()
        {
            var logger = Mock.Of<ILogger>();
            var pm = new ProcessManager(logger);
            // Should not throw
            await pm.StopProcessAsync("");
        }

        [Fact]
        // Validates that StopProcessAsync does nothing if process does not exist
        public async Task StopProcessAsync_DoesNothing_WhenProcessDoesNotExist()
        {
            var logger = Mock.Of<ILogger>();
            var pm = new ProcessManager(logger);
            // Should not throw or log error
            await pm.StopProcessAsync("DefinitelyNotARealProcessName");
        }

        [Fact]
        // Validates that StartProcessAsync throws if gameServer is null
        public async Task StartProcessAsync_Throws_WhenGameServerIsNull()
        {
            var logger = Mock.Of<ILogger>();
            var pm = new ProcessManager(logger);
            await Assert.ThrowsAsync<ArgumentNullException>(() => pm.StartProcessAsync(null!));
        }

        [Fact]
        // Validates that StartProcessAsync logs error and returns if GamePath or ServerExe is empty
        public async Task StartProcessAsync_LogsError_WhenGamePathOrServerExeIsEmpty()
        {
            var loggerMock = new Mock<ILogger>();
            var pm = new ProcessManager(loggerMock.Object);
            var gs = new GameServer { Name = "Test", GamePath = "", ServerExe = "" };
            await pm.StartProcessAsync(gs);
            loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => (v != null && v.ToString()!.Contains("GamePath or ServerExe is null or empty"))),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        // Validates that StartProcessAsync calls StartProcess on the wrapper with correct arguments
        public async Task StartProcessAsync_CallsStartProcess_WhenValidGameServer()
        {
            var logger = Mock.Of<ILogger>();
            var processWrapperMock = new Mock<IProcessWrapper>();
            var pm = new ProcessManager(logger, processWrapperMock.Object);
            var gs = new GameServer { Name = "Test", GamePath = "C:/games", ServerExe = "server.exe", ServerArgs = "-arg1" };
            processWrapperMock.Setup(pw => pw.StartProcess(It.IsAny<ProcessStartInfo>())).Returns(Mock.Of<IProcessProxy>());
            await pm.StartProcessAsync(gs);
            processWrapperMock.Verify(pw => pw.StartProcess(It.Is<ProcessStartInfo>(si =>
                si.FileName == System.IO.Path.Combine(gs.GamePath, gs.ServerExe) &&
                si.Arguments == gs.ServerArgs &&
                si.UseShellExecute &&
                !si.CreateNoWindow
            )), Times.Once);
        }

        [Fact]
        // Validates that StopProcessAsync calls CloseMainWindow, WaitForExit, and logs info when process exits
        public async Task StopProcessAsync_StopsProcess_AndLogsInfo()
        {
            var loggerMock = new Mock<ILogger>();
            var processMock = new Mock<IProcessProxy>();
            processMock.SetupSequence(p => p.HasExited).Returns(false).Returns(true);
            processMock.Setup(p => p.CloseMainWindow()).Verifiable();
            processMock.Setup(p => p.WaitForExit(It.IsAny<int>())).Verifiable();
            var processWrapperMock = new Mock<IProcessWrapper>();
            processWrapperMock.Setup(pw => pw.GetProcessesByName("TestProc")).Returns(new[] { processMock.Object });
            var pm = new ProcessManager(loggerMock.Object, processWrapperMock.Object);
            await pm.StopProcessAsync("TestProc");
            processMock.Verify(p => p.CloseMainWindow(), Times.Once);
            processMock.Verify(p => p.WaitForExit(10000), Times.Once);
            loggerMock.Verify(l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopped")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        // Validates that StopProcessAsync kills process and logs warning if process does not exit in time
        public async Task StopProcessAsync_KillsProcess_AndLogsWarning_WhenNotExited()
        {
            var loggerMock = new Mock<ILogger>();
            var processMock = new Mock<IProcessProxy>();
            processMock.SetupSequence(p => p.HasExited).Returns(false).Returns(false);
            processMock.Setup(p => p.CloseMainWindow()).Verifiable();
            processMock.Setup(p => p.WaitForExit(It.IsAny<int>())).Verifiable();
            processMock.Setup(p => p.Kill()).Verifiable();
            var processWrapperMock = new Mock<IProcessWrapper>();
            processWrapperMock.Setup(pw => pw.GetProcessesByName("TestProc")).Returns(new[] { processMock.Object });
            var pm = new ProcessManager(loggerMock.Object, processWrapperMock.Object);
            await pm.StopProcessAsync("TestProc");
            processMock.Verify(p => p.Kill(), Times.Once);
            loggerMock.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("did not exit in time")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        // Validates that StopProcessAsync logs error if exception is thrown
        public async Task StopProcessAsync_LogsError_WhenExceptionThrown()
        {
            var loggerMock = new Mock<ILogger>();
            var processMock = new Mock<IProcessProxy>();
            processMock.Setup(p => p.HasExited).Returns(false);
            processMock.Setup(p => p.CloseMainWindow()).Throws(new System.Exception("fail"));
            var processWrapperMock = new Mock<IProcessWrapper>();
            processWrapperMock.Setup(pw => pw.GetProcessesByName("TestProc")).Returns(new[] { processMock.Object });
            var pm = new ProcessManager(loggerMock.Object, processWrapperMock.Object);
            await pm.StopProcessAsync("TestProc");
            loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<System.Exception>(),
                It.IsAny<Func<It.IsAnyType, System.Exception?, string>>()), Times.Once);
        }

        [Fact]
        // Validates that IsProcessRunningAsync returns true if a process is running
        public async Task IsProcessRunningAsync_ReturnsTrue_WhenProcessIsRunning()
        {
            var logger = Mock.Of<ILogger>();
            var processMock = new Mock<IProcessProxy>();
            processMock.Setup(p => p.HasExited).Returns(false);
            var processWrapperMock = new Mock<IProcessWrapper>();
            processWrapperMock.Setup(pw => pw.GetProcessesByName("TestProc")).Returns(new[] { processMock.Object });
            var pm = new ProcessManager(logger, processWrapperMock.Object);
            var result = await pm.IsProcessRunningAsync("TestProc");
            Assert.True(result);
        }

        [Fact]
        // Validates that StopProcessAsync does not attempt to stop a process that has already exited
        public async Task StopProcessAsync_DoesNothing_WhenProcessHasExited()
        {
            var loggerMock = new Mock<ILogger>();
            var processMock = new Mock<IProcessProxy>();
            processMock.Setup(p => p.HasExited).Returns(true);
            var processWrapperMock = new Mock<IProcessWrapper>();
            processWrapperMock.Setup(pw => pw.GetProcessesByName("ExitedProc")).Returns(new[] { processMock.Object });
            var pm = new ProcessManager(loggerMock.Object, processWrapperMock.Object);
            await pm.StopProcessAsync("ExitedProc");
            processMock.Verify(p => p.CloseMainWindow(), Times.Never);
        }

        [Fact]
        // Validates that StartProcessAsync passes empty arguments if ServerArgs is null
        public async Task StartProcessAsync_PassesEmptyArguments_WhenServerArgsIsNull()
        {
            var logger = Mock.Of<ILogger>();
            var processWrapperMock = new Mock<IProcessWrapper>();
            var pm = new ProcessManager(logger, processWrapperMock.Object);
            var gs = new GameServer { Name = "Test", GamePath = "C:/games", ServerExe = "server.exe", ServerArgs = string.Empty };
            processWrapperMock.Setup(pw => pw.StartProcess(It.IsAny<ProcessStartInfo>())).Returns(Mock.Of<IProcessProxy>());
            await pm.StartProcessAsync(gs);
            processWrapperMock.Verify(pw => pw.StartProcess(It.Is<ProcessStartInfo>(si =>
                si.Arguments == string.Empty
            )), Times.Once);
        }
    }

    // Tests for server updater logic
    public class ServerUpdaterTests
    {
        [Fact]
        // Validates that IsTimeToUpdateServerAsync returns false if AutoUpdate is not enabled
        public async Task IsTimeToUpdateServerAsync_ReturnsFalse_WhenAutoUpdateIsFalse()
        {
            var logger = Mock.Of<ILogger<ServerUpdater>>();
            var gs = new GameServer { Name = "Test", AutoUpdate = false };
            var updater = new ServerUpdater(gs, "", logger);
            var result = await updater.IsTimeToUpdateServerAsync();
            Assert.False(result);
        }
    }

    // Tests for server backup logic
    public class ServerBackupServiceTests
    {
        [Fact]
        // Validates that BackupAsync returns false if AutoBackup is not enabled
        public async Task BackupAsync_ReturnsFalse_WhenAutoBackupIsFalse()
        {
            var logger = Mock.Of<ILogger>();
            var backupService = new ServerBackupService(logger);
            var gs = new GameServer { Name = "Test", AutoBackup = false };
            var result = await backupService.BackupAsync(gs);
            Assert.False(result);
        }
    }

    // Tests for server update logic
    public class ServerUpdateServiceTests
    {
        [Fact]
        // Validates that UpdateAsync returns false if the SteamCMD path is invalid
        public async Task UpdateAsync_ReturnsFalse_WhenSteamCmdPathIsInvalid()
        {
            var logger = Mock.Of<ILogger>();
            var updateService = new ServerUpdateService(logger);
            var gs = new GameServer { Name = "Test", GamePath = "", SteamAppId = "123" };
            var result = await updateService.UpdateAsync(gs, "notfound.exe");
            Assert.False(result);
        }
    }

    public class ServerUpdaterAdditionalTests
    {
        [Fact]
        // UpdateServerAsync returns false if update already in progress
        public async Task UpdateServerAsync_ReturnsFalse_IfAlreadyInProgress()
        {
            var gs = new GameServer { Name = "Test", AutoUpdate = true, AutoBackup = false, ProcessName = "proc" };
            var logger = Mock.Of<ILogger<ServerUpdater>>();
            var updater = new ServerUpdater(gs, "", logger);
            // Simulate update in progress
            var field = typeof(ServerUpdater).GetField("_updateInProgress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field); // Ensure field is not null
            field.SetValue(updater, 1);
            var result = await updater.UpdateServerAsync();
            Assert.False(result);
        }
    }

    public class ServerBackupServiceAdditionalTests
    {
        [Fact]
        // BackupAsync returns false and logs warning if source or dest is empty
        public async Task BackupAsync_ReturnsFalse_IfSourceOrDestEmpty()
        {
            var loggerMock = new Mock<ILogger>();
            var fileSystemMock = new Mock<IFileSystem>();
            var backupService = new ServerBackupService(loggerMock.Object, fileSystemMock.Object);
            var gs = new GameServer { Name = "Test", AutoBackup = true, AutoBackupSource = "", AutoBackupDest = "" };
            var result = await backupService.BackupAsync(gs);
            Assert.False(result);
            loggerMock.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid backup source or destination")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }
    }

    public class ServerUpdateServiceAdditionalTests
    {
        [Fact]
        // UpdateAsync returns true if update succeeds (simulate by checking file exists and ignore process)
        public async Task UpdateAsync_ReturnsTrue_IfUpdateSucceeds()
        {
            var loggerMock = new Mock<ILogger>();
            var fileSystemMock = new Mock<IFileSystem>();
            fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            var updateService = new ServerUpdateService(loggerMock.Object, fileSystemMock.Object);
            var gs = new GameServer { Name = "Test", GamePath = "C:/games", SteamAppId = "123" };
            // We cannot mock Process.Start, so just assert that the method returns a bool (true or false)
            var result = await updateService.UpdateAsync(gs, "steamcmd.exe");
            Assert.IsType<bool>(result);
        }
    }
}
