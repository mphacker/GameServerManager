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
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Serilog.Log.Warning($"AppSettings file not found at {ConfigPath}. Cannot update server dates for {serverName}.");
                    return;
                }
                
                var json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<Settings>(json, ReadOptions);
                
                if (settings?.GameServers == null)
                {
                    Serilog.Log.Warning($"No game servers found in settings. Cannot update dates for {serverName}.");
                    return;
                }
                
                var server = settings.GameServers.FirstOrDefault(s => s.Name == serverName);
                if (server == null)
                {
                    Serilog.Log.Warning($"Server {serverName} not found in settings. Cannot update dates.");
                    return;
                }
                
                if (lastUpdate.HasValue)
                    server.LastUpdateDate = lastUpdate;
                if (lastBackup.HasValue)
                    server.LastBackupDate = lastBackup;
                
                var updatedJson = JsonSerializer.Serialize(settings, WriteOptions);
                File.WriteAllText(ConfigPath, updatedJson);
                Serilog.Log.Debug($"Successfully updated dates for {serverName}");
            }
            catch (IOException ioEx)
            {
                Serilog.Log.Error(ioEx, $"IO error updating dates for {serverName}: {ioEx.Message}");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                Serilog.Log.Error(uaEx, $"Access denied updating dates for {serverName}: {uaEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                Serilog.Log.Error(jsonEx, $"JSON error updating dates for {serverName}: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Unexpected error updating dates for {serverName}: {ex.Message}");
            }
        }

        public static void UpdateServerBuildId(string serverName, int buildId)
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Serilog.Log.Warning($"AppSettings file not found at {ConfigPath}. Cannot update build ID for {serverName}.");
                    return;
                }
                
                var json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<Settings>(json, ReadOptions);
                
                if (settings?.GameServers == null)
                {
                    Serilog.Log.Warning($"No game servers found in settings. Cannot update build ID for {serverName}.");
                    return;
                }
                
                var server = settings.GameServers.FirstOrDefault(s => s.Name == serverName);
                if (server == null)
                {
                    Serilog.Log.Warning($"Server {serverName} not found in settings. Cannot update build ID.");
                    return;
                }
                
                server.CurrentBuildId = buildId;
                
                var updatedJson = JsonSerializer.Serialize(settings, WriteOptions);
                File.WriteAllText(ConfigPath, updatedJson);
                Serilog.Log.Debug($"Successfully updated build ID for {serverName} to {buildId}");
            }
            catch (IOException ioEx)
            {
                Serilog.Log.Error(ioEx, $"IO error updating build ID for {serverName}: {ioEx.Message}");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                Serilog.Log.Error(uaEx, $"Access denied updating build ID for {serverName}: {uaEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                Serilog.Log.Error(jsonEx, $"JSON error updating build ID for {serverName}: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Unexpected error updating build ID for {serverName}: {ex.Message}");
            }
        }

        public static void UpdateServerLastCheck(string serverName, DateTime lastCheck)
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Serilog.Log.Warning($"AppSettings file not found at {ConfigPath}. Cannot update last check for {serverName}.");
                    return;
                }
                
                var json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<Settings>(json, ReadOptions);
                
                if (settings?.GameServers == null)
                {
                    Serilog.Log.Warning($"No game servers found in settings. Cannot update last check for {serverName}.");
                    return;
                }
                
                var server = settings.GameServers.FirstOrDefault(s => s.Name == serverName);
                if (server == null)
                {
                    Serilog.Log.Warning($"Server {serverName} not found in settings. Cannot update last check.");
                    return;
                }
                
                server.LastUpdateCheck = lastCheck;
                
                var updatedJson = JsonSerializer.Serialize(settings, WriteOptions);
                File.WriteAllText(ConfigPath, updatedJson);
                Serilog.Log.Debug($"Successfully updated last check for {serverName}");
            }
            catch (IOException ioEx)
            {
                Serilog.Log.Error(ioEx, $"IO error updating last check for {serverName}: {ioEx.Message}");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                Serilog.Log.Error(uaEx, $"Access denied updating last check for {serverName}: {uaEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                Serilog.Log.Error(jsonEx, $"JSON error updating last check for {serverName}: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Unexpected error updating last check for {serverName}: {ex.Message}");
            }
        }

        public static void UpdateServerBuildIdAndCheck(string serverName, int buildId, DateTime lastCheck)
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Serilog.Log.Warning($"AppSettings file not found at {ConfigPath}. Cannot update build ID and check for {serverName}.");
                    return;
                }
                
                var json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<Settings>(json, ReadOptions);
                
                if (settings?.GameServers == null)
                {
                    Serilog.Log.Warning($"No game servers found in settings. Cannot update build ID and check for {serverName}.");
                    return;
                }
                
                var server = settings.GameServers.FirstOrDefault(s => s.Name == serverName);
                if (server == null)
                {
                    Serilog.Log.Warning($"Server {serverName} not found in settings. Cannot update build ID and check.");
                    return;
                }
                
                server.CurrentBuildId = buildId;
                server.LastUpdateCheck = lastCheck;
                
                var updatedJson = JsonSerializer.Serialize(settings, WriteOptions);
                File.WriteAllText(ConfigPath, updatedJson);
                Serilog.Log.Debug($"Successfully updated build ID and check for {serverName}");
            }
            catch (IOException ioEx)
            {
                Serilog.Log.Error(ioEx, $"IO error updating build ID and check for {serverName}: {ioEx.Message}");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                Serilog.Log.Error(uaEx, $"Access denied updating build ID and check for {serverName}: {uaEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                Serilog.Log.Error(jsonEx, $"JSON error updating build ID and check for {serverName}: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Unexpected error updating build ID and check for {serverName}: {ex.Message}");
            }
        }
    }
}
