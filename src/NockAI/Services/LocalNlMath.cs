using System.Globalization;
using System.Text.RegularExpressions;

namespace NockAI.Services;

/// <summary>
/// Cheap local NL → expression for common calculator phrases.
/// Keeps simple asks working when Groq is down / overloaded.
/// </summary>
public static class LocalNlMath
{
    private static readonly Regex MultiplyBy = new(
        @"^(?:multipl[ey]|times|product(?:\s+of)?)\s+(-?\d+(?:\.\d+)?)\s+(?:by|and|with|×|x|\*)\s+(-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TimesInfix = new(
        @"^(-?\d+(?:\.\d+)?)\s+(?:times|multiplied\s+by|x|×)\s+(-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DivideBy = new(
        @"^(?:divid(?:e|ed)|division)\s+(-?\d+(?:\.\d+)?)\s+(?:by|over)\s+(-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DividedInfix = new(
        @"^(-?\d+(?:\.\d+)?)\s+(?:divided\s+by|over|/)\s+(-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Add = new(
        @"^(?:add|plus|sum(?:\s+of)?)\s+(-?\d+(?:\.\d+)?)\s+(?:and|plus|\+|with)\s+(-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PlusInfix = new(
        @"^(-?\d+(?:\.\d+)?)\s+(?:plus|\+|and)\s+(-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Subtract = new(
        @"^(?:subtract|minus)\s+(-?\d+(?:\.\d+)?)\s+(?:from)\s+(-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MinusInfix = new(
        @"^(-?\d+(?:\.\d+)?)\s+(?:minus|-|subtract(?:ed)?)\s+(-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PercentOf = new(
        @"^(-?\d+(?:\.\d+)?)\s*%?\s*(?:percent|%)\s+(?:of)\s+(-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // "percent increase from 7.5 to 7.9"
    private static readonly Regex PercentChange = new(
        @"^(?:(?:what(?:'?s| is)\s+)?(?:the\s+)?)?percent(?:age)?\s+(?:increase|decrease|change|growth|drop)\s+(?:from\s+)?(-?\d+(?:\.\d+)?)\s+to\s+(-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // "increase from 7 to 9" / "whats the increase from 7 to 9" (absolute, not %)
    private static readonly Regex AbsoluteChange = new(
        @"^(?:(?:what(?:'?s| is)\s+)?(?:the\s+)?)?(?:increase|decrease|change|difference|growth|drop)\s+from\s+(-?\d+(?:\.\d+)?)\s+to\s+(-?\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MeanOf = new(
        @"^(?:mean|average|avg)\s+(?:of\s+)?(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // "6 85s and 90 5s" / "6 85s, 90 5s"
    private static readonly Regex FreqSuffix = new(
        @"^(?:(?:mean|average|avg)\s+(?:of|for|if)?\s*)?(\d+)\s+(\d+)s(?:\s*,\s*|\s+and\s+)(\d+)\s+(\d+)s\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // "mean if 6 students scored 85 and 4 scored 95"
    private static readonly Regex FreqScored = new(
        @"^(?:(?:mean|average|avg)\s+(?:of|for|if)?\s*)?(\d+)\s+(?:students?\s+|people\s+|scores?\s+)?(?:scored|got|have|with)\s+(\d+)(?:\s*(?:points?|marks?))?\s+and\s+(\d+)\s+(?:students?\s+|people\s+|scores?\s+)?(?:scored|got|have|with)?\s*(\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // 12n=144 | 12*n = 144 | 12 n = 144 | find n: 12n=144
    private static readonly Regex SimpleLinear = new(
        @"(?:(?:find|solve(?:\s+for)?|whats?|what'?s?|value\s+of)\s+)?(?<a>-?\d+(?:\.\d+)?)\s*(?:\*|×|x)?\s*(?<v>[a-z])\s*=\s*(?<b>-?\d+(?:\.\d+)?)\s*(?:find\s+(?:the\s+)?\k<v>|whats?\s+(?:the\s+)?\k<v>|solve.*)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SimpleLinearRhsFirst = new(
        @"^(?<b>-?\d+(?:\.\d+)?)\s*=\s*(?<a>-?\d+(?:\.\d+)?)\s*(?:\*|×|x)?\s*(?<v>[a-z])\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Choose = new(
        @"^(?:(?:what\s+is|whats?|find|compute|calculate)\s+)?(\d+)\s+choose\s+(\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Permute = new(
        @"^(?:(?:what\s+is|whats?|find|compute|calculate)\s+)?(?:(?:permutations?(?:\s+of)?|npr|p)\s+)?(\d+)\s+(?:p|permute(?:d)?|taken|take)\s+(\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FactorialNl = new(
        @"^(?:(?:what\s+is|whats?|find|compute|calculate)\s+)?(\d+)\s*(?:!|factorial)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GcdNl = new(
        @"^(?:(?:what\s+is|whats?|find)\s+)?gcd\s+(?:of\s+)?(\d+)\s+and\s+(\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LcmNl = new(
        @"^(?:(?:what\s+is|whats?|find)\s+)?lcm\s+(?:of\s+)?(\d+)\s+and\s+(\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ModeOf = new(
        @"^(?<fn>mode|median|range|variance|stdev|std)\s+(?:of\s+)?(?<list>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryToExpression(string input, out string expression)
    {
        expression = "";
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var t = Normalize(input);

        if (TryPair(MultiplyBy, t, "*", out expression)) return true;
        if (TryPair(TimesInfix, t, "*", out expression)) return true;
        if (TryPair(DivideBy, t, "/", out expression)) return true;
        if (TryPair(DividedInfix, t, "/", out expression)) return true;
        if (TryPair(Add, t, "+", out expression)) return true;
        if (TryPair(PlusInfix, t, "+", out expression)) return true;
        if (TrySubtract(t, out expression)) return true;
        if (TryPair(MinusInfix, t, "-", out expression)) return true;
        if (TryPercentChange(t, out expression)) return true;
        if (TryAbsoluteChange(t, out expression)) return true;
        if (TryPercent(t, out expression)) return true;
        if (TryFrequencyMean(t, out expression)) return true;
        if (TryChoosePermFact(t, out expression)) return true;
        if (TryMean(t, out expression)) return true;
        if (TryStatList(t, out expression)) return true;
        if (TryLinear(t, out expression)) return true;

        return false;
    }

    /// <summary>
    /// Fix bad model output like mean(6*85,4*95) → frequency mean.
    /// </summary>
    public static bool TryRewriteBadFrequencyMean(string? expression, out string fixedExpression)
    {
        fixedExpression = "";
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var m = Regex.Match(
            expression.Trim(),
            @"^mean\s*\(\s*(-?\d+(?:\.\d+)?)\s*\*\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*\*\s*(-?\d+(?:\.\d+)?)\s*\)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            return false;

        fixedExpression =
            $"({m.Groups[1].Value}*{m.Groups[2].Value}+{m.Groups[3].Value}*{m.Groups[4].Value})/({m.Groups[1].Value}+{m.Groups[3].Value})";
        return true;
    }

    private static string Normalize(string input)
    {
        var t = input.Trim();
        t = Regex.Replace(t, @"^(?:yo+|hey|hi|hello|bro|dude|pls|plz|please|can you|could you|would you|help me)\s+", "", RegexOptions.IgnoreCase).Trim();
        t = Regex.Replace(t, @"\b(pls|plz|please)\b[.!?]*$", "", RegexOptions.IgnoreCase).Trim();
        t = t.Replace('×', '*').Replace('÷', '/').Replace('−', '-');
        // common typo: multiple → multiply (handled by multipl[ey] regex)
        return t;
    }

    private static bool TryPair(Regex re, string t, string op, out string expression)
    {
        expression = "";
        var m = re.Match(t);
        if (!m.Success) return false;
        expression = $"{m.Groups[1].Value}{op}{m.Groups[2].Value}";
        return true;
    }

    private static bool TrySubtract(string t, out string expression)
    {
        expression = "";
        var m = Subtract.Match(t);
        if (!m.Success) return false;
        // subtract A from B → B-A
        expression = $"{m.Groups[2].Value}-{m.Groups[1].Value}";
        return true;
    }

    private static bool TryPercent(string t, out string expression)
    {
        expression = "";
        var m = PercentOf.Match(t);
        if (!m.Success) return false;
        expression = $"({m.Groups[1].Value}/100)*{m.Groups[2].Value}";
        return true;
    }

    private static bool TryPercentChange(string t, out string expression)
    {
        expression = "";
        var m = PercentChange.Match(t);
        if (!m.Success) return false;
        // ((to - from) / from) * 100
        expression = $"(({m.Groups[2].Value}-{m.Groups[1].Value})/{m.Groups[1].Value})*100";
        return true;
    }

    private static bool TryAbsoluteChange(string t, out string expression)
    {
        expression = "";
        // Don't steal percent asks
        if (Regex.IsMatch(t, @"\bpercent(?:age)?\b|%", RegexOptions.IgnoreCase))
            return false;
        var m = AbsoluteChange.Match(t);
        if (!m.Success) return false;
        // to - from
        expression = $"{m.Groups[2].Value}-{m.Groups[1].Value}";
        return true;
    }

    private static bool TryFrequencyMean(string t, out string expression)
    {
        expression = "";
        var m = FreqSuffix.Match(t);
        if (!m.Success)
            m = FreqScored.Match(t);
        if (!m.Success)
            return false;

        // (n1*v1 + n2*v2) / (n1+n2)
        expression =
            $"({m.Groups[1].Value}*{m.Groups[2].Value}+{m.Groups[3].Value}*{m.Groups[4].Value})/({m.Groups[1].Value}+{m.Groups[3].Value})";
        return true;
    }

    private static bool TryChoosePermFact(string t, out string expression)
    {
        expression = "";
        var m = Choose.Match(t);
        if (m.Success)
        {
            expression = $"nCr({m.Groups[1].Value},{m.Groups[2].Value})";
            return true;
        }

        m = Permute.Match(t);
        if (m.Success)
        {
            expression = $"nPr({m.Groups[1].Value},{m.Groups[2].Value})";
            return true;
        }

        m = FactorialNl.Match(t);
        if (m.Success)
        {
            expression = $"fact({m.Groups[1].Value})";
            return true;
        }

        m = GcdNl.Match(t);
        if (m.Success)
        {
            expression = $"gcd({m.Groups[1].Value},{m.Groups[2].Value})";
            return true;
        }

        m = LcmNl.Match(t);
        if (m.Success)
        {
            expression = $"lcm({m.Groups[1].Value},{m.Groups[2].Value})";
            return true;
        }

        return false;
    }

    private static bool TryMean(string t, out string expression)
    {
        expression = "";
        var m = MeanOf.Match(t);
        if (!m.Success) return false;

        if (!TryParseNumList(m.Groups[1].Value, out var nums) || nums.Count < 2)
            return false;
        expression = $"mean({string.Join(",", nums)})";
        return true;
    }

    private static bool TryStatList(string t, out string expression)
    {
        expression = "";
        var m = ModeOf.Match(t);
        if (!m.Success) return false;

        var fn = m.Groups["fn"].Value.ToLowerInvariant();
        if (!TryParseNumList(m.Groups["list"].Value, out var nums) || nums.Count < 1)
            return false;
        if ((fn is "stdev" or "std" or "variance" or "range") && nums.Count < 2)
            return false;

        expression = $"{fn}({string.Join(",", nums)})";
        return true;
    }

    private static bool TryParseNumList(string rest, out List<string> nums)
    {
        nums = [];
        rest = rest.Trim().Replace(" and ", ",", StringComparison.OrdinalIgnoreCase);
        var parts = rest.Split([',', ' ', ';', '\t'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
                !double.TryParse(p, NumberStyles.Float, CultureInfo.CurrentCulture, out _))
                return false;
            nums.Add(p);
        }

        return nums.Count > 0;
    }

    private static bool TryLinear(string t, out string expression)
    {
        expression = "";
        var m = SimpleLinear.Match(t);
        if (!m.Success)
            m = SimpleLinearRhsFirst.Match(t);
        if (!m.Success) return false;

        var a = m.Groups["a"].Value;
        var b = m.Groups["b"].Value;
        if (!double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var av) ||
            Math.Abs(av) < double.Epsilon)
            return false;

        expression = $"{b}/{a}";
        return true;
    }
}
