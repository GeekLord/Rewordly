using System.Text.RegularExpressions;

namespace Rewordly.Core;

public static partial class TextUtil
{
    [GeneratedRegex(@"\b\w+\b")] private static partial Regex WordRx();
    [GeneratedRegex(@"^[ \t]+$", RegexOptions.Multiline)] private static partial Regex BlankLineRx();
    [GeneratedRegex(@"(\r?\n){3,}")] private static partial Regex ManyBreaksRx();
    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex ThinkRx();
    [GeneratedRegex(@"^```[\w-]*\r?\n(?<body>.*)\r?\n```$", RegexOptions.Singleline)] private static partial Regex FenceRx();
    // A first line like "Sure! Here's a revised version of the text in a friendly style:" that small models add.
    [GeneratedRegex(@"^(?:(?:sure|certainly|of course|okay|ok)[,!.]?\s*)?here(?:'s|’s| is| are)\s[^\n:]{0,80}?\b(?:revised|rewritten|corrected|improved|edited|polished|translated|translation|summary|summarized|summarised|explanation|version|text|prompt|rewrite)\b[^\n:]{0,80}:[ \t]*\r?\n", RegexOptions.IgnoreCase)]
    private static partial Regex PreambleRx();

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

    /// <summary>Cleans a model reply: drops reasoning blocks, echoed tags, a wrapping code fence, a "Here's the revised text:" opening line and wrapping quotes.</summary>
    public static string CleanModelOutput(string reply, string? original = null)
    {
        var t = ThinkRx().Replace(reply ?? "", "").Trim();
        if (t.StartsWith("<text>", StringComparison.OrdinalIgnoreCase) && t.EndsWith("</text>", StringComparison.OrdinalIgnoreCase))
            t = t[6..^7].Trim();
        var fence = FenceRx().Match(t);
        if (fence.Success && (original == null || !original.TrimStart().StartsWith("```")))
            t = fence.Groups["body"].Value.Trim();
        var pre = PreambleRx().Match(t);
        if (pre.Success && pre.Length < t.Length && (original == null || !PreambleRx().IsMatch(original.TrimStart() + "\n")))
            t = t[pre.Length..].Trim();
        return NormalizeBlankLines(StripWrappingQuotes(t, original));
    }

    /// <summary>Removes one pair of quotes around the whole reply, unless the original was quoted or the quotes belong to the text.</summary>
    private static string StripWrappingQuotes(string t, string? original)
    {
        if (t.Length < 2) return t;
        var o = original?.TrimStart() ?? "";
        foreach (var (open, close) in new[] { ('"', '"'), ('“', '”') })
        {
            if (t[0] != open || t[^1] != close || (o.Length > 0 && o[0] == open)) continue;
            var inner = t[1..^1];
            // More quotes of the same kind inside: the outer ones may be part of the text, so leave it.
            return inner.IndexOf(open) >= 0 || inner.IndexOf(close) >= 0 ? t : inner.Trim();
        }
        return t;
    }
}
