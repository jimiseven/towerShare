package com.towershare.app

import android.app.Activity
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.Bundle
import android.widget.Toast

/**
 * Micro-actividad invisible / transparente (Opción B).
 * Adquiere el foco de pantalla durante unos milisegundos para satisfacer
 * la restricción de Android 12/13/14 y escribe el texto en el portapapeles local.
 */
class TransparentCopyActivity : Activity() {

    companion object {
        const val EXTRA_TEXT = "extra_text"

        fun createIntent(context: Context, text: String): Intent {
            return Intent(context, TransparentCopyActivity::class.java).apply {
                putExtra(EXTRA_TEXT, text)
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_NO_ANIMATION)
            }
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            overrideActivityTransition(OVERRIDE_TRANSITION_OPEN, 0, 0)
            overrideActivityTransition(OVERRIDE_TRANSITION_CLOSE, 0, 0)
        } else {
            @Suppress("DEPRECATION")
            overridePendingTransition(0, 0)
        }

        val text = intent.getStringExtra(EXTRA_TEXT)
        if (!text.isNullOrEmpty()) {
            try {
                val clipboard = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                val clip = ClipData.newPlainText("TowerShare", text)
                clipboard.setPrimaryClip(clip)

                // En Android 12 y anteriores, mostramos un Toast rápido si no hay preview nativo
                if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU) {
                    Toast.makeText(this, "Copiado al portapapeles desde Windows", Toast.LENGTH_SHORT).show()
                }
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }

        finish()
    }

    override fun finish() {
        super.finish()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            overrideActivityTransition(OVERRIDE_TRANSITION_CLOSE, 0, 0)
        } else {
            @Suppress("DEPRECATION")
            overridePendingTransition(0, 0)
        }
    }
}
