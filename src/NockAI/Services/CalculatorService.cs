namespace NockAI.Services;

public enum CalcResultKind
{
    Empty,
    Number,
    Explain,
    Unknown
}

public sealed class CalcResult
{
    public required CalcResultKind Kind { get; init; }
    public required string Display { get; init; }
    public string? SecondaryDisplay { get; init; }
    public string? Markdown { get; init; }
    public string? ExpressionUsed { get; init; }
    public AiReply? RawReply { get; init; }
    public bool Ok => Kind is CalcResultKind.Number or CalcResultKind.Explain;

    /// <summary>Best string to put on the clipboard (fraction if we have one).</summary>
    public string CopyText =>
        Kind == CalcResultKind.Explain
            ? (Markdown ?? "")
            : !string.IsNullOrWhiteSpace(Display)
                ? Display
                : "";

    public static CalcResult Empty() =>
        new() { Kind = CalcResultKind.Empty, Display = "" };

    public static CalcResult Unknown(string? expression = null, AiReply? raw = null) =>
        new() { Kind = CalcResultKind.Unknown, Display = "???", ExpressionUsed = expression, RawReply = raw };

    public static CalcResult Number(string value, string? expression = null, AiReply? raw = null, string? secondary = null) =>
        new()
        {
            Kind = CalcResultKind.Number,
            Display = value,
            SecondaryDisplay = secondary,
            ExpressionUsed = expression,
            RawReply = raw
        };

    public static CalcResult Explain(string markdown, AiReply? raw = null) =>
        new() { Kind = CalcResultKind.Explain, Display = "", Markdown = markdown, RawReply = raw };
}

public sealed class CalculatorService
{
    private readonly MathEngine _math;
    private readonly GroqParser _groq;

    public CalculatorService(MathEngine math, GroqParser groq)
    {
        _math = math;
        _groq = groq;
    }

    public Task<CalcResult> EvaluateAsync(string input, CancellationToken ct) =>
        EvaluateAsync(input, history: null, ct);

    public async Task<CalcResult> EvaluateAsync(
        string input,
        IReadOnlyList<ChatTurn>? history,
        CancellationToken ct)
    {
        input = input.Trim();
        if (input.Length == 0)
            return CalcResult.Empty();

        var hasHistory = history is { Count: > 0 };

        // Local-first: pure symbols
        if (!hasHistory &&
            _math.LooksLikeSymbolicMath(input) &&
            _math.TryEvaluate(input, out var local, out var localSec))
            return CalcResult.Number(local, input, secondary: localSec);

        // Local-first: common NL (multiply/typos/simple algebra) - no API needed
        if (!hasHistory &&
            LocalNlMath.TryToExpression(input, out var nlExpr) &&
            _math.TryEvaluate(nlExpr, out var nlAnswer, out var nlSec))
            return CalcResult.Number(nlAnswer, nlExpr, AiReply.Calc(nlExpr), nlSec);

        AiReply reply;
        try
        {
            reply = await _groq.InterpretAsync(input, history, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Groq down/over capacity - last local attempt
            if (!hasHistory &&
                LocalNlMath.TryToExpression(input, out var fallbackExpr) &&
                _math.TryEvaluate(fallbackExpr, out var fallbackAnswer, out var fallbackSec))
                return CalcResult.Number(fallbackAnswer, fallbackExpr, AiReply.Calc(fallbackExpr), fallbackSec);
            throw;
        }

        var result = FromReply(reply);

        // Algebra leftover variables or failed eval on equation-looking input
        if (!result.Ok &&
            (GroqParser.LooksLikeEquationSolve(input) ||
             GroqParser.ExpressionHasVariables(reply.Expression)))
        {
            try
            {
                var retry = await _groq.AskSolveNumericAsync(input, ct).ConfigureAwait(false);
                result = FromReply(retry);
            }
            catch
            {
                // ignore; try local below
            }

            if (!result.Ok &&
                LocalNlMath.TryToExpression(input, out var algExpr) &&
                _math.TryEvaluate(algExpr, out var algAnswer, out var algSec))
                return CalcResult.Number(algAnswer, algExpr, AiReply.Calc(algExpr), algSec);
        }

        // Calc expression came back but engine rejected it - one rewrite pass
        if (result.Kind == CalcResultKind.Unknown &&
            reply.Kind == AiReplyKind.Calc &&
            !string.IsNullOrWhiteSpace(reply.Expression) &&
            !hasHistory)
        {
            try
            {
                var rewrite = await _groq.AskSolveNumericAsync(
                    "Rewrite as a pure numeric expression (no words): " + input,
                    ct).ConfigureAwait(false);
                result = FromReply(rewrite);
            }
            catch
            {
                // leave as unknown
            }
        }

        // Follow-up still unknown -> hard continue retry
        if (result.Kind == CalcResultKind.Unknown &&
            hasHistory &&
            GroqParser.LooksLikeFollowUpContinue(input))
        {
            try
            {
                var retry = await _groq.AskFollowUpAsync(input, history!, ct).ConfigureAwait(false);
                result = FromReply(retry);
            }
            catch
            {
                // keep unknown
            }
        }

        return result;
    }

    private CalcResult FromReply(AiReply reply)
    {
        switch (reply.Kind)
        {
            case AiReplyKind.Calc:
            {
                var expr = reply.Expression;
                if (LocalNlMath.TryRewriteBadFrequencyMean(expr, out var fixedFreq))
                    expr = fixedFreq;

                if (!string.IsNullOrWhiteSpace(expr) &&
                    _math.TryEvaluate(expr, out var answer, out var secondary))
                    return CalcResult.Number(answer, expr, reply, secondary);
                return CalcResult.Unknown(reply.Expression, reply);
            }

            case AiReplyKind.Explain:
                if (!string.IsNullOrWhiteSpace(reply.Markdown))
                    return CalcResult.Explain(reply.Markdown, reply);
                return CalcResult.Unknown(raw: reply);

            default:
                return CalcResult.Unknown(raw: reply);
        }
    }
}
