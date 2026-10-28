package com.nockai.app.ai

import com.nockai.app.data.AppSettings
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.util.concurrent.TimeUnit

class GroqClient(
    private val apiKey: String,
    private val model: String = "qwen/qwen3.8-27b",
) {
    private val http = OkHttpClient.Builder()
        .connectTimeout(20, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .build()

    private val fallbacks = listOf("openai/gpt-oss-20b", "openai/gpt-oss-120b")

    suspend fun interpret(
        naturalLanguage: String,
        history: List<ChatTurn>?,
        settings: AppSettings,
    ): AiReply {
        val cleaned = normalizeUserText(naturalLanguage)
        val hasHistory = !history.isNullOrEmpty()

        if (!hasHistory && isBarePrompt(cleaned)) return AiReply.unknown()

        var reply = ask(cleaned, history, settings)
        if (reply.kind == AiReplyKind.Unknown) {
            reply = ask(cleaned, history, settings)
        }

        if (hasHistory) {
            if (reply.kind == AiReplyKind.Unknown) {
                reply = askFollowUp(cleaned, history!!, settings)
            }
        } else if (reply.kind == AiReplyKind.Unknown && looksLikeMathQuestion(cleaned) && !looksLikeEquationSolve(cleaned)) {
            reply = ask("Explain this math topic clearly with a formula and one example: $cleaned", history, settings)
        }

        return reply
    }

    private fun askFollowUp(followUp: String, history: List<ChatTurn>, settings: AppSettings): AiReply {
        val prompt =
            "CONTINUE the previous math conversation. " +
                "The user follow-up is short but STILL about that math topic. " +
                "Reply kind=explain with helpful markdown (examples/steps). " +
                "Do NOT return unknown. Follow-up: ${followUp.trim()}"
        return ask(prompt, history, settings)
    }

    private fun ask(userText: String, history: List<ChatTurn>?, settings: AppSettings): AiReply {
        val messages = buildMessages(userText, history, settings)
        var lastError: Exception? = null
        val models = listOf(model) + fallbacks.filter { !it.equals(model, ignoreCase = true) }
        for (m in models) {
            repeat(2) { attempt ->
                try {
                    return send(m, messages, settings)
                } catch (ex: Exception) {
                    lastError = ex
                    if (!isTransient(ex) || attempt == 1) { /* try next model */ }
                    else Thread.sleep(300L * (attempt + 1))
                }
            }
        }
        throw lastError ?: IllegalStateException("Groq request failed.")
    }

    private fun send(modelName: String, messages: JSONArray, settings: AppSettings): AiReply {
        val payload = JSONObject()
            .put("model", modelName)
            .put("temperature", settings.aiTemperature)
            .put("max_tokens", settings.aiMaxTokens)
            .put("response_format", JSONObject().put("type", "json_object"))
            .put("messages", messages)

        val req = Request.Builder()
            .url("https://api.groq.com/openai/v1/chat/completions")
            .addHeader("Authorization", "Bearer $apiKey")
            .addHeader("Content-Type", "application/json")
            .post(payload.toString().toRequestBody("application/json".toMediaType()))
            .build()

        http.newCall(req).execute().use { res ->
            val body = res.body?.string().orEmpty()
            if (!res.isSuccessful) {
                throw IllegalStateException("Groq error ${res.code}: ${body.take(240)}")
            }
            val root = JSONObject(body)
            val message = root.getJSONArray("choices").getJSONObject(0).getJSONObject("message")
            var content = message.optString("content", "")
            if (content.isBlank() && message.has("reasoning")) {
                content = extractJson(message.optString("reasoning", "")) ?: ""
            }
            return parseReply(content)
        }
    }

    private fun buildMessages(userText: String, history: List<ChatTurn>?, settings: AppSettings): JSONArray {
        val messages = JSONArray()
        messages.put(JSONObject().put("role", "system").put("content", buildSystemPrompt(settings)))

        val effective = if (settings.enableChatHistory && !history.isNullOrEmpty()) {
            history.takeLast(settings.historyMessageCount)
        } else null

        if (effective == null) {
            if (settings.useFewShotExamples) addColdFewShots(messages)
        } else {
            if (settings.useFewShotExamples) addFollowUpFewShots(messages)
            for (turn in effective) {
                val role = if (turn.role == "assistant" || turn.role == "user") turn.role else "user"
                messages.put(JSONObject().put("role", role).put("content", turn.content))
            }
        }

        messages.put(JSONObject().put("role", "user").put("content", userText))
        return messages
    }

    private fun buildSystemPrompt(s: AppSettings): String = buildString {
        append(SYSTEM_PROMPT)
        appendLine()
        appendLine("========================")
        appendLine("SESSION STYLE")
        appendLine("========================")
        appendLine(toneInstructions(s.aiTone))
        appendLine("Explain markdown soft cap: under ~${s.explainMaxWords} words.")
        if (s.preferDetailedExplain) {
            appendLine(
                "Prefer kind=explain with a bit more teaching detail for how/why/what concept questions " +
                    "(still respect the word cap; still use calc when they want a number)."
            )
        }
        if (s.aiProfilePrompt.isNotBlank()) {
            appendLine()
            appendLine("USER PROFILE / CUSTOM INSTRUCTIONS (obey for voice, personality, and extra rules):")
            appendLine(s.aiProfilePrompt.trim())
        }
    }

    private fun toneInstructions(tone: String) = when (tone.trim().lowercase()) {
        "tutor" ->
            "Tone preset: Tutor. Patient teacher. Slightly more steps. Still no pep talk outros."
        "formal" ->
            "Tone preset: Formal. Precise academic wording. No slang. No jokes. Clean definitions."
        "sassy" ->
            "Tone preset: Sassy. Witty, slightly roasting wrong approaches - but stay correct and short. " +
                "Never insult the person; roast the mistake."
        else ->
            "Tone preset: Chill. Short. Direct. No pep talk. No closing offers."
    }

    private fun addColdFewShots(messages: JSONArray) {
        fun ua(u: String, a: String) {
            messages.put(JSONObject().put("role", "user").put("content", u))
            messages.put(JSONObject().put("role", "assistant").put("content", a))
        }
        ua("multiple 264 by 1446", """{"kind":"calc","expression":"264*1446"}""")
        ua("12n=144 find the n", """{"kind":"calc","expression":"144/12"}""")
        ua(
            "yo pls explain how do i find mean ?",
            """{"kind":"explain","markdown":"The **mean** (average) is the sum of values divided by how many there are.\n\n==mean = (sum of values) / (count)==\n\nExample: `2, 4, 6` → `(2+4+6)/3 = 4`."}"""
        )
        ua("mean of 10 and 20", """{"kind":"calc","expression":"mean(10,20)"}""")
        ua("whats the weather", """{"kind":"unknown"}""")
    }

    private fun addFollowUpFewShots(messages: JSONArray) {
        messages.put(JSONObject().put("role", "user").put("content", "how do i find average"))
        messages.put(
            JSONObject().put("role", "assistant").put(
                "content",
                """{"kind":"explain","markdown":"The **average** (mean) is sum divided by count.\n\n==average = sum / count=="}"""
            )
        )
        messages.put(JSONObject().put("role", "user").put("content", "more examples pls"))
        messages.put(
            JSONObject().put("role", "assistant").put(
                "content",
                """{"kind":"explain","markdown":"Examples:\n\n- `3, 6, 9` → ==(3+6+9)/3 = 6==\n- `1, 1, 7` → ==(1+1+7)/3 = 3=="}"""
            )
        )
    }

    companion object {
        private val SYSTEM_PROMPT = """
        You are NockAI - a chill MATH assistant on mobile.

        Reply with ONE JSON object only (no markdown fence, no extra text):

        1) Compute / solve a value:
        {"kind":"calc","expression":"<engine expression>"}

        2) Explain / teach / examples / clarify:
        {"kind":"explain","markdown":"<short clear explanation>"}

        3) Truly not math (weather, jokes, coding, life advice with zero math):
        {"kind":"unknown"}

        TONE:
        - Short. Direct. No pep talk.
        - No "Glad that helped!", "Let me know if...", "Great question!", "Hope this helps!".
        - No closing offers. Just the math.

        ========================
        DECISION RULES
        ========================
        - Math topics: arithmetic, algebra, geometry, stats, percentages, formulas, how-to-math.
        - Ignore slang: yo, pls, please, bro, hey, can you, help me.
        - Fix typos: multiple→multiply, devided→divided, avrage→average, ect.
        - how/what/why/explain/define a math idea -> explain (even with slang).
        - numbers to compute, or solve for n/x -> calc.
        - Prefer explain over unknown whenever the topic is math-related.
        - Prefer calc over explain when they want a number.
        - Bare "explain"/"how"/"why"/"steps"/"help" with NO math topic and NO prior messages
          -> {"kind":"unknown"}. Do NOT invent a problem. Do NOT reuse examples from this prompt.
        - thanks / thx / ty / ok / cool / nice / got it / bet (acknowledgements)
          -> {"kind":"unknown"}. Do NOT re-explain.

        FOLLOW-UPS (very important):
        - Prior messages are the SAME math conversation.
        - Short replies like "more examples", "simpler", "another one", "eli5", "again" CONTINUE that topic.
        - Acknowledgements (thanks/ok/cool/got it) are NOT continue cues -> unknown.
        - With history, almost NEVER return unknown for real math follow-ups.
        - If they ask for more examples, give 2-3 NEW worked numeric examples in markdown.

        CALC expression syntax:
        - + - * / ^ () sqrt abs sin cos tan log ln floor ceil round
        - mean median mode sum min max stdev variance range
        - nCr(n,k) / choose, nPr(n,k), fact(n), gcd(a,b), lcm(a,b)
        - Number words -> digits. Percent: (N/100)*X
        - Frequency / repeated scores:
          "6 students scored 85 and 4 scored 95" -> (6*85+4*95)/(6+4)
          NEVER write mean(6*85,4*95) - that is wrong.
        - Digits and operators ONLY. NO variable letters (n,x,y). NO equals sign.
        - "10 choose 3" -> nCr(10,3) ; "5!" -> fact(5)

        ALGEBRA / SOLVE:
        - Rearrange to a numeric expression for the unknown.
        - 12n=144 -> {"kind":"calc","expression":"144/12"}
        - 2x+4=10 -> {"kind":"calc","expression":"(10-4)/2"}
        - "increase from A to B" (NO percent word) -> {"kind":"calc","expression":"B-A"}
        - "percent increase from A to B" -> {"kind":"calc","expression":"((B-A)/A)*100"}
        - "chances" / "probability" / "odds" -> {"kind":"calc","expression":"favorable/total"}
        - Never copy these examples into an explain reply unless the user typed that same problem.

        EXPLAIN markdown:
        - Keep short (word cap is set per session below).
        - **bold** key terms
        - ==highlight== core formula
        - `code` for short formulas
        - "- " bullets OK
        - No links, no # headings, no fluff outro
        """.trimIndent()

        private val slangPrefix = Regex(
            """^(?:yo+|hey|hi|hello|bro|dude|pls|plz|please|can you|could you|would you|help me|ok|okay|sup)[\s,.:!]+""",
            RegexOption.IGNORE_CASE
        )

        fun isAcknowledgement(input: String): Boolean {
            val lower = normalizeAck(input)
            if (lower.isEmpty()) return false
            val acks = setOf(
                "thanks", "thank you", "thank u", "thx", "ty", "tysm", "tyvm",
                "ok", "okay", "k", "kk", "cool", "nice", "sick", "fire", "bet",
                "got it", "gotcha", "makes sense", "alr", "alright", "np",
                "perfect", "great", "awesome", "good", "yep", "ye", "yeah", "yea",
                "ig", "fs", "word", "noted", "done", "sweet"
            )
            if (lower in acks) return true
            return Regex(
                """^(thanks|thank you|thank u|thx|ty|tysm|ok|okay|cool|nice|got it|gotcha|bet)(\s+(bro|dude|man|lol|tho|though|a lot|so much))?$"""
            ).matches(lower)
        }

        fun isBarePrompt(input: String): Boolean {
            val lower = normalizeAck(input)
            if (lower.isEmpty()) return true
            if (isAcknowledgement(lower)) return true
            val bare = setOf(
                "explain", "explain this", "explain that", "explain please", "explain pls",
                "how", "why", "what", "help", "help me", "steps", "show steps", "show",
                "teach", "teach me", "solve", "calculate", "compute", "math", "idk"
            )
            if (lower in bare) return true
            return Regex("""^(explain|how|why|what|help|teach|solve)(\s+(me|this|that|pls|plz|please|bro|dude))?$""")
                .matches(lower)
        }

        fun toAssistantJson(reply: AiReply): String = when (reply.kind) {
            AiReplyKind.Calc -> JSONObject().put("kind", "calc").put("expression", reply.expression ?: "").toString()
            AiReplyKind.Explain -> JSONObject().put("kind", "explain").put("markdown", reply.markdown ?: "").toString()
            else -> """{"kind":"unknown"}"""
        }

        private fun normalizeAck(input: String): String {
            var lower = input.trim().lowercase().replace(Regex("""[.!?🥰🙏👍]+$"""), "").trim()
            lower = slangPrefix.replace(lower, "").trim()
            lower = lower.replace(Regex("""[.!?]+$"""), "").trim()
            return lower
        }

        private fun normalizeUserText(input: String): String {
            var t = input.trim()
            repeat(4) {
                val next = slangPrefix.replace(t, "").trim()
                if (next == t) return@repeat
                t = next
            }
            t = t.replace(Regex("""\b(pls|plz|please)\b[.!?]*$""", RegexOption.IGNORE_CASE), "").trim()
            t = t.replace(Regex("""\bmultiple\b""", RegexOption.IGNORE_CASE), "multiply")
            t = t.replace(Regex("""\bdevided\b""", RegexOption.IGNORE_CASE), "divided")
            t = t.replace(Regex("""\bavrage\b""", RegexOption.IGNORE_CASE), "average")
            return t.ifBlank { input.trim() }
        }

        private fun looksLikeMathQuestion(input: String): Boolean {
            val lower = input.lowercase()
            val hooks = listOf(
                "mean", "median", "mode", "average", "avg", "percent", "fraction", "algebra",
                "geometry", "equation", "formula", "probability", "calculate", "compute", "math",
                "multiply", "divide", "example", "simplify", "times", "plus", "minus"
            )
            val asks = listOf("how", "what", "why", "explain", "define", "show", "more")
            return hooks.any { lower.contains(it) } && (asks.any { lower.contains(it) } || lower.contains('?'))
        }

        private fun looksLikeEquationSolve(input: String): Boolean {
            val lower = input.lowercase()
            return lower.contains("equals") || lower.contains('=') || lower.contains("solve")
        }

        private fun isTransient(ex: Exception): Boolean {
            val msg = ex.message.orEmpty()
            return msg.contains("over capacity", true) || msg.contains("429") ||
                msg.contains("503") || msg.contains("502") || msg.contains("rate limit", true)
        }

        private fun extractJson(text: String): String? {
            val m = Regex("""\{[\s\S]*\}""").find(text) ?: return null
            return m.value
        }

        private fun parseReply(content: String?): AiReply {
            if (content.isNullOrBlank()) return AiReply.unknown()
            val m = Regex("""\{[\s\S]*\}""").find(content.trim()) ?: return AiReply.unknown()
            return try {
                val root = JSONObject(m.value)
                when (root.optString("kind").trim().lowercase()) {
                    "calc" -> {
                        val expr = root.optString("expression").trim().trim('"', '\'', '`', '.')
                            .removePrefix("=").trim()
                        if (expr.isBlank() || expr == "?" || expr.equals("UNPARSABLE", true)) AiReply.unknown()
                        else AiReply.calc(expr)
                    }
                    "explain" -> {
                        val md = root.optString("markdown").trim()
                        if (md.isBlank()) AiReply.unknown() else AiReply.explain(md)
                    }
                    else -> AiReply.unknown()
                }
            } catch (_: Exception) {
                AiReply.unknown()
            }
        }
    }
}
