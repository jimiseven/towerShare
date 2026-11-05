package com.towershare.app

import android.app.Activity
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.widget.Toast

/**
 * Micro-actividad invisible / transparente en tarea aislada (taskAffinity separada).
 * Adquiere el foco de ventana del sistema operativo durante unos milisegundos
 * para satisfacer la restricción de Android 10/11/12/13/14+ y escribe el texto
 * de forma directa al portapapeles en segundo plano antes de destruirse con finishAndRemoveTask().
 */
class TransparentCopyActivity : Activity() {

    companion object {
        const val EXTRA_TEXT = "extra_text"

        fun createIntent(context: Context, text: String): Intent {
            return Intent(context, TransparentCopyActivity::class.java).apply {
                putExtra(EXTRA_TEXT, text)
                addFlags(
                    Intent.FLAG_ACTIVITY_NEW_TASK or
                    Intent.FLAG_ACTIVITY_MULTIPLE_TASK or
                    Intent.FLAG_ACTIVITY_NO_ANIMATION
                )
            }
        }
    }

    private var pendingText: String? = null
    private var isCopied = false
    private val mainHandler = Handler(Looper.getMainLooper())
    private val safetyRunnable = Runnable {
        if (!isCopied) {
            performCopy()
        } else if (!isFinishing) {
            finish()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        disableAnimations()

        pendingText = intent.getStringExtra(EXTRA_TEXT)

        // Temporizador de seguridad: si tras 350ms no ha llegado el evento de foco,
        // intentamos la copia y cerramos la tarea aislada
        mainHandler.postDelayed(safetyRunnable, 350)
    }

    override fun onNewIntent(intent: Intent?) {
        super.onNewIntent(intent)
        setIntent(intent)
        pendingText = intent?.getStringExtra(EXTRA_TEXT)
        isCopied = false

        if (hasWindowFocus()) {
            mainHandler.removeCallbacks(safetyRunnable)
            performCopy()
        } else {
            mainHandler.removeCallbacks(safetyRunnable)
            mainHandler.postDelayed(safetyRunnable, 350)
        }
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus && !isCopied) {
            mainHandler.removeCallbacks(safetyRunnable)
            performCopy()
        }
    }

    private fun performCopy() {
        if (isCopied) return
        val text = pendingText
        if (!text.isNullOrEmpty()) {
            try {
                val clipboard = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                val clip = ClipData.newPlainText("TowerShare", text)
                clipboard.setPrimaryClip(clip)

                // En Android 12 y anteriores mostramos confirmación visual rápida (en Android 13+ el SO ya muestra el preview nativo)
                if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU) {
                    Toast.makeText(this, "Copiado al portapapeles desde Windows", Toast.LENGTH_SHORT).show()
                }
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }
        isCopied = true
        finish()
    }

    private fun disableAnimations() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            overrideActivityTransition(OVERRIDE_TRANSITION_OPEN, 0, 0)
            overrideActivityTransition(OVERRIDE_TRANSITION_CLOSE, 0, 0)
        } else {
            @Suppress("DEPRECATION")
            overridePendingTransition(0, 0)
        }
        window?.setWindowAnimations(0)
    }

    override fun finish() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            finishAndRemoveTask()
        } else {
            super.finish()
        }
        disableAnimations()
    }

    override fun onDestroy() {
        mainHandler.removeCallbacks(safetyRunnable)
        super.onDestroy()
    }
}
