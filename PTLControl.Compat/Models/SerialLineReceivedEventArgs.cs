using System;

namespace PTLControl.Compat.Models
{
    /// <summary>MCU 通过串口返回的一行文本，例如 OK:SET 或 ERR:CMD。</summary>
    public sealed class SerialLineReceivedEventArgs : EventArgs
    {
        public SerialLineReceivedEventArgs(string line)
        {
            Line = line ?? string.Empty;
        }

        public string Line { get; private set; }
    }
}
