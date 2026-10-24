namespace Rewordly.Core;

public enum ActionKind { Style, Improve, Fix, Summary, Explain, Translate, Chat, Listen }

/// <summary>One thing the user can ask for from the menu.</summary>
public sealed record ActionSpec(ActionKind Kind, string Argument = "")
{
    public string Title => Kind switch
    {
        ActionKind.Style => Argument,
        ActionKind.Improve => "Correct and improve",
        ActionKind.Fix => "Fix errors only",
        ActionKind.Summary => $"Summary ({Argument}%)",
        ActionKind.Explain => $"Explain ({Catalog.LevelLabel(Argument)})",
        ActionKind.Translate => $"Translate into {Argument}",
        ActionKind.Chat => "Chat",
        ActionKind.Listen => "Listen",
        _ => Kind.ToString(),
    };
}

public static class Catalog
{
    public static readonly string[] MainStyles =
    {
        "Informal", "Friendly", "Simple", "Formal", "Professional", "Argumentative",
        "Persuasive", "Creative", "Technical", "Academic", "Journalistic", "Humorous",
    };

    public static readonly string[] OtherStyles = { "Narrative", "Descriptive", "Expository", "Poetic", "Satirical" };

    public static IEnumerable<string> AllStyles => MainStyles.Concat(OtherStyles);

    public static readonly IReadOnlyDictionary<string, string> HindiStyles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Informal"] = "अनौपचारिक (Informal)",
            ["Friendly"] = "मैत्रीपूर्ण (Friendly)",
            ["Simple"] = "सरल (Simple)",
            ["Formal"] = "औपचारिक (Formal)",
            ["Professional"] = "व्यावसायिक (Professional)",
            ["Argumentative"] = "तर्कपूर्ण (Argumentative)",
            ["Persuasive"] = "प्रेरक (Persuasive)",
            ["Creative"] = "रचनात्मक (Creative)",
            ["Technical"] = "तकनीकी (Technical)",
            ["Academic"] = "अकादमिक (Academic)",
            ["Journalistic"] = "पत्रकारिता (Journalistic)",
            ["Humorous"] = "हास्यपूर्ण (Humorous)",
            ["Narrative"] = "कथात्मक (Narrative)",
            ["Descriptive"] = "वर्णनात्मक (Descriptive)",
            ["Expository"] = "व्याख्यात्मक (Expository)",
            ["Poetic"] = "काव्यात्मक (Poetic)",
            ["Satirical"] = "व्यंग्यात्मक (Satirical)",
        };

    public static readonly int[] SummaryPercents = { 70, 50, 30 };

    public static readonly (string Id, string Label, string Description)[] Levels =
    {
        ("none", "None", "no prior knowledge of the subject; use plain everyday words, short sentences and simple examples"),
        ("basic", "Basic", "knows the fundamental ideas; briefly define any term that is not everyday language"),
        ("intermediate", "Intermediate", "comfortable with the basics; standard terminology is fine"),
        ("advanced", "Advanced", "deep familiarity with the subject; technical language and nuance are welcome"),
    };

    public static readonly IReadOnlyDictionary<string, (string Label, string Description)> HindiLevels =
        new Dictionary<string, (string Label, string Description)>(StringComparer.OrdinalIgnoreCase)
        {
            ["none"] = ("कोई पूर्व ज्ञान नहीं (None)", "विषय का कोई पूर्व ज्ञान नहीं; सरल दैनिक शब्दों, छोटे वाक्यों और सरल उदाहरणों का उपयोग करें"),
            ["basic"] = ("बुनियादी (Basic)", "मूलभूत विचारों को जानता है; गैर-दैनिक भाषा वाले किसी भी शब्द को संक्षेप में परिभाषित करें"),
            ["intermediate"] = ("मध्यम (Intermediate)", "बुनियादी बातों से परिचित; मानक शब्दावली ठीक है"),
            ["advanced"] = ("उन्नत (Advanced)", "विषय से गहरा परिचय; तकनीकी भाषा और सूक्ष्मताओं का स्वागत है"),
        };

    public static string StyleLabel(string style, string? lang = null)
    {
        if (lang?.Equals("hi", StringComparison.OrdinalIgnoreCase) == true && HindiStyles.TryGetValue(style, out var hi))
            return hi;
        return style;
    }

    public static string LevelLabel(string id, string? lang = null)
    {
        if (lang?.Equals("hi", StringComparison.OrdinalIgnoreCase) == true && HindiLevels.TryGetValue(id, out var hi))
            return hi.Label;
        return Levels.FirstOrDefault(l => l.Id == id).Label ?? id;
    }

    public static string LevelDescription(string id, string? lang = null)
    {
        if (lang?.Equals("hi", StringComparison.OrdinalIgnoreCase) == true && HindiLevels.TryGetValue(id, out var hi))
            return hi.Description;
        return Levels.FirstOrDefault(l => l.Id == id).Description ?? id;
    }

    public static readonly string[] MainLanguages =
    {
        "Arabic", "Bengali", "Chinese", "English", "French", "German", "Hindi", "Italian", "Japanese",
        "Korean", "Persian", "Polish", "Portuguese", "Russian", "Spanish", "Tagalog", "Tamil",
        "Telugu", "Turkish", "Ukrainian", "Urdu", "Vietnamese",
    };

    public static readonly string[] OtherLanguages = { "Gujarati", "Kannada", "Marathi", "Punjabi", "Swahili" };

    public static IEnumerable<string> AllLanguages => MainLanguages.Concat(OtherLanguages);
}
