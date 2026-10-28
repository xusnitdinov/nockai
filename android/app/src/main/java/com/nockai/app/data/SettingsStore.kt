package com.nockai.app.data

import android.content.Context
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.doublePreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.dataStore by preferencesDataStore("nockai_settings")

class SettingsStore(private val context: Context) {
    val settings: Flow<AppSettings> = context.dataStore.data.map { p ->
        AppSettings(
            aiTone = p[Keys.tone] ?: "Chill",
            aiProfilePrompt = p[Keys.profile] ?: "",
            enableChatHistory = p[Keys.history] ?: true,
            historyMessageCount = p[Keys.historyCount] ?: 8,
            useFewShotExamples = p[Keys.fewShot] ?: true,
            aiTemperature = p[Keys.temp] ?: 0.0,
            aiMaxTokens = p[Keys.maxTokens] ?: 550,
            explainMaxWords = p[Keys.explainWords] ?: 80,
            preferDetailedExplain = p[Keys.preferExplain] ?: false,
            probabilityAsPercent = p[Keys.probPct] ?: true,
        ).clamp()
    }

    suspend fun save(s: AppSettings) {
        val c = s.clamp()
        context.dataStore.edit { p ->
            p[Keys.tone] = c.aiTone
            p[Keys.profile] = c.aiProfilePrompt
            p[Keys.history] = c.enableChatHistory
            p[Keys.historyCount] = c.historyMessageCount
            p[Keys.fewShot] = c.useFewShotExamples
            p[Keys.temp] = c.aiTemperature
            p[Keys.maxTokens] = c.aiMaxTokens
            p[Keys.explainWords] = c.explainMaxWords
            p[Keys.preferExplain] = c.preferDetailedExplain
            p[Keys.probPct] = c.probabilityAsPercent
        }
    }

    private object Keys {
        val tone = stringPreferencesKey("ai_tone")
        val profile = stringPreferencesKey("ai_profile")
        val history = booleanPreferencesKey("enable_history")
        val historyCount = intPreferencesKey("history_count")
        val fewShot = booleanPreferencesKey("few_shot")
        val temp = doublePreferencesKey("temperature")
        val maxTokens = intPreferencesKey("max_tokens")
        val explainWords = intPreferencesKey("explain_words")
        val preferExplain = booleanPreferencesKey("prefer_explain")
        val probPct = booleanPreferencesKey("prob_pct")
    }
}
