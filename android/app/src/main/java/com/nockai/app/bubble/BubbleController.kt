package com.nockai.app.bubble

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.core.content.ContextCompat

object BubbleController {
    const val ACTION_STOP = "com.nockai.app.bubble.STOP"

    fun canDrawOverlays(context: Context): Boolean =
        Settings.canDrawOverlays(context)

    fun openOverlaySettings(activity: Activity) {
        val intent = Intent(
            Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
            Uri.parse("package:${activity.packageName}"),
        )
        activity.startActivity(intent)
    }

    fun isRunning(context: Context): Boolean = BubbleService.isRunning

    fun start(context: Context) {
        if (!canDrawOverlays(context)) return
        val intent = Intent(context, BubbleService::class.java)
        ContextCompat.startForegroundService(context, intent)
    }

    fun stop(context: Context) {
        context.stopService(Intent(context, BubbleService::class.java))
    }

    fun toggle(context: Context): Boolean {
        return if (isRunning(context)) {
            stop(context)
            false
        } else {
            start(context)
            true
        }
    }

    fun needsNotificationPermission(): Boolean =
        Build.VERSION.SDK_INT >= 33
}
