using System.Collections.Generic;
using PTLControl.Models;
using CompatConfigService = PTLControl.Compat.Services.ConfigService;
using CompatCellConfig = PTLControl.Compat.Models.CellConfig;
using CompatMqttMappingConfig = PTLControl.Compat.Models.MqttMappingConfig;
using CompatMqttNodeConfig = PTLControl.Compat.Models.MqttNodeConfig;
using CompatMatrixConfig = PTLControl.Compat.Models.MatrixConfig;
using CompatRowConfig = PTLControl.Compat.Models.RowConfig;
using CompatStartupConfig = PTLControl.Compat.Models.StartupConfig;

namespace PTLControl.Services;

/// <summary>读写 serial/mqtt/startup 配置</summary>
public static class ConfigService
{
    public static MatrixConfig Load()
    {
        return FromCompat(CompatConfigService.Load());
    }

    public static void Save(MatrixConfig config)
    {
        CompatConfigService.Save(ToCompat(config));
    }

    public static StartupConfig LoadStartup()
    {
        var compat = CompatConfigService.LoadStartup();
        return new StartupConfig
        {
            ConnectionMode = compat.ConnectionMode,
            LogLevel = string.IsNullOrWhiteSpace(compat.LogLevel) ? "Info" : compat.LogLevel,
            Serial = new SerialStartupConfig
            {
                PortName = compat.Serial?.PortName ?? string.Empty
            },
            Mqtt = new MqttStartupConfig
            {
                Broker = compat.Mqtt?.Broker ?? "127.0.0.1",
                Port = compat.Mqtt?.Port ?? 2026,
                Username = compat.Mqtt?.Username ?? string.Empty,
                Password = compat.Mqtt?.Password ?? string.Empty,
                EStationId = compat.Mqtt?.EStationId ?? string.Empty,
                Qos = compat.Mqtt?.Qos ?? 1,
                KeepAliveSec = compat.Mqtt?.KeepAliveSec ?? 30
            },
            WirelessDefaults = new WirelessDefaultsConfig
            {
                TaskTimeSlot = compat.WirelessDefaults?.TaskTimeSlot ?? 5,
                BlinkTimeSlot = compat.WirelessDefaults?.BlinkTimeSlot ?? 5,
                BeepDefault = compat.WirelessDefaults?.BeepDefault ?? false,
                HeartbeatTimeoutSec = compat.WirelessDefaults?.HeartbeatTimeoutSec ?? 90
            }
        };
    }

    public static void SaveStartup(StartupConfig startup)
    {
        var compat = new CompatStartupConfig
        {
            ConnectionMode = startup?.ConnectionMode ?? "serial",
            LogLevel = string.IsNullOrWhiteSpace(startup?.LogLevel) ? "Info" : startup.LogLevel,
            Serial = new PTLControl.Compat.Models.SerialStartupConfig
            {
                PortName = startup?.Serial?.PortName ?? string.Empty
            },
            Mqtt = new PTLControl.Compat.Models.MqttStartupConfig
            {
                Broker = startup?.Mqtt?.Broker ?? "127.0.0.1",
                Port = startup?.Mqtt?.Port ?? 2026,
                Username = startup?.Mqtt?.Username ?? string.Empty,
                Password = startup?.Mqtt?.Password ?? string.Empty,
                EStationId = startup?.Mqtt?.EStationId ?? string.Empty,
                Qos = startup?.Mqtt?.Qos ?? 1,
                KeepAliveSec = startup?.Mqtt?.KeepAliveSec ?? 30
            },
            WirelessDefaults = new PTLControl.Compat.Models.WirelessDefaultsConfig
            {
                TaskTimeSlot = startup?.WirelessDefaults?.TaskTimeSlot ?? 5,
                BlinkTimeSlot = startup?.WirelessDefaults?.BlinkTimeSlot ?? 5,
                BeepDefault = startup?.WirelessDefaults?.BeepDefault ?? false,
                HeartbeatTimeoutSec = startup?.WirelessDefaults?.HeartbeatTimeoutSec ?? 90
            }
        };
        CompatConfigService.SaveStartup(compat);
    }

    public static MqttMappingConfig LoadMqttMapping()
    {
        var compat = CompatConfigService.LoadMqttMapping();
        var model = new MqttMappingConfig();
        foreach (var node in compat.Nodes)
        {
            model.Nodes.Add(new MqttNodeConfig
            {
                Key = node.Key ?? string.Empty,
                TagId = node.TagId ?? string.Empty,
                Group = node.Group,
                Alias = node.Alias ?? string.Empty
            });
        }
        return model;
    }

    public static void SaveMqttMapping(MqttMappingConfig mapping)
    {
        var compat = new CompatMqttMappingConfig();
        foreach (var node in mapping?.Nodes ?? new List<MqttNodeConfig>())
        {
            compat.Nodes.Add(new CompatMqttNodeConfig
            {
                Key = node.Key ?? string.Empty,
                TagId = node.TagId ?? string.Empty,
                Group = node.Group,
                Alias = node.Alias ?? string.Empty
            });
        }
        CompatConfigService.SaveMqttMapping(compat);
    }

    /// <summary>将配置展开为扫码字典 Key/Alias → (Layer, Index)</summary>
    public static Dictionary<string, (int Layer, int Index)> BuildDict(MatrixConfig config)
    {
        var compatDict = CompatConfigService.BuildDict(ToCompat(config));
        var dict = new Dictionary<string, (int Layer, int Index)>();
        foreach (var item in compatDict)
            dict[item.Key] = (item.Value.Key, item.Value.Value);
        return dict;
    }

    public static Dictionary<string, MqttNodeConfig> BuildMqttNodeDict(MqttMappingConfig config)
    {
        var compat = new CompatMqttMappingConfig();
        foreach (var node in config?.Nodes ?? new List<MqttNodeConfig>())
        {
            compat.Nodes.Add(new CompatMqttNodeConfig
            {
                Key = node.Key ?? string.Empty,
                TagId = node.TagId ?? string.Empty,
                Group = node.Group,
                Alias = node.Alias ?? string.Empty
            });
        }

        var compatDict = CompatConfigService.BuildMqttNodeDict(compat);
        var dict = new Dictionary<string, MqttNodeConfig>(System.StringComparer.Ordinal);
        foreach (var item in compatDict)
        {
            dict[item.Key] = new MqttNodeConfig
            {
                Key = item.Value.Key ?? string.Empty,
                TagId = item.Value.TagId ?? string.Empty,
                Group = item.Value.Group,
                Alias = item.Value.Alias ?? string.Empty
            };
        }
        return dict;
    }

    private static MatrixConfig FromCompat(CompatMatrixConfig compat)
    {
        var model = new MatrixConfig
        {
        };

        foreach (var row in compat.Rows)
        {
            var newRow = new RowConfig { Layer = row.Layer };
            foreach (var cell in row.Cells)
            {
                newRow.Cells.Add(new CellConfig
                {
                    Key = cell.Key ?? string.Empty,
                    Alias = cell.Alias ?? string.Empty,
                    Index = cell.Index
                });
            }
            model.Rows.Add(newRow);
        }

        return model;
    }

    private static CompatMatrixConfig ToCompat(MatrixConfig model)
    {
        var compat = new CompatMatrixConfig
        {
        };

        foreach (var row in model.Rows)
        {
            var newRow = new CompatRowConfig { Layer = row.Layer };
            foreach (var cell in row.Cells)
            {
                newRow.Cells.Add(new CompatCellConfig
                {
                    Key = cell.Key ?? string.Empty,
                    Alias = cell.Alias ?? string.Empty,
                    Index = cell.Index
                });
            }
            compat.Rows.Add(newRow);
        }

        return compat;
    }
}
