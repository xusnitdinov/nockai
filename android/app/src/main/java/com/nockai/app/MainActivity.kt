package com.nockai.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.nockai.app.ui.MainScreen
import com.nockai.app.ui.NockTheme
import com.nockai.app.ui.NockViewModel
import com.nockai.app.ui.SettingsScreen
import com.nockai.app.ui.SetupScreen

class MainActivity : ComponentActivity() {
    private val vm: NockViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            NockTheme {
                val ui by vm.ui.collectAsState()
                val settings by vm.settings.collectAsState()
                val nav = rememberNavController()
                val start = if (ui.hasKey) "main" else "setup"

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
                            onQuery = vm::setQuery,
                            onFollowUp = vm::setFollowUp,
                            onSubmit = vm::evaluateMain,
                            onFollowSubmit = vm::evaluateFollowUp,
                            onOpenSettings = { nav.navigate("settings") },
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
}
