using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TowerShareWindows
{
    public static class Protocol
    {
        // UUID único compartido entre Windows y Android (RFCOMM SPP)
        public static readonly Guid ServiceUuid = new Guid("fa87c0d0-afac-11de-8a39-0800200c9a66");
        public const string ServiceName = "TowerShare Clipboard Service";

        /// <summary>
        /// Escribe un mensaje con framing: 4 bytes de longitud (Big-Endian) + contenido UTF-8.
        /// </summary>
        public static async Task SendMessageAsync(Stream stream, string text, CancellationToken cancellationToken = default)
        {
            if (stream == null || string.IsNullOrEmpty(text))
                return;

            byte[] payload = Encoding.UTF8.GetBytes(text);
            int length = payload.Length;

            // Longitud en Network Byte Order (Big Endian)
            int networkOrderLength = IPAddress.HostToNetworkOrder(length);
            byte[] header = BitConverter.GetBytes(networkOrderLength);

            // Escribir cabecera y payload
            await stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Lee un mensaje con framing: 4 bytes de longitud (Big-Endian) + contenido UTF-8.
        /// Devuelve null si el stream se cierra.
        /// </summary>
        public static async Task<string?> ReadMessageAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            byte[] header = new byte[4];
            int read = await ReadExactAsync(stream, header, 0, 4, cancellationToken).ConfigureAwait(false);
            if (read < 4)
                return null; // Stream cerrado

            int networkOrderLength = BitConverter.ToInt32(header, 0);
            int length = IPAddress.NetworkToHostOrder(networkOrderLength);

            if (length <= 0 || length > 10 * 1024 * 1024) // Límite de seguridad 10MB
                return null;

            byte[] buffer = new byte[length];
            int payloadRead = await ReadExactAsync(stream, buffer, 0, length, cancellationToken).ConfigureAwait(false);
            if (payloadRead < length)
                return null;

            return Encoding.UTF8.GetString(buffer, 0, length);
        }

        private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer, offset + totalBytesRead, count - totalBytesRead, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                    break;
                totalBytesRead += bytesRead;
            }
            return totalBytesRead;
        }
    }
}
