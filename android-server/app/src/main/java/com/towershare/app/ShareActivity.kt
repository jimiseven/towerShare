package com.towershare.app

import android.app.Activity
import android.content.Intent
import android.os.Bundle
import android.widget.Toast

/**
 * Actividad sin interfaz (Theme.NoDisplay) para manejar el menú "Compartir" de Android (ACTION_SEND).
 * Envía el texto recibido directamente a la PC por el socket Bluetooth.
 */
class ShareActivity : Activity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        if (intent?.action == Intent.ACTION_SEND && intent.type == "text/plain") {
            val sharedText = intent.getStringExtra(Intent.EXTRA_TEXT)
            if (!sharedText.isNullOrEmpty()) {
                val sent = BluetoothServerService.sendText(sharedText)
                if (sent) {
                    Toast.makeText(this, getString(R.string.toast_sent_to_windows), Toast.LENGTH_SHORT).show()
                } else {
                    Toast.makeText(this, getString(R.string.toast_not_connected), Toast.LENGTH_LONG).show()
                }
            }
        }

        finish()
    }
}
