using System;
using PTLControl.Compat.Models;

namespace PTLControl.Compat.Services
{
    internal interface ITransportService
    {
        string TransportType { get; }
        bool IsConnected { get; }
        DateTime? LastHeartbeatUtc { get; }

        event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;
        event EventHandler<TagEventArgs> TagEventReceived;

        void Connect(string endpoint);
        void Disconnect();
        string[] GetPortNames();
        void SendSerialCommand(string cmd);
        void PublishWirelessTask(WirelessTask task);
    }
}
