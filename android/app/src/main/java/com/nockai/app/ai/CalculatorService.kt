package com.nockai.app.ai

import com.nockai.app.data.AppSettings

class CalculatorService(
    private val groq: GroqClient,
) {
    suspend fun evaluate(
        inputRaw: String,
        history: List<ChatTurn>?,
        settings: AppSettings,
    ): CalcResult {
        val input = inputRaw.trim()
        if (input.isEmpty()) return CalcResult.empty()

        val hasHistory = !history.isNullOrEmpty()

        if (GroqClient.isAcknowledgement(input)) {
            return CalcResult.explain("No problem.")
        }
        if (!hasHistory && GroqClient.isBarePrompt(input)) {
            return CalcResult.explain("What should I explain? Type a topic like **mean**, or paste an equation.")
        }

        if (!hasHistory && LocalMath.looksSymbolic(input)) {
            val v = LocalMath.tryEval(input)
            if (v != null) {
                val (main, sec) = ExpressionEngine.format(v)
                return CalcResult.number(main, input, sec)
            }
        }

        val reply = groq.interpret(input, history, settings)
        return fromReply(input, reply, settings)
    }

    private fun fromReply(input: String, reply: AiReply, settings: AppSettings): CalcResult {
        return when (reply.kind) {
            AiReplyKind.Explain -> CalcResult.explain(reply.markdown.orEmpty(), reply)
            AiReplyKind.Unknown -> CalcResult.unknown(raw = reply)
            AiReplyKind.Calc -> {
                val expr = reply.expression.orEmpty()
                val value = ExpressionEngine.tryEvaluate(expr)
                if (value == null) return CalcResult.unknown(expr, reply)
                val wantsPct = settings.probabilityAsPercent && looksProbability(input)
                val (main, sec) = ExpressionEngine.format(value, asPercent = wantsPct)
                CalcResult.number(main, expr, sec, reply)
            }
        }
    }

    private fun looksProbability(input: String): Boolean {
        val lower = input.lowercase()
        return listOf("chance", "chances", "probability", "prob", "odds", "likely").any { lower.contains(it) }
    }
}
