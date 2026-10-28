package com.nockai.app.ui

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.nockai.app.ai.CalcResult
import com.nockai.app.ai.CalcResultKind
import com.nockai.app.ai.CalculatorService
import com.nockai.app.ai.ChatTurn
import com.nockai.app.ai.GroqClient
import com.nockai.app.data.AppSettings
import com.nockai.app.data.SecretsStore
import com.nockai.app.data.SettingsStore
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

data class UiState(
    val query: String = "",
    val followUp: String = "",
    val result: CalcResult? = null,
    val busy: Boolean = false,
    val error: String? = null,
    val hasKey: Boolean = false,
)

class NockViewModel(app: Application) : AndroidViewModel(app) {
    private val secrets = SecretsStore(app)
    private val settingsStore = SettingsStore(app)

    val settings: StateFlow<AppSettings> = settingsStore.settings
        .stateIn(viewModelScope, SharingStarted.Eagerly, AppSettings())

    private val _ui = MutableStateFlow(UiState(hasKey = secrets.hasKey))
    val ui: StateFlow<UiState> = _ui.asStateFlow()

    private val history = mutableListOf<ChatTurn>()
    private var job: Job? = null

    fun saveApiKey(key: String) {
        secrets.groqApiKey = key
        _ui.update { it.copy(hasKey = secrets.hasKey) }
    }

    fun clearApiKey() {
        secrets.groqApiKey = ""
        _ui.update { it.copy(hasKey = false) }
    }

    fun setQuery(q: String) {
        if (q.trim() != _ui.value.query.trim()) {
            history.clear()
        }
        _ui.update { it.copy(query = q, error = null) }
    }

    fun setFollowUp(q: String) = _ui.update { it.copy(followUp = q) }

    fun saveSettings(s: AppSettings) {
        viewModelScope.launch { settingsStore.save(s) }
        if (!s.enableChatHistory) history.clear()
    }

    fun evaluateMain() {
        val text = _ui.value.query.trim()
        if (text.isEmpty()) return
        history.clear()
        runQuery(text, historySnapshot = null, append = true)
    }

    fun evaluateFollowUp() {
        val s = settings.value
        if (!s.enableChatHistory || history.isEmpty()) return
        val text = _ui.value.followUp.trim()
        if (text.isEmpty()) return
        _ui.update { it.copy(followUp = "") }
        if (GroqClient.isAcknowledgement(text)) {
            _ui.update { it.copy(result = CalcResult.explain("No problem.")) }
            return
        }
        runQuery(text, historySnapshot = history.toList(), append = true)
    }

    private fun runQuery(text: String, historySnapshot: List<ChatTurn>?, append: Boolean) {
        val key = secrets.groqApiKey
        if (key.isBlank()) {
            _ui.update { it.copy(error = "Add a Groq API key in setup.", hasKey = false) }
            return
        }
        job?.cancel()
        job = viewModelScope.launch {
            _ui.update { it.copy(busy = true, error = null) }
            try {
                val calc = CalculatorService(GroqClient(key))
                val result = withContext(Dispatchers.IO) {
                    calc.evaluate(text, historySnapshot, settings.value)
                }
                if (append && result.ok && settings.value.enableChatHistory) {
                    history += ChatTurn("user", text)
                    val assistant = when {
                        result.raw != null -> GroqClient.toAssistantJson(result.raw)
                        result.kind == CalcResultKind.Explain ->
                            GroqClient.toAssistantJson(com.nockai.app.ai.AiReply.explain(result.markdown.orEmpty()))
                        else ->
                            GroqClient.toAssistantJson(
                                com.nockai.app.ai.AiReply.calc(result.expressionUsed ?: result.display)
                            )
                    }
                    history += ChatTurn("assistant", assistant)
                    while (history.size > settings.value.historyMessageCount) history.removeAt(0)
                }
                _ui.update { it.copy(busy = false, result = result) }
            } catch (ex: Exception) {
                _ui.update {
                    it.copy(
                        busy = false,
                        error = ex.message?.take(160) ?: "Request failed",
                        result = if (historySnapshot.isNullOrEmpty()) CalcResult.unknown() else it.result,
                    )
                }
            }
        }
    }
}
