using System;
using PTLControl.Compat.Models;

namespace PTLControl.Compat.Services
{
    internal sealed class SerialTransportService : ITransportService
    {
        public SerialTransportService()
        {
            SerialService.Instance.ConnectionFaulted += OnSerialConnectionFaulted;
        }

        public string TransportType => "serial";
        public bool IsConnected => SerialService.Instance.IsOpen;
        public DateTime? LastHeartbeatUtc => null;

        public event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;
        public event EventHandler<TagEventArgs> TagEventReceived
        {
            add { }
            remove { }
        }

        public void Connect(string endpoint)
        {
            var port = endpoint ?? string.Empty;
            if (string.IsNullOrWhiteSpace(port))
                throw new InvalidOperationException("串口模式未提供端口号。");

            SerialService.Instance.Open(port);
            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
            {
                TransportType = TransportType,
                IsConnected = true,
                Message = SerialService.Instance.ConnectionMessage
            });
        }

        public void Disconnect()
        {
            var wasConnected = IsConnected;
            SerialService.Instance.Close();
            if (wasConnected)
            {
                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    TransportType = TransportType,
                    IsConnected = false,
                    Message = SerialService.Instance.ConnectionMessage
                });
            }
        }

        public string[] GetPortNames()
        {
            return SerialService.Instance.GetPortNames();
        }

        public void SendSerialCommand(string cmd)
        {
            SerialService.Instance.Send(cmd);
        }

        public void PublishWirelessTask(WirelessTask task)
        {
            throw new NotSupportedException("串口模式不支持无线任务发布。");
        }

        private void OnSerialConnectionFaulted(object sender, EventArgs e)
        {
            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
            {
                TransportType = TransportType,
                IsConnected = false,
                Message = "串口物理连接已断开"
            });
        }
    }
}
