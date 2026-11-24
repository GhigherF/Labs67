package com.example.design

import android.Manifest
import android.app.Activity
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.net.Uri
import android.os.Build
import android.os.BatteryManager
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.net.wifi.WifiInfo
import android.net.wifi.WifiManager
import android.content.Intent
import android.provider.MediaStore
import android.location.LocationManager
import android.util.Log
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.ByteArrayOutputStream

class MainActivity : FlutterActivity() {

    private companion object {
        const val CHANNEL = "wifi_channel"
        const val DEVICE_ACTIONS_CHANNEL = "device_actions"
        const val BATTERY_CHANNEL = "battery_channel"
        const val PERMISSION_REQUEST_CODE = 1001
        const val CAMERA_REQUEST_CODE = 1002
    }

    private var pendingResult: MethodChannel.Result? = null
    private var pendingCameraResult: MethodChannel.Result? = null

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            CHANNEL
        ).setMethodCallHandler { call, result ->

            when (call.method) {
                "getWifiSSID" -> returnWifiSSID(result)
                else -> result.notImplemented()
            }
        }

        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            DEVICE_ACTIONS_CHANNEL
        ).setMethodCallHandler { call, result ->
            when (call.method) {
                "openMap" -> openMap(call.arguments, result)
                "capturePhoto" -> capturePhoto(result)
                else -> result.notImplemented()
            }
        }

        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            BATTERY_CHANNEL
        ).setMethodCallHandler { call, result ->
            when (call.method) {
                "getBatteryLevel" -> getBatteryLevel(result)
                else -> result.notImplemented()
            }
        }
    }

    @Suppress("DEPRECATION")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (requestCode != CAMERA_REQUEST_CODE) return

        val result = pendingCameraResult
        pendingCameraResult = null
        if (result == null) return

        if (resultCode != Activity.RESULT_OK) {
            result.success(null)
            return
        }

        val bitmap = data?.extras?.get("data") as? Bitmap
        if (bitmap == null) {
            result.error(
                "PHOTO_UNAVAILABLE",
                "Камера не вернула фотографию",
                null
            )
            return
        }

        val output = ByteArrayOutputStream()
        if (bitmap.compress(Bitmap.CompressFormat.JPEG, 90, output)) {
            result.success(output.toByteArray())
        } else {
            result.error(
                "PHOTO_ENCODING_FAILED",
                "Не удалось подготовить фотографию",
                null
            )
        }
    }

    private fun openMap(arguments: Any?, result: MethodChannel.Result) {
        val coordinates = arguments as? Map<*, *>
        val latitude = (coordinates?.get("latitude") as? Number)?.toDouble()
        val longitude = (coordinates?.get("longitude") as? Number)?.toDouble()

        if (latitude == null || longitude == null ||
            !latitude.isFinite() || !longitude.isFinite() ||
            latitude !in -90.0..90.0 || longitude !in -180.0..180.0
        ) {
            result.error(
                "INVALID_COORDINATES",
                "Укажите корректные координаты",
                null
            )
            return
        }

        val mapUri = Uri.parse("geo:$latitude,$longitude?q=$latitude,$longitude")
        try {
            startActivity(Intent(Intent.ACTION_VIEW, mapUri))
            result.success(null)
        } catch (e: android.content.ActivityNotFoundException) {
            result.error(
                "MAP_APP_UNAVAILABLE",
                "На устройстве не найдено приложение карт",
                null
            )
        } catch (e: Exception) {
            result.error("MAP_LAUNCH_FAILED", e.message, null)
        }
    }

    private fun capturePhoto(result: MethodChannel.Result) {
        if (pendingCameraResult != null) {
            result.error(
                "CAMERA_REQUEST_IN_PROGRESS",
                "Съёмка фотографии уже выполняется",
                null
            )
            return
        }

        pendingCameraResult = result
        try {
            startActivityForResult(
                Intent(MediaStore.ACTION_IMAGE_CAPTURE),
                CAMERA_REQUEST_CODE
            )
        } catch (e: Exception) {
            pendingCameraResult = null
            result.error("CAMERA_LAUNCH_FAILED", e.message, null)
        }
    }

    private fun getBatteryLevel(result: MethodChannel.Result) {
        try {
            val batteryManager = getSystemService(BATTERY_SERVICE) as BatteryManager
            val level = batteryManager.getIntProperty(
                BatteryManager.BATTERY_PROPERTY_CAPACITY
            )

            if (level !in 0..100) {
                result.error(
                    "BATTERY_UNAVAILABLE",
                    "Уровень заряда батареи недоступен",
                    null
                )
                return
            }

            result.success(level)
        } catch (e: Exception) {
            result.error("BATTERY_ERROR", e.message, null)
        }
    }

    private fun returnWifiSSID(result: MethodChannel.Result) {
        try {
            val connectivityManager =
                getSystemService(CONNECTIVITY_SERVICE) as ConnectivityManager

            val network = connectivityManager.activeNetwork
            val capabilities =
                connectivityManager.getNetworkCapabilities(network)

            if (capabilities == null ||
                !capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)
            ) {
                result.error(
                    "NO_WIFI",
                    "Устройство не подключено к Wi-Fi",
                    null
                )
                return
            }

            if (ContextCompat.checkSelfPermission(
                    this,
                    Manifest.permission.ACCESS_FINE_LOCATION
                ) != PackageManager.PERMISSION_GRANTED
            ) {
                if (pendingResult != null) {
                    result.error(
                        "PERMISSION_REQUEST_IN_PROGRESS",
                        "Запрос разрешения уже выполняется",
                        null
                    )
                    return
                }

                pendingResult = result
                ActivityCompat.requestPermissions(
                    this,
                    arrayOf(
                        Manifest.permission.ACCESS_FINE_LOCATION,
                        Manifest.permission.ACCESS_COARSE_LOCATION
                    ),
                    PERMISSION_REQUEST_CODE
                )
                return
            }

            if (!isLocationEnabled()) {
                result.error(
                    "LOCATION_DISABLED",
                    "Чтобы получить имя Wi-Fi сети, включите геолокацию в настройках Android и обновите данные",
                    null
                )
                return
            }

            val transportInfo = getTransportWifiInfo(capabilities)
            val wifiManager = applicationContext.getSystemService(WIFI_SERVICE) as WifiManager
            val managerInfo = wifiManager.connectionInfo
            val wifiInfo = transportInfo?.takeIf { it.ssid.isUsableSsid() }
                ?: managerInfo?.takeIf { it.ssid.isUsableSsid() }
                ?: transportInfo
            val ssid = wifiInfo?.ssid?.removeSurrounding("\"")

            if (ssid == null ||
                ssid.isBlank() ||
                ssid == WifiManager.UNKNOWN_SSID ||
                ssid == "<unknown ssid>"
            ) {
                Log.w(
                    "WifiSSID",
                    "SSID unavailable: api=${Build.VERSION.SDK_INT}, " +
                        "transportInfoType=${capabilities.transportInfo?.javaClass?.name}, " +
                        "transportSsidUnknown=${transportInfo?.ssid.isUnavailableSsid()}, " +
                        "managerSsidUnknown=${managerInfo?.ssid.isUnavailableSsid()}, " +
                        "fineLocationGranted=${ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED}, " +
                        "locationEnabled=${isLocationEnabled()}"
                )
                result.error(
                    "SSID_UNAVAILABLE",
                    "Android не предоставил SSID. Проверьте точное разрешение геолокации, включите системную геолокацию и повторите попытку",
                    null
                )
                return
            }

            result.success(ssid)

        } catch (e: Exception) {
            if (pendingResult === result) {
                pendingResult = null
            }
            result.error(
                "WIFI_ERROR",
                e.message,
                null
            )
        }
    }

    @Suppress("DEPRECATION")
    override fun onRequestPermissionsResult(
        requestCode: Int,
        permissions: Array<out String>,
        grantResults: IntArray
    ) {
        super.onRequestPermissionsResult(
            requestCode,
            permissions,
            grantResults
        )

        if (requestCode == PERMISSION_REQUEST_CODE) {
            val result = pendingResult
            pendingResult = null

            if (ContextCompat.checkSelfPermission(
                    this,
                    Manifest.permission.ACCESS_FINE_LOCATION
                ) == PackageManager.PERMISSION_GRANTED
            ) {
                result?.let {
                    returnWifiSSID(it)
                }
            } else {
                result?.error(
                    "PERMISSION_DENIED",
                    "Разрешите приложению точное местоположение. Приблизительного разрешения недостаточно для получения SSID",
                    null
                )
            }
        }
    }

    @Suppress("DEPRECATION")
    private fun getTransportWifiInfo(capabilities: NetworkCapabilities): WifiInfo? {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            capabilities.transportInfo as? WifiInfo
        } else {
            null
        }
    }

    private fun String?.isUsableSsid(): Boolean {
        return !this.isNullOrBlank() &&
            this != WifiManager.UNKNOWN_SSID &&
            this != "<unknown ssid>"
    }

    private fun String?.isUnavailableSsid(): Boolean {
        return this.isNullOrBlank() ||
            this == WifiManager.UNKNOWN_SSID ||
            this == "<unknown ssid>"
    }

    @Suppress("DEPRECATION")
    private fun isLocationEnabled(): Boolean {
        val locationManager = getSystemService(LOCATION_SERVICE) as LocationManager
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            locationManager.isLocationEnabled
        } else {
            locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER) ||
                locationManager.isProviderEnabled(LocationManager.NETWORK_PROVIDER)
        }
    }
}
