namespace NockAI.Services;

public sealed class AppSettings
{
    /// <summary>If true, evaluate after typing pause. If false, only Enter sends.</summary>
    public bool AutoSend { get; set; } = true;

    /// <summary>Debounce delay when AutoSend is on (ms).</summary>
    public int AutoSendDelayMs { get; set; } = 340;

    /// <summary>Hide overlay when it loses focus.</summary>
    public bool HideWhenUnfocused { get; set; } = true;

    /// <summary>Show Thinking... + progress bar for slow answers.</summary>
    public bool ShowThinkingAnimation { get; set; } = true;

    /// <summary>Prefer SAT-style fractions (2/5) for plain numbers when nice.</summary>
    public bool PreferNiceFractions { get; set; } = true;

    /// <summary>Chance/probability asks show as % (40%) instead of bare 2/5.</summary>
    public bool ProbabilityAsPercent { get; set; } = true;

    /// <summary>Keep overlay above other windows.</summary>
    public bool StayOnTop { get; set; } = true;

    /// <summary>Select all input text when opening with Ctrl+Space.</summary>
    public bool SelectAllOnOpen { get; set; } = true;

    /// <summary>Ctrl+C copies the answer when nothing is selected in a text box.</summary>
    public bool CopyAnswerWithCtrlC { get; set; } = true;

    // ===== AI =====

    /// <summary>Chill | Tutor | Formal | Sassy</summary>
    public string AiTone { get; set; } = "Chill";

    /// <summary>Extra profile/style text appended to the system prompt every request.</summary>
    public string AiProfilePrompt { get; set; } = "";

    /// <summary>Keep follow-up chat history and send it to the model.</summary>
    public bool EnableChatHistory { get; set; } = true;

    /// <summary>How many recent chat messages to keep/send (user+assistant pairs count as 2).</summary>
    public int HistoryMessageCount { get; set; } = 8;

    /// <summary>Include baked few-shot examples in the prompt (helps accuracy, uses tokens).</summary>
    public bool UseFewShotExamples { get; set; } = true;

    /// <summary>0 = deterministic, higher = more creative (usually keep low for math).</summary>
    public double AiTemperature { get; set; } = 0;

    /// <summary>Max tokens for model replies.</summary>
    public int AiMaxTokens { get; set; } = 550;

    /// <summary>Soft cap for explain markdown length (words), injected into prompt.</summary>
    public int ExplainMaxWords { get; set; } = 80;

    /// <summary>When true, nudge model toward explain for how/why questions.</summary>
    public bool PreferDetailedExplain { get; set; } = false;

    public AppSettings Clone() => new()
    {
        AutoSend = AutoSend,
        AutoSendDelayMs = AutoSendDelayMs,
        HideWhenUnfocused = HideWhenUnfocused,
        ShowThinkingAnimation = ShowThinkingAnimation,
        PreferNiceFractions = PreferNiceFractions,
        ProbabilityAsPercent = ProbabilityAsPercent,
        StayOnTop = StayOnTop,
        SelectAllOnOpen = SelectAllOnOpen,
        CopyAnswerWithCtrlC = CopyAnswerWithCtrlC,
        AiTone = AiTone,
        AiProfilePrompt = AiProfilePrompt,
        EnableChatHistory = EnableChatHistory,
        HistoryMessageCount = HistoryMessageCount,
        UseFewShotExamples = UseFewShotExamples,
        AiTemperature = AiTemperature,
        AiMaxTokens = AiMaxTokens,
        ExplainMaxWords = ExplainMaxWords,
        PreferDetailedExplain = PreferDetailedExplain
    };

    public void Clamp()
    {
        if (AutoSendDelayMs < 100) AutoSendDelayMs = 100;
        if (AutoSendDelayMs > 2000) AutoSendDelayMs = 2000;
        if (HistoryMessageCount < 2) HistoryMessageCount = 2;
        if (HistoryMessageCount > 24) HistoryMessageCount = 24;
        if (AiTemperature < 0) AiTemperature = 0;
        if (AiTemperature > 1.2) AiTemperature = 1.2;
        if (AiMaxTokens < 100) AiMaxTokens = 100;
        if (AiMaxTokens > 2000) AiMaxTokens = 2000;
        if (ExplainMaxWords < 40) ExplainMaxWords = 40;
        if (ExplainMaxWords > 220) ExplainMaxWords = 220;
        AiTone = string.IsNullOrWhiteSpace(AiTone) ? "Chill" : AiTone.Trim();
        AiProfilePrompt ??= "";
        if (AiProfilePrompt.Length > 4000)
            AiProfilePrompt = AiProfilePrompt[..4000];
    }
}
