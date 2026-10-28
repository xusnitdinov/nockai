package com.nockai.app.ui

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

val Accent = Color(0xFF60CDFF)
val Bg = Color(0xFF1C1C1C)
val Card = Color(0xFF252525)
val TextPrimary = Color(0xFFFFFFFF)
val TextSecondary = Color(0xFFB3B3B3)
val TextTertiary = Color(0xFF8A8A8A)

private val scheme = darkColorScheme(
    primary = Accent,
    onPrimary = Color.Black,
    background = Bg,
    onBackground = TextPrimary,
    surface = Card,
    onSurface = TextPrimary,
    surfaceVariant = Color(0xFF2C2C2C),
    onSurfaceVariant = TextSecondary,
    outline = Color(0x28FFFFFF),
)

@Composable
fun NockTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = scheme, content = content)
}
