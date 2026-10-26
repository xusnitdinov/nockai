namespace QuickCalcAI.Services;

public enum AiReplyKind
{
    Calc,
    Explain,
    Unknown
}

public sealed class AiReply
{
    public AiReplyKind Kind { get; init; } = AiReplyKind.Unknown;
    public string? Expression { get; init; }
    public string? Markdown { get; init; }

    public static AiReply Unknown() => new() { Kind = AiReplyKind.Unknown };
    public static AiReply Calc(string expression) => new() { Kind = AiReplyKind.Calc, Expression = expression };
    public static AiReply Explain(string markdown) => new() { Kind = AiReplyKind.Explain, Markdown = markdown };
}
