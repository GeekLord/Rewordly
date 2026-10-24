using System.Text.RegularExpressions;

namespace Rewordly.Core;

public static partial class TextUtil
{
    [GeneratedRegex(@"\b\w+\b")] private static partial Regex WordRx();
    [GeneratedRegex(@"^[ \t]+$", RegexOptions.Multiline)] private static partial Regex BlankLineRx();
    [GeneratedRegex(@"(\r?\n){3,}")] private static partial Regex ManyBreaksRx();
    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex ThinkRx();
    [GeneratedRegex(@"^```[\w-]*\r?\n(?<body>.*)\r?\n```$", RegexOptions.Singleline)] private static partial Regex FenceRx();

    public static int CountWords(string? text) => string.IsNullOrEmpty(text) ? 0 : WordRx().Matches(text).Count;

    /// <summary>Word budget for a summary: percent of the original, at least 5 words.</summary>
    public static int SummaryWordLimit(string text, int percent) => Math.Max(5, CountWords(text) * percent / 100);

    /// <summary>Removes whitespace-only lines' padding and collapses 3+ line breaks into one blank line.</summary>
    public static string NormalizeBlankLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var t = text.Replace("\r\n", "\n");
        t = BlankLineRx().Replace(t, "");
        t = ManyBreaksRx().Replace(t, "\n\n");
        return t.Replace("\n", Environment.NewLine);
    }

    /// <summary>Cleans a model reply: drops reasoning blocks, echoed tags and a wrapping code fence.</summary>
    public static string CleanModelOutput(string reply, string? original = null)
    {
        var t = ThinkRx().Replace(reply ?? "", "").Trim();
        if (t.StartsWith("<text>", StringComparison.OrdinalIgnoreCase) && t.EndsWith("</text>", StringComparison.OrdinalIgnoreCase))
            t = t[6..^7].Trim();
        var fence = FenceRx().Match(t);
        if (fence.Success && (original == null || !original.TrimStart().StartsWith("```")))
            t = fence.Groups["body"].Value.Trim();
        return NormalizeBlankLines(t);
    }
}
