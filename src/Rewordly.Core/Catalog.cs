namespace Rewordly.Core;

public enum ActionKind { Style, Improve, Fix, Summary, Explain, Translate, Chat, Listen, AgentPrompt }

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
        ActionKind.AgentPrompt => $"AI agent prompt ({Prompts.AgentLabel(Argument)})",
        _ => Kind.ToString(),
    };

    public string LocalizedTitle(string? lang = null)
    {
        if (lang?.Equals("hi", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Kind switch
            {
                ActionKind.Style => Catalog.StyleLabel(Argument, "hi"),
                ActionKind.Improve => "सुधारें और बेहतर बनाएं (Improve)",
                ActionKind.Fix => "सिर्फ गलतियाँ ठीक करें (Fix errors)",
                ActionKind.Summary => $"समरी ({Argument}%)",
                ActionKind.Explain => $"समझाएं ({Catalog.LevelLabel(Argument, "hi")})",
                ActionKind.Translate => $"{Argument} में अनुवाद (Translate)",
                ActionKind.Chat => "बातचीत (Chat)",
                ActionKind.Listen => "बोलकर सुनाएं (Listen)",
                ActionKind.AgentPrompt => $"AI एजेंट प्रॉम्प्ट ({Prompts.AgentLabel(Argument)})",
                _ => Kind.ToString(),
            };
        }

        return Title;
    }
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
            ["Informal"] = "कैजुअल / अनौपचारिक (Informal)",
            ["Friendly"] = "दोस्ताना (Friendly)",
            ["Simple"] = "आसान / सरल (Simple)",
            ["Formal"] = "फॉर्मल / औपचारिक (Formal)",
            ["Professional"] = "कामकाजी / प्रोफेशनल (Professional)",
            ["Argumentative"] = "दलीलों वाला (Argumentative)",
            ["Persuasive"] = "असरदार / मनाने वाला (Persuasive)",
            ["Creative"] = "क्रिएटिव / अनोखा (Creative)",
            ["Technical"] = "टेक्निकल / तकनीकी (Technical)",
            ["Academic"] = "पढ़ाई-लिखाई जैसा (Academic)",
            ["Journalistic"] = "अख़बार जैसा (Journalistic)",
            ["Humorous"] = "मज़ाकिया (Humorous)",
            ["Narrative"] = "कहानी की तरह (Narrative)",
            ["Descriptive"] = "विस्तार से बताने वाला (Descriptive)",
            ["Expository"] = "खोलकर समझाने वाला (Expository)",
            ["Poetic"] = "शायराना / काव्यात्मक (Poetic)",
            ["Satirical"] = "तंजिया / मज़ाकिया कटाक्ष (Satirical)",
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
            ["none"] = ("शुरुआती (None)", "विषय की कोई पहले से जानकारी नहीं है; आम बोलचाल के आसान शब्दों, छोटे वाक्यों और सीधे उदाहरणों से समझाएं"),
            ["basic"] = ("बेसिक (Basic)", "बुनियादी बातें मालूम हैं; जो शब्द आम बोलचाल के न हों, उनका आसान मतलब संक्षेप में बता दें"),
            ["intermediate"] = ("सामान्य (Intermediate)", "ज़रूरी बातें अच्छी तरह पता हैं; सामान्य शब्दों में बात रख सकते हैं"),
            ["advanced"] = ("गहरा जानकार (Advanced)", "विषय की बहुत अच्छी समझ है; तकनीकी शब्दों और बारीकियों का खुलकर इस्तेमाल करें"),
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
