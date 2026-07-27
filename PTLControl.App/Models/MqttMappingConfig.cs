using System.Collections.Generic;

namespace PTLControl.Models;

public class MqttMappingConfig
{
    public List<MqttNodeConfig> Nodes { get; set; } = new();
}

public class MqttNodeConfig
{
    public string Key { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public int Group { get; set; }
    public string Alias { get; set; } = string.Empty;
}
