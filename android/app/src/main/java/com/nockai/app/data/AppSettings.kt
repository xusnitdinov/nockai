package com.nockai.app.data

data class AppSettings(
    val aiTone: String = "Chill",
    val aiProfilePrompt: String = "",
    val enableChatHistory: Boolean = true,
    val historyMessageCount: Int = 8,
    val useFewShotExamples: Boolean = true,
    val aiTemperature: Double = 0.0,
    val aiMaxTokens: Int = 550,
    val explainMaxWords: Int = 80,
    val preferDetailedExplain: Boolean = false,
    val probabilityAsPercent: Boolean = true,
) {
    fun clamp(): AppSettings = copy(
        aiTone = aiTone.ifBlank { "Chill" }.trim(),
        aiProfilePrompt = aiProfilePrompt.take(4000),
        historyMessageCount = historyMessageCount.coerceIn(2, 24),
        aiTemperature = aiTemperature.coerceIn(0.0, 1.2),
        aiMaxTokens = aiMaxTokens.coerceIn(100, 2000),
        explainMaxWords = explainMaxWords.coerceIn(40, 220),
    )
}
