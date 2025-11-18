using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameServerManager
{
    [JsonSourceGenerationOptions(
        WriteIndented = true, 
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(Settings))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
    
    /// <summary>
    /// Custom DateTime converter that treats empty strings as null
    /// </summary>
    public class NullableDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    return null;
                }
                
                if (DateTime.TryParse(stringValue, out var dateValue))
                {
                    return dateValue;
                }
            }
            else if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            
            return null;
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }

    public static class AppSettingsHelper
    {
        private static readonly string ConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = SourceGenerationContext.Default,
            Converters = { new NullableDateTimeConverter() }
        };
        
        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            TypeInfoResolver = SourceGenerationContext.Default,
            Converters = { new NullableDateTimeConverter() }
        };

        public static void UpdateServerLastDates(string serverName, DateTime? lastUpdate, DateTime? lastBackup)
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            var settings = JsonSerializer.Deserialize<Settings>(json, ReadOptions) ?? new Settings();
            if (settings.GameServers == null) return;
            var server = settings.GameServers.FirstOrDefault(s => s.Name == serverName);
            if (server == null) return;
            if (lastUpdate.HasValue)
                server.LastUpdateDate = lastUpdate;
            if (lastBackup.HasValue)
                server.LastBackupDate = lastBackup;
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(settings, WriteOptions));
        }

        public static void UpdateServerBuildId(string serverName, int buildId)
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            var settings = JsonSerializer.Deserialize<Settings>(json, ReadOptions) ?? new Settings();
            if (settings.GameServers == null) return;
            var server = settings.GameServers.FirstOrDefault(s => s.Name == serverName);
            if (server == null) return;
            server.CurrentBuildId = buildId;
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(settings, WriteOptions));
        }

        public static void UpdateServerLastCheck(string serverName, DateTime lastCheck)
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            var settings = JsonSerializer.Deserialize<Settings>(json, ReadOptions) ?? new Settings();
            if (settings.GameServers == null) return;
            var server = settings.GameServers.FirstOrDefault(s => s.Name == serverName);
            if (server == null) return;
            server.LastUpdateCheck = lastCheck;
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(settings, WriteOptions));
        }

        public static void UpdateServerBuildIdAndCheck(string serverName, int buildId, DateTime lastCheck)
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            var settings = JsonSerializer.Deserialize<Settings>(json, ReadOptions) ?? new Settings();
            if (settings.GameServers == null) return;
            var server = settings.GameServers.FirstOrDefault(s => s.Name == serverName);
            if (server == null) return;
            server.CurrentBuildId = buildId;
            server.LastUpdateCheck = lastCheck;
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(settings, WriteOptions));
        }
    }
}
