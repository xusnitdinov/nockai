using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace NockAI.Services;

/// <summary>
/// Tiny markdown subset: **bold**, ==highlight==, `code`, :icons:, newlines, "- " bullets.
/// </summary>
public static class MarkdownInlines
{
    // Icons, bold, highlight, code
    private static readonly Regex Token = new(
        @"(:[a-z]+:|\*\*.+?\*\*|==.+?==|`.+?`)",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Brush HighlightBg = Brush("#332A7DE1");
    private static readonly Brush HighlightFg = Brush("#FF9CDCFE");
    private static readonly Brush CodeFg = Brush("#FF60CDFF");
    private static readonly Brush BodyFg = Brush("#FFFFFFFF");
    private static readonly Brush UpFg = Brush("#FF6CCB5F");
    private static readonly Brush DownFg = Brush("#FFE81123");
    private static readonly Brush AccentFg = Brush("#FF60CDFF");
    private static readonly Brush WarnFg = Brush("#FFFFB900");
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons");
    private static readonly FontFamily BodyFont = new("Segoe UI Variable Text, Segoe UI");
    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");

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
        block.FontFamily = BodyFont;
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
            if (token.Length >= 3 && token.StartsWith(':') && token.EndsWith(':'))
            {
                AppendIcon(inlines, token[1..^1].ToLowerInvariant());
            }
            else if (token.StartsWith("**", StringComparison.Ordinal) && token.EndsWith("**", StringComparison.Ordinal) && token.Length >= 4)
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
                    FontFamily = MonoFont,
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

    private static void AppendIcon(InlineCollection inlines, string name)
    {
        // Segoe Fluent Icons glyphs - shortcodes the model can emit
        var (glyph, brush) = name switch
        {
            "up" or "increase" or "rise" or "growth" => ("\uE74A", UpFg),
            "down" or "decrease" or "drop" or "fall" => ("\uE74B", DownFg),
            "flat" or "same" or "neutral" => ("\uE108", BodyFg),
            "pct" or "percent" or "%" => ("%", AccentFg),
            "ok" or "check" or "yes" => ("\uE73E", UpFg),
            "x" or "no" or "bad" => ("\uE711", DownFg),
            "warn" or "warning" or "tip" => ("\uE7BA", WarnFg),
            "calc" or "math" => ("\uE8EF", AccentFg),
            "sum" => ("\uE94E", AccentFg),
            "star" => ("\uE734", WarnFg),
            "info" => ("\uE946", AccentFg),
            _ => ("", BodyFg)
        };

        if (string.IsNullOrEmpty(glyph))
        {
            inlines.Add(new Run($":{name}:") { Foreground = BodyFg });
            return;
        }

        // % is not a Fluent icon glyph - render as bold accent text
        if (glyph == "%")
        {
            inlines.Add(new Run("%")
            {
                FontWeight = FontWeights.SemiBold,
                Foreground = brush,
                BaselineAlignment = BaselineAlignment.Center
            });
            inlines.Add(new Run(" ") { Foreground = BodyFg });
            return;
        }

        inlines.Add(new Run(glyph)
        {
            FontFamily = IconFont,
            FontSize = 15,
            Foreground = brush,
            BaselineAlignment = BaselineAlignment.Center
        });
        inlines.Add(new Run(" ") { Foreground = BodyFg });
    }
}
