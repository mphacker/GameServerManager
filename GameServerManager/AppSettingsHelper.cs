using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GameServerManager
{
    public static class AppSettingsHelper
    {
        private static readonly string ConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

        public static void UpdateServerLastDates(string serverName, DateTime? lastUpdate, DateTime? lastBackup)
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var settings = JsonSerializer.Deserialize<Settings>(json, options) ?? new Settings();
            if (settings.GameServers == null) return;
            var server = settings.GameServers.FirstOrDefault(s => s.Name == serverName);
            if (server == null) return;
            if (lastUpdate.HasValue)
                server.LastUpdateDate = lastUpdate;
            if (lastBackup.HasValue)
                server.LastBackupDate = lastBackup;
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
