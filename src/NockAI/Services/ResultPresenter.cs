using System.Globalization;
using System.Text.RegularExpressions;

namespace NockAI.Services;

public enum ResultIconKind
{
    None,
    Increase,
    Decrease,
    Percent,
    Neutral
}

/// <summary>
/// Turns raw numeric answers into human display.
/// % only when the user actually asked for percent.
/// </summary>
public static class ResultPresenter
{
    private static readonly Regex AsksPercent = new(
        @"\bpercent(?:age)?s?\b|%",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // "chances", "probability", "odds" → show as % even if they didn't type "percent"
    private static readonly Regex AsksProbability = new(
        @"\b(?:chance|chances|probability|probabilities|odds|likelihood|likely|probable)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // "percent increase from 7.5 to 7.9"
    private static readonly Regex PercentChange = new(
        @"percent(?:age)?\s+(?<dir>increase|decrease|change|growth|drop)\s+(?:from\s+)?(?<a>-?\d+(?:\.\d+)?)\s+to\s+(?<b>-?\d+(?:\.\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // "increase from 7.5 to 7.9 percent" / "% increase from ..."
    private static readonly Regex PercentChangeLoose = new(
        @"(?:(?<dir>increase|decrease|growth|drop)\s+from\s+(?<a>-?\d+(?:\.\d+)?)\s+to\s+(?<b>-?\d+(?:\.\d+)?)\s*(?:percent|%)|(?:%|percent(?:age)?)\s+(?<dir>increase|decrease)\s+(?:from\s+)?(?<a>-?\d+(?:\.\d+)?)\s+to\s+(?<b>-?\d+(?:\.\d+)?))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // "increase from 7 to 9" / "whats the increase from 7 to 9" (NO percent)
    private static readonly Regex AbsoluteChange = new(
        @"(?<dir>increase|decrease|change|difference|growth|drop)\s+from\s+(?<a>-?\d+(?:\.\d+)?)\s+to\s+(?<b>-?\d+(?:\.\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static (string Display, string? Secondary, ResultIconKind Icon) Present(
        string? userInput,
        string? expression,
        double value,
        string rawPrimary,
        string? rawSecondary)
    {
        var wantsPercent = !string.IsNullOrWhiteSpace(userInput) && AsksPercent.IsMatch(userInput);
        var wantsProbability = !string.IsNullOrWhiteSpace(userInput) && AsksProbability.IsMatch(userInput);

        if (wantsPercent &&
            TryPercentChange(userInput, value, out var pctDisplay, out var pctSec, out var pctIcon))
            return (pctDisplay, pctSec, pctIcon);

        // "what are the chances..." → 40% (not bare 2/5), fraction on the secondary line
        if (wantsProbability &&
            SettingsStore.Current.ProbabilityAsPercent &&
            TryProbability(value, rawPrimary, rawSecondary, out var probDisplay, out var probSec, out var probIcon))
            return (probDisplay, probSec, probIcon);

        // Absolute change: "increase from 7 to 9" → +2 (never invent %)
        if (!wantsPercent &&
            TryAbsoluteChange(userInput, value, expression, out var absDisplay, out var absSec, out var absIcon))
            return (absDisplay, absSec, absIcon);

        // Model returned % math but user never asked % → show absolute change instead
        if (!wantsPercent && !wantsProbability &&
            !string.IsNullOrWhiteSpace(expression) &&
            LooksLikePercentChangeExpr(expression) &&
            TryRecoverAbsoluteFromPercentExpr(userInput, expression, value, out var recovered, out var recSec, out var recIcon))
            return (recovered, recSec, recIcon);

        return (rawPrimary, rawSecondary, ResultIconKind.None);
    }

    public static string Glyph(ResultIconKind kind) => kind switch
    {
        ResultIconKind.Increase => "\uE74A",
        ResultIconKind.Decrease => "\uE74B",
        ResultIconKind.Percent => "\uE94E",
        ResultIconKind.Neutral => "\uE108",
        _ => ""
    };

    private static bool TryProbability(
        double value,
        string rawPrimary,
        string? rawSecondary,
        out string display,
        out string? secondary,
        out ResultIconKind icon)
    {
        display = "";
        secondary = null;
        icon = ResultIconKind.Percent;

        // Engine may return 0.4, or already 40 if model did *100
        double pct;
        if (value >= 0 && value <= 1.0000001)
            pct = value * 100.0;
        else if (value > 1 && value <= 100)
            pct = value;
        else
            return false; // not a probability-looking number

        display = $"{FormatPercent(pct)}%";

        // Keep the fraction/decimal as proof: "2/5 = 0.4"
        var bits = new List<string>();
        if (!string.IsNullOrWhiteSpace(rawPrimary) &&
            !rawPrimary.Contains('%', StringComparison.Ordinal) &&
            !string.Equals(rawPrimary, display, StringComparison.Ordinal))
            bits.Add(rawPrimary);
        if (!string.IsNullOrWhiteSpace(rawSecondary) &&
            !bits.Contains(rawSecondary, StringComparer.Ordinal))
            bits.Add(rawSecondary);
        if (bits.Count > 0)
            secondary = string.Join(" = ", bits);

        return true;
    }

    private static bool TryPercentChange(
        string? userInput,
        double value,
        out string display,
        out string? secondary,
        out ResultIconKind icon)
    {
        display = "";
        secondary = null;
        icon = ResultIconKind.None;

        if (string.IsNullOrWhiteSpace(userInput))
            return false;

        var m = PercentChange.Match(userInput);
        if (!m.Success)
            m = PercentChangeLoose.Match(userInput);
        if (!m.Success || HasTrailingMath(userInput, m))
            return false;

        var dir = m.Groups["dir"].Value.ToLowerInvariant();
        TryParse(m.Groups["a"].Value, out var from);
        TryParse(m.Groups["b"].Value, out var to);

        var signed = value;
        if (Math.Abs(from) > 1e-15)
            signed = (to - from) / from * 100.0;

        if (dir is "decrease" or "drop")
        {
            icon = ResultIconKind.Decrease;
            display = $"{FormatPercent(Math.Abs(signed))}% decrease";
        }
        else if (dir is "increase" or "growth")
        {
            icon = signed < -1e-12 ? ResultIconKind.Decrease : ResultIconKind.Increase;
            display = signed < -1e-12
                ? $"{FormatPercent(Math.Abs(signed))}% decrease"
                : $"{FormatPercent(Math.Abs(signed))}% increase";
        }
        else
        {
            icon = signed > 1e-12 ? ResultIconKind.Increase
                : signed < -1e-12 ? ResultIconKind.Decrease
                : ResultIconKind.Neutral;
            display = icon switch
            {
                ResultIconKind.Increase => $"{FormatPercent(signed)}% increase",
                ResultIconKind.Decrease => $"{FormatPercent(Math.Abs(signed))}% decrease",
                _ => "0% change"
            };
        }

        secondary = $"from {TrimNum(from)} → {TrimNum(to)}";
        var exact = ExactHint(signed);
        if (exact is not null)
            secondary = $"{secondary}  ·  {exact}";

        return true;
    }

    private static bool TryAbsoluteChange(
        string? userInput,
        double value,
        string? expression,
        out string display,
        out string? secondary,
        out ResultIconKind icon)
    {
        display = "";
        secondary = null;
        icon = ResultIconKind.None;

        if (string.IsNullOrWhiteSpace(userInput))
            return false;

        var m = AbsoluteChange.Match(userInput);
        // "increase from 7 to 9 and ..." → don't steal; let the full expression win
        if (!m.Success || HasTrailingMath(userInput, m))
            return false;

        TryParse(m.Groups["a"].Value, out var from);
        TryParse(m.Groups["b"].Value, out var to);
        var delta = to - from;

        // If model wrongly did percent (*100), recover absolute delta from endpoints
        if (LooksLikePercentChangeExpr(expression ?? "") || Math.Abs(value - delta) > 0.05 && Math.Abs(from) > 1e-15)
            delta = to - from;
        else if (Math.Abs(value - delta) <= Math.Max(0.05, Math.Abs(delta) * 0.01))
            delta = value; // trust engine when it already did B-A

        var dir = m.Groups["dir"].Value.ToLowerInvariant();
        icon = delta > 1e-12 ? ResultIconKind.Increase
            : delta < -1e-12 ? ResultIconKind.Decrease
            : ResultIconKind.Neutral;

        if (dir is "decrease" or "drop")
        {
            icon = ResultIconKind.Decrease;
            display = TrimNum(Math.Abs(delta));
        }
        else if (dir is "increase" or "growth")
        {
            display = delta >= 0 ? $"+{TrimNum(delta)}" : TrimNum(delta);
            if (delta < 0) icon = ResultIconKind.Decrease;
            else icon = ResultIconKind.Increase;
        }
        else
        {
            display = delta > 0 ? $"+{TrimNum(delta)}" : TrimNum(delta);
        }

        secondary = $"from {TrimNum(from)} → {TrimNum(to)}";
        return true;
    }

    /// <summary>
    /// True when the match is only a prefix and more math follows ("and increase by 10").
    /// </summary>
    private static bool HasTrailingMath(string userInput, Match m)
    {
        var after = userInput[(m.Index + m.Length)..].Trim();
        if (after.Length == 0)
            return false;
        // bare punctuation / thanks = ok to ignore
        if (Regex.IsMatch(after, @"^[.!?,\s]+$"))
            return false;
        if (GroqParser.IsAcknowledgement(after))
            return false;
        return true;
    }

    private static bool TryRecoverAbsoluteFromPercentExpr(
        string? userInput,
        string expression,
        double percentValue,
        out string display,
        out string? secondary,
        out ResultIconKind icon)
    {
        display = "";
        secondary = null;
        icon = ResultIconKind.None;

        // Pull A,B from "from A to B" if present
        var m = AbsoluteChange.Match(userInput ?? "");
        if (m.Success)
        {
            TryParse(m.Groups["a"].Value, out var from);
            TryParse(m.Groups["b"].Value, out var to);
            var delta = to - from;
            icon = delta >= 0 ? ResultIconKind.Increase : ResultIconKind.Decrease;
            display = delta >= 0 ? $"+{TrimNum(delta)}" : TrimNum(delta);
            secondary = $"from {TrimNum(from)} → {TrimNum(to)}";
            return true;
        }

        // Last resort: can't recover endpoints — show raw percent value as plain number (no % label)
        // Prefer not to show wrong unit; fall through
        _ = expression;
        _ = percentValue;
        return false;
    }

    private static bool LooksLikePercentChangeExpr(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return false;
        var e = expression.Replace(" ", "", StringComparison.Ordinal);
        return e.Contains("*100", StringComparison.Ordinal) &&
               e.Contains('/') &&
               (e.Contains('-') || e.Contains('+'));
    }

    private static string FormatPercent(double absOrSigned)
    {
        var v = Math.Abs(absOrSigned);
        if (Math.Abs(v - Math.Round(v)) < 1e-9)
            return ((long)Math.Round(v)).ToString(CultureInfo.CurrentCulture);

        return Math.Round(v, 2, MidpointRounding.AwayFromZero)
            .ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static string? ExactHint(double percentValue)
    {
        if (!MathEngine.TryToNiceFraction(percentValue, out var n, out var d) || d <= 1)
            return null;
        if (d > 16 || Math.Abs(n) > 64)
            return null;
        return $"exactly {n}/{d}%";
    }

    private static string TrimNum(double d)
    {
        if (Math.Abs(d - Math.Round(d)) < 1e-9)
            return ((long)Math.Round(d)).ToString(CultureInfo.CurrentCulture);
        return d.ToString("0.####", CultureInfo.CurrentCulture);
    }

    private static bool TryParse(string s, out double n) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out n) ||
        double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out n);
}
