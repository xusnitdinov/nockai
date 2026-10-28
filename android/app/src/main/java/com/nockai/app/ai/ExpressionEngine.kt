package com.nockai.app.ai

import kotlin.math.PI
import kotlin.math.abs
import kotlin.math.ceil
import kotlin.math.cos
import kotlin.math.exp
import kotlin.math.floor
import kotlin.math.ln
import kotlin.math.log10
import kotlin.math.max
import kotlin.math.min
import kotlin.math.pow
import kotlin.math.round
import kotlin.math.sin
import kotlin.math.sqrt
import kotlin.math.tan

/** Evaluates Groq calc expressions (mean, nCr, etc.). */
object ExpressionEngine {
    fun tryEvaluate(expression: String): Double? {
        return try {
            val expr = expression.trim().lowercase()
                .replace(" ", "")
                .replace("×", "*")
                .replace("÷", "/")
            eval(expr)
        } catch (_: Exception) {
            null
        }
    }

    fun format(value: Double, asPercent: Boolean = false): Pair<String, String?> {
        if (asPercent) {
            val pct = value * 100.0
            val main = "${LocalMath.format(pct)}%"
            val frac = niceFraction(value)
            return main to frac
        }
        val frac = niceFraction(value)
        return if (frac != null) frac to LocalMath.format(value) else LocalMath.format(value) to null
    }

    private fun niceFraction(value: Double): String? {
        if (value.isNaN() || value.isInfinite()) return null
        for (den in 1..64) {
            val num = Math.round(value * den).toInt()
            if (abs(value - num.toDouble() / den) < 1e-9) {
                if (den == 1) return null
                val g = gcd(abs(num), den)
                return "${num / g}/${den / g}"
            }
        }
        return null
    }

    private fun gcd(a: Int, b: Int): Int {
        var x = a
        var y = b
        while (y != 0) {
            val t = y
            y = x % y
            x = t
        }
        return x.coerceAtLeast(1)
    }

    private fun eval(expr: String): Double {
        // function calls first
        var s = expr
        s = replaceFn(s, "ncr") { args ->
            require(args.size == 2)
            nCr(args[0].toLong(), args[1].toLong()).toDouble()
        }
        s = replaceFn(s, "choose") { args ->
            require(args.size == 2)
            nCr(args[0].toLong(), args[1].toLong()).toDouble()
        }
        s = replaceFn(s, "npr") { args ->
            require(args.size == 2)
            nPr(args[0].toLong(), args[1].toLong()).toDouble()
        }
        s = replaceFn(s, "fact") { args ->
            require(args.size == 1)
            fact(args[0].toLong()).toDouble()
        }
        s = replaceFn(s, "factorial") { args ->
            require(args.size == 1)
            fact(args[0].toLong()).toDouble()
        }
        s = replaceFn(s, "mean") { args -> args.average() }
        s = replaceFn(s, "average") { args -> args.average() }
        s = replaceFn(s, "avg") { args -> args.average() }
        s = replaceFn(s, "median") { args -> median(args) }
        s = replaceFn(s, "mode") { args -> mode(args) }
        s = replaceFn(s, "sum") { args -> args.sum() }
        s = replaceFn(s, "min") { args -> args.minOrNull() ?: Double.NaN }
        s = replaceFn(s, "max") { args -> args.maxOrNull() ?: Double.NaN }
        s = replaceFn(s, "range") { args -> (args.maxOrNull() ?: 0.0) - (args.minOrNull() ?: 0.0) }
        s = replaceFn(s, "stdev") { args -> stdev(args) }
        s = replaceFn(s, "std") { args -> stdev(args) }
        s = replaceFn(s, "variance") { args -> variance(args) }
        s = replaceFn(s, "var") { args -> variance(args) }
        s = replaceFn(s, "sqrt") { args -> sqrt(args.single()) }
        s = replaceFn(s, "abs") { args -> abs(args.single()) }
        s = replaceFn(s, "sin") { args -> sin(args.single()) }
        s = replaceFn(s, "cos") { args -> cos(args.single()) }
        s = replaceFn(s, "tan") { args -> tan(args.single()) }
        s = replaceFn(s, "log") { args -> log10(args.single()) }
        s = replaceFn(s, "ln") { args -> ln(args.single()) }
        s = replaceFn(s, "floor") { args -> floor(args.single()) }
        s = replaceFn(s, "ceil") { args -> ceil(args.single()) }
        s = replaceFn(s, "round") { args -> round(args.single()) }
        s = replaceFn(s, "gcd") { args ->
            require(args.size == 2)
            gcd(args[0].toInt(), args[1].toInt()).toDouble()
        }
        s = replaceFn(s, "lcm") { args ->
            require(args.size == 2)
            val a = args[0].toInt()
            val b = args[1].toInt()
            (abs(a.toLong() * b) / gcd(abs(a), abs(b))).toDouble()
        }

        s = s.replace("pi", PI.toString()).replace(Regex("""(?<![a-z\d.])e(?![a-z\d.])"""), exp(1.0).toString())
        return LocalMath.tryEval(s) ?: throw IllegalArgumentException("eval failed: $s")
    }

    private fun replaceFn(expr: String, name: String, fn: (List<Double>) -> Double): String {
        var s = expr
        while (true) {
            val idx = s.indexOf("$name(")
            if (idx < 0) break
            val startArgs = idx + name.length + 1
            var depth = 1
            var i = startArgs
            while (i < s.length && depth > 0) {
                when (s[i]) {
                    '(' -> depth++
                    ')' -> depth--
                }
                i++
            }
            if (depth != 0) throw IllegalArgumentException("paren in $name")
            val inside = s.substring(startArgs, i - 1)
            val args = splitArgs(inside).map { eval(it) }
            val value = fn(args)
            s = s.substring(0, idx) + value.toString() + s.substring(i)
        }
        return s
    }

    private fun splitArgs(inside: String): List<String> {
        if (inside.isBlank()) return emptyList()
        val parts = mutableListOf<String>()
        var depth = 0
        var start = 0
        for (i in inside.indices) {
            when (inside[i]) {
                '(' -> depth++
                ')' -> depth--
                ',' -> if (depth == 0) {
                    parts += inside.substring(start, i)
                    start = i + 1
                }
            }
        }
        parts += inside.substring(start)
        return parts
    }

    private fun fact(n: Long): Long {
        require(n in 0..20)
        var r = 1L
        for (i in 2..n) r *= i
        return r
    }

    private fun nCr(n: Long, k: Long): Long {
        require(n >= 0 && k >= 0 && k <= n)
        val kk = min(k, n - k)
        var r = 1L
        for (i in 1..kk) {
            r = r * (n - kk + i) / i
        }
        return r
    }

    private fun nPr(n: Long, k: Long): Long {
        require(n >= 0 && k >= 0 && k <= n)
        var r = 1L
        for (i in 0 until k) r *= (n - i)
        return r
    }

    private fun median(args: List<Double>): Double {
        val s = args.sorted()
        val m = s.size / 2
        return if (s.size % 2 == 0) (s[m - 1] + s[m]) / 2.0 else s[m]
    }

    private fun mode(args: List<Double>): Double {
        val freq = args.groupingBy { it }.eachCount()
        val maxF = freq.values.maxOrNull() ?: return Double.NaN
        return freq.filterValues { it == maxF }.keys.minOrNull() ?: Double.NaN
    }

    private fun variance(args: List<Double>): Double {
        if (args.isEmpty()) return Double.NaN
        val mean = args.average()
        return args.map { (it - mean).pow(2) }.average()
    }

    private fun stdev(args: List<Double>): Double = sqrt(variance(args))
}
