package com.towershare.app

import java.io.DataInputStream
import java.io.DataOutputStream
import java.io.InputStream
import java.io.OutputStream
import java.nio.charset.StandardCharsets
import java.util.UUID

object Protocol {
    // Mismo UUID configurado en Windows
    val SERVICE_UUID: UUID = UUID.fromString("fa87c0d0-afac-11de-8a39-0800200c9a66")
    const val SERVICE_NAME = "TowerShare Clipboard Service"

    /**
     * Escribe un mensaje con framing: 4 bytes int (Big Endian) + payload UTF-8.
     */
    fun sendMessage(outputStream: OutputStream, text: String) {
        val payload = text.toByteArray(StandardCharsets.UTF_8)
        val dataOut = DataOutputStream(outputStream)
        dataOut.writeInt(payload.size)
        dataOut.write(payload)
        dataOut.flush()
    }

    /**
     * Lee un mensaje con framing: 4 bytes int (Big Endian) + payload UTF-8.
     * Retorna null si el stream finaliza o los datos son inválidos.
     */
    fun readMessage(inputStream: InputStream): String? {
        val dataIn = DataInputStream(inputStream)
        val length = try {
            dataIn.readInt()
        } catch (e: Exception) {
            return null
        }

        if (length <= 0 || length > 10 * 1024 * 1024) { // Límite de seguridad 10MB
            return null
        }

        val buffer = ByteArray(length)
        dataIn.readFully(buffer)
        return String(buffer, StandardCharsets.UTF_8)
    }
}
