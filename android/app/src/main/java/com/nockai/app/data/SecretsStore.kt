package com.nockai.app.data

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

class SecretsStore(context: Context) {
    private val prefs = EncryptedSharedPreferences.create(
        context,
        "nockai_secrets",
        MasterKey.Builder(context).setKeyScheme(MasterKey.KeyScheme.AES256_GCM).build(),
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
    )

    var groqApiKey: String
        get() = prefs.getString(KEY, "") ?: ""
        set(value) = prefs.edit().putString(KEY, value.trim()).apply()

    val hasKey: Boolean get() = groqApiKey.isNotBlank()

    companion object {
        private const val KEY = "groq_api_key"
    }
}
