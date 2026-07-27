// ============================================================
// PTL LED Matrix Control System - .NET Standard 2.0 Compat
// Developer: Ezio @ IDEMIA
// Description: Read/write serial_mapping/startup/mqtt mapping config
//              (compatible with .NET Framework 4.7.2)
// ============================================================
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PTLControl.Compat.Models;

namespace PTLControl.Compat.Services
{
    /// <summary>读写本地配置（串口映射、MQTT映射、启动参数）</summary>
    public static class ConfigService
    {
        private static readonly string NewConfigRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PTLControl");
        private static readonly string LegacyConfigRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PTLDemo");
        private static readonly object MigrationLock = new object();
        private static bool _migrationDone;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static MatrixConfig Load()
        {
            EnsureConfigMigrated();
            var serialConfigPath = Path.Combine(NewConfigRoot, "serial_mapping.json");
            var legacySerialConfigPath = Path.Combine(NewConfigRoot, "ptl_config.json");
            if (!File.Exists(serialConfigPath))
            {
                if (File.Exists(legacySerialConfigPath))
                {
                    var legacyJson = File.ReadAllText(legacySerialConfigPath);
                    var legacyConfig = JsonConvert.DeserializeObject<MatrixConfig>(legacyJson, JsonSettings) ?? new MatrixConfig();
                    var migrated = Normalize(legacyConfig);
                    Save(migrated);
                    return migrated;
                }
                return Normalize(new MatrixConfig());
            }

            var json = File.ReadAllText(serialConfigPath);
            var config = JsonConvert.DeserializeObject<MatrixConfig>(json, JsonSettings) ?? new MatrixConfig();
            return Normalize(config);
        }

        public static void Save(MatrixConfig config)
        {
            EnsureConfigMigrated();
            Directory.CreateDirectory(NewConfigRoot);
            var normalized = Normalize(config ?? new MatrixConfig());
            var json = JsonConvert.SerializeObject(normalized, JsonSettings);
            File.WriteAllText(Path.Combine(NewConfigRoot, "serial_mapping.json"), json);
        }

        public static StartupConfig LoadStartup()
        {
            EnsureConfigMigrated();
            var startupConfigPath = Path.Combine(NewConfigRoot, "startup_config.json");
            if (!File.Exists(startupConfigPath))
                return NormalizeStartup(new StartupConfig());

            var json = File.ReadAllText(startupConfigPath);
            var config = JsonConvert.DeserializeObject<StartupConfig>(json, JsonSettings) ?? new StartupConfig();
            return NormalizeStartup(config);
        }

        public static void SaveStartup(StartupConfig config)
        {
            EnsureConfigMigrated();
            Directory.CreateDirectory(NewConfigRoot);
            var normalized = NormalizeStartup(config ?? new StartupConfig());
            var json = JsonConvert.SerializeObject(normalized, JsonSettings);
            File.WriteAllText(Path.Combine(NewConfigRoot, "startup_config.json"), json);
        }

        public static MqttMappingConfig LoadMqttMapping()
        {
            EnsureConfigMigrated();
            var mqttMappingPath = Path.Combine(NewConfigRoot, "mqtt_mapping.json");
            if (!File.Exists(mqttMappingPath))
                return NormalizeMqttMapping(new MqttMappingConfig());

            var json = File.ReadAllText(mqttMappingPath);
            var config = JsonConvert.DeserializeObject<MqttMappingConfig>(json, JsonSettings) ?? new MqttMappingConfig();
            return NormalizeMqttMapping(config);
        }

        public static void SaveMqttMapping(MqttMappingConfig config)
        {
            EnsureConfigMigrated();
            Directory.CreateDirectory(NewConfigRoot);
            var normalized = NormalizeMqttMapping(config ?? new MqttMappingConfig());
            var json = JsonConvert.SerializeObject(normalized, JsonSettings);
            File.WriteAllText(Path.Combine(NewConfigRoot, "mqtt_mapping.json"), json);
        }

        /// <summary>将配置展开为扫码字典 Key/Alias → (Layer, Index)</summary>
        public static Dictionary<string, KeyValuePair<int, int>> BuildDict(MatrixConfig config)
        {
            var dict = new Dictionary<string, KeyValuePair<int, int>>();
            var normalized = Normalize(config ?? new MatrixConfig());
            foreach (var row in normalized.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    var entry = new KeyValuePair<int, int>(row.Layer, cell.Index);
                    if (!string.IsNullOrWhiteSpace(cell.Key))
                        dict[cell.Key] = entry;
                    if (!string.IsNullOrWhiteSpace(cell.Alias) && !dict.ContainsKey(cell.Alias))
                        dict[cell.Alias] = entry;
                }
            }
            return dict;
        }

        public static Dictionary<string, MqttNodeConfig> BuildMqttNodeDict(MqttMappingConfig config)
        {
            var dict = new Dictionary<string, MqttNodeConfig>(System.StringComparer.Ordinal);
            var normalized = NormalizeMqttMapping(config ?? new MqttMappingConfig());
            foreach (var node in normalized.Nodes)
            {
                if (!string.IsNullOrWhiteSpace(node.Key))
                    dict[node.Key] = node;
                if (!string.IsNullOrWhiteSpace(node.Alias) && !dict.ContainsKey(node.Alias))
                    dict[node.Alias] = node;
            }
            return dict;
        }

        private static MatrixConfig Normalize(MatrixConfig config)
        {
            var normalized = config ?? new MatrixConfig();

            if (normalized.Rows == null)
                normalized.Rows = new List<RowConfig>();

            foreach (var row in normalized.Rows)
            {
                if (row.Cells == null)
                    row.Cells = new List<CellConfig>();
                foreach (var cell in row.Cells)
                {
                    if (cell.Key == null) cell.Key = string.Empty;
                    if (cell.Alias == null) cell.Alias = string.Empty;
                }
            }

            return normalized;
        }

        private static StartupConfig NormalizeStartup(StartupConfig config)
        {
            var normalized = config ?? new StartupConfig();
            if (string.IsNullOrWhiteSpace(normalized.ConnectionMode))
                normalized.ConnectionMode = "serial";
            normalized.ConnectionMode = normalized.ConnectionMode.Trim().ToLowerInvariant();
            if (normalized.ConnectionMode != "mqtt")
                normalized.ConnectionMode = "serial";

            if (string.IsNullOrWhiteSpace(normalized.LogLevel))
                normalized.LogLevel = "Info";

            if (normalized.Serial == null)
                normalized.Serial = new SerialStartupConfig();
            if (normalized.Mqtt == null)
                normalized.Mqtt = new MqttStartupConfig();
            if (string.IsNullOrWhiteSpace(normalized.Mqtt.Broker))
                normalized.Mqtt.Broker = "127.0.0.1";
            if (normalized.Mqtt.Port <= 0)
                normalized.Mqtt.Port = 1883;
            if (normalized.Mqtt.Qos < 0 || normalized.Mqtt.Qos > 2)
                normalized.Mqtt.Qos = 1;
            if (normalized.Mqtt.KeepAliveSec <= 0)
                normalized.Mqtt.KeepAliveSec = 30;

            if (normalized.WirelessDefaults == null)
                normalized.WirelessDefaults = new WirelessDefaultsConfig();
            if (normalized.WirelessDefaults.TaskTimeSlot < 0 || normalized.WirelessDefaults.TaskTimeSlot > 255)
                normalized.WirelessDefaults.TaskTimeSlot = 5;
            if (normalized.WirelessDefaults.BlinkTimeSlot < 0 || normalized.WirelessDefaults.BlinkTimeSlot > 255)
                normalized.WirelessDefaults.BlinkTimeSlot = 5;
            if (normalized.WirelessDefaults.HeartbeatTimeoutSec <= 0)
                normalized.WirelessDefaults.HeartbeatTimeoutSec = 90;

            return normalized;
        }

        private static MqttMappingConfig NormalizeMqttMapping(MqttMappingConfig config)
        {
            var normalized = config ?? new MqttMappingConfig();
            if (normalized.Nodes == null)
                normalized.Nodes = new List<MqttNodeConfig>();

            foreach (var node in normalized.Nodes)
            {
                if (node.Key == null) node.Key = string.Empty;
                if (node.Alias == null) node.Alias = string.Empty;
                if (node.TagId == null) node.TagId = string.Empty;
                if (node.Group < 0 || node.Group > 254)
                    node.Group = 0;
            }

            return normalized;
        }

        private static void EnsureConfigMigrated()
        {
            lock (MigrationLock)
            {
                if (_migrationDone)
                    return;

                try
                {
                    Directory.CreateDirectory(NewConfigRoot);
                    TryCopyIfMissing(
                        Path.Combine(LegacyConfigRoot, "serial_mapping.json"),
                        Path.Combine(NewConfigRoot, "serial_mapping.json"));
                    TryCopyIfMissing(
                        Path.Combine(LegacyConfigRoot, "ptl_config.json"),
                        Path.Combine(NewConfigRoot, "ptl_config.json"));
                    TryCopyIfMissing(
                        Path.Combine(LegacyConfigRoot, "mqtt_mapping.json"),
                        Path.Combine(NewConfigRoot, "mqtt_mapping.json"));
                    TryCopyIfMissing(
                        Path.Combine(LegacyConfigRoot, "startup_config.json"),
                        Path.Combine(NewConfigRoot, "startup_config.json"));
                }
                catch
                {
                    // 迁移失败时不阻塞业务，后续按新路径正常读写。
                }
                finally
                {
                    _migrationDone = true;
                }
            }
        }

        private static void TryCopyIfMissing(string sourcePath, string targetPath)
        {
            if (!File.Exists(sourcePath) || File.Exists(targetPath))
                return;

            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDir))
                Directory.CreateDirectory(targetDir);
            File.Copy(sourcePath, targetPath);
        }
    }
}
