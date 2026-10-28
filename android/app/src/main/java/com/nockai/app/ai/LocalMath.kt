package com.nockai.app.ai

import kotlin.math.pow

/** Tiny local evaluator for plain symbolic math - no API needed. */
object LocalMath {
    private val symbolic = Regex("""^[\d\s+\-*/^().]+$""")

    fun looksSymbolic(input: String): Boolean {
        val t = input.trim()
        return t.length in 1..120 && symbolic.matches(t) && t.any { it.isDigit() } &&
            t.any { it in "+-*/^()" }
    }

    fun tryEval(input: String): Double? {
        return try {
            val tokens = tokenize(input.trim().replace(" ", ""))
            val rpn = toRpn(tokens)
            evalRpn(rpn)
        } catch (_: Exception) {
            null
        }
    }

    fun format(value: Double): String {
        if (value.isNaN() || value.isInfinite()) return "???"
        val asLong = value.toLong()
        if (value == asLong.toDouble() && value in -1e15..1e15) return asLong.toString()
        var s = "%.10f".format(value).trimEnd('0').trimEnd('.')
        if (s == "-0") s = "0"
        return s
    }

    private fun tokenize(s: String): List<String> {
        val out = mutableListOf<String>()
        var i = 0
        while (i < s.length) {
            val c = s[i]
            when {
                c.isDigit() || c == '.' -> {
                    val start = i
                    i++
                    while (i < s.length && (s[i].isDigit() || s[i] == '.')) i++
                    out += s.substring(start, i)
                }
                c in "+-*/^()" -> {
                    // unary minus
                    if (c == '-' && (out.isEmpty() || out.last() in listOf("(", "+", "-", "*", "/", "^"))) {
                        out += "0"
                    }
                    out += c.toString()
                    i++
                }
                else -> throw IllegalArgumentException("bad char")
            }
        }
        return out
    }

    private fun prec(op: String) = when (op) {
        "+", "-" -> 1
        "*", "/" -> 2
        "^" -> 3
        else -> 0
    }

    private fun toRpn(tokens: List<String>): List<String> {
        val out = mutableListOf<String>()
        val ops = ArrayDeque<String>()
        for (t in tokens) {
            when {
                t.toDoubleOrNull() != null -> out += t
                t == "(" -> ops.addFirst(t)
                t == ")" -> {
                    while (ops.isNotEmpty() && ops.first() != "(") out += ops.removeFirst()
                    if (ops.isEmpty() || ops.first() != "(") throw IllegalArgumentException("paren")
                    ops.removeFirst()
                }
                else -> {
                    while (ops.isNotEmpty() && ops.first() != "(" &&
                        (prec(ops.first()) > prec(t) || (prec(ops.first()) == prec(t) && t != "^"))
                    ) {
                        out += ops.removeFirst()
                    }
                    ops.addFirst(t)
                }
            }
        }
        while (ops.isNotEmpty()) {
            val op = ops.removeFirst()
            if (op == "(" || op == ")") throw IllegalArgumentException("paren")
            out += op
        }
        return out
    }

    private fun evalRpn(rpn: List<String>): Double {
        val st = ArrayDeque<Double>()
        for (t in rpn) {
            val n = t.toDoubleOrNull()
            if (n != null) {
                st.addFirst(n)
                continue
            }
            val b = st.removeFirst()
            val a = st.removeFirst()
            st.addFirst(
                when (t) {
                    "+" -> a + b
                    "-" -> a - b
                    "*" -> a * b
                    "/" -> a / b
                    "^" -> a.pow(b)
                    else -> throw IllegalArgumentException(t)
                }
            )
        }
        if (st.size != 1) throw IllegalArgumentException("stack")
        return st.first()
    }
}
