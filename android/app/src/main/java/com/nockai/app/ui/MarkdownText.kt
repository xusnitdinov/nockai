package com.nockai.app.ui

import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.sp

@Composable
fun MarkdownText(markdown: String, modifier: Modifier = Modifier) {
    val annotated = buildAnnotatedString {
        var i = 0
        val s = markdown
        while (i < s.length) {
            when {
                s.startsWith("**", i) -> {
                    val end = s.indexOf("**", i + 2)
                    if (end < 0) {
                        append(s[i]); i++
                    } else {
                        withStyle(SpanStyle(fontWeight = FontWeight.Bold, color = TextPrimary)) {
                            append(s.substring(i + 2, end))
                        }
                        i = end + 2
                    }
                }
                s.startsWith("==", i) -> {
                    val end = s.indexOf("==", i + 2)
                    if (end < 0) {
                        append(s[i]); i++
                    } else {
                        withStyle(
                            SpanStyle(
                                background = Color(0x3360CDFF),
                                color = Accent,
                                fontWeight = FontWeight.SemiBold,
                            )
                        ) {
                            append(s.substring(i + 2, end))
                        }
                        i = end + 2
                    }
                }
                s.startsWith("`", i) -> {
                    val end = s.indexOf('`', i + 1)
                    if (end < 0) {
                        append(s[i]); i++
                    } else {
                        withStyle(
                            SpanStyle(
                                fontFamily = FontFamily.Monospace,
                                background = Color(0x22FFFFFF),
                                color = TextPrimary,
                            )
                        ) {
                            append(s.substring(i + 1, end))
                        }
                        i = end + 1
                    }
                }
                else -> {
                    append(s[i])
                    i++
                }
            }
        }
    }
    Text(
        text = annotated,
        modifier = modifier,
        color = TextSecondary,
        fontSize = 16.sp,
        lineHeight = 24.sp,
    )
}
