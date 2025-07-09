using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServerManager
{
    public class Settings
    {
        public string SteamCMDPath { get; set; } = string.Empty;
        public List<GameServer>? GameServers { get; set; }

    }

    public class GameServer
    {
        public string Name { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string GamePath { get; set; } = string.Empty;
        public string ServerExe { get; set; } = string.Empty;
        public string ServerArgs { get; set; } = string.Empty;
        public string SteamAppId { get; set; } = string.Empty;
        public bool AutoRestart { get; set; } = false;
        public bool AutoUpdate { get; set; } = false;
        public string AutoUpdateTime { get; set; } = "05:30 AM";
        public bool AutoBackup { get; set; } = false;
        public string AutoBackupTime { get; set; } = "05:30 AM";
        public string AutoBackupSource { get; set; } = string.Empty;
        public string AutoBackupDest { get; set; } = string.Empty;
        public int AutoBackupsToKeep { get; set; } = 30;
        public bool BackupWithoutShutdown { get; set; } = false;
    }

    public interface IFileSystem
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        string[] GetFiles(string path, string searchPattern);
        void DeleteFile(string path);
    }

    public class FileSystem : IFileSystem
    {
        public bool FileExists(string path) => System.IO.File.Exists(path);
        public bool DirectoryExists(string path) => System.IO.Directory.Exists(path);
        public string[] GetFiles(string path, string searchPattern) => System.IO.Directory.GetFiles(path, searchPattern);
        public void DeleteFile(string path) => System.IO.File.Delete(path);
    }
}
