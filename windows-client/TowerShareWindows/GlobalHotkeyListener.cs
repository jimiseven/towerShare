using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TowerShareWindows
{
    /// <summary>
    /// Escucha atajos de teclado globales en un HWND nativo persistente.
    /// No se ve afectado cuando la ventana principal se oculta o cambia ShowInTaskbar.
    /// </summary>
    public class GlobalHotkeyListener : NativeWindow, IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;

        public const uint VK_T = 0x54; // Tecla 'T'

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID_1 = 9001; // Ctrl + Alt + T
        private const int HOTKEY_ID_2 = 9002; // Ctrl + Alt + Shift + T

        public event Action? HotkeyPressed;
        private bool _isDisposed;

        public bool IsRegisteredPrimary { get; private set; }
        public bool IsRegisteredSecondary { get; private set; }

        public GlobalHotkeyListener()
        {
            // Crea una ventana Win32 exclusiva para mensajes cuyo HWND es persistente
            CreateHandle(new CreateParams());

            // Intentar registrar Ctrl + Alt + T (3 teclas, muy fácil de pulsar)
            IsRegisteredPrimary = RegisterHotKey(Handle, HOTKEY_ID_1, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_T);

            // Registrar también Ctrl + Alt + Shift + T como respaldo
            IsRegisteredSecondary = RegisterHotKey(Handle, HOTKEY_ID_2, MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, VK_T);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_ID_1 || id == HOTKEY_ID_2)
                {
                    HotkeyPressed?.Invoke();
                }
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (IsRegisteredPrimary) UnregisterHotKey(Handle, HOTKEY_ID_1);
                if (IsRegisteredSecondary) UnregisterHotKey(Handle, HOTKEY_ID_2);
                DestroyHandle();
                _isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
