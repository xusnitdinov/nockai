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
    public ResultIconKind Icon { get; init; }
    public bool Ok => Kind is CalcResultKind.Number or CalcResultKind.Explain;

    /// <summary>Best string to put on the clipboard.</summary>
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

    public static CalcResult Number(
        string value,
        string? expression = null,
        AiReply? raw = null,
        string? secondary = null,
        ResultIconKind icon = ResultIconKind.None) =>
        new()
        {
            Kind = CalcResultKind.Number,
            Display = value,
            SecondaryDisplay = secondary,
            ExpressionUsed = expression,
            RawReply = raw,
            Icon = icon
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

        // Social ack in the main box - short chill reply, no API
        if (GroqParser.IsAcknowledgement(input))
            return CalcResult.Explain("No problem.");

        // "explain" alone - don't hit Groq (it used to invent/prompt-leak an equation)
        if (!hasHistory && GroqParser.IsBarePrompt(input))
            return CalcResult.Explain(
                "What should I explain? Type a topic like **mean**, or paste an equation.");

        // Local-first: pure symbols
        if (!hasHistory &&
            _math.LooksLikeSymbolicMath(input) &&
            _math.TryEvaluate(input, out var local, out var localSec, out var localVal))
            return PresentNumber(input, input, localVal, local, localSec);

        // Local-first: common NL (multiply/typos/simple algebra) - no API needed
        if (!hasHistory &&
            LocalNlMath.TryToExpression(input, out var nlExpr) &&
            _math.TryEvaluate(nlExpr, out var nlAnswer, out var nlSec, out var nlVal))
            return PresentNumber(input, nlExpr, nlVal, nlAnswer, nlSec, AiReply.Calc(nlExpr));

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
                _math.TryEvaluate(fallbackExpr, out var fallbackAnswer, out var fallbackSec, out var fallbackVal))
                return PresentNumber(input, fallbackExpr, fallbackVal, fallbackAnswer, fallbackSec, AiReply.Calc(fallbackExpr));
            throw;
        }

        var result = FromReply(input, reply);

        // Algebra leftover variables or failed eval on equation-looking input
        if (!result.Ok &&
            (GroqParser.LooksLikeEquationSolve(input) ||
             GroqParser.ExpressionHasVariables(reply.Expression)))
        {
            try
            {
                var retry = await _groq.AskSolveNumericAsync(input, ct).ConfigureAwait(false);
                result = FromReply(input, retry);
            }
            catch
            {
                // ignore; try local below
            }

            if (!result.Ok &&
                LocalNlMath.TryToExpression(input, out var algExpr) &&
                _math.TryEvaluate(algExpr, out var algAnswer, out var algSec, out var algVal))
                return PresentNumber(input, algExpr, algVal, algAnswer, algSec, AiReply.Calc(algExpr));
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
                result = FromReply(input, rewrite);
            }
            catch
            {
                // leave as unknown
            }
        }

        // Follow-up still unknown -> hard continue retry (skip acks - those aren't math)
        if (result.Kind == CalcResultKind.Unknown &&
            hasHistory &&
            !GroqParser.IsAcknowledgement(input) &&
            GroqParser.LooksLikeFollowUpContinue(input))
        {
            try
            {
                var retry = await _groq.AskFollowUpAsync(input, history!, ct).ConfigureAwait(false);
                result = FromReply(input, retry);
            }
            catch
            {
                // keep unknown
            }
        }

        return result;
    }

    private CalcResult FromReply(string userInput, AiReply reply)
    {
        switch (reply.Kind)
        {
            case AiReplyKind.Calc:
            {
                var expr = reply.Expression;
                if (LocalNlMath.TryRewriteBadFrequencyMean(expr, out var fixedFreq))
                    expr = fixedFreq;

                if (!string.IsNullOrWhiteSpace(expr) &&
                    _math.TryEvaluate(expr, out var answer, out var secondary, out var value))
                    return PresentNumber(userInput, expr, value, answer, secondary, reply);
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

    private static CalcResult PresentNumber(
        string userInput,
        string expression,
        double value,
        string rawPrimary,
        string? rawSecondary,
        AiReply? raw = null)
    {
        var (display, secondary, icon) =
            ResultPresenter.Present(userInput, expression, value, rawPrimary, rawSecondary);
        return CalcResult.Number(display, expression, raw, secondary, icon);
    }
}
