package com.nockai.app

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.core.content.ContextCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.nockai.app.bubble.BubbleController
import com.nockai.app.ui.MainScreen
import com.nockai.app.ui.NockTheme
import com.nockai.app.ui.NockViewModel
import com.nockai.app.ui.SettingsScreen
import com.nockai.app.ui.SetupScreen

class MainActivity : ComponentActivity() {
    private val vm: NockViewModel by viewModels()

    private val notifPermission = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { /* bubble can still run without notif on some OEMs */ }

    private val overlayReturn = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) {
        if (BubbleController.canDrawOverlays(this)) {
            BubbleController.start(this)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            NockTheme {
                val ui by vm.ui.collectAsState()
                val settings by vm.settings.collectAsState()
                val nav = rememberNavController()
                val start = if (ui.hasKey) "main" else "setup"
                var bubbleOn by remember { mutableStateOf(BubbleController.isRunning(this)) }

                DisposableEffect(Unit) {
                    val observer = LifecycleEventObserver { _, event ->
                        if (event == Lifecycle.Event.ON_RESUME) {
                            bubbleOn = BubbleController.isRunning(this@MainActivity)
                        }
                    }
                    lifecycle.addObserver(observer)
                    onDispose { lifecycle.removeObserver(observer) }
                }

                NavHost(navController = nav, startDestination = start) {
                    composable("setup") {
                        SetupScreen(onSaved = {
                            vm.saveApiKey(it)
                            nav.navigate("main") {
                                popUpTo("setup") { inclusive = true }
                            }
                        })
                    }
                    composable("main") {
                        MainScreen(
                            ui = ui,
                            settings = settings,
                            bubbleOn = bubbleOn,
                            onQuery = vm::setQuery,
                            onFollowUp = vm::setFollowUp,
                            onSubmit = vm::evaluateMain,
                            onFollowSubmit = vm::evaluateFollowUp,
                            onOpenSettings = { nav.navigate("settings") },
                            onToggleBubble = {
                                ensureBubblePermissionsThen {
                                    bubbleOn = BubbleController.toggle(this@MainActivity)
                                }
                            },
                        )
                    }
                    composable("settings") {
                        SettingsScreen(
                            settings = settings,
                            onBack = { nav.popBackStack() },
                            onSave = vm::saveSettings,
                            onClearKey = {
                                vm.clearApiKey()
                                nav.navigate("setup") {
                                    popUpTo(0) { inclusive = true }
                                }
                            },
                        )
                    }
                }
            }
        }
    }

    private fun ensureBubblePermissionsThen(onReady: () -> Unit) {
        if (BubbleController.needsNotificationPermission()) {
            val granted = ContextCompat.checkSelfPermission(
                this, Manifest.permission.POST_NOTIFICATIONS
            ) == PackageManager.PERMISSION_GRANTED
            if (!granted && Build.VERSION.SDK_INT >= 33) {
                notifPermission.launch(Manifest.permission.POST_NOTIFICATIONS)
            }
        }

        if (!BubbleController.canDrawOverlays(this)) {
            val intent = android.content.Intent(
                android.provider.Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                android.net.Uri.parse("package:$packageName"),
            )
            overlayReturn.launch(intent)
            return
        }

        onReady()
    }
}
