package com.towershare.app

import android.app.NotificationManager
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

/**
 * Receptor de difusión para atender el botón de acción rápida '[ 📋 Copiar ]'
 * en las notificaciones del sistema sin necesidad de abrir la aplicación principal.
 */
class ClipboardActionReceiver : BroadcastReceiver() {

    companion object {
        const val ACTION_COPY = "com.towershare.app.ACTION_COPY"
        const val EXTRA_TEXT = "extra_copy_text"
        const val EXTRA_NOTIFICATION_ID = "extra_notification_id"

        fun createIntent(context: Context, text: String, notificationId: Int): Intent {
            return Intent(context, ClipboardActionReceiver::class.java).apply {
                action = ACTION_COPY
                putExtra(EXTRA_TEXT, text)
                putExtra(EXTRA_NOTIFICATION_ID, notificationId)
            }
        }
    }

    override fun onReceive(context: Context, intent: Intent?) {
        if (intent?.action != ACTION_COPY) return

        val text = intent.getStringExtra(EXTRA_TEXT) ?: return
        val notifId = intent.getIntExtra(EXTRA_NOTIFICATION_ID, -1)

        // Cancelar o actualizar notificación si corresponde
        if (notifId != -1 && notifId != BluetoothServerService.NOTIFICATION_ID) {
            val nm = context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
            nm.cancel(notifId)
        }

        // Inyectar al portapapeles a través de la micro-actividad transparente
        val copyIntent = TransparentCopyActivity.createIntent(context, text)
        context.startActivity(copyIntent)
    }
}
