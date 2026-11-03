using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;

namespace TowerShareWindows
{
    public class BluetoothDeviceInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected
    }

    public class BluetoothClient : IDisposable
    {
        private StreamSocket? _socket;
        private Stream? _readStream;
        private Stream? _writeStream;
        private CancellationTokenSource? _connectionCts;
        private bool _isDisposed;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public string? ConnectedDeviceName { get; private set; }

        public event Action<ConnectionState, string?>? StateChanged;
        public event Action<string>? MessageReceived;
        public event Action<string>? LogMessage;

        /// <summary>
        /// Obtiene la lista de dispositivos Bluetooth emparejados con Windows.
        /// </summary>
        public static async Task<List<BluetoothDeviceInfo>> GetPairedDevicesAsync()
        {
            var list = new List<BluetoothDeviceInfo>();
            string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var devices = await DeviceInformation.FindAllAsync(selector);

            foreach (var dev in devices)
            {
                if (!string.IsNullOrWhiteSpace(dev.Name))
                {
                    list.Add(new BluetoothDeviceInfo
                    {
                        Id = dev.Id,
                        Name = dev.Name
                    });
                }
            }

            return list;
        }

        /// <summary>
        /// Conecta al dispositivo Bluetooth seleccionado buscando el servicio RFCOMM de TowerShare.
        /// </summary>
        public async Task ConnectAsync(string deviceId, string deviceName)
        {
            Disconnect();

            SetState(ConnectionState.Connecting, deviceName);
            Log($"Iniciando conexión con {deviceName}...");

            _connectionCts = new CancellationTokenSource();
            var ct = _connectionCts.Token;

            try
            {
                using var bluetoothDevice = await BluetoothDevice.FromIdAsync(deviceId).AsTask(ct);
                if (bluetoothDevice == null)
                {
                    throw new InvalidOperationException($"No se pudo acceder al dispositivo Bluetooth '{deviceName}'.");
                }

                Log("Buscando servicio RFCOMM de TowerShare en el celular...");
                var serviceId = RfcommServiceId.FromUuid(Protocol.ServiceUuid);

                // Consulta de servicios RFCOMM (primero Uncached, fallback Cached)
                var rfcommResult = await bluetoothDevice.GetRfcommServicesForIdAsync(serviceId, BluetoothCacheMode.Uncached).AsTask(ct);
                if (rfcommResult.Services.Count == 0)
                {
                    rfcommResult = await bluetoothDevice.GetRfcommServicesForIdAsync(serviceId, BluetoothCacheMode.Cached).AsTask(ct);
                }

                if (rfcommResult.Services.Count == 0)
                {
                    throw new InvalidOperationException(
                        "No se encontró el servicio TowerShare en el teléfono.\n" +
                        "Asegúrate de que la app de Android esté abierta y el servidor Bluetooth activo.");
                }

                var service = rfcommResult.Services[0];
                _socket = new StreamSocket();

                Log("Estableciendo socket Bluetooth...");
                await _socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName).AsTask(ct);

                _readStream = _socket.InputStream.AsStreamForRead();
                _writeStream = _socket.OutputStream.AsStreamForWrite();

                SetState(ConnectionState.Connected, deviceName);
                Log($"¡Conexión establecida con éxito con {deviceName}!");

                // Iniciar tarea de lectura en segundo plano
                _ = Task.Run(() => ReadLoopAsync(ct), ct);
            }
            catch (Exception ex)
            {
                Log($"Error al conectar: {ex.Message}");
                Disconnect();
                throw;
            }
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _readStream != null)
                {
                    string? message = await Protocol.ReadMessageAsync(_readStream, ct);
                    if (message == null)
                    {
                        Log("El servidor remoto cerró la conexión.");
                        break;
                    }

                    Log($"[Recibido de Android] ({message.Length} caracteres)");
                    MessageReceived?.Invoke(message);
                }
            }
            catch (OperationCanceledException)
            {
                // Conexión terminada intencionalmente
            }
            catch (Exception ex)
            {
                Log($"Error en la lectura del socket: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Envía texto al teléfono Android vía RFCOMM.
        /// </summary>
        public async Task<bool> SendTextAsync(string text)
        {
            if (State != ConnectionState.Connected || _writeStream == null)
            {
                return false;
            }

            try
            {
                await Protocol.SendMessageAsync(_writeStream, text);
                Log($"[Enviado a Android] ({text.Length} caracteres)");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error al enviar texto a Android: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        public void Disconnect()
        {
            _connectionCts?.Cancel();
            _connectionCts?.Dispose();
            _connectionCts = null;

            try { _readStream?.Dispose(); } catch { }
            try { _writeStream?.Dispose(); } catch { }
            try { _socket?.Dispose(); } catch { }

            _readStream = null;
            _writeStream = null;
            _socket = null;

            SetState(ConnectionState.Disconnected, null);
        }

        private void SetState(ConnectionState state, string? deviceName)
        {
            State = state;
            ConnectedDeviceName = deviceName;
            StateChanged?.Invoke(state, deviceName);
        }

        private void Log(string message)
        {
            LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                Disconnect();
                _isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
