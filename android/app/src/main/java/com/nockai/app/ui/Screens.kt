package com.nockai.app.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
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
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Slider
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.nockai.app.ai.CalcResultKind
import com.nockai.app.data.AppSettings

@Composable
fun fieldColors() = OutlinedTextFieldDefaults.colors(
    focusedBorderColor = Accent,
    unfocusedBorderColor = TextTertiary.copy(alpha = 0.4f),
    focusedTextColor = TextPrimary,
    unfocusedTextColor = TextPrimary,
    cursorColor = Accent,
    focusedLabelColor = Accent,
    unfocusedLabelColor = TextTertiary,
    focusedContainerColor = Card,
    unfocusedContainerColor = Card,
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SetupScreen(onSaved: (String) -> Unit) {
    var key by remember { mutableStateOf("") }
    Scaffold(
        containerColor = Bg,
        topBar = {
            TopAppBar(
                title = { Text("NockAI setup", fontWeight = FontWeight.SemiBold) },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = Bg, titleContentColor = TextPrimary),
            )
        }
    ) { pad ->
        Column(
            Modifier
                .fillMaxSize()
                .padding(pad)
                .padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            Text("Paste your Groq API key. Same one as the Windows app.", color = TextSecondary, fontSize = 15.sp)
            OutlinedTextField(
                value = key,
                onValueChange = { key = it },
                label = { Text("Groq API key") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
                visualTransformation = PasswordVisualTransformation(),
                colors = fieldColors(),
            )
            Button(
                onClick = { if (key.isNotBlank()) onSaved(key.trim()) },
                enabled = key.isNotBlank(),
                colors = ButtonDefaults.buttonColors(containerColor = Accent, contentColor = Bg),
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text("Continue", fontWeight = FontWeight.SemiBold)
            }
            Text("Get a free key at console.groq.com", color = TextTertiary, fontSize = 13.sp)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
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
    Scaffold(
        containerColor = Bg,
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text("NockAI", fontWeight = FontWeight.SemiBold)
                        Text("Math chat", color = TextTertiary, fontSize = 12.sp)
                    }
                },
                actions = {
                    IconButton(onClick = onOpenSettings) {
                        Icon(Icons.Default.Settings, contentDescription = "Settings", tint = TextSecondary)
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = Bg, titleContentColor = TextPrimary),
            )
        }
    ) { pad ->
        Column(
            Modifier
                .fillMaxSize()
                .padding(pad)
                .padding(horizontal = 16.dp),
        ) {
            OutlinedTextField(
                value = ui.query,
                onValueChange = onQuery,
                modifier = Modifier.fillMaxWidth(),
                placeholder = { Text("2+2, mean of 1 2 3, explain median…") },
                singleLine = true,
                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Go),
                keyboardActions = KeyboardActions(onGo = { onSubmit() }),
                colors = fieldColors(),
                shape = RoundedCornerShape(14.dp),
            )
            Spacer(Modifier.height(10.dp))
            Button(
                onClick = onSubmit,
                enabled = ui.query.isNotBlank() && !ui.busy,
                colors = ButtonDefaults.buttonColors(containerColor = Accent, contentColor = Bg),
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
            ) {
                Text(if (ui.busy) "Thinking…" else "Calculate", fontWeight = FontWeight.SemiBold)
            }

            if (ui.busy) {
                Spacer(Modifier.height(12.dp))
                LinearProgressIndicator(
                    modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(4.dp)),
                    color = Accent,
                    trackColor = Card,
                )
            }

            ui.error?.let {
                Spacer(Modifier.height(12.dp))
                Text(it, color = ColorWarn, fontSize = 13.sp)
            }

            val result = ui.result
            if (result != null && result.kind != CalcResultKind.Empty) {
                Spacer(Modifier.height(16.dp))
                Box(
                    Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(16.dp))
                        .background(Card)
                        .padding(16.dp),
                ) {
                    Column {
                        when (result.kind) {
                            CalcResultKind.Number -> {
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Text(
                                        result.display,
                                        color = Accent,
                                        fontSize = 36.sp,
                                        fontWeight = FontWeight.Bold,
                                        modifier = Modifier.weight(1f),
                                    )
                                    IconButton(onClick = {
                                        clipboard.setText(AnnotatedString(result.copyText))
                                    }) {
                                        Icon(Icons.Default.ContentCopy, null, tint = TextTertiary)
                                    }
                                }
                                result.secondary?.let {
                                    Text(it, color = TextTertiary, fontSize = 14.sp)
                                }
                                result.expressionUsed?.let {
                                    Spacer(Modifier.height(6.dp))
                                    Text(it, color = TextTertiary, fontSize = 12.sp)
                                }
                            }
                            CalcResultKind.Explain -> {
                                Row(verticalAlignment = Alignment.Top) {
                                    MarkdownText(result.markdown.orEmpty(), Modifier.weight(1f))
                                    IconButton(onClick = {
                                        clipboard.setText(AnnotatedString(result.copyText))
                                    }) {
                                        Icon(Icons.Default.ContentCopy, null, tint = TextTertiary)
                                    }
                                }
                            }
                            CalcResultKind.Unknown -> {
                                Text("???", color = TextTertiary, fontSize = 32.sp, fontWeight = FontWeight.Bold)
                                Text("Not a math question I can help with.", color = TextTertiary, fontSize = 13.sp)
                            }
                            else -> {}
                        }
                    }
                }
            }

            if (settings.enableChatHistory && result?.ok == true) {
                Spacer(Modifier.height(14.dp))
                OutlinedTextField(
                    value = ui.followUp,
                    onValueChange = onFollowUp,
                    modifier = Modifier.fillMaxWidth(),
                    placeholder = { Text("Follow-up: more examples, simpler…") },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Send),
                    keyboardActions = KeyboardActions(onSend = { onFollowSubmit() }),
                    colors = fieldColors(),
                    shape = RoundedCornerShape(14.dp),
                    trailingIcon = {
                        if (ui.busy) {
                            CircularProgressIndicator(Modifier.size(18.dp), color = Accent, strokeWidth = 2.dp)
                        }
                    }
                )
            }
        }
    }
}

private val ColorWarn = androidx.compose.ui.graphics.Color(0xFFFFB4A8)

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

    Scaffold(
        containerColor = Bg,
        topBar = {
            TopAppBar(
                title = { Text("AI settings", fontWeight = FontWeight.SemiBold) },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, null, tint = TextPrimary)
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = Bg, titleContentColor = TextPrimary),
            )
        }
    ) { pad ->
        Column(
            Modifier
                .fillMaxSize()
                .padding(pad)
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Text("Personality", color = TextTertiary, fontSize = 12.sp, fontWeight = FontWeight.SemiBold)
            SettingsCard {
                Text("Tone preset", color = TextPrimary)
                Spacer(Modifier.height(8.dp))
                ExposedDropdownMenuBox(expanded = toneOpen, onExpandedChange = { toneOpen = it }) {
                    OutlinedTextField(
                        value = draft.aiTone,
                        onValueChange = {},
                        readOnly = true,
                        trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(toneOpen) },
                        modifier = Modifier.menuAnchor().fillMaxWidth(),
                        colors = fieldColors(),
                    )
                    ExposedDropdownMenu(expanded = toneOpen, onDismissRequest = { toneOpen = false }) {
                        tones.forEach { t ->
                            DropdownMenuItem(
                                text = { Text(t) },
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
                Text("Profile / custom prompt", color = TextPrimary)
                Text(
                    "Extra instructions on every request.",
                    color = TextTertiary,
                    fontSize = 12.sp,
                    modifier = Modifier.padding(top = 4.dp, bottom = 8.dp),
                )
                OutlinedTextField(
                    value = draft.aiProfilePrompt,
                    onValueChange = { draft = draft.copy(aiProfilePrompt = it) },
                    modifier = Modifier.fillMaxWidth().height(120.dp),
                    colors = fieldColors(),
                )
                TextButton(onClick = { push(draft) }) { Text("Save profile", color = Accent) }
            }

            Text("Conversation", color = TextTertiary, fontSize = 12.sp, fontWeight = FontWeight.SemiBold)
            SettingsToggle(
                title = "Chat history / follow-ups",
                checked = draft.enableChatHistory,
                onChange = { push(draft.copy(enableChatHistory = it)) },
            )
            if (draft.enableChatHistory) {
                SettingsCard {
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                        Text("History messages", color = TextPrimary)
                        Text("${draft.historyMessageCount}", color = Accent, fontWeight = FontWeight.SemiBold)
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

            Text("Model knobs", color = TextTertiary, fontSize = 12.sp, fontWeight = FontWeight.SemiBold)
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
                    Text("Temperature", color = TextPrimary)
                    Text("%.2f".format(draft.aiTemperature), color = Accent, fontWeight = FontWeight.SemiBold)
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
                    Text("Max tokens", color = TextPrimary)
                    Text("${draft.aiMaxTokens}", color = Accent, fontWeight = FontWeight.SemiBold)
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
                    Text("Explain word limit", color = TextPrimary)
                    Text("${draft.explainMaxWords}", color = Accent, fontWeight = FontWeight.SemiBold)
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
                Text("Clear API key / re-run setup", color = ColorWarn)
            }
            Spacer(Modifier.height(24.dp))
        }
    }
}

@Composable
private fun SettingsCard(content: @Composable () -> Unit) {
    Column(
        Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(Card)
            .padding(14.dp),
    ) { content() }
}

@Composable
private fun SettingsToggle(title: String, checked: Boolean, onChange: (Boolean) -> Unit) {
    Row(
        Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(Card)
            .padding(horizontal = 14.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(title, color = TextPrimary, modifier = Modifier.weight(1f).padding(end = 12.dp))
        Switch(
            checked = checked,
            onCheckedChange = onChange,
            colors = SwitchDefaults.colors(checkedTrackColor = Accent, checkedThumbColor = Bg),
        )
    }
}
