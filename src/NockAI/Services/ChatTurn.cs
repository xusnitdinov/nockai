namespace NockAI.Services;

public sealed class ChatTurn
{
    public required string Role { get; init; } // "user" | "assistant"
    public required string Content { get; init; }
}
