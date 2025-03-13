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
        public string AutoUpdateBackupTime { get; set; } = "05:30 AM";
        public bool AutoBackup { get; set; } = false;
        public string AutoBackupSource { get; set; } = string.Empty;
        public string AutoBackupDest { get; set; } = string.Empty;
        public int AutoBackupDaysToKeep { get; set; } = 30;

    }
}
