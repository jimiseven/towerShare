using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TowerShareWindows
{
    internal static class Program
    {
        public const string MutexName = "TowerShare_SingleInstance_Mutex";
        public const string MessageName = "TowerShare_ShowUI_Message";

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        public static uint ShowUiMessage;

        [STAThread]
        static void Main(string[] args)
        {
            ShowUiMessage = RegisterWindowMessage(MessageName);

            using var mutex = new Mutex(true, MutexName, out bool isFirstInstance);
            if (!isFirstInstance)
            {
                // Si ya está ejecutándose (en Modo Fantasma o en segundo plano),
                // enviamos mensaje para mostrar la interfaz y salimos.
                PostMessage(HWND_BROADCAST, ShowUiMessage, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            ApplicationConfiguration.Initialize();
            bool startMinimized = args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                                                a.Equals("-m", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm(startMinimized));
        }
    }
}
