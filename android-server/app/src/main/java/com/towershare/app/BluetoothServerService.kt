package com.towershare.app

import android.app.ActivityOptions
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothManager
import android.bluetooth.BluetoothServerSocket
import android.bluetooth.BluetoothSocket
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import android.provider.Settings
import androidx.core.app.NotificationCompat
import androidx.core.app.ServiceCompat
import kotlinx.coroutines.*
import java.io.IOException

class BluetoothServerService : Service() {

    companion object {
        const val CHANNEL_ID = "towershare_channel"
        const val CHANNEL_INCOMING_ID = "towershare_incoming_channel"
        const val NOTIFICATION_ID = 1001
        const val INCOMING_NOTIFICATION_ID = 1002

        const val ACTION_START = "ACTION_START"
        const val ACTION_STOP = "ACTION_STOP"

        var isRunning: Boolean = false
            private set
        var isConnected: Boolean = false
            private set
        var connectedDeviceName: String? = null
            private set

        var onStateChanged: ((running: Boolean, connected: Boolean, deviceName: String?) -> Unit)? = null

        private var activeSocket: BluetoothSocket? = null
        private var lastReceivedText: String? = null
        private var lastSentText: String? = null

        /**
         * Envía texto a Windows por Bluetooth (usado por ShareActivity).
         */
        fun sendText(text: String): Boolean {
            val socket = activeSocket ?: return false
            if (!socket.isConnected) return false

            // Anti-eco: evitar enviar si es exactamente lo que acabamos de recibir
            if (text == lastReceivedText) {
                return true
            }

            return try {
                lastSentText = text
                Protocol.sendMessage(socket.outputStream, text)
                true
            } catch (e: Exception) {
                e.printStackTrace()
                false
            }
        }
    }

    private val serviceScope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private var serverSocket: BluetoothServerSocket? = null
    private var bluetoothAdapter: BluetoothAdapter? = null
    private var screenReceiver: BroadcastReceiver? = null
    private var pendingScreenText: String? = null

    override fun onCreate() {
        super.onCreate()
        createNotificationChannels()
        registerScreenReceiver()
        val bluetoothManager = getSystemService(Context.BLUETOOTH_SERVICE) as BluetoothManager
        bluetoothAdapter = bluetoothManager.adapter
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_STOP) {
            stopServiceInternal()
            return START_NOT_STICKY
        }

        startForegroundServiceNotification("Iniciando servicio...")
        isRunning = true
        notifyState()

        serviceScope.launch {
            runServerLoop()
        }

        return START_STICKY
    }

    private fun runServerLoop() {
        val adapter = bluetoothAdapter
        if (adapter == null || !adapter.isEnabled) {
            updateNotification("Bluetooth no disponible o desactivado")
            return
        }

        while (isRunning) {
            try {
                updateNotification(getString(R.string.service_listening))
                isConnected = false
                connectedDeviceName = null
                activeSocket = null
                notifyState()

                // Abre el socket de escucha RFCOMM con el UUID de TowerShare
                serverSocket = adapter.listenUsingRfcommWithServiceRecord(
                    Protocol.SERVICE_NAME,
                    Protocol.SERVICE_UUID
                )

                val socket = serverSocket?.accept() ?: break
                // Una vez conectado un cliente, cerramos el serverSocket para liberar el puerto
                serverSocket?.close()

                activeSocket = socket
                isConnected = true
                connectedDeviceName = try { socket.remoteDevice.name ?: "Windows PC" } catch (e: SecurityException) { "Windows PC" }
                updateNotification(getString(R.string.service_connected, connectedDeviceName))
                notifyState()

                // Bucle de lectura de mensajes enviados desde Windows
                handleClientCommunication(socket)

            } catch (e: IOException) {
                if (isRunning) {
                    try { Thread.sleep(2000) } catch (ignored: Exception) {}
                }
            } finally {
                try { activeSocket?.close() } catch (ignored: Exception) {}
                activeSocket = null
                isConnected = false
                connectedDeviceName = null
                notifyState()
            }
        }
    }

    private fun handleClientCommunication(socket: BluetoothSocket) {
        val inputStream = socket.inputStream
        while (isRunning && socket.isConnected) {
            try {
                val message = Protocol.readMessage(inputStream) ?: break

                // Control de Eco: Si el texto recibido es igual al último enviado desde Android, se ignora
                if (message == lastSentText) {
                    continue
                }

                lastReceivedText = message

                // Inyección automática al portapapeles sin necesidad de abrir la app
                dispatchToClipboard(message)

            } catch (e: Exception) {
                break
            }
        }
    }

    private fun dispatchToClipboard(text: String) {
        if (Settings.canDrawOverlays(this)) {
            if (isScreenInteractive()) {
                launchTransparentCopy(text)
            } else {
                // Si la pantalla está apagada/bloqueada, guardar para inyectar automáticamente al despertar
                pendingScreenText = text
            }
        } else {
            // Si el usuario aún no autorizó 'Aparecer encima', ofrecer botón rápido en notificación
            showIncomingNotificationWithAction(text)
        }
    }

    private fun launchTransparentCopy(text: String) {
        try {
            val copyIntent = TransparentCopyActivity.createIntent(this, text)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
                val options = ActivityOptions.makeBasic()
                startActivity(copyIntent, options.toBundle())
            } else {
                startActivity(copyIntent)
            }
        } catch (e: Exception) {
            e.printStackTrace()
            // Respaldo en caso de fallo inesperado al abrir la actividad
            showIncomingNotificationWithAction(text)
        }
    }

    private fun isScreenInteractive(): Boolean {
        val pm = getSystemService(Context.POWER_SERVICE) as? PowerManager
        return pm?.isInteractive ?: true
    }

    private fun registerScreenReceiver() {
        if (screenReceiver == null) {
            screenReceiver = object : BroadcastReceiver() {
                override fun onReceive(context: Context, intent: Intent?) {
                    val action = intent?.action
                    if (action == Intent.ACTION_USER_PRESENT || action == Intent.ACTION_SCREEN_ON) {
                        val text = pendingScreenText
                        if (!text.isNullOrEmpty()) {
                            pendingScreenText = null
                            launchTransparentCopy(text)
                        }
                    }
                }
            }
            val filter = IntentFilter().apply {
                addAction(Intent.ACTION_USER_PRESENT)
                addAction(Intent.ACTION_SCREEN_ON)
            }
            registerReceiver(screenReceiver, filter)
        }
    }

    private fun showIncomingNotificationWithAction(text: String) {
        val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager

        val copyIntent = ClipboardActionReceiver.createIntent(this, text, INCOMING_NOTIFICATION_ID)
        val pendingIntent = PendingIntent.getBroadcast(
            this,
            0,
            copyIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val preview = if (text.length > 50) text.take(50) + "..." else text

        val notification = NotificationCompat.Builder(this, CHANNEL_INCOMING_ID)
            .setContentTitle("📋 Texto recibido desde Windows")
            .setContentText(preview)
            .setStyle(NotificationCompat.BigTextStyle().bigText(text))
            .setSmallIcon(R.drawable.ic_stat_sync)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setAutoCancel(true)
            .setContentIntent(pendingIntent)
            .addAction(R.drawable.ic_stat_sync, "📋 Copiar", pendingIntent)
            .build()

        manager.notify(INCOMING_NOTIFICATION_ID, notification)
    }

    private fun startForegroundServiceNotification(statusText: String) {
        val notification = buildNotification(statusText)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            ServiceCompat.startForeground(
                this,
                NOTIFICATION_ID,
                notification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_CONNECTED_DEVICE
            )
        } else {
            startForeground(NOTIFICATION_ID, notification)
        }
    }

    private fun updateNotification(statusText: String) {
        val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        manager.notify(NOTIFICATION_ID, buildNotification(statusText))
    }

    private fun buildNotification(statusText: String): Notification {
        val openAppIntent = Intent(this, MainActivity::class.java)
        val pendingIntent = PendingIntent.getActivity(
            this,
            0,
            openAppIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle(getString(R.string.service_title))
            .setContentText(statusText)
            .setSmallIcon(R.drawable.ic_stat_sync)
            .setOngoing(true)
            .setContentIntent(pendingIntent)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .build()
    }

    private fun createNotificationChannels() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager

            val mainChannel = NotificationChannel(
                CHANNEL_ID,
                "TowerShare Servicio",
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = "Mantiene activa la sincronización de portapapeles con Windows"
            }

            val incomingChannel = NotificationChannel(
                CHANNEL_INCOMING_ID,
                "TowerShare Textos Recibidos",
                NotificationManager.IMPORTANCE_HIGH
            ).apply {
                description = "Notificaciones con botón de copia rápida al recibir texto"
            }

            manager.createNotificationChannel(mainChannel)
            manager.createNotificationChannel(incomingChannel)
        }
    }

    private fun notifyState() {
        CoroutineScope(Dispatchers.Main).launch {
            onStateChanged?.invoke(isRunning, isConnected, connectedDeviceName)
        }
    }

    private fun stopServiceInternal() {
        isRunning = false
        isConnected = false
        connectedDeviceName = null
        try { serverSocket?.close() } catch (ignored: Exception) {}
        try { activeSocket?.close() } catch (ignored: Exception) {}
        if (screenReceiver != null) {
            try { unregisterReceiver(screenReceiver) } catch (ignored: Exception) {}
            screenReceiver = null
        }
        serviceScope.cancel()
        notifyState()
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    override fun onDestroy() {
        stopServiceInternal()
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null
}
