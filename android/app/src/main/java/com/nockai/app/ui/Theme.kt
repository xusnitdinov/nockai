package com.nockai.app.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp
import com.nockai.app.R

val Accent = Color(0xFF60CDFF)
val Bg = Color(0xFF0E1114)
val BgElevated = Color(0xFF161A1F)
val Card = Color(0xFF1C2229)
val CardStroke = Color(0x22FFFFFF)
val TextPrimary = Color(0xFFF4F7FA)
val TextSecondary = Color(0xFFA8B0BA)
val TextTertiary = Color(0xFF6F7884)
val ColorWarn = Color(0xFFFFB4A8)

val Outfit = FontFamily(
    Font(R.font.outfit_wght, FontWeight.Light),
    Font(R.font.outfit_wght, FontWeight.Normal),
    Font(R.font.outfit_wght, FontWeight.Medium),
    Font(R.font.outfit_wght, FontWeight.SemiBold),
    Font(R.font.outfit_wght, FontWeight.Bold),
)

private val scheme = darkColorScheme(
    primary = Accent,
    onPrimary = Color(0xFF041018),
    background = Bg,
    onBackground = TextPrimary,
    surface = Card,
    onSurface = TextPrimary,
    surfaceVariant = BgElevated,
    onSurfaceVariant = TextSecondary,
    outline = CardStroke,
)

private val type = Typography(
    displayLarge = TextStyle(fontFamily = Outfit, fontWeight = FontWeight.Bold, fontSize = 48.sp, letterSpacing = (-1).sp),
    displayMedium = TextStyle(fontFamily = Outfit, fontWeight = FontWeight.SemiBold, fontSize = 36.sp, letterSpacing = (-0.5).sp),
    headlineLarge = TextStyle(fontFamily = Outfit, fontWeight = FontWeight.SemiBold, fontSize = 28.sp),
    headlineMedium = TextStyle(fontFamily = Outfit, fontWeight = FontWeight.SemiBold, fontSize = 22.sp),
    titleLarge = TextStyle(fontFamily = Outfit, fontWeight = FontWeight.SemiBold, fontSize = 20.sp),
    titleMedium = TextStyle(fontFamily = Outfit, fontWeight = FontWeight.Medium, fontSize = 16.sp),
    bodyLarge = TextStyle(fontFamily = Outfit, fontWeight = FontWeight.Normal, fontSize = 16.sp, lineHeight = 24.sp),
    bodyMedium = TextStyle(fontFamily = Outfit, fontWeight = FontWeight.Normal, fontSize = 14.sp, lineHeight = 20.sp),
    labelLarge = TextStyle(fontFamily = Outfit, fontWeight = FontWeight.SemiBold, fontSize = 14.sp),
    labelMedium = TextStyle(fontFamily = Outfit, fontWeight = FontWeight.Medium, fontSize = 12.sp),
)

@Composable
fun NockTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = scheme, typography = type, content = content)
}

@Composable
fun AmbientBackground(modifier: Modifier = Modifier, content: @Composable () -> Unit) {
    BoxWithConstraints(modifier.fillMaxSize()) {
        val density = LocalDensity.current
        val w = with(density) { maxWidth.toPx() }
        val h = with(density) { maxHeight.toPx() }
        Box(
            Modifier
                .fillMaxSize()
                .background(Bg)
                .background(
                    Brush.radialGradient(
                        colors = listOf(Accent.copy(alpha = 0.22f), Accent.copy(alpha = 0.05f), Color.Transparent),
                        center = Offset(w * 0.5f, h * 0.16f),
                        radius = w * 0.95f,
                    )
                )
        ) {
            content()
        }
    }
}
