using System.Globalization;
using System.Text.RegularExpressions;
using org.mariuszgromada.math.mxparser;

namespace NockAI.Services;

public sealed class MathEngine
{
    private static readonly Regex StatsCall = new(
        @"^\s*(median|mean|average|avg|mode|sum|min|max|stdev|std|variance|var|range)\s*\((.*)\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ComboCall = new(
        @"^\s*(ncr|npr|c|p|choose|fact|factorial|gcd|lcm)\s*\(\s*(-?\d+(?:\.\d+)?)\s*(?:,\s*(-?\d+(?:\.\d+)?)\s*)?\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BangFactorial = new(
        @"^\s*(-?\d+)\s*!\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    static MathEngine()
    {
        License.iConfirmNonCommercialUse("NockAI");
    }

    public bool TryEvaluate(string expression, out string formattedResult) =>
        TryEvaluate(expression, out formattedResult, out _);

    public bool TryEvaluate(string expression, out string formattedResult, out string? secondary)
    {
        formattedResult = "";
        secondary = null;
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var normalized = Normalize(expression);

        if (TryEvaluateCombo(normalized, out var comboVal))
        {
            FormatValue(comboVal, out formattedResult, out secondary);
            return true;
        }

        if (TryEvaluateStats(normalized, out var statsVal))
        {
            FormatValue(statsVal, out formattedResult, out secondary);
            return true;
        }

        try
        {
            var expr = new Expression(normalized);
            if (!expr.checkSyntax())
                return false;

            var value = expr.calculate();
            if (double.IsNaN(value) || double.IsInfinity(value))
                return false;

            FormatValue(value, out formattedResult, out secondary);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool LooksLikeSymbolicMath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var t = input.Trim();
        if (BangFactorial.IsMatch(t))
            return true;

        if (t.Any(char.IsLetter) && !ContainsKnownFunction(t))
            return false;

        return t.Any(char.IsDigit)
               && t.Any(c => "+-*/^%().,!".Contains(c) || char.IsDigit(c) || char.IsWhiteSpace(c));
    }

    private static bool ContainsKnownFunction(string t)
    {
        string[] fns =
        [
            "sqrt", "sin", "cos", "tan", "abs", "log", "ln", "pow", "min", "max",
            "floor", "ceil", "round", "median", "mean", "average", "avg", "mode", "sum",
            "stdev", "std", "variance", "var", "range", "ncr", "npr", "choose", "fact",
            "factorial", "gcd", "lcm"
        ];
        var lower = t.ToLowerInvariant();
        return fns.Any(f => lower.Contains(f + "(", StringComparison.Ordinal));
    }

    private static string Normalize(string expression)
    {
        var s = expression.Trim();
        s = s.Replace('×', '*').Replace('÷', '/').Replace('−', '-');
        s = s.Replace("**", "^");

        // 5! → fact(5) for mxparser path; also handled directly
        s = Regex.Replace(s, @"(?<![\w.])(\d+)\s*!", "fact($1)", RegexOptions.CultureInvariant);

        // C(n,k) / P(n,k) aliases
        s = Regex.Replace(s, @"\bC\s*\(", "nCr(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        s = Regex.Replace(s, @"\bP\s*\(", "nPr(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return s;
    }

    private static bool TryEvaluateCombo(string expression, out double result)
    {
        result = double.NaN;

        var bang = BangFactorial.Match(expression);
        if (bang.Success)
        {
            if (!int.TryParse(bang.Groups[1].Value, out var n) || n < 0 || n > 20)
                return false;
            result = Factorial(n);
            return true;
        }

        var m = ComboCall.Match(expression);
        if (!m.Success)
            return false;

        var name = m.Groups[1].Value.ToLowerInvariant();
        if (!TryParseDouble(m.Groups[2].Value, out var a))
            return false;

        double b = 0;
        var hasB = m.Groups[3].Success && TryParseDouble(m.Groups[3].Value, out b);

        result = name switch
        {
            "fact" or "factorial" when !hasB && IsNonNegInt(a, out var fn) && fn <= 20 => Factorial(fn),
            "ncr" or "c" or "choose" when hasB && IsNonNegInt(a, out var n) && IsNonNegInt(b, out var k) => NCr(n, k),
            "npr" or "p" when hasB && IsNonNegInt(a, out var n2) && IsNonNegInt(b, out var k2) => NPr(n2, k2),
            "gcd" when hasB && IsInt(a, out var ga) && IsInt(b, out var gb) => Gcd(Math.Abs(ga), Math.Abs(gb)),
            "lcm" when hasB && IsInt(a, out var la) && IsInt(b, out var lb) => Lcm(Math.Abs(la), Math.Abs(lb)),
            _ => double.NaN
        };

        return !double.IsNaN(result) && !double.IsInfinity(result);
    }

    private static bool TryEvaluateStats(string expression, out double result)
    {
        result = double.NaN;
        var m = StatsCall.Match(expression);
        if (!m.Success)
            return false;

        var name = m.Groups[1].Value.ToLowerInvariant();
        if (!TryParseNumberList(m.Groups[2].Value, out var nums) || nums.Count == 0)
            return false;

        result = name switch
        {
            "median" => Median(nums),
            "mean" or "average" or "avg" => nums.Average(),
            "sum" => nums.Sum(),
            "min" => nums.Min(),
            "max" => nums.Max(),
            "mode" => Mode(nums),
            "stdev" or "std" => Stdev(nums),
            "variance" or "var" => Variance(nums),
            "range" => nums.Max() - nums.Min(),
            _ => double.NaN
        };

        return !double.IsNaN(result) && !double.IsInfinity(result);
    }

    private static bool TryParseNumberList(string inside, out List<double> nums)
    {
        nums = [];
        if (string.IsNullOrWhiteSpace(inside))
            return false;

        var parts = inside.Split([',', ' ', ';', '\t'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (!TryParseDouble(part, out var n))
                return false;
            nums.Add(n);
        }

        return nums.Count > 0;
    }

    private static bool TryParseDouble(string s, out double n) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out n) ||
        double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out n);

    private static bool IsNonNegInt(double d, out int n)
    {
        n = 0;
        if (d < 0 || Math.Abs(d - Math.Round(d)) > 1e-9) return false;
        n = (int)Math.Round(d);
        return true;
    }

    private static bool IsInt(double d, out long n)
    {
        n = 0;
        if (Math.Abs(d - Math.Round(d)) > 1e-9) return false;
        n = (long)Math.Round(d);
        return true;
    }

    private static double Factorial(int n)
    {
        double r = 1;
        for (var i = 2; i <= n; i++) r *= i;
        return r;
    }

    private static double NCr(int n, int k)
    {
        if (k < 0 || k > n) return double.NaN;
        k = Math.Min(k, n - k);
        double r = 1;
        for (var i = 1; i <= k; i++)
            r = r * (n - k + i) / i;
        return Math.Round(r, 10);
    }

    private static double NPr(int n, int k)
    {
        if (k < 0 || k > n) return double.NaN;
        double r = 1;
        for (var i = 0; i < k; i++)
            r *= n - i;
        return r;
    }

    private static long Gcd(long a, long b)
    {
        while (b != 0)
        {
            var t = a % b;
            a = b;
            b = t;
        }
        return a;
    }

    private static long Lcm(long a, long b)
    {
        if (a == 0 || b == 0) return 0;
        return checked(a / Gcd(a, b) * b);
    }

    private static double Median(List<double> nums)
    {
        var sorted = nums.OrderBy(x => x).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }

    private static double Mode(List<double> nums)
    {
        var groups = nums.GroupBy(x => x).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).ToList();
        return groups[0].Key;
    }

    private static double Stdev(List<double> nums)
    {
        var v = Variance(nums);
        return double.IsNaN(v) ? double.NaN : Math.Sqrt(v);
    }

    private static double Variance(List<double> nums)
    {
        if (nums.Count < 2) return double.NaN;
        var mean = nums.Average();
        return nums.Sum(x => (x - mean) * (x - mean)) / (nums.Count - 1);
    }

    private static void FormatValue(double d, out string primary, out string? secondary)
    {
        secondary = null;
        var decimalForm = FormatDecimal(d);

        if (TryToNiceFraction(d, out var num, out var den) &&
            den > 1 &&
            den <= 16 &&
            Math.Abs(num) <= 64)
        {
            var frac = $"{num}/{den}";
            // Nice SAT-style fraction as primary; decimal underneath
            primary = frac;
            if (!string.Equals(decimalForm, frac, StringComparison.Ordinal))
                secondary = decimalForm;
            return;
        }

        // Still expose a fraction hint for messier rationals (e.g. 378/5)
        if (TryToNiceFraction(d, out num, out den) && den > 1 && den <= 100)
            secondary = $"{num}/{den}";

        primary = decimalForm;
    }

    private static string FormatDecimal(double d)
    {
        if (Math.Abs(d) >= 1e12 || (Math.Abs(d) > 0 && Math.Abs(d) < 1e-9))
            return d.ToString("G10", CultureInfo.CurrentCulture);

        var rounded = Math.Round(d, 10, MidpointRounding.AwayFromZero);
        return rounded.ToString("G15", CultureInfo.CurrentCulture);
    }

    /// <summary>Continued-fraction approx for nice rationals (denom ≤ 100).</summary>
    public static bool TryToNiceFraction(double value, out long numerator, out long denominator)
    {
        numerator = 0;
        denominator = 1;
        if (double.IsNaN(value) || double.IsInfinity(value))
            return false;

        var sign = value < 0 ? -1 : 1;
        var x = Math.Abs(value);
        if (Math.Abs(x - Math.Round(x)) < 1e-12)
        {
            numerator = sign * (long)Math.Round(x);
            denominator = 1;
            return false; // integer - no fraction UI
        }

        const long maxDen = 100;
        const double eps = 1e-10;

        long a0 = (long)Math.Floor(x);
        double frac = x - a0;
        if (frac < eps)
        {
            numerator = sign * a0;
            denominator = 1;
            return false;
        }

        long p0 = 1, q0 = 0;
        long p1 = a0, q1 = 1;

        for (var i = 0; i < 20; i++)
        {
            if (frac < eps) break;
            var inv = 1.0 / frac;
            var ai = (long)Math.Floor(inv);
            var p = ai * p1 + p0;
            var q = ai * q1 + q0;
            if (q > maxDen) break;

            p0 = p1; q0 = q1;
            p1 = p; q1 = q;
            frac = inv - ai;

            if (Math.Abs((double)p1 / q1 - x) < eps)
                break;
        }

        if (q1 <= 1 || q1 > maxDen)
            return false;
        if (Math.Abs((double)p1 / q1 - x) > 1e-8)
            return false;

        numerator = sign * p1;
        denominator = q1;
        return true;
    }
}
