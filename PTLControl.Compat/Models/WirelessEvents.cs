using System;
using System.Collections.Generic;

namespace PTLControl.Compat.Models
{
    public enum TagEventType
    {
        Unknown = 0,
        Button = 0xFD,
        Communication = 0xFE,
        Heartbeat = 0xFF
    }

    public sealed class ConnectionChangedEventArgs : EventArgs
    {
        public string TransportType { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class TagEventArgs : EventArgs
    {
        public string EStationId { get; set; } = string.Empty;
        public string TagId { get; set; } = string.Empty;
        public int Group { get; set; }
        public TagEventType EventType { get; set; } = TagEventType.Unknown;
        public bool R { get; set; }
        public bool G { get; set; }
        public bool B { get; set; }
        public bool IsOff { get; set; }
        public double BatteryVoltage { get; set; }
        public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
        public string RawPayload { get; set; } = string.Empty;
    }

    public sealed class NodeState
    {
        public string TagId { get; set; } = string.Empty;
        public int Group { get; set; }
        public bool R { get; set; }
        public bool G { get; set; }
        public bool B { get; set; }
        public bool IsOff { get; set; }
        public TagEventType LastEventType { get; set; } = TagEventType.Unknown;
        public double BatteryVoltage { get; set; }
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    internal sealed class WirelessTaskItem
    {
        public string TagId { get; set; } = string.Empty;
        public int Group { get; set; }
        public bool R { get; set; }
        public bool G { get; set; }
        public bool B { get; set; }
        public bool? Flashing { get; set; }
        public bool Beep { get; set; }
    }

    internal sealed class WirelessTask
    {
        public int TimeSlot { get; set; }
        public List<WirelessTaskItem> Items { get; set; } = new List<WirelessTaskItem>();
    }
}
