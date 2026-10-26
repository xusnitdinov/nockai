using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace NockAI.Services;

/// <summary>
/// Tiny markdown subset: **bold**, ==highlight==, `code`, newlines, "- " bullets.
/// </summary>
public static class MarkdownInlines
{
    // Allow '=' inside highlights so ==mean = sum/n== works
    private static readonly Regex Token = new(
        @"(\*\*.+?\*\*|==.+?==|`.+?`)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // Fluent Win11-ish accents
    private static readonly Brush HighlightBg = Brush("#332A7DE1");
    private static readonly Brush HighlightFg = Brush("#FF9CDCFE");
    private static readonly Brush CodeFg = Brush("#FF60CDFF");
    private static readonly Brush BodyFg = Brush("#FFFFFFFF");

    private static SolidColorBrush Brush(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        b.Freeze();
        return b;
    }

    public static void Fill(TextBlock block, string markdown)
    {
        block.Inlines.Clear();
        block.TextWrapping = TextWrapping.Wrap;
        block.Foreground = BodyFg;
        block.FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        block.FontSize = 15;
        block.LineHeight = 22;

        try
        {
            var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var first = true;
            foreach (var raw in lines)
            {
                var line = raw.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    block.Inlines.Add(new LineBreak());
                    continue;
                }

                if (!first)
                    block.Inlines.Add(new LineBreak());
                first = false;

                if (line.StartsWith("- ", StringComparison.Ordinal) ||
                    line.StartsWith("* ", StringComparison.Ordinal))
                    line = "• " + line[2..].TrimStart();

                // numbered list "1. "
                if (line.Length > 2 && char.IsDigit(line[0]) && line.Contains(". "))
                {
                    var dot = line.IndexOf(". ", StringComparison.Ordinal);
                    if (dot > 0 && dot <= 3)
                        line = line[..dot] + "." + line[(dot + 1)..];
                }

                AppendFormatted(block.Inlines, line);
            }

            if (block.Inlines.Count == 0)
                block.Inlines.Add(new Run("???") { Foreground = BodyFg });
        }
        catch
        {
            block.Inlines.Clear();
            block.Inlines.Add(new Run(markdown) { Foreground = BodyFg });
        }
    }

    private static void AppendFormatted(InlineCollection inlines, string text)
    {
        var idx = 0;
        foreach (Match m in Token.Matches(text))
        {
            if (m.Index > idx)
                inlines.Add(new Run(text[idx..m.Index]) { Foreground = BodyFg });

            var token = m.Value;
            if (token.StartsWith("**", StringComparison.Ordinal) && token.EndsWith("**", StringComparison.Ordinal) && token.Length >= 4)
            {
                inlines.Add(new Run(token[2..^2])
                {
                    FontWeight = FontWeights.SemiBold,
                    Foreground = BodyFg
                });
            }
            else if (token.StartsWith("==", StringComparison.Ordinal) && token.EndsWith("==", StringComparison.Ordinal) && token.Length >= 4)
            {
                inlines.Add(new Run(token[2..^2])
                {
                    FontWeight = FontWeights.SemiBold,
                    Foreground = HighlightFg,
                    Background = HighlightBg
                });
            }
            else if (token.StartsWith('`') && token.EndsWith('`') && token.Length >= 2)
            {
                inlines.Add(new Run(token[1..^1])
                {
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    Foreground = CodeFg
                });
            }
            else
            {
                inlines.Add(new Run(token) { Foreground = BodyFg });
            }

            idx = m.Index + m.Length;
        }

        if (idx < text.Length)
            inlines.Add(new Run(text[idx..]) { Foreground = BodyFg });
    }
}
