using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TowerShareWindows
{
    public partial class MainForm : Form
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "TowerShare";

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

        private async void MainForm_Load(object sender, EventArgs e)
        {
            CheckWindowsAutostartStatus();

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
                }));
            }

            await LoadPairedDevicesAsync();
        }

        private void CheckWindowsAutostartStatus()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                chkStartWithWindows.Checked = key?.GetValue(AppRegistryName) != null;
            }
            catch
            {
                // Ignorar si no se puede leer el registro
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

        private async Task LoadPairedDevicesAsync()
        {
            AppendLog("Buscando dispositivos Bluetooth vinculados en Windows...");
            cmbDevices.Items.Clear();

            try
            {
                var devices = await BluetoothClient.GetPairedDevicesAsync();
                foreach (var device in devices)
                {
                    cmbDevices.Items.Add(device);
                }

                if (cmbDevices.Items.Count > 0)
                {
                    cmbDevices.SelectedIndex = 0;
                    AppendLog($"Se encontraron {devices.Count} dispositivos vinculados.");
                }
                else
                {
                    AppendLog("No se encontraron dispositivos vinculados. Empareja tu celular desde Configuración de Windows.");
                }
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
            // Si el usuario minimiza la ventana, la ocultamos por completo de la barra de tareas
            if (WindowState == FormWindowState.Minimized && chkStartMinimized.Checked)
            {
                Hide();
                ShowInTaskbar = false;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_isExiting && chkStartMinimized.Checked && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                return;
            }

            _isExiting = true;
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
