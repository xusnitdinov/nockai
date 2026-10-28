package com.nockai.app.ai

enum class AiReplyKind { Calc, Explain, Unknown }

data class AiReply(
    val kind: AiReplyKind,
    val expression: String? = null,
    val markdown: String? = null,
) {
    companion object {
        fun calc(expr: String) = AiReply(AiReplyKind.Calc, expression = expr)
        fun explain(md: String) = AiReply(AiReplyKind.Explain, markdown = md)
        fun unknown() = AiReply(AiReplyKind.Unknown)
    }
}

data class ChatTurn(val role: String, val content: String)

enum class CalcResultKind { Empty, Number, Explain, Unknown }

data class CalcResult(
    val kind: CalcResultKind,
    val display: String = "",
    val secondary: String? = null,
    val markdown: String? = null,
    val expressionUsed: String? = null,
    val raw: AiReply? = null,
) {
    val ok: Boolean get() = kind == CalcResultKind.Number || kind == CalcResultKind.Explain

    val copyText: String
        get() = when (kind) {
            CalcResultKind.Explain -> markdown.orEmpty()
            else -> display
        }

    companion object {
        fun empty() = CalcResult(CalcResultKind.Empty)
        fun unknown(expr: String? = null, raw: AiReply? = null) =
            CalcResult(CalcResultKind.Unknown, display = "???", expressionUsed = expr, raw = raw)
        fun number(value: String, expr: String? = null, secondary: String? = null, raw: AiReply? = null) =
            CalcResult(CalcResultKind.Number, display = value, secondary = secondary, expressionUsed = expr, raw = raw)
        fun explain(md: String, raw: AiReply? = null) =
            CalcResult(CalcResultKind.Explain, markdown = md, raw = raw)
    }
}
