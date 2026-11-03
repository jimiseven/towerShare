using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TowerShareWindows
{
    public class ClipboardMonitor : NativeWindow, IDisposable
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        public event Action<string>? ClipboardChanged;

        private string? _lastReceivedText;
        private string? _lastSentText;
        private bool _isDisposed;

        public ClipboardMonitor()
        {
            // Crea una ventana oculta para recibir mensajes de Windows
            CreateHandle(new CreateParams());
            AddClipboardFormatListener(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CLIPBOARDUPDATE)
            {
                OnClipboardUpdated();
            }
            base.WndProc(ref m);
        }

        private void OnClipboardUpdated()
        {
            // Intentar leer el portapapeles con reintentos rápidos (a veces está bloqueado por otra app)
            string? currentText = null;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        currentText = Clipboard.GetText();
                        break;
                    }
                }
                catch (ExternalException)
                {
                    Thread.Sleep(50);
                }
            }

            if (string.IsNullOrEmpty(currentText))
                return;

            // PREVENCIÓN DE BUCLE (ECO):
            // Si el texto en el portapapeles es el que acabamos de recibir por Bluetooth, se ignora.
            if (currentText == _lastReceivedText)
            {
                return;
            }

            // Si es igual al último enviado, tampoco lo reenviamos
            if (currentText == _lastSentText)
            {
                return;
            }

            _lastSentText = currentText;
            ClipboardChanged?.Invoke(currentText);
        }

        /// <summary>
        /// Coloca un texto recibido de Android en el portapapeles local
        /// registrándolo en _lastReceivedText para evitar reenviarlo.
        /// </summary>
        public void SetClipboardTextFromRemote(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            _lastReceivedText = text;

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    break;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(50);
                }
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                RemoveClipboardFormatListener(Handle);
                DestroyHandle();
                _isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
