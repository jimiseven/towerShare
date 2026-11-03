using System;
using System.Linq;
using System.Windows.Forms;

namespace TowerShareWindows
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            bool startMinimized = args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                                                a.Equals("-m", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm(startMinimized));
        }
    }
}
