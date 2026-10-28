using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NockAI.Services;

/// <summary>
/// Routes natural language to calc expressions or math explanations (JSON).
/// </summary>
public sealed class GroqParser : IDisposable
{
    private const string SystemPrompt =
        """
        You are NockAI - a MATH assistant in a calculator overlay.

        Reply with ONE JSON object only (no markdown fence, no extra text):

        1) Compute / solve a value:
        {"kind":"calc","expression":"<engine expression>"}

        2) Explain / teach / examples / clarify:
        {"kind":"explain","markdown":"<short clear explanation>"}

        3) Truly not math (weather, jokes, coding, life advice with zero math):
        {"kind":"unknown"}

        ========================
        DECISION RULES
        ========================
        - Math topics: arithmetic, algebra, geometry, stats, percentages, formulas, how-to-math.
        - Ignore slang: yo, pls, please, bro, hey, can you, help me, thanks.
        - Fix typos: multiple→multiply, devided→divided, avrage→average, ect.
        - how/what/why/explain/define a math idea -> explain (even with slang).
        - numbers to compute, or solve for n/x -> calc.
        - Prefer explain over unknown whenever the topic is math-related.
        - Prefer calc over explain when they want a number.
        - Bare "explain"/"how"/"why"/"steps"/"help" with NO math topic and NO prior messages
          -> {"kind":"unknown"}. Do NOT invent a problem. Do NOT reuse examples from this prompt.

        FOLLOW-UPS (very important):
        - Prior messages are the SAME math conversation.
        - Short replies like "more examples", "more examples pls", "simpler", "another one",
          "what about median", "show steps", "eli5", "again", "ok and mode?" CONTINUE that topic.
        - With history, almost NEVER return unknown. Continue with kind=explain (or calc if they give numbers).
        - Build on the previous assistant answer; do not pretend you forgot it.
        - If they ask for more examples, give 2-3 NEW worked numeric examples in markdown.

        CALC expression syntax:
        - + - * / ^ () sqrt abs sin cos tan log ln floor ceil round
        - mean median mode sum min max stdev variance range
        - nCr(n,k) / choose, nPr(n,k), fact(n), gcd(a,b), lcm(a,b)
        - Number words -> digits. Percent: (N/100)*X
        - Frequency / repeated scores:
          "6 85s, 90 5s" -> (6*85+90*5)/(6+90)
          "6 students scored 85 and 4 scored 95" -> (6*85+4*95)/(6+4)
          NEVER write mean(6*85,4*95) - that is wrong.
        - Digits and operators ONLY. NO variable letters (n,x,y). NO equals sign.
        - "multiple/multiply/times A by B" -> A*B
        - "divide A by B" -> A/B
        - "10 choose 3" -> nCr(10,3) ; "5!" -> fact(5)

        ALGEBRA / SOLVE:
        - Rearrange to a numeric expression for the unknown.
        - 12n=144 -> {"kind":"calc","expression":"144/12"}
        - 2x+4=10 -> {"kind":"calc","expression":"(10-4)/2"}
        - Never copy these examples into an explain reply unless the user typed that same problem.

        EXPLAIN markdown (under ~140 words):
        - **bold** key terms
        - ==highlight== core formula (equals inside highlight OK)
        - `code` for short formulas
        - "- " bullets OK
        - No links, no # headings
        """;

    private static readonly string[] FallbackModels =
    [
        "openai/gpt-oss-20b",
        "openai/gpt-oss-120b"
    ];

    private static readonly Regex JsonObject = new(@"\{[\s\S]*\}", RegexOptions.Compiled);
    private static readonly Regex SlangPrefix = new(
        @"^(?:yo+|hey|hi|hello|bro|dude|pls|plz|please|can you|could you|would you|help me|ok|okay|sup)[\s,.:!]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly string _model;

    public GroqParser(string apiKey, string model)
    {
        _model = string.IsNullOrWhiteSpace(model) ? "qwen/qwen3.8-27b" : model;
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.groq.com/openai/v1/"),
            Timeout = TimeSpan.FromSeconds(25)
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<AiReply> InterpretAsync(
        string naturalLanguage,
        IReadOnlyList<ChatTurn>? history,
        CancellationToken ct)
    {
        var cleaned = NormalizeUserText(naturalLanguage);
        var hasHistory = history is { Count: > 0 };

        // Bare "explain" with no topic + no history: model used to parrot prompt examples
        if (!hasHistory && IsBarePrompt(cleaned))
            return AiReply.Unknown();

        var reply = await AskAsync(cleaned, history, ct).ConfigureAwait(false);

        // Empty / weird API body - one soft retry
        if (reply.Kind == AiReplyKind.Unknown)
            reply = await AskAsync(cleaned, history, ct).ConfigureAwait(false);

        if (hasHistory)
        {
            if (reply.Kind == AiReplyKind.Unknown)
                reply = await AskFollowUpAsync(cleaned, history!, ct).ConfigureAwait(false);
        }
        else
        {
            if (reply.Kind == AiReplyKind.Unknown &&
                !string.Equals(cleaned, naturalLanguage.Trim(), StringComparison.Ordinal) &&
                LooksLikeMathQuestion(cleaned))
            {
                reply = await AskAsync(cleaned, history, ct).ConfigureAwait(false);
            }

            if (reply.Kind == AiReplyKind.Unknown &&
                LooksLikeMathQuestion(cleaned) &&
                !LooksLikeEquationSolve(cleaned))
            {
                reply = await AskAsync(
                    "Explain this math topic clearly with a formula and one example: " + cleaned,
                    history,
                    ct).ConfigureAwait(false);
            }
        }

        return reply;
    }

    public Task<AiReply> AskSolveNumericAsync(string naturalLanguage, CancellationToken ct)
    {
        var prompt =
            "Solve for the unknown. Reply JSON calc only. " +
            "expression MUST be digits and + - * / ^ () only - no letters like n or x. " +
            "Problem: " + naturalLanguage.Trim();
        return AskAsync(prompt, history: null, ct);
    }

    public Task<AiReply> AskFollowUpAsync(
        string followUp,
        IReadOnlyList<ChatTurn> history,
        CancellationToken ct)
    {
        var prompt =
            "CONTINUE the previous math conversation. " +
            "The user follow-up is short but STILL about that math topic. " +
            "Reply kind=explain with helpful markdown (examples/steps). " +
            "Do NOT return unknown. Follow-up: " + followUp.Trim();
        return AskAsync(prompt, history, ct);
    }

    public static bool ExpressionHasVariables(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var lower = expression.ToLowerInvariant();
        // Longer names first so "ncr" wins over stray letters inside longer tokens
        string[] fns =
        [
            "factorial", "variance", "average", "median", "stdev", "choose",
            "sqrt", "abs", "sin", "cos", "tan", "log", "ln", "mean", "mode",
            "sum", "min", "max", "std", "var", "floor", "ceil", "round",
            "ncr", "npr", "fact", "gcd", "lcm", "range", "pi", "e"
        ];
        var scrubbed = lower;
        foreach (var f in fns)
            scrubbed = scrubbed.Replace(f, "", StringComparison.Ordinal);

        return scrubbed.Any(char.IsLetter);
    }

    public static bool LooksLikeEquationSolve(string input)
    {
        var lower = input.ToLowerInvariant();
        if (lower.Contains("equals", StringComparison.Ordinal) ||
            lower.Contains('=') ||
            lower.Contains("solve", StringComparison.Ordinal))
            return true;

        if (Regex.IsMatch(lower, @"\b(whats?|find|value of)\b.*\b[a-z]\b") ||
            Regex.IsMatch(lower, @"\d+[a-z]\b") ||
            Regex.IsMatch(lower, @"\b[a-z]\s*\="))
            return true;

        return false;
    }

    public static bool LooksLikeFollowUpContinue(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        string[] cues =
        [
            "more", "example", "examples", "another", "again", "simpler", "eli5",
            "steps", "explain", "why", "how", "what about", "and", "also", "ok",
            "same", "show", "detail", "details", "continue", "go on", "pls", "please"
        ];
        return cues.Any(c => lower.Contains(c, StringComparison.Ordinal)) || lower.Length <= 40;
    }

    /// <summary>
    /// "explain" / "how" / "why" with nothing to explain - not a real math ask.
    /// </summary>
    public static bool IsBarePrompt(string input)
    {
        var lower = Regex.Replace(input.Trim().ToLowerInvariant(), @"[.!?]+$", "").Trim();
        if (lower.Length == 0)
            return true;

        // strip leftover slang the normalizer may have left
        lower = SlangPrefix.Replace(lower, "").Trim();
        lower = Regex.Replace(lower, @"[.!?]+$", "").Trim();

        string[] bare =
        [
            "explain", "explain this", "explain that", "explain please", "explain pls",
            "how", "why", "what", "help", "help me", "steps", "show steps", "show",
            "teach", "teach me", "solve", "calculate", "compute", "math", "idk"
        ];
        if (bare.Any(b => lower.Equals(b, StringComparison.Ordinal)))
            return true;

        // "explain please bro" etc. still no topic
        if (Regex.IsMatch(lower, @"^(explain|how|why|what|help|teach|solve)(\s+(me|this|that|pls|plz|please|bro|dude))?$"))
            return true;

        return false;
    }

    private async Task<AiReply> AskAsync(
        string userText,
        IReadOnlyList<ChatTurn>? history,
        CancellationToken ct)
    {
        var messages = BuildMessages(userText, history);
        Exception? lastError = null;

        foreach (var model in ModelCandidates())
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    return await SendAsync(model, messages, ct).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex) when (IsCapacityOrTransient(ex))
                {
                    lastError = ex;
                    await Task.Delay(300 * (attempt + 1), ct).ConfigureAwait(false);
                }
            }
        }

        throw lastError ?? new InvalidOperationException("Groq request failed.");
    }

    private IEnumerable<string> ModelCandidates()
    {
        yield return _model;
        foreach (var m in FallbackModels)
        {
            if (!string.Equals(m, _model, StringComparison.OrdinalIgnoreCase))
                yield return m;
        }
    }

    private static bool IsCapacityOrTransient(InvalidOperationException ex)
    {
        var msg = ex.Message;
        return msg.Contains("over capacity", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("429", StringComparison.Ordinal)
               || msg.Contains("503", StringComparison.Ordinal)
               || msg.Contains("502", StringComparison.Ordinal)
               || msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }

    private List<object> BuildMessages(string userText, IReadOnlyList<ChatTurn>? history)
    {
        var messages = new List<object>
        {
            new { role = "system", content = SystemPrompt }
        };

        if (history is not { Count: > 0 })
        {
            messages.Add(new { role = "user", content = "multiple 264 by 1446" });
            messages.Add(new { role = "assistant", content = "{\"kind\":\"calc\",\"expression\":\"264*1446\"}" });
            messages.Add(new { role = "user", content = "12n=144 find the n" });
            messages.Add(new { role = "assistant", content = "{\"kind\":\"calc\",\"expression\":\"144/12\"}" });
            messages.Add(new { role = "user", content = "yo pls explain how do i find mean ?" });
            messages.Add(new
            {
                role = "assistant",
                content =
                    "{\"kind\":\"explain\",\"markdown\":\"The **mean** (average) is the sum of values divided by how many there are.\\n\\n==mean = (sum of values) / (count)==\\n\\n- Add every number\\n- Divide by the count\\n\\nExample: `2, 4, 6` → `(2+4+6)/3 = 4`.\"}"
            });
            messages.Add(new { role = "user", content = "more examples pls" });
            messages.Add(new
            {
                role = "assistant",
                content =
                    "{\"kind\":\"explain\",\"markdown\":\"More **mean** examples:\\n\\n- `10, 20` → ==(10+20)/2 = 15==\\n- `5, 5, 5, 9` → ==(5+5+5+9)/4 = 6==\\n- `2, 4, 6, 8, 10` → ==(2+4+6+8+10)/5 = 6==\"}"
            });
            messages.Add(new { role = "user", content = "mean of 10 and 20" });
            messages.Add(new { role = "assistant", content = "{\"kind\":\"calc\",\"expression\":\"mean(10,20)\"}" });
            messages.Add(new { role = "user", content = "mean if 6 students scored 85 and 4 scored 95" });
            messages.Add(new
            {
                role = "assistant",
                content = "{\"kind\":\"calc\",\"expression\":\"(6*85+4*95)/(6+4)\"}"
            });
            messages.Add(new { role = "user", content = "mode of 2, 3, 3, 4, 5, 5, 5, 6" });
            messages.Add(new { role = "assistant", content = "{\"kind\":\"calc\",\"expression\":\"mode(2,3,3,4,5,5,5,6)\"}" });
            messages.Add(new
            {
                role = "user",
                content = "19n equals to 6*29 plus 7 multiplied by (n-6), whats the n"
            });
            messages.Add(new
            {
                role = "assistant",
                content = "{\"kind\":\"calc\",\"expression\":\"(6*29-7*6)/(19-7)\"}"
            });
            messages.Add(new { role = "user", content = "whats the weather" });
            messages.Add(new { role = "assistant", content = "{\"kind\":\"unknown\"}" });
        }
        else
        {
            messages.Add(new { role = "user", content = "how do i find average" });
            messages.Add(new
            {
                role = "assistant",
                content =
                    "{\"kind\":\"explain\",\"markdown\":\"The **average** (mean) is sum divided by count.\\n\\n==average = sum / count==\"}"
            });
            messages.Add(new { role = "user", content = "more examples pls" });
            messages.Add(new
            {
                role = "assistant",
                content =
                    "{\"kind\":\"explain\",\"markdown\":\"Examples:\\n\\n- `3, 6, 9` → ==(3+6+9)/3 = 6==\\n- `1, 1, 7` → ==(1+1+7)/3 = 3==\"}"
            });

            foreach (var turn in history.TakeLast(8))
            {
                var role = turn.Role is "assistant" or "user" ? turn.Role : "user";
                messages.Add(new { role, content = turn.Content });
            }
        }

        messages.Add(new { role = "user", content = userText });
        return messages;
    }

    private async Task<AiReply> SendAsync(string model, List<object> messages, CancellationToken ct)
    {
        var payload = new
        {
            model,
            temperature = 0,
            max_tokens = 550,
            response_format = new { type = "json_object" },
            messages
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Groq error {(int)res.StatusCode}: {Trim(body, 240)}");

        using var doc = JsonDocument.Parse(body);
        var message = doc.RootElement.GetProperty("choices")[0].GetProperty("message");

        // gpt-oss sometimes puts answer in reasoning / refuses content
        var content = message.TryGetProperty("content", out var c) ? c.GetString() : null;
        if (string.IsNullOrWhiteSpace(content) &&
            message.TryGetProperty("reasoning", out var reasoning))
            content = ExtractJsonFromReasoning(reasoning.GetString());

        return ParseReply(content);
    }

    private static string? ExtractJsonFromReasoning(string? reasoning)
    {
        if (string.IsNullOrWhiteSpace(reasoning))
            return null;
        var m = JsonObject.Match(reasoning);
        return m.Success ? m.Value : null;
    }

    public static string ToAssistantJson(AiReply reply) =>
        reply.Kind switch
        {
            AiReplyKind.Calc => JsonSerializer.Serialize(new { kind = "calc", expression = reply.Expression ?? "" }),
            AiReplyKind.Explain => JsonSerializer.Serialize(new { kind = "explain", markdown = reply.Markdown ?? "" }),
            _ => "{\"kind\":\"unknown\"}"
        };

    private static string NormalizeUserText(string input)
    {
        var t = input.Trim();
        for (var i = 0; i < 4; i++)
        {
            var next = SlangPrefix.Replace(t, "");
            if (next == t) break;
            t = next.Trim();
        }

        t = Regex.Replace(t, @"\b(pls|plz|please)\b[.!?]*$", "", RegexOptions.IgnoreCase).Trim();

        // light typo fixes before the model sees it
        t = Regex.Replace(t, @"\bmultiple\b", "multiply", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\bdevided\b", "divided", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\bavrage\b", "average", RegexOptions.IgnoreCase);

        return string.IsNullOrWhiteSpace(t) ? input.Trim() : t;
    }

    private static bool LooksLikeMathQuestion(string input)
    {
        var lower = input.ToLowerInvariant();
        string[] hooks =
        [
            "mean", "median", "mode", "average", "avg", "percent", "fraction", "algebra",
            "geometry", "equation", "formula", "derivative", "integral", "matrix",
            "probability", "std", "variance", "calculate", "compute", "math",
            "multiply", "multiple", "divide", "subtract", "addition", "square root", "factorial",
            "example", "examples", "simplify", "expand", "factor", "times", "plus", "minus"
        ];
        string[] asks = ["how", "what", "why", "explain", "define", "difference", "formula", "show", "more"];
        return hooks.Any(h => lower.Contains(h, StringComparison.Ordinal)) &&
               (asks.Any(a => lower.Contains(a, StringComparison.Ordinal)) || lower.Contains('?'));
    }

    private static AiReply ParseReply(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return AiReply.Unknown();

        var text = content.Trim();
        var m = JsonObject.Match(text);
        if (!m.Success)
        {
            if (text.Contains("**", StringComparison.Ordinal) || text.Contains("==", StringComparison.Ordinal))
                return AiReply.Explain(text);
            return AiReply.Unknown();
        }

        try
        {
            using var json = JsonDocument.Parse(m.Value);
            var root = json.RootElement;
            var kind = root.TryGetProperty("kind", out var k) ? k.GetString()?.Trim().ToLowerInvariant() : null;

            switch (kind)
            {
                case "calc":
                {
                    var expr = root.TryGetProperty("expression", out var e) ? e.GetString()?.Trim() : null;
                    expr = CleanExpression(expr);
                    return string.IsNullOrWhiteSpace(expr) ? AiReply.Unknown() : AiReply.Calc(expr);
                }
                case "explain":
                {
                    var md = root.TryGetProperty("markdown", out var mdEl) ? mdEl.GetString()?.Trim() : null;
                    if (string.IsNullOrWhiteSpace(md))
                        return AiReply.Unknown();
                    return AiReply.Explain(md);
                }
                default:
                    return AiReply.Unknown();
            }
        }
        catch
        {
            return AiReply.Unknown();
        }
    }

    private static string? CleanExpression(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var text = content.Trim().Trim('"', '\'', '`', '.');
        if (text.StartsWith('='))
            text = text[1..].Trim();

        if (text is "???" or "?" ||
            text.Equals("UNPARSABLE", StringComparison.OrdinalIgnoreCase))
            return null;

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string Trim(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    public void Dispose() => _http.Dispose();
}
