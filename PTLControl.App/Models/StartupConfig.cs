namespace PTLControl.Models;

public class StartupConfig
{
    public string ConnectionMode { get; set; } = "serial";
    public string LogLevel { get; set; } = "Info";
    public SerialStartupConfig Serial { get; set; } = new();
    public MqttStartupConfig Mqtt { get; set; } = new();
    public WirelessDefaultsConfig WirelessDefaults { get; set; } = new();
}

public class SerialStartupConfig
{
    public string PortName { get; set; } = string.Empty;
}

public class MqttStartupConfig
{
    public string Broker { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 2026;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string EStationId { get; set; } = string.Empty;
    public int Qos { get; set; } = 1;
    public int KeepAliveSec { get; set; } = 30;
}

public class WirelessDefaultsConfig
{
    public int TaskTimeSlot { get; set; } = 5;
    public int BlinkTimeSlot { get; set; } = 5;
    public bool BeepDefault { get; set; }
    public int HeartbeatTimeoutSec { get; set; } = 90;
}
