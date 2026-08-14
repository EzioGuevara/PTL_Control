using System;
using System.Threading;
using System.Windows.Forms;

namespace PTLControl.HardwareHost
{
    internal static class Program
    {
        private const string MutexName = @"Global\PTLControl.HardwareHost.v1";
        private const string ShowEventName = @"Global\PTLControl.HardwareHost.Show.v1";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    try
                    {
                        using (var showEvent = EventWaitHandle.OpenExisting(ShowEventName))
                            showEvent.Set();
                    }
                    catch { }
                    return;
                }

                bool eventCreated;
                using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName, out eventCreated))
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new HardwareHostForm(showEvent));
                }
            }
        }
    }
}
