using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TowerShareWindows
{
    public partial class MainForm : Form
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppSettingsRegistryKey = @"Software\TowerShare";
        private const string AppRegistryName = "TowerShare";

        // Registro de Atajo Global: Ctrl + Alt + Shift + T
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9001;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint VK_T = 0x54; // Código de tecla 'T'
        private const int WM_HOTKEY = 0x0312;

        private readonly BluetoothClient _btClient = new();
        private ClipboardMonitor? _clipboardMonitor;
        private readonly System.Windows.Forms.Timer _reconnectTimer = new();
        private bool _isExiting = false;
        private string? _lastConnectedDeviceId;
        private string? _lastConnectedDeviceName;
        private readonly bool _startMinimized = false;

        public MainForm(bool startMinimized = false)
        {
            InitializeComponent();
            _startMinimized = startMinimized;

            notifyIcon.Icon = SystemIcons.Application;
            Icon = SystemIcons.Application;

            _reconnectTimer.Interval = 10000;
            _reconnectTimer.Tick += ReconnectTimer_Tick;

            if (_startMinimized)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
            }
        }

        protected override void WndProc(ref Message m)
        {
            // Mensaje nativo de Windows para reabrir la interfaz gráfica cuando corre en Modo Fantasma
            if (Program.ShowUiMessage != 0 && m.Msg == Program.ShowUiMessage)
            {
                RestoreWindow();
            }
            // Atajo de teclado global presionado (Ctrl + Alt + Shift + T)
            else if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                ToggleWindowVisibility();
            }

            base.WndProc(ref m);
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            LoadPersistentSettings();

            // Registrar atajo de teclado global en Windows
            RegisterGlobalHotkey();

            _clipboardMonitor = new ClipboardMonitor();
            _clipboardMonitor.ClipboardChanged += OnLocalClipboardChanged;

            _btClient.StateChanged += OnBluetoothStateChanged;
            _btClient.MessageReceived += OnBluetoothMessageReceived;
            _btClient.LogMessage += AppendLog;

            if (_startMinimized)
            {
                BeginInvoke(new Action(() =>
                {
                    Hide();
                    ShowInTaskbar = false;
                    if (chkStealthMode.Checked)
                    {
                        notifyIcon.Visible = false; // Modo Fantasma: Cero presencia en la bandeja ni en la flecha ^
                    }
                    else
                    {
                        notifyIcon.Visible = true;
                    }
                }));
            }
            else
            {
                notifyIcon.Visible = true;
            }

            await LoadPairedDevicesAsync();

            // Auto-conectar al dispositivo guardado si existe
            if (!string.IsNullOrEmpty(_lastConnectedDeviceId))
            {
                AppendLog($"Auto-conectando al dispositivo guardado: {_lastConnectedDeviceName}...");
                _ = ConnectToDeviceAsync(_lastConnectedDeviceId, _lastConnectedDeviceName ?? "Android");
            }
        }

        private void RegisterGlobalHotkey()
        {
            try
            {
                bool registered = RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, VK_T);
                if (registered)
                {
                    AppendLog("Atajo global activado: Ctrl + Alt + Shift + T");
                }
                else
                {
                    AppendLog("Aviso: No se pudo registrar el atajo global (posiblemente en uso por otra app).");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error al registrar atajo global: {ex.Message}");
            }
        }

        private void ToggleWindowVisibility()
        {
            if (Visible && WindowState != FormWindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
                if (chkStealthMode.Checked)
                {
                    notifyIcon.Visible = false;
                }
            }
            else
            {
                RestoreWindow();
            }
        }

        private void LoadPersistentSettings()
        {
            try
            {
                // Estado de arranque con Windows
                using (var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
                {
                    chkStartWithWindows.Checked = runKey?.GetValue(AppRegistryName) != null;
                }

                // Ajustes propios de TowerShare
                using var key = Registry.CurrentUser.CreateSubKey(AppSettingsRegistryKey);
                if (key != null)
                {
                    _lastConnectedDeviceId = key.GetValue("LastDeviceId") as string;
                    _lastConnectedDeviceName = key.GetValue("LastDeviceName") as string;
                    chkStealthMode.Checked = Convert.ToInt32(key.GetValue("StealthMode", 1)) == 1;
                    chkAutoReconnect.Checked = Convert.ToInt32(key.GetValue("AutoReconnect", 1)) == 1;
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error al leer configuración: {ex.Message}");
            }
        }

        private void SavePersistentSettings()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(AppSettingsRegistryKey);
                if (key != null)
                {
                    if (!string.IsNullOrEmpty(_lastConnectedDeviceId))
                    {
                        key.SetValue("LastDeviceId", _lastConnectedDeviceId);
                    }
                    if (!string.IsNullOrEmpty(_lastConnectedDeviceName))
                    {
                        key.SetValue("LastDeviceName", _lastConnectedDeviceName);
                    }
                    key.SetValue("StealthMode", chkStealthMode.Checked ? 1 : 0);
                    key.SetValue("AutoReconnect", chkAutoReconnect.Checked ? 1 : 0);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error al guardar configuración: {ex.Message}");
            }
        }

        private void ChkStartWithWindows_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key != null)
                {
                    if (chkStartWithWindows.Checked)
                    {
                        string runCommand = $"\"{Application.ExecutablePath}\" --minimized";
                        key.SetValue(AppRegistryName, runCommand);
                    }
                    else
                    {
                        key.DeleteValue(AppRegistryName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error al configurar inicio automático: {ex.Message}");
            }
        }

        private void ChkStealthMode_CheckedChanged(object sender, EventArgs e)
        {
            SavePersistentSettings();
            if (WindowState == FormWindowState.Minimized || !Visible)
            {
                notifyIcon.Visible = !chkStealthMode.Checked;
            }
        }

        private void ChkAutoReconnect_CheckedChanged(object sender, EventArgs e)
        {
            SavePersistentSettings();
        }

        private async Task LoadPairedDevicesAsync()
        {
            AppendLog("Buscando dispositivos Bluetooth vinculados en Windows...");
            cmbDevices.Items.Clear();

            try
            {
                var devices = await BluetoothClient.GetPairedDevicesAsync();
                int selectedIndex = -1;

                for (int i = 0; i < devices.Count; i++)
                {
                    cmbDevices.Items.Add(devices[i]);
                    if (!string.IsNullOrEmpty(_lastConnectedDeviceId) && devices[i].Id == _lastConnectedDeviceId)
                    {
                        selectedIndex = i;
                    }
                }

                if (selectedIndex >= 0)
                {
                    cmbDevices.SelectedIndex = selectedIndex;
                }
                else if (cmbDevices.Items.Count > 0)
                {
                    cmbDevices.SelectedIndex = 0;
                }

                AppendLog($"Se encontraron {devices.Count} dispositivos vinculados.");
            }
            catch (Exception ex)
            {
                AppendLog($"Error al buscar dispositivos: {ex.Message}");
            }
        }

        private async void BtnRefresh_Click(object sender, EventArgs e)
        {
            await LoadPairedDevicesAsync();
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            if (_btClient.State == ConnectionState.Connected || _btClient.State == ConnectionState.Connecting)
            {
                _reconnectTimer.Stop();
                _btClient.Disconnect();
                return;
            }

            if (cmbDevices.SelectedItem is not BluetoothDeviceInfo selectedDevice)
            {
                MessageBox.Show("Por favor selecciona un dispositivo de la lista.", "TowerShare", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _lastConnectedDeviceId = selectedDevice.Id;
            _lastConnectedDeviceName = selectedDevice.Name;
            SavePersistentSettings();

            await ConnectToDeviceAsync(selectedDevice.Id, selectedDevice.Name);
        }

        private async Task ConnectToDeviceAsync(string deviceId, string deviceName)
        {
            btnConnect.Enabled = false;
            btnRefresh.Enabled = false;
            cmbDevices.Enabled = false;

            try
            {
                await _btClient.ConnectAsync(deviceId, deviceName);
            }
            catch (Exception ex)
            {
                AppendLog($"Fallo de conexión: {ex.Message}");
            }
            finally
            {
                btnConnect.Enabled = true;
                btnRefresh.Enabled = _btClient.State == ConnectionState.Disconnected;
                cmbDevices.Enabled = _btClient.State == ConnectionState.Disconnected;
            }
        }

        private async void OnLocalClipboardChanged(string newText)
        {
            if (_btClient.State == ConnectionState.Connected)
            {
                AppendLog($"Copiado en Windows: \"{(newText.Length > 40 ? newText.Substring(0, 40) + "..." : newText)}\"");
                await _btClient.SendTextAsync(newText);
            }
        }

        private void OnBluetoothMessageReceived(string receivedText)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnBluetoothMessageReceived(receivedText)));
                return;
            }

            _clipboardMonitor?.SetClipboardTextFromRemote(receivedText);
            AppendLog($"Recibido de Android y copiado al portapapeles local: \"{(receivedText.Length > 40 ? receivedText.Substring(0, 40) + "..." : receivedText)}\"");
        }

        private void OnBluetoothStateChanged(ConnectionState state, string? deviceName)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnBluetoothStateChanged(state, deviceName)));
                return;
            }

            switch (state)
            {
                case ConnectionState.Connected:
                    lblStatus.Text = $"Conectado a {deviceName}";
                    lblStatus.ForeColor = Color.DarkGreen;
                    btnConnect.Text = "Desconectar";
                    notifyIcon.Text = $"TowerShare: Conectado a {deviceName}";
                    _reconnectTimer.Stop();
                    break;

                case ConnectionState.Connecting:
                    lblStatus.Text = $"Conectando a {deviceName}...";
                    lblStatus.ForeColor = Color.DarkOrange;
                    btnConnect.Text = "Cancelar";
                    notifyIcon.Text = "TowerShare: Conectando...";
                    break;

                case ConnectionState.Disconnected:
                    lblStatus.Text = "Desconectado";
                    lblStatus.ForeColor = Color.DarkRed;
                    btnConnect.Text = "Conectar";
                    notifyIcon.Text = "TowerShare (Desconectado)";
                    btnRefresh.Enabled = true;
                    cmbDevices.Enabled = true;

                    if (chkAutoReconnect.Checked && !string.IsNullOrEmpty(_lastConnectedDeviceId) && !_isExiting)
                    {
                        _reconnectTimer.Start();
                    }
                    break;
            }
        }

        private async void ReconnectTimer_Tick(object? sender, EventArgs e)
        {
            if (_btClient.State == ConnectionState.Disconnected && !string.IsNullOrEmpty(_lastConnectedDeviceId) && chkAutoReconnect.Checked)
            {
                AppendLog("Reintentando conexión automática con el celular...");
                await ConnectToDeviceAsync(_lastConnectedDeviceId, _lastConnectedDeviceName ?? "Android");
            }
        }

        private void AppendLog(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendLog(message)));
                return;
            }

            txtLog.AppendText(message + Environment.NewLine);
        }

        private void BtnClearLog_Click(object sender, EventArgs e)
        {
            txtLog.Clear();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            // Al minimizar la ventana, se oculta de la barra de tareas
            if (WindowState == FormWindowState.Minimized && chkStartMinimized.Checked)
            {
                Hide();
                ShowInTaskbar = false;

                // Modo Fantasma: se quita también de la bandeja del sistema (flecha ^)
                if (chkStealthMode.Checked)
                {
                    notifyIcon.Visible = false;
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Cerrar con la 'X' minimiza a segundo plano silencioso
            if (!_isExiting && chkStartMinimized.Checked && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;

                // Modo Fantasma: se quita también de la bandeja del sistema (flecha ^)
                if (chkStealthMode.Checked)
                {
                    notifyIcon.Visible = false;
                }
                return;
            }

            _isExiting = true;
            UnregisterHotKey(Handle, HOTKEY_ID);
            _reconnectTimer.Stop();
            _clipboardMonitor?.Dispose();
            _btClient.Dispose();
            notifyIcon.Visible = false;
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            notifyIcon.Visible = true; // Visible temporalmente mientras la ventana de configuración está abierta
            BringToFront();
            Activate();
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            RestoreWindow();
        }

        private void TrayMenuOpen_Click(object sender, EventArgs e)
        {
            RestoreWindow();
        }

        private async void TrayMenuReconnect_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastConnectedDeviceId))
            {
                await ConnectToDeviceAsync(_lastConnectedDeviceId, _lastConnectedDeviceName ?? "Android");
            }
            else
            {
                RestoreWindow();
            }
        }

        private void TrayMenuExit_Click(object sender, EventArgs e)
        {
            _isExiting = true;
            Close();
        }
    }
}
