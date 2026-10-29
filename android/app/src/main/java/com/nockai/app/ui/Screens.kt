package com.nockai.app.ui

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Slider
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.nockai.app.ai.CalcResult
import com.nockai.app.ai.CalcResultKind
import com.nockai.app.data.AppSettings

@Composable
fun fieldColors() = OutlinedTextFieldDefaults.colors(
    focusedBorderColor = Accent,
    unfocusedBorderColor = CardStroke,
    focusedTextColor = TextPrimary,
    unfocusedTextColor = TextPrimary,
    cursorColor = Accent,
    focusedLabelColor = Accent,
    unfocusedLabelColor = TextTertiary,
    focusedContainerColor = Card,
    unfocusedContainerColor = Card,
)

@Composable
fun SetupScreen(onSaved: (String) -> Unit) {
    var key by remember { mutableStateOf("") }
    AmbientBackground {
        Column(
            Modifier
                .fillMaxSize()
                .statusBarsPadding()
                .navigationBarsPadding()
                .imePadding()
                .padding(horizontal = 28.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            Text(
                "NockAI",
                style = MaterialThemeDisplay(),
                color = TextPrimary,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(10.dp))
            Text(
                "Paste your Groq key to start.",
                color = TextSecondary,
                fontSize = 16.sp,
                fontFamily = Outfit,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(36.dp))
            Surface(
                modifier = Modifier
                    .fillMaxWidth()
                    .widthIn(max = 420.dp),
                shape = RoundedCornerShape(18.dp),
                color = Card,
                border = androidx.compose.foundation.BorderStroke(1.dp, CardStroke),
            ) {
                Column(Modifier.padding(20.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
                    OutlinedTextField(
                        value = key,
                        onValueChange = { key = it },
                        label = { Text("Groq API key") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        visualTransformation = PasswordVisualTransformation(),
                        colors = fieldColors(),
                        shape = RoundedCornerShape(12.dp),
                    )
                    Button(
                        onClick = { if (key.isNotBlank()) onSaved(key.trim()) },
                        enabled = key.isNotBlank(),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = Accent,
                            contentColor = Bg,
                            disabledContainerColor = Card.copy(alpha = 0.6f),
                            disabledContentColor = TextTertiary,
                        ),
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(52.dp),
                        shape = RoundedCornerShape(14.dp),
                    ) {
                        Text("Continue", fontFamily = Outfit, fontWeight = FontWeight.SemiBold, fontSize = 16.sp)
                    }
                }
            }
            Spacer(Modifier.height(18.dp))
            Text(
                "console.groq.com/keys",
                color = TextTertiary,
                fontSize = 13.sp,
                fontFamily = Outfit,
            )
        }
    }
}

@Composable
private fun MaterialThemeDisplay() = TextStyle(
    fontFamily = Outfit,
    fontWeight = FontWeight.Bold,
    fontSize = 42.sp,
    letterSpacing = (-1).sp,
)

@Composable
fun MainScreen(
    ui: UiState,
    settings: AppSettings,
    onQuery: (String) -> Unit,
    onFollowUp: (String) -> Unit,
    onSubmit: () -> Unit,
    onFollowSubmit: () -> Unit,
    onOpenSettings: () -> Unit,
) {
    val clipboard = LocalClipboardManager.current
    val hasResult = ui.result != null && ui.result.kind != CalcResultKind.Empty
    val examples = listOf("2+2", "mean of 1 2 3 4", "explain median")

    AmbientBackground {
        Box(
            Modifier
                .fillMaxSize()
                .statusBarsPadding()
                .navigationBarsPadding()
                .imePadding()
        ) {
            // Settings - floating
            IconButton(
                onClick = onOpenSettings,
                modifier = Modifier
                    .align(Alignment.TopEnd)
                    .padding(8.dp)
                    .clip(CircleShape)
            ) {
                Icon(Icons.Default.Settings, contentDescription = "Settings", tint = TextSecondary)
            }

            AnimatedContent(
                targetState = hasResult,
                transitionSpec = {
                    (fadeIn() + slideInVertically { it / 12 }) togetherWith
                        (fadeOut() + slideOutVertically { -it / 12 })
                },
                label = "main-state",
                modifier = Modifier.fillMaxSize(),
            ) { showingResult ->
                if (!showingResult) {
                    EmptyHome(
                        query = ui.query,
                        busy = ui.busy,
                        error = ui.error,
                        examples = examples,
                        onQuery = onQuery,
                        onSubmit = onSubmit,
                        onExample = { onQuery(it); onSubmit() },
                    )
                } else {
                    ResultHome(
                        ui = ui,
                        settings = settings,
                        onQuery = onQuery,
                        onFollowUp = onFollowUp,
                        onSubmit = onSubmit,
                        onFollowSubmit = onFollowSubmit,
                        onCopy = { clipboard.setText(AnnotatedString(it)) },
                    )
                }
            }
        }
    }
}

@Composable
private fun EmptyHome(
    query: String,
    busy: Boolean,
    error: String?,
    examples: List<String>,
    onQuery: (String) -> Unit,
    onSubmit: () -> Unit,
    onExample: (String) -> Unit,
) {
    Column(
        Modifier
            .fillMaxSize()
            .padding(horizontal = 24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text("NockAI", style = MaterialThemeDisplay(), color = TextPrimary, textAlign = TextAlign.Center)
        Spacer(Modifier.height(8.dp))
        Text(
            "Type math. Get the answer.",
            color = TextSecondary,
            fontSize = 16.sp,
            fontFamily = Outfit,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(36.dp))

        QueryField(
            value = query,
            onValueChange = onQuery,
            onSubmit = onSubmit,
            busy = busy,
            placeholder = "Ask anything math…",
            modifier = Modifier
                .fillMaxWidth()
                .widthIn(max = 440.dp),
        )

        Spacer(Modifier.height(14.dp))

        Button(
            onClick = onSubmit,
            enabled = query.isNotBlank() && !busy,
            colors = ButtonDefaults.buttonColors(
                containerColor = Accent,
                contentColor = Bg,
                disabledContainerColor = BgElevated,
                disabledContentColor = TextTertiary,
            ),
            modifier = Modifier
                .fillMaxWidth()
                .widthIn(max = 440.dp)
                .height(52.dp),
            shape = RoundedCornerShape(16.dp),
            elevation = ButtonDefaults.buttonElevation(defaultElevation = 0.dp),
        ) {
            if (busy) {
                CircularProgressIndicator(Modifier.size(20.dp), color = Bg, strokeWidth = 2.dp)
            } else {
                Text("Calculate", fontFamily = Outfit, fontWeight = FontWeight.SemiBold, fontSize = 16.sp)
            }
        }

        error?.let {
            Spacer(Modifier.height(12.dp))
            Text(it, color = ColorWarn, fontSize = 13.sp, fontFamily = Outfit, textAlign = TextAlign.Center)
        }

        Spacer(Modifier.height(28.dp))
        Text("Try", color = TextTertiary, fontSize = 12.sp, fontFamily = Outfit, fontWeight = FontWeight.Medium)
        Spacer(Modifier.height(10.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            examples.forEach { ex ->
                ExampleChip(ex) { onExample(ex) }
            }
        }
    }
}

@Composable
private fun ExampleChip(label: String, onClick: () -> Unit) {
    Surface(
        onClick = onClick,
        shape = RoundedCornerShape(20.dp),
        color = Card.copy(alpha = 0.85f),
        border = androidx.compose.foundation.BorderStroke(1.dp, CardStroke),
    ) {
        Text(
            label,
            modifier = Modifier.padding(horizontal = 14.dp, vertical = 8.dp),
            color = TextSecondary,
            fontSize = 13.sp,
            fontFamily = Outfit,
        )
    }
}

@Composable
private fun ResultHome(
    ui: UiState,
    settings: AppSettings,
    onQuery: (String) -> Unit,
    onFollowUp: (String) -> Unit,
    onSubmit: () -> Unit,
    onFollowSubmit: () -> Unit,
    onCopy: (String) -> Unit,
) {
    val result = ui.result!!
    Column(
        Modifier
            .fillMaxSize()
            .padding(horizontal = 20.dp),
    ) {
        Column(
            Modifier
                .weight(1f)
                .fillMaxWidth()
                .verticalScroll(rememberScrollState())
                .padding(top = 48.dp, bottom = 16.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            AnimatedVisibility(visible = true, enter = fadeIn()) {
                ResultHero(result = result, onCopy = { onCopy(result.copyText) })
            }

            ui.error?.let {
                Spacer(Modifier.height(12.dp))
                Text(it, color = ColorWarn, fontSize = 13.sp, fontFamily = Outfit, textAlign = TextAlign.Center)
            }

            if (settings.enableChatHistory && result.ok) {
                Spacer(Modifier.height(28.dp))
                QueryField(
                    value = ui.followUp,
                    onValueChange = onFollowUp,
                    onSubmit = onFollowSubmit,
                    busy = ui.busy,
                    placeholder = "Follow up… more examples, simpler",
                    modifier = Modifier
                        .fillMaxWidth()
                        .widthIn(max = 440.dp),
                    trailingSend = true,
                )
            }
        }

        // Bottom dock for new question
        Surface(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 8.dp),
            shape = RoundedCornerShape(20.dp),
            color = Card,
            border = androidx.compose.foundation.BorderStroke(1.dp, CardStroke),
            shadowElevation = 0.dp,
        ) {
            Column(Modifier.padding(12.dp)) {
                QueryField(
                    value = ui.query,
                    onValueChange = onQuery,
                    onSubmit = onSubmit,
                    busy = ui.busy,
                    placeholder = "New question…",
                    modifier = Modifier.fillMaxWidth(),
                    compact = true,
                    trailingSend = true,
                )
            }
        }
    }
}

@Composable
private fun ResultHero(result: CalcResult, onCopy: () -> Unit) {
    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = Modifier
            .fillMaxWidth()
            .widthIn(max = 440.dp),
    ) {
        when (result.kind) {
            CalcResultKind.Number -> {
                Text(
                    result.display,
                    color = Accent,
                    fontSize = 56.sp,
                    fontWeight = FontWeight.Bold,
                    fontFamily = Outfit,
                    textAlign = TextAlign.Center,
                    letterSpacing = (-1.5).sp,
                    lineHeight = 62.sp,
                )
                result.secondary?.let {
                    Spacer(Modifier.height(6.dp))
                    Text(it, color = TextTertiary, fontSize = 16.sp, fontFamily = Outfit, textAlign = TextAlign.Center)
                }
                result.expressionUsed?.let {
                    Spacer(Modifier.height(10.dp))
                    Text(it, color = TextTertiary, fontSize = 13.sp, fontFamily = Outfit, textAlign = TextAlign.Center)
                }
            }
            CalcResultKind.Explain -> {
                Surface(
                    shape = RoundedCornerShape(20.dp),
                    color = Card.copy(alpha = 0.9f),
                    border = androidx.compose.foundation.BorderStroke(1.dp, CardStroke),
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    Box(Modifier.padding(20.dp)) {
                        MarkdownText(result.markdown.orEmpty())
                    }
                }
            }
            CalcResultKind.Unknown -> {
                Text("???", color = TextTertiary, fontSize = 48.sp, fontWeight = FontWeight.Bold, fontFamily = Outfit)
                Spacer(Modifier.height(8.dp))
                Text(
                    "Not a math question I can help with.",
                    color = TextTertiary,
                    fontSize = 14.sp,
                    fontFamily = Outfit,
                    textAlign = TextAlign.Center,
                )
            }
            else -> {}
        }

        if (result.copyText.isNotBlank()) {
            Spacer(Modifier.height(18.dp))
            TextButton(onClick = onCopy) {
                Icon(Icons.Default.ContentCopy, null, tint = TextSecondary, modifier = Modifier.size(16.dp))
                Spacer(Modifier.size(8.dp))
                Text("Copy", color = TextSecondary, fontFamily = Outfit)
            }
        }
    }
}

@Composable
private fun QueryField(
    value: String,
    onValueChange: (String) -> Unit,
    onSubmit: () -> Unit,
    busy: Boolean,
    placeholder: String,
    modifier: Modifier = Modifier,
    compact: Boolean = false,
    trailingSend: Boolean = false,
) {
    val shape = RoundedCornerShape(if (compact) 14.dp else 18.dp)
    Row(
        modifier
            .clip(shape)
            .border(1.dp, if (value.isNotBlank()) Accent.copy(alpha = 0.55f) else CardStroke, shape)
            .background(Card)
            .padding(horizontal = 16.dp, vertical = if (compact) 10.dp else 14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(Modifier.weight(1f)) {
            if (value.isEmpty()) {
                Text(placeholder, color = TextTertiary, fontSize = 16.sp, fontFamily = Outfit)
            }
            BasicTextField(
                value = value,
                onValueChange = onValueChange,
                singleLine = true,
                cursorBrush = SolidColor(Accent),
                textStyle = TextStyle(
                    color = TextPrimary,
                    fontSize = 16.sp,
                    fontFamily = Outfit,
                ),
                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Go),
                keyboardActions = KeyboardActions(onGo = { onSubmit() }),
                modifier = Modifier.fillMaxWidth(),
            )
        }
        if (busy) {
            CircularProgressIndicator(
                Modifier.size(18.dp),
                color = Accent,
                strokeWidth = 2.dp,
            )
        } else if (trailingSend && value.isNotBlank()) {
            IconButton(
                onClick = onSubmit,
                modifier = Modifier.size(36.dp),
            ) {
                Icon(Icons.AutoMirrored.Filled.Send, contentDescription = "Send", tint = Accent)
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(
    settings: AppSettings,
    onBack: () -> Unit,
    onSave: (AppSettings) -> Unit,
    onClearKey: () -> Unit,
) {
    var draft by remember(settings) { mutableStateOf(settings) }
    var toneOpen by remember { mutableStateOf(false) }
    val tones = listOf("Chill", "Tutor", "Formal", "Sassy")

    fun push(s: AppSettings) {
        draft = s.clamp()
        onSave(draft)
    }

    AmbientBackground {
        Column(
            Modifier
                .fillMaxSize()
                .statusBarsPadding()
                .navigationBarsPadding()
        ) {
            Row(
                Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 8.dp, vertical = 4.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                IconButton(onClick = onBack) {
                    Icon(Icons.AutoMirrored.Filled.ArrowBack, null, tint = TextPrimary)
                }
                Text(
                    "Settings",
                    color = TextPrimary,
                    fontFamily = Outfit,
                    fontWeight = FontWeight.SemiBold,
                    fontSize = 20.sp,
                )
            }

            Column(
                Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = 20.dp, vertical = 8.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                SectionLabel("Personality")
                SettingsCard {
                    Text("Tone", color = TextPrimary, fontFamily = Outfit, fontWeight = FontWeight.Medium)
                    Spacer(Modifier.height(8.dp))
                    ExposedDropdownMenuBox(expanded = toneOpen, onExpandedChange = { toneOpen = it }) {
                        OutlinedTextField(
                            value = draft.aiTone,
                            onValueChange = {},
                            readOnly = true,
                            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(toneOpen) },
                            modifier = Modifier.menuAnchor().fillMaxWidth(),
                            colors = fieldColors(),
                            shape = RoundedCornerShape(12.dp),
                        )
                        ExposedDropdownMenu(expanded = toneOpen, onDismissRequest = { toneOpen = false }) {
                            tones.forEach { t ->
                                DropdownMenuItem(
                                    text = { Text(t, fontFamily = Outfit) },
                                    onClick = {
                                        toneOpen = false
                                        push(draft.copy(aiTone = t))
                                    },
                                )
                            }
                        }
                    }
                }

                SettingsCard {
                    Text("Profile prompt", color = TextPrimary, fontFamily = Outfit, fontWeight = FontWeight.Medium)
                    Text(
                        "Extra instructions on every request.",
                        color = TextTertiary,
                        fontSize = 12.sp,
                        fontFamily = Outfit,
                        modifier = Modifier.padding(top = 4.dp, bottom = 8.dp),
                    )
                    OutlinedTextField(
                        value = draft.aiProfilePrompt,
                        onValueChange = { draft = draft.copy(aiProfilePrompt = it) },
                        modifier = Modifier
                            .fillMaxWidth()
                            .heightIn(min = 110.dp),
                        colors = fieldColors(),
                        shape = RoundedCornerShape(12.dp),
                    )
                    TextButton(onClick = { push(draft) }) {
                        Text("Save profile", color = Accent, fontFamily = Outfit)
                    }
                }

                SectionLabel("Conversation")
                SettingsToggle("Chat history / follow-ups", draft.enableChatHistory) {
                    push(draft.copy(enableChatHistory = it))
                }
                if (draft.enableChatHistory) {
                    SettingsCard {
                        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                            Text("History messages", color = TextPrimary, fontFamily = Outfit)
                            Text("${draft.historyMessageCount}", color = Accent, fontWeight = FontWeight.SemiBold, fontFamily = Outfit)
                        }
                        Slider(
                            value = draft.historyMessageCount.toFloat(),
                            onValueChange = { draft = draft.copy(historyMessageCount = it.toInt()) },
                            onValueChangeFinished = { push(draft) },
                            valueRange = 2f..24f,
                            steps = 10,
                        )
                    }
                }

                SectionLabel("Model")
                SettingsToggle("Few-shot examples", draft.useFewShotExamples) {
                    push(draft.copy(useFewShotExamples = it))
                }
                SettingsToggle("Prefer detailed explains", draft.preferDetailedExplain) {
                    push(draft.copy(preferDetailedExplain = it))
                }
                SettingsToggle("Chances as percent", draft.probabilityAsPercent) {
                    push(draft.copy(probabilityAsPercent = it))
                }

                SettingsCard {
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                        Text("Temperature", color = TextPrimary, fontFamily = Outfit)
                        Text("%.2f".format(draft.aiTemperature), color = Accent, fontWeight = FontWeight.SemiBold, fontFamily = Outfit)
                    }
                    Slider(
                        value = draft.aiTemperature.toFloat(),
                        onValueChange = { draft = draft.copy(aiTemperature = it.toDouble()) },
                        onValueChangeFinished = { push(draft) },
                        valueRange = 0f..1f,
                    )
                }
                SettingsCard {
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                        Text("Max tokens", color = TextPrimary, fontFamily = Outfit)
                        Text("${draft.aiMaxTokens}", color = Accent, fontWeight = FontWeight.SemiBold, fontFamily = Outfit)
                    }
                    Slider(
                        value = draft.aiMaxTokens.toFloat(),
                        onValueChange = { draft = draft.copy(aiMaxTokens = it.toInt()) },
                        onValueChangeFinished = { push(draft) },
                        valueRange = 100f..1200f,
                    )
                }
                SettingsCard {
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                        Text("Explain word limit", color = TextPrimary, fontFamily = Outfit)
                        Text("${draft.explainMaxWords}", color = Accent, fontWeight = FontWeight.SemiBold, fontFamily = Outfit)
                    }
                    Slider(
                        value = draft.explainMaxWords.toFloat(),
                        onValueChange = { draft = draft.copy(explainMaxWords = it.toInt()) },
                        onValueChangeFinished = { push(draft) },
                        valueRange = 40f..200f,
                    )
                }

                Spacer(Modifier.height(8.dp))
                TextButton(onClick = onClearKey) {
                    Text("Clear API key", color = ColorWarn, fontFamily = Outfit)
                }
                Spacer(Modifier.height(28.dp))
            }
        }
    }
}

@Composable
private fun SectionLabel(text: String) {
    Text(
        text.uppercase(),
        color = TextTertiary,
        fontSize = 11.sp,
        fontFamily = Outfit,
        fontWeight = FontWeight.SemiBold,
        letterSpacing = 1.2.sp,
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 8.dp, bottom = 2.dp),
    )
}

@Composable
private fun SettingsCard(content: @Composable () -> Unit) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .widthIn(max = 480.dp),
        shape = RoundedCornerShape(16.dp),
        color = Card,
        border = androidx.compose.foundation.BorderStroke(1.dp, CardStroke),
    ) {
        Column(Modifier.padding(16.dp)) { content() }
    }
}

@Composable
private fun SettingsToggle(title: String, checked: Boolean, onChange: (Boolean) -> Unit) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .widthIn(max = 480.dp),
        shape = RoundedCornerShape(16.dp),
        color = Card,
        border = androidx.compose.foundation.BorderStroke(1.dp, CardStroke),
    ) {
        Row(
            Modifier.padding(horizontal = 16.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Text(
                title,
                color = TextPrimary,
                fontFamily = Outfit,
                modifier = Modifier
                    .weight(1f)
                    .padding(end = 12.dp),
            )
            Switch(
                checked = checked,
                onCheckedChange = onChange,
                colors = SwitchDefaults.colors(checkedTrackColor = Accent, checkedThumbColor = Bg),
            )
        }
    }
}
