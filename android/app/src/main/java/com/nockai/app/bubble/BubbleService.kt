package com.nockai.app.bubble

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Intent
import android.content.pm.ServiceInfo
import android.graphics.PixelFormat
import android.os.Build
import android.view.Gravity
import android.view.WindowManager
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.ComposeView
import androidx.core.app.NotificationCompat
import androidx.lifecycle.LifecycleService
import androidx.lifecycle.setViewTreeLifecycleOwner
import androidx.savedstate.SavedStateRegistry
import androidx.savedstate.SavedStateRegistryController
import androidx.savedstate.SavedStateRegistryOwner
import androidx.savedstate.setViewTreeSavedStateRegistryOwner
import com.nockai.app.MainActivity
import com.nockai.app.R
import com.nockai.app.ai.CalcResult
import com.nockai.app.ai.CalculatorService
import com.nockai.app.ai.GroqClient
import com.nockai.app.data.AppSettings
import com.nockai.app.data.SecretsStore
import com.nockai.app.data.SettingsStore
import com.nockai.app.ui.NockTheme
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class BubbleService : LifecycleService(), SavedStateRegistryOwner {
    private val savedStateRegistryController = SavedStateRegistryController.create(this)
    override val savedStateRegistry: SavedStateRegistry
        get() = savedStateRegistryController.savedStateRegistry

    private val serviceJob = SupervisorJob()
    private val scope = CoroutineScope(serviceJob + Dispatchers.Main.immediate)

    private lateinit var windowManager: WindowManager
    private var composeView: ComposeView? = null
    private var layoutParams: WindowManager.LayoutParams? = null

    private var expanded by mutableStateOf(false)
    private var query by mutableStateOf("")
    private var busy by mutableStateOf(false)
    private var result by mutableStateOf<CalcResult?>(null)
    private var error by mutableStateOf<String?>(null)
    private var evalJob: Job? = null

    override fun onCreate() {
        savedStateRegistryController.performRestore(null)
        super.onCreate()
        isRunning = true
        windowManager = getSystemService(WINDOW_SERVICE) as WindowManager
        startAsForeground()
        attachOverlay()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        super.onStartCommand(intent, flags, startId)
        if (intent?.action == BubbleController.ACTION_STOP) {
            stopSelf()
            return START_NOT_STICKY
        }
        return START_STICKY
    }

    override fun onDestroy() {
        isRunning = false
        detachOverlay()
        scope.cancel()
        super.onDestroy()
    }

    private fun startAsForeground() {
        val channelId = "nockai_bubble"
        val nm = getSystemService(NotificationManager::class.java)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            nm.createNotificationChannel(
                NotificationChannel(channelId, "NockAI bubble", NotificationManager.IMPORTANCE_LOW).apply {
                    description = "Keeps the bottom math bubble alive"
                }
            )
        }

        val openApp = PendingIntent.getActivity(
            this, 0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        val stop = PendingIntent.getService(
            this, 1,
            Intent(this, BubbleService::class.java).setAction(BubbleController.ACTION_STOP),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )

        val notification: Notification = NotificationCompat.Builder(this, channelId)
            .setContentTitle("NockAI bubble")
            .setContentText("Ask math from the bottom pill")
            .setSmallIcon(R.drawable.ic_stat_bubble)
            .setContentIntent(openApp)
            .addAction(0, "Stop", stop)
            .setOngoing(true)
            .setSilent(true)
            .build()

        if (Build.VERSION.SDK_INT >= 34) {
            startForeground(42, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE)
        } else {
            startForeground(42, notification)
        }
    }

    private fun attachOverlay() {
        val params = WindowManager.LayoutParams(
            WindowManager.LayoutParams.MATCH_PARENT,
            WindowManager.LayoutParams.WRAP_CONTENT,
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
            collapsedFlags(),
            PixelFormat.TRANSLUCENT,
        ).apply {
            gravity = Gravity.BOTTOM or Gravity.CENTER_HORIZONTAL
            softInputMode = WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE
        }
        layoutParams = params

        val view = ComposeView(this).apply {
            setViewTreeLifecycleOwner(this@BubbleService)
            setViewTreeSavedStateRegistryOwner(this@BubbleService)
            setContent {
                NockTheme {
                    BubbleOverlay(
                        expanded = expanded,
                        query = query,
                        busy = busy,
                        result = result,
                        error = error,
                        onExpand = { updateExpanded(true) },
                        onCollapse = { updateExpanded(false) },
                        onQueryChange = {
                            query = it
                            error = null
                        },
                        onSubmit = { submit() },
                        onClose = { stopSelf() },
                    )
                }
            }
        }
        composeView = view
        windowManager.addView(view, params)
    }

    private fun detachOverlay() {
        composeView?.let {
            try {
                windowManager.removeView(it)
            } catch (_: Exception) {
            }
        }
        composeView = null
    }

    private fun collapsedFlags(): Int =
        WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
            WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN or
            WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL

    private fun expandedFlags(): Int =
        WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN or
            WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL

    private fun updateExpanded(value: Boolean) {
        expanded = value
        val params = layoutParams ?: return
        val view = composeView ?: return
        params.flags = if (value) expandedFlags() else collapsedFlags()
        try {
            windowManager.updateViewLayout(view, params)
        } catch (_: Exception) {
        }
    }

    private fun submit() {
        val text = query.trim()
        if (text.isEmpty() || busy) return
        evalJob?.cancel()
        evalJob = scope.launch {
            busy = true
            error = null
            try {
                val key = SecretsStore(this@BubbleService).groqApiKey
                if (key.isBlank()) {
                    error = "Add a Groq key in the app first"
                    result = null
                    return@launch
                }
                val settings = runCatching {
                    SettingsStore(this@BubbleService).settings.first()
                }.getOrElse { AppSettings() }

                val calc = CalculatorService(GroqClient(key))
                result = withContext(Dispatchers.IO) {
                    calc.evaluate(text, history = null, settings = settings)
                }
            } catch (ex: Exception) {
                error = ex.message?.take(120) ?: "Request failed"
            } finally {
                busy = false
            }
        }
    }

    companion object {
        @Volatile
        var isRunning: Boolean = false
            private set
    }
}
