using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TowerShareInstaller
{
    public class InstallerForm : Form
    {
        private readonly string _defaultInstallDir;
        private Label _lblTitle = null!;
        private Label _lblDesc = null!;
        private Label _lblPath = null!;
        private TextBox _txtPath = null!;
        private CheckBox _chkStartMenu = null!;
        private CheckBox _chkDesktop = null!;
        private CheckBox _chkAutoStart = null!;
        private CheckBox _chkLaunchNow = null!;
        private ProgressBar _progressBar = null!;
        private Label _lblStatus = null!;
        private Button _btnInstall = null!;
        private Button _btnCancel = null!;

        public InstallerForm()
        {
            _defaultInstallDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "TowerShare"
            );

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Instalador de TowerShare";
            Width = 500;
            Height = 380;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            Icon = SystemIcons.Application;

            _lblTitle = new Label
            {
                Text = "Instalar TowerShare",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true
            };

            _lblDesc = new Label
            {
                Text = "Sincronizador silencioso de portapapeles Bluetooth entre Windows y Android.\nSe instalará en tu usuario sin requerir permisos de administrador.",
                Location = new Point(22, 45),
                Size = new Size(440, 35),
                ForeColor = Color.DimGray
            };

            _lblPath = new Label
            {
                Text = "Carpeta de instalación:",
                Location = new Point(20, 90),
                AutoSize = true
            };

            _txtPath = new TextBox
            {
                Text = _defaultInstallDir,
                Location = new Point(22, 110),
                Size = new Size(440, 25),
                ReadOnly = true
            };

            _chkStartMenu = new CheckBox
            {
                Text = "Crear acceso directo en el Menú Inicio",
                Location = new Point(22, 145),
                Size = new Size(400, 22),
                Checked = true
            };

            _chkDesktop = new CheckBox
            {
                Text = "Crear acceso directo en el Escritorio",
                Location = new Point(22, 170),
                Size = new Size(400, 22),
                Checked = true
            };

            _chkAutoStart = new CheckBox
            {
                Text = "Iniciar automáticamente al encender Windows (segundo plano silencioso)",
                Location = new Point(22, 195),
                Size = new Size(440, 22),
                Checked = true
            };

            _chkLaunchNow = new CheckBox
            {
                Text = "Iniciar TowerShare al finalizar la instalación",
                Location = new Point(22, 220),
                Size = new Size(400, 22),
                Checked = true
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(22, 255),
                Size = new Size(440, 18),
                Visible = false
            };

            _lblStatus = new Label
            {
                Text = "",
                Location = new Point(22, 275),
                Size = new Size(440, 20),
                ForeColor = Color.DarkSlateGray
            };

            _btnInstall = new Button
            {
                Text = "Instalar Ahora",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(250, 300),
                Size = new Size(110, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnInstall.Click += async (s, e) => await PerformInstallAsync();

            _btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(370, 300),
                Size = new Size(90, 32)
            };
            _btnCancel.Click += (s, e) => Close();

            Controls.AddRange(new Control[]
            {
                _lblTitle, _lblDesc, _lblPath, _txtPath,
                _chkStartMenu, _chkDesktop, _chkAutoStart, _chkLaunchNow,
                _progressBar, _lblStatus, _btnInstall, _btnCancel
            });
        }

        private async Task PerformInstallAsync()
        {
            _btnInstall.Enabled = false;
            _btnCancel.Enabled = false;
            _progressBar.Visible = true;
            _progressBar.Style = ProgressBarStyle.Marquee;

            try
            {
                string targetDir = _txtPath.Text;
                string targetExe = Path.Combine(targetDir, "TowerShare.exe");

                _lblStatus.Text = "Cerrando instancias previas si existen...";
                await Task.Run(() =>
                {
                    foreach (var p in Process.GetProcessesByName("TowerShare"))
                    {
                        try { p.Kill(); p.WaitForExit(2000); } catch { }
                    }
                    foreach (var p in Process.GetProcessesByName("TowerShareWindows"))
                    {
                        try { p.Kill(); p.WaitForExit(2000); } catch { }
                    }
                });

                _lblStatus.Text = "Extrayendo archivos de TowerShare...";
                await Task.Run(() =>
                {
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    // Extraer el binario incrustado
                    var assembly = Assembly.GetExecutingAssembly();
                    using var stream = assembly.GetManifestResourceStream("TowerSharePayload.exe");
                    if (stream == null)
                    {
                        throw new InvalidOperationException("No se encontró el recurso del payload de TowerShare.");
                    }

                    using var fs = new FileStream(targetExe, FileMode.Create, FileAccess.Write, FileShare.None);
                    stream.CopyTo(fs);
                });

                _lblStatus.Text = "Creando accesos directos...";
                await Task.Run(() =>
                {
                    if (_chkStartMenu.Checked)
                    {
                        string startMenuDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            @"Microsoft\Windows\Start Menu\Programs"
                        );
                        string shortcutPath = Path.Combine(startMenuDir, "TowerShare.lnk");
                        CreateShortcut(shortcutPath, targetExe, "--minimized");
                    }

                    if (_chkDesktop.Checked)
                    {
                        string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        string shortcutPath = Path.Combine(desktopDir, "TowerShare.lnk");
                        CreateShortcut(shortcutPath, targetExe, "");
                    }
                });

                _lblStatus.Text = "Registrando en Windows y configurando inicio automático...";
                await Task.Run(() =>
                {
                    // Registro para Agregar/Quitar Programas
                    using (var uninstKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\TowerShare"))
                    {
                        uninstKey.SetValue("DisplayName", "TowerShare");
                        uninstKey.SetValue("DisplayVersion", "1.0.0");
                        uninstKey.SetValue("Publisher", "TowerShare");
                        uninstKey.SetValue("InstallLocation", targetDir);
                        uninstKey.SetValue("DisplayIcon", targetExe);
                        uninstKey.SetValue("UninstallString", $"cmd.exe /c rmdir /s /q \"{targetDir}\"");
                    }

                    // Inicio automático al encender Windows
                    using (var runKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        if (_chkAutoStart.Checked)
                        {
                            runKey.SetValue("TowerShare", $"\"{targetExe}\" --minimized");
                        }
                        else
                        {
                            runKey.DeleteValue("TowerShare", false);
                        }
                    }
                });

                _progressBar.Style = ProgressBarStyle.Blocks;
                _progressBar.Value = 100;
                _lblStatus.Text = "¡Instalación completada!";

                if (_chkLaunchNow.Checked)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = targetExe,
                        UseShellExecute = true
                    });
                }

                MessageBox.Show(
                    "¡TowerShare se ha instalado con éxito en tu computadora!\n\n" +
                    "La aplicación ya está activa en segundo plano junto al reloj.\n" +
                    "Ya puedes borrar este archivo instalador (TowerShare_Setup.exe).",
                    "Instalación Completa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                Close();
            }
            catch (Exception ex)
            {
                _progressBar.Visible = false;
                _lblStatus.Text = "Error en la instalación.";
                _btnInstall.Enabled = true;
                _btnCancel.Enabled = true;
                MessageBox.Show($"Ocurrió un error al instalar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetExePath, string arguments)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        shortcut.TargetPath = targetExePath;
                        shortcut.Arguments = arguments;
                        shortcut.WorkingDirectory = Path.GetDirectoryName(targetExePath);
                        shortcut.Description = "TowerShare - Portapapeles Bluetooth";
                        shortcut.Save();
                    }
                }
            }
            catch
            {
                // Si falla WScript, ignorar silenciosamente
            }
        }
    }
}
