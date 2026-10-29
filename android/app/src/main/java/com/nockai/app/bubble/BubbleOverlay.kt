package com.nockai.app.bubble

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.nockai.app.ai.CalcResult
import com.nockai.app.ai.CalcResultKind
import com.nockai.app.ui.Accent
import com.nockai.app.ui.Bg
import com.nockai.app.ui.Card
import com.nockai.app.ui.CardStroke
import com.nockai.app.ui.ColorWarn
import com.nockai.app.ui.MarkdownText
import com.nockai.app.ui.Outfit
import com.nockai.app.ui.TextPrimary
import com.nockai.app.ui.TextSecondary
import com.nockai.app.ui.TextTertiary

@Composable
fun BubbleOverlay(
    expanded: Boolean,
    query: String,
    busy: Boolean,
    result: CalcResult?,
    error: String?,
    onExpand: () -> Unit,
    onCollapse: () -> Unit,
    onQueryChange: (String) -> Unit,
    onSubmit: () -> Unit,
    onClose: () -> Unit,
) {
    Box(
        Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 10.dp),
        contentAlignment = Alignment.BottomCenter,
    ) {
        AnimatedContent(
            targetState = expanded,
            transitionSpec = {
                (fadeIn() + slideInVertically { it / 3 }) togetherWith
                    (fadeOut() + slideOutVertically { it / 4 })
            },
            label = "bubble",
        ) { isExpanded ->
            if (isExpanded) {
                ExpandedSheet(
                    query = query,
                    busy = busy,
                    result = result,
                    error = error,
                    onQueryChange = onQueryChange,
                    onSubmit = onSubmit,
                    onCollapse = onCollapse,
                    onClose = onClose,
                )
            } else {
                CollapsedPill(onClick = onExpand, onClose = onClose)
            }
        }
    }
}

@Composable
private fun CollapsedPill(onClick: () -> Unit, onClose: () -> Unit) {
    Row(
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Surface(
            onClick = onClick,
            shape = RoundedCornerShape(28.dp),
            color = Card.copy(alpha = 0.96f),
            border = androidx.compose.foundation.BorderStroke(1.dp, Accent.copy(alpha = 0.35f)),
            shadowElevation = 10.dp,
            modifier = Modifier
                .height(52.dp)
                .widthIn(min = 148.dp),
        ) {
            Row(
                Modifier.padding(horizontal = 14.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(10.dp),
            ) {
                Box(
                    Modifier
                        .size(28.dp)
                        .clip(CircleShape)
                        .background(
                            Brush.radialGradient(listOf(Accent, Accent.copy(alpha = 0.55f)))
                        ),
                    contentAlignment = Alignment.Center,
                ) {
                    Text(
                        "N",
                        color = Bg,
                        fontFamily = Outfit,
                        fontWeight = FontWeight.Bold,
                        fontSize = 14.sp,
                    )
                }
                Column {
                    Text(
                        "Ask NockAI",
                        color = TextPrimary,
                        fontFamily = Outfit,
                        fontWeight = FontWeight.SemiBold,
                        fontSize = 14.sp,
                    )
                    Text(
                        "Math anywhere",
                        color = TextTertiary,
                        fontFamily = Outfit,
                        fontSize = 11.sp,
                    )
                }
            }
        }

        Surface(
            onClick = onClose,
            shape = CircleShape,
            color = Card.copy(alpha = 0.9f),
            border = androidx.compose.foundation.BorderStroke(1.dp, CardStroke),
            modifier = Modifier.size(40.dp),
        ) {
            Box(contentAlignment = Alignment.Center) {
                Icon(Icons.Default.Close, contentDescription = "Stop bubble", tint = TextSecondary, modifier = Modifier.size(18.dp))
            }
        }
    }
}

@Composable
private fun ExpandedSheet(
    query: String,
    busy: Boolean,
    result: CalcResult?,
    error: String?,
    onQueryChange: (String) -> Unit,
    onSubmit: () -> Unit,
    onCollapse: () -> Unit,
    onClose: () -> Unit,
) {
    Surface(
        shape = RoundedCornerShape(28.dp),
        color = Card.copy(alpha = 0.98f),
        border = androidx.compose.foundation.BorderStroke(1.dp, Accent.copy(alpha = 0.22f)),
        shadowElevation = 16.dp,
        modifier = Modifier
            .fillMaxWidth()
            .widthIn(max = 480.dp),
    ) {
        Column(
            Modifier
                .padding(horizontal = 18.dp, vertical = 14.dp)
                .heightIn(max = 420.dp),
        ) {
            Row(
                Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Box(
                    Modifier
                        .weight(1f)
                        .clickable(
                            indication = null,
                            interactionSource = remember { MutableInteractionSource() },
                            onClick = onCollapse,
                        ),
                    contentAlignment = Alignment.Center,
                ) {
                    Box(
                        Modifier
                            .width(36.dp)
                            .height(4.dp)
                            .clip(RoundedCornerShape(2.dp))
                            .background(TextTertiary.copy(alpha = 0.55f))
                    )
                }
                IconButton(onClick = onClose, modifier = Modifier.size(32.dp)) {
                    Icon(Icons.Default.Close, null, tint = TextTertiary, modifier = Modifier.size(18.dp))
                }
            }

            Text(
                "NockAI",
                color = TextPrimary,
                fontFamily = Outfit,
                fontWeight = FontWeight.SemiBold,
                fontSize = 18.sp,
            )
            Text(
                "Quick math over any app",
                color = TextTertiary,
                fontFamily = Outfit,
                fontSize = 12.sp,
            )

            Spacer(Modifier.height(14.dp))

            val fieldShape = RoundedCornerShape(16.dp)
            Row(
                Modifier
                    .fillMaxWidth()
                    .clip(fieldShape)
                    .border(1.dp, if (query.isNotBlank()) Accent.copy(alpha = 0.5f) else CardStroke, fieldShape)
                    .background(Bg)
                    .padding(horizontal = 14.dp, vertical = 12.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Box(Modifier.weight(1f)) {
                    if (query.isEmpty()) {
                        Text("2+2, mean, explain…", color = TextTertiary, fontFamily = Outfit, fontSize = 15.sp)
                    }
                    BasicTextField(
                        value = query,
                        onValueChange = onQueryChange,
                        singleLine = true,
                        cursorBrush = SolidColor(Accent),
                        textStyle = TextStyle(color = TextPrimary, fontSize = 15.sp, fontFamily = Outfit),
                        keyboardOptions = KeyboardOptions(imeAction = ImeAction.Go),
                        keyboardActions = KeyboardActions(onGo = { onSubmit() }),
                        modifier = Modifier.fillMaxWidth(),
                    )
                }
                if (busy) {
                    CircularProgressIndicator(Modifier.size(18.dp), color = Accent, strokeWidth = 2.dp)
                } else if (query.isNotBlank()) {
                    IconButton(onClick = onSubmit, modifier = Modifier.size(34.dp)) {
                        Icon(Icons.AutoMirrored.Filled.Send, "Send", tint = Accent)
                    }
                }
            }

            error?.let {
                Spacer(Modifier.height(8.dp))
                Text(it, color = ColorWarn, fontSize = 12.sp, fontFamily = Outfit)
            }

            val show = result != null && result.kind != CalcResultKind.Empty
            if (show) {
                Spacer(Modifier.height(14.dp))
                Column(
                    Modifier
                        .fillMaxWidth()
                        .heightIn(max = 220.dp)
                        .verticalScroll(rememberScrollState()),
                    horizontalAlignment = Alignment.CenterHorizontally,
                ) {
                    when (result!!.kind) {
                        CalcResultKind.Number -> {
                            Text(
                                result.display,
                                color = Accent,
                                fontFamily = Outfit,
                                fontWeight = FontWeight.Bold,
                                fontSize = 40.sp,
                                textAlign = TextAlign.Center,
                                maxLines = 2,
                                overflow = TextOverflow.Ellipsis,
                            )
                            result.secondary?.let {
                                Text(it, color = TextTertiary, fontFamily = Outfit, fontSize = 13.sp)
                            }
                            result.expressionUsed?.let {
                                Spacer(Modifier.height(4.dp))
                                Text(it, color = TextTertiary, fontFamily = Outfit, fontSize = 12.sp)
                            }
                        }
                        CalcResultKind.Explain -> {
                            MarkdownText(result.markdown.orEmpty())
                        }
                        CalcResultKind.Unknown -> {
                            Text("???", color = TextTertiary, fontFamily = Outfit, fontWeight = FontWeight.Bold, fontSize = 32.sp)
                            Text("Not math I can help with.", color = TextTertiary, fontFamily = Outfit, fontSize = 12.sp)
                        }
                        else -> {}
                    }
                }
            }
        }
    }
}
