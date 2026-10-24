namespace Rewordly.Core;

/// <summary>
/// Lightweight offline guess of a text's language, used only to pick a text-to-speech voice.
/// Non-Latin scripts are identified by Unicode range; Latin-script languages by common words.
/// </summary>
public static class LanguageGuess
{
    private static readonly (int From, int To, string Culture)[] Scripts =
    {
        (0x0900, 0x097F, "hi-IN"), (0x0980, 0x09FF, "bn-IN"), (0x0A00, 0x0A7F, "pa-IN"), (0x0A80, 0x0AFF, "gu-IN"),
        (0x0B80, 0x0BFF, "ta-IN"), (0x0C00, 0x0C7F, "te-IN"), (0x0C80, 0x0CFF, "kn-IN"), (0x0D00, 0x0D7F, "ml-IN"),
        (0x0600, 0x06FF, "ar-SA"), (0x0590, 0x05FF, "he-IL"), (0x0400, 0x04FF, "ru-RU"), (0x0370, 0x03FF, "el-GR"),
        (0x0E00, 0x0E7F, "th-TH"), (0x3040, 0x30FF, "ja-JP"), (0xAC00, 0xD7AF, "ko-KR"), (0x4E00, 0x9FFF, "zh-CN"),
    };

    private static readonly (string Culture, HashSet<string> Words)[] LatinWords =
    {
        ("en-US", Set("the and is of to in that it you with for this are was have")),
        ("es-ES", Set("el la de que y en los es por una para con las del se")),
        ("fr-FR", Set("le la les de et est un une des pour que dans pas du vous")),
        ("de-DE", Set("der die und das ist nicht ein zu mit ich sie den auch es")),
        ("it-IT", Set("il di che e un per non una sono della gli le si con")),
        ("pt-BR", Set("o de que e do da em um para com não uma os no se")),
        ("nl-NL", Set("de het een en van is dat niet ik je op te met")),
    };

    private static HashSet<string> Set(string words) => new(words.Split(' '), StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns a culture name such as "hi-IN" or "en-US".</summary>
    public static string GuessCulture(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "en-US";
        var counts = new Dictionary<string, int>();
        int letters = 0;
        foreach (var ch in text)
        {
            if (!char.IsLetter(ch)) continue;
            letters++;
            foreach (var (from, to, culture) in Scripts)
                if (ch >= from && ch <= to) { counts[culture] = counts.GetValueOrDefault(culture) + 1; break; }
        }
        if (counts.Count > 0)
        {
            var top = counts.MaxBy(kv => kv.Value);
            // Japanese text mixes kana and kanji: prefer ja when any kana is present.
            if (counts.ContainsKey("ja-JP") && top.Key == "zh-CN") return "ja-JP";
            if (top.Value * 3 >= letters) return top.Key;
        }

        var words = text.Split(new[] { ' ', '\n', '\r', '\t', ',', '.', ';', ':', '!', '?', '"', '(', ')' },
            StringSplitOptions.RemoveEmptyEntries);
        var best = ("en-US", 0);
        foreach (var (culture, set) in LatinWords)
        {
            int score = words.Count(w => set.Contains(w));
            if (score > best.Item2) best = (culture, score);
        }
        return best.Item1;
    }
}
