package com.towershare.app

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import com.towershare.app.databinding.ActivityMainBinding

class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding

    private val permissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { permissions ->
        val allGranted = permissions.entries.all { it.value }
        if (allGranted) {
            startServerService()
        } else {
            Toast.makeText(this, getString(R.string.perm_bt_needed), Toast.LENGTH_LONG).show()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        setupListeners()
        updateUiState(
            BluetoothServerService.isRunning,
            BluetoothServerService.isConnected,
            BluetoothServerService.connectedDeviceName
        )
    }

    override fun onResume() {
        super.onResume()
        checkOverlayPermissionStatus()

        BluetoothServerService.onStateChanged = { running, connected, deviceName ->
            runOnUiThread {
                updateUiState(running, connected, deviceName)
            }
        }
    }

    override fun onPause() {
        super.onPause()
        BluetoothServerService.onStateChanged = null
    }

    private fun setupListeners() {
        binding.btnToggleService.setOnClickListener {
            if (BluetoothServerService.isRunning) {
                stopServerService()
            } else {
                checkAndStartService()
            }
        }

        binding.btnGrantOverlay.setOnClickListener {
            val intent = Intent(
                Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                Uri.parse("package:$packageName")
            )
            startActivity(intent)
        }

        binding.btnOpenBluetooth.setOnClickListener {
            val intent = Intent(Settings.ACTION_BLUETOOTH_SETTINGS)
            startActivity(intent)
        }
    }

    private fun checkOverlayPermissionStatus() {
        if (Settings.canDrawOverlays(this)) {
            binding.tvOverlayStatus.text = "Estado: Concedido (Copia Automática Activa) \u2705"
            binding.tvOverlayStatus.setTextColor(ContextCompat.getColor(this, R.color.status_green))
            binding.btnGrantOverlay.isEnabled = false
            binding.btnGrantOverlay.text = "Permiso Concedido"
        } else {
            binding.tvOverlayStatus.text = "Estado: Pendiente de autorización \u26A0\uFE0F"
            binding.tvOverlayStatus.setTextColor(ContextCompat.getColor(this, R.color.status_orange))
            binding.btnGrantOverlay.isEnabled = true
            binding.btnGrantOverlay.text = getString(R.string.btn_grant_overlay)
        }
    }

    private fun checkAndStartService() {
        val permissionsToRequest = mutableListOf<String>()

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.BLUETOOTH_CONNECT) != PackageManager.PERMISSION_GRANTED) {
                permissionsToRequest.add(Manifest.permission.BLUETOOTH_CONNECT)
            }
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.BLUETOOTH_SCAN) != PackageManager.PERMISSION_GRANTED) {
                permissionsToRequest.add(Manifest.permission.BLUETOOTH_SCAN)
            }
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
                permissionsToRequest.add(Manifest.permission.POST_NOTIFICATIONS)
            }
        }

        if (permissionsToRequest.isNotEmpty()) {
            permissionLauncher.launch(permissionsToRequest.toTypedArray())
        } else {
            startServerService()
        }
    }

    private fun startServerService() {
        val intent = Intent(this, BluetoothServerService::class.java).apply {
            action = BluetoothServerService.ACTION_START
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            startForegroundService(intent)
        } else {
            startService(intent)
        }
    }

    private fun stopServerService() {
        val intent = Intent(this, BluetoothServerService::class.java).apply {
            action = BluetoothServerService.ACTION_STOP
        }
        startService(intent)
    }

    private fun updateUiState(running: Boolean, connected: Boolean, deviceName: String?) {
        if (!running) {
            binding.tvStatus.text = getString(R.string.status_server_stopped)
            binding.tvStatus.setTextColor(ContextCompat.getColor(this, R.color.status_red))
            binding.tvDevice.text = ""
            binding.btnToggleService.text = getString(R.string.btn_start_service)
        } else if (connected) {
            binding.tvStatus.text = getString(R.string.status_connected, deviceName ?: "PC")
            binding.tvStatus.setTextColor(ContextCompat.getColor(this, R.color.status_green))
            binding.tvDevice.text = "Sincronizando en tiempo real con Windows"
            binding.btnToggleService.text = getString(R.string.btn_stop_service)
        } else {
            binding.tvStatus.text = getString(R.string.status_server_running)
            binding.tvStatus.setTextColor(ContextCompat.getColor(this, R.color.status_orange))
            binding.tvDevice.text = "Esperando que presiones 'Conectar' en la app de Windows"
            binding.btnToggleService.text = getString(R.string.btn_stop_service)
        }
    }
}
