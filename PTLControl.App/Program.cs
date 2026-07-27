// ============================================================
// PTL LED Matrix Control System
// Developer: Ezio @ IDEMIA
// Description: Application entry point.
// ============================================================
using System.Windows.Forms;

namespace PTLControl;

static class Program
{
    [System.STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
