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

    public static readonly int[] SummaryPercents = { 70, 50, 30 };

    public static readonly (string Id, string Label, string Description)[] Levels =
    {
        ("none", "None", "no prior knowledge of the subject; use plain everyday words, short sentences and simple examples"),
        ("basic", "Basic", "knows the fundamental ideas; briefly define any term that is not everyday language"),
        ("intermediate", "Intermediate", "comfortable with the basics; standard terminology is fine"),
        ("advanced", "Advanced", "deep familiarity with the subject; technical language and nuance are welcome"),
    };

    public static string LevelLabel(string id) =>
        Levels.FirstOrDefault(l => l.Id == id).Label ?? id;

    public static string LevelDescription(string id) =>
        Levels.FirstOrDefault(l => l.Id == id).Description ?? id;

    public static readonly string[] MainLanguages =
    {
        "Arabic", "Bengali", "Chinese", "English", "French", "German", "Hindi", "Italian", "Japanese",
        "Korean", "Persian", "Polish", "Portuguese", "Russian", "Spanish", "Tagalog", "Tamil",
        "Telugu", "Turkish", "Ukrainian", "Urdu", "Vietnamese",
    };

    public static readonly string[] OtherLanguages = { "Gujarati", "Kannada", "Marathi", "Punjabi", "Swahili" };

    public static IEnumerable<string> AllLanguages => MainLanguages.Concat(OtherLanguages);
}
