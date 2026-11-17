using System.Text.RegularExpressions;

namespace Rewordly.Core;

/// <summary>A system + user prompt pair ready to send.</summary>
public sealed record PromptPair(string System, string User);

/// <summary>
/// Default prompt templates per action and language. Every template can be overridden in settings.
/// Placeholders: {selectedText} or {text}, {selectedStyle} or {style}, {num} or {words}, {value} or {level}, {currentResultText}.
/// The "AI agent prompt" templates live as files in prompts/agent-prompt/ (embedded at build time); see prompts/README.md.
/// </summary>
public static class Prompts
{
    public const string KeyStyle = "STYLE";
    public const string KeyImprove = "IMPROVE";
    public const string KeyFix = "FIX";
    public const string KeySummary = "SUMMARY";
    public const string KeyExplain = "EXPLAIN";
    public const string KeyTranslate = "TRANSLATE";
    public const string KeyPreviewRetranslate = "PREVIEW_RETRANSLATE";
    public const string KeySystem = "system";
    public const string KeyChatSystem = "chat-system";

    public static readonly IReadOnlyList<string> SupportedLanguages = new[] { "en", "it", "es", "fr", "de", "hi" };

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> MultilingualDefaults =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            [KeyStyle] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["it"] = "Riscrivi il seguente testo in stile {selectedStyle}, correggendo eventuali errori grammaticali, ortografici, sintattici, semantici e di stile, migliorandone la chiarezza e la scorrevolezza. Non omettere né aggiungere informazioni: ogni concetto e ogni parte presenti devono essere riportati integralmente e con precisione. Mantieni la lingua originale relativa al testo che devi riscrivere, cioè, non tradurre. Non aggiungere altro testo nella risposta: la tua risposta deve contenere solo il testo riscritto in lingua originale {text}:\n\n{selectedText}",
                ["es"] = "Reescribe el siguiente texto en estilo {selectedStyle}, corrigiendo posibles errores gramaticales, ortográficos, sintácticos, semánticos y de estilo, mejorando su claridad y fluidez. No omitas ni añadas información: cada concepto y cada parte presentes deben ser incluidos íntegramente y con precisión. Mantén el idioma original relativo al texto que debes reescribir, es decir, no traduzcas. No añadas otro texto en la respuesta: tu respuesta debe contener solo el texto reescrito en idioma original {text}:\n\n{selectedText}",
                ["fr"] = "Réécris le texte suivant dans le style {selectedStyle}, en corrigeant les éventuelles erreurs grammaticales, orthographiques, syntaxiques, sémantiques et de style, en améliorant sa clarté et sa fluidité. N'omets ni n'ajoute aucune information : chaque concept et chaque partie présents doivent être rapportés intégralement et avec précision. Conserve la langue originale relative au texte que tu dois réécrire, c'est-à-dire, ne traduis pas. N'ajoute aucun autre texte dans la réponse : ta réponse doit contenir uniquement le texte réécrit dans la langue originale {text} :\n\n{selectedText}",
                ["de"] = "Schreibe den folgenden Text im Stil von {selectedStyle} um, korrigiere eventuelle grammatische, orthografische, syntaktische, semantische und stilistische Fehler und verbessere seine Klarheit und seinen Fluss. Lasse keine Informationen aus und füge keine hinzu: Jedes vorhandene Konzept und jeder Teil müssen vollständig und präzise wiedergegeben werden. Behalte die Originalsprache des Textes bei, den du umschreiben sollst, d. h., übersetze ihn nicht. Füge der Antwort keinen weiteren Text hinzu: Deine Antwort darf nur den umgeschriebenen Text in der Originalsprache {text} enthalten:\n\n{selectedText}",
                ["en"] = "Rewrite the following text in {selectedStyle} style, correcting any grammatical, spelling, syntactic, semantic, and stylistic errors, improving its clarity and flow. Do not omit or add information: every concept and every part present must be reported in full and accurately. Maintain the original language related to the text you need to rewrite, that is, do not translate. Do not add any other text in the response: your response must contain only the rewritten text in the original language {text}:\n\n{selectedText}",
                ["hi"] = "नीचे दिए गए टेक्स्ट को {selectedStyle} स्टाइल में दोबारा लिखें। व्याकरण, स्पेलिंग और वाक्यों की गलतियाँ ठीक करें ताकि बात बिल्कुल साफ और आसानी से समझ आए। कोई भी बात न छोड़ें और न ही अपनी तरफ से कुछ नया जोड़ें—पूरी बात और उसका सही मतलब वैसा ही रहना चाहिए। टेक्स्ट की असली भाषा बनाए रखें, यानी अनुवाद न करें। जवाब में कोई अतिरिक्त बात या भूमिका न जोड़ें: आपके जवाब में सिर्फ असली भाषा {text} में लिखा हुआ टेक्स्ट होना चाहिए:\n\n{selectedText}",
            },
            [KeyImprove] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["it"] = "Riscrivi il seguente testo, correggendo eventuali errori grammaticali, ortografici, sintattici e semantici, migliorandone la chiarezza e la scorrevolezza. Non omettere né aggiungere informazioni: ogni concetto e ogni parte presenti devono essere riportati integralmente e con precisione. Cerca di mantenere gli stessi termini e parole, entro i limiti di una forma corretta. Mantieni la lingua originale relativa al testo che devi riscrivere, cioè, non tradurre. Non aggiungere altro testo nella risposta: la tua risposta deve contenere solo il testo riscritto in lingua originale {text}:\n\n{selectedText}",
                ["es"] = "Reescribe el siguiente texto, corrigiendo posibles errores gramaticales, ortográficos, sintácticos y semánticos, mejorando su claridad y fluidez. No omitas ni añadas información: cada concepto और cada parte presentes deben ser incluidos íntegramente y con precisión. Intenta mantener los mismos términos y palabras, dentro de los límites de una forma correcta. Mantén el idioma original relativo al texto que debes reescribir, es decir, no traduzcas. No añadas otro texto en la respuesta: tu respuesta debe contener solo el texto reescrito en idioma original {text}:\n\n{selectedText}",
                ["fr"] = "Réécris le texte suivant, en corrigeant les éventuelles erreurs grammaticales, orthographiques, syntaxiques et sémantiques, en améliorant sa clarté et sa fluidité. N'omets ni n'ajoute aucune information : chaque concept et chaque partie présents doivent être rapportés intégralement et avec précision. Essayez de conserver les mêmes termes et mots, dans les limites d'une forme correcte. Conserve la langue originale relative au texte que tu dois réécrire, c'est-à-dire, ne traduis pas. N'ajoute aucun autre texte dans la réponse : ta réponse doit contenir uniquement le texte réécrit dans la langue originale {text} :\n\n{selectedText}",
                ["de"] = "Schreibe den folgenden Text um, korrigiere eventuelle grammatische, orthografische, syntaktische und semantische Fehler und verbessere seine Klarheit und seinen Fluss. Lasse keine Informationen aus und füge keine hinzu: Jedes vorhandene Konzept und jeder Teil müssen vollständig und präzise wiedergegeben werden. Versuchen Sie, dieselben Begriffe und Wörter beizubehalten, im Rahmen einer korrekten Form. Behalte die Originalsprache des Textes bei, den du umschreiben sollst, d. h., übersetze ihn nicht. Füge der Antwort keinen weiteren Text hinzu: Deine Antwort darf nur den umgeschriebenen Text in der Originalsprache {text} enthalten:\n\n{selectedText}",
                ["en"] = "Rewrite the following text, correcting any grammatical, spelling, syntactic, and semantic errors, improving its clarity and flow. Do not omit or add information: every concept and every part present must be reported in full and accurately. Try to maintain the same terms and words, within the limits of a correct form. Maintain the original language related to the text you need to rewrite, that is, do not translate. Do not add any other text in the response: your response must contain only the rewritten text in the original language {text}:\n\n{selectedText}",
                ["hi"] = "नीचे दिए गए टेक्स्ट को सुधारकर दोबारा लिखें। व्याकरण, स्पेलिंग और वाक्यों की गलतियाँ ठीक करें ताकि बात साफ और आसान लगे। कोई भी बात न छोड़ें और न ही अपनी तरफ से कुछ नया जोड़ें। जहाँ तक हो सके, वही शब्द इस्तेमाल करें। टेक्स्ट की असली भाषा बनाए रखें, यानी अनुवाद न करें। जवाब में कोई अतिरिक्त बात या भूमिका न जोड़ें: आपके जवाब में सिर्फ असली भाषा {text} में सुधरा हुआ टेक्स्ट होना चाहिए:\n\n{selectedText}",
            },
            [KeyFix] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["it"] = "Riscrivi il seguente testo in modo identico e preciso, correggendo solo eventuali errori grammaticali, ortografici, sintattici e semantici. Non omettere né aggiungere informazioni: il testo deve essere riscritto integralmente in modo identico e con precisione, correggendo solo gli eventuali errori grammaticali, ortografici, sintattici e semantici. Mantieni la lingua originale relativa al testo che devi riscrivere, cioè, non tradurre. Non aggiungere altro testo nella risposta: la tua risposta deve contenere solo il testo riscritto in lingua originale {text}:\n\n{selectedText}",
                ["es"] = "Reescribe el siguiente texto de manera idéntica y precisa, corrigiendo solo los posibles errores gramaticales, ortográficos, sintácticos y semánticos. No omitas ni añadas información: el texto debe ser reescrito integralmente de manera idéntica y con precisión, corrigiendo solo los posibles errores gramaticales, ortográficos, sintácticos y semánticos. Mantén el idioma original relativo al texto que debes reescribir, es decir, no traduzcas. No añadas otro texto en la respuesta: tu respuesta debe contener solo el texto reescrito en idioma original {text}:\n\n{selectedText}",
                ["fr"] = "Réécris le texte suivant de manière identique et précise, en corrigeant seulement les éventuelles erreurs grammaticales, orthographiques, syntaxiques et sémantiques. N'omets ni n'ajoute aucune information : le texte doit être réécrit intégralement de manière identique et avec précision, en corrigeant seulement les éventuelles erreurs grammaticales, orthographiques, syntaxiques et sémantiques. Conserve la langue originale relative au texte que tu dois réécrire, c'est-à-dire, ne traduis pas. N'ajoute aucun autre texte dans la réponse : ta réponse doit contenir uniquement le texte réécrit dans la langue originale {text} :\n\n{selectedText}",
                ["de"] = "Schreibe den folgenden Text auf identische und präzise Weise um, korrigiere nur eventuelle grammatische, orthografische, syntaktische und semantische Fehler. Lasse keine Informationen aus und füge keine hinzu: der Text muss vollständig auf identische Weise und präzise umgeschrieben werden, wobei nur eventuelle grammatische, orthografische, syntaktische und semantische Fehler korrigiert werden. Behalte die Originalsprache des Textes bei, den du umschreiben sollst, d. h., übersetze ihn nicht. Füge der Antwort keinen weiteren Text hinzu: Deine Antwort darf nur den umgeschriebenen Text in der Originalsprache {text} enthalten:\n\n{selectedText}",
                ["en"] = "Rewrite the following text in an identical and precise manner, correcting only any grammatical, spelling, syntactic, and semantic errors. Do not omit or add information: the text must be rewritten entirely in an identical and precise manner, correcting only any grammatical, spelling, syntactic, and semantic errors. Maintain the original language related to the text you need to rewrite, that is, do not translate. Do not add any other text in the response: your response must contain only the rewritten text in the original language {text}:\n\n{selectedText}",
                ["hi"] = "नीचे दिए गए टेक्स्ट को हूबहू वैसा ही रखें, बस व्याकरण, स्पेलिंग और शब्दों की गलतियाँ ठीक करें। कोई भी जानकारी न घटाएं और न बढ़ाएं—बात का तरीका, अंदाज़ और फॉर्मेट वैसा ही रहना चाहिए। टेक्स्ट की असली भाषा बनाए रखें, यानी अनुवाद न करें। जवाब में कोई अतिरिक्त बात या भूमिका न जोड़ें: आपके जवाब में सिर्फ असली भाषा {text} में ठीक किया गया टेक्स्ट होना चाहिए:\n\n{selectedText}",
            },
            [KeySummary] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["it"] = "Riscrivi il seguente testo, correggendo eventuali errori grammaticali, ortografici, sintattici e semantici, migliorandone la chiarezza e la scorrevolezza. Mantieni la lingua originale relativa al testo che devi riscrivere, cioè, non tradurre. Non aggiungere altro testo nella risposta e non interpretare: la tua risposta deve contenere solo il testo riscritto in lingua originale {text}, ma in forma sintetica con un numero massimo di {num} parole:\n\n{selectedText}",
                ["es"] = "Reescribe el siguiente texto, corrigiendo posibles errores gramaticales, ortográficos, sintácticos y semánticos, mejorando su claridad y fluidez. Mantén el idioma original del texto que debes reescribir, es decir, no traduzcas. No añadas más texto en la respuesta y no interpretes: tu respuesta debe contener solo el texto reescrito en idioma original {text}, pero de forma sintética con un número máximo de {num} palabras:\n\n{selectedText}",
                ["fr"] = "Réécris le texte suivant, en corrigeant les éventuelles erreurs grammaticales, orthographiques, syntaxiques et sémantiques, en améliorant sa clarté et sa fluidité. Conserve la langue d'origine du texte que tu dois réécrire, c'est-à-dire, ne traduis pas. N'ajoute aucun autre texte dans la réponse et n'interprète pas : ta réponse doit contenir uniquement le texte réécrit dans la langue d'origine {text}, mais sous une forme synthétique avec un nombre maximum de {num} mots :\n\n{selectedText}",
                ["de"] = "Schreibe den folgenden Text neu, korrigiere eventuelle grammatische, orthografische, syntaktische und semantische Fehler und verbessere seine Klarheit und seinen Fluss. Behalte die Originalsprache des Textes bei, den du umschreiben sollst, d. h. übersetze ihn nicht. Füge keinen weiteren Text in die Antwort ein und interpretiere nicht: Deine Antwort soll nur den umgeschriebenen Text in der Originalsprache {text} enthalten, jedoch in einer prägnanten Form mit einer maximalen Anzahl von {num} Wörtern:\n\n{selectedText}",
                ["en"] = "Rewrite the following text, correcting any grammatical, spelling, syntactic, and semantic errors, improving its clarity and flow. Maintain the original language of the text you need to rewrite, that is, do not translate. Do not add any other text in the response and do not interpret: your response must contain only the rewritten text in the original language {text}, but in a concise form with a maximum number of {num} words:\n\n{selectedText}",
                ["hi"] = "नीचे दिए गए टेक्स्ट को छोटा करके दोबारा लिखें। व्याकरण और स्पेलिंग की गलतियाँ ठीक करें ताकि बात साफ रहे। टेक्स्ट की असली भाषा बनाए रखें, यानी अनुवाद न करें। अपनी तरफ से कोई नई व्याख्या या बात न जोड़ें: आपके जवाब में सिर्फ असली भाषा {text} में ज़्यादा से ज़्यादा {num} शब्दों की छोटी समरी होनी चाहिए:\n\n{selectedText}",
            },
            [KeyExplain] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["it"] = "Spiega il contenuto del seguente testo in modo chiaro e didattico. Importante: adatta la spiegazione al livello di conoscenza {selectedStyle} ({value}). Mantieni la lingua originale relativa al testo che devi spiegare, cioè, non tradurre. Non aggiungere altro testo nella risposta, ne introduzioni o commenti extra: la tua risposta deve contenere solo la spiegazione in lingua originale {text} con un massimo di 1000 parole:\n\n{selectedText}",
                ["es"] = "Explica el contenido del siguiente texto de forma clara y didáctica. Importante: adapta la explicación al nivel de conocimiento {selectedStyle} ({value}). Mantén el idioma original del texto que debes explicar, es decir, no traduzcas. No añadas ningún otro texto en la respuesta, ni introducciones ni comentarios adicionales: tu respuesta debe contener únicamente la explicación en el idioma original {text} con un máximo de 1000 palabras:\n\n{selectedText}",
                ["fr"] = "Explique le contenu du texte suivant de manière claire et pédagogique. Important : adapte l’explication au niveau de connaissance {selectedStyle} ({value}). Conserve la langue originale du texte à expliquer, c’est-à-dire ne le traduis pas. N’ajoute aucun autre texte dans la réponse, ni introduction ni commentaire supplémentaire : ta réponse doit contenir uniquement l’explication dans la langue originale {text} avec un maximum de 1000 mots :\n\n{selectedText}",
                ["de"] = "Erkläre den Inhalt des folgenden Textes klar und didaktisch. Wichtig: Passe die Erklärung an das Kenntnisniveau {selectedStyle} ({value}) an. Behalte die Originalsprache des zu erklärenden Textes bei, das heißt, übersetze ihn nicht. Füge keinen weiteren Text zur Antwort hinzu, keine Einleitungen oder zusätzlichen Kommentare: Deine Antwort darf ausschließlich die Erklärung in der Originalsprache {text} enthalten und maximal 1000 Wörter umfassen:\n\n{selectedText}",
                ["en"] = "Explain the content of the following text in a clear and didactic way. Important: adapt the explanation to the knowledge level {selectedStyle} ({value}). Keep the original language of the text you have to explain, that is, do not translate it. Do not add any other text to the response, no introductions or extra comments: your response must contain only the explanation in the original language {text} with a maximum of 1000 words:\n\n{selectedText}",
                ["hi"] = "नीचे दिए गए टेक्स्ट को आसान और सीधी भाषा में समझाएं। ध्यान दें: समझाने का तरीका {selectedStyle} ({value}) के हिसाब से रखें। टेक्स्ट की असली भाषा बनाए रखें, यानी इसका अनुवाद न करें। जवाब में कोई भूमिका या अतिरिक्त बात न जोड़ें: आपके जवाब में सिर्फ असली भाषा {text} में ज़्यादा से ज़्यादा 1000 शब्दों में समझाई गई बात होनी चाहिए:\n\n{selectedText}",
            },
            [KeyTranslate] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["it"] = "Traduci il seguente testo in lingua {selectedStyle}. Non aggiungere altro testo nella risposta: la tua risposta deve contenere solo il testo tradotto in lingua {selectedStyle}:\n\n{selectedText}",
                ["es"] = "Traduce el siguiente texto al idioma {selectedStyle}. No añadas otro texto en la respuesta: tu respuesta deve contener solo el texto traducido al idioma {selectedStyle}:\n\n{selectedText}",
                ["fr"] = "Traduis le texte suivant en langue {selectedStyle}. N'ajoute aucun autre texte dans la réponse : ta réponse doit contenir uniquement le texte traduit en langue {selectedStyle} :\n\n{selectedText}",
                ["de"] = "Übersetze den folgenden Text in die Sprache {selectedStyle}. Füge der Antwort keinen weiteren Text hinzu: Deine Antwort darf nur den übersetzten Text in der Sprache {selectedStyle} enthalten:\n\n{selectedText}",
                ["en"] = "Translate the following text into {selectedStyle} language. Do not add any other text in the response: your response must contain only the translated text in {selectedStyle} language:\n\n{selectedText}",
                ["hi"] = "नीचे दिए गए टेक्स्ट का {selectedStyle} भाषा में अनुवाद करें। जवाब में कोई अतिरिक्त बात न जोड़ें: आपके जवाब में सिर्फ {selectedStyle} भाषा में अनुवाद किया गया टेक्स्ट होना चाहिए:\n\n{selectedText}",
            },
            [KeyPreviewRetranslate] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["it"] = "Traduci il seguente testo in lingua {value}. Non aggiungere altro testo nella risposta: la tua risposta deve contenere solo il testo tradotto in lingua {value}:\n\n{currentResultText}",
                ["es"] = "Traduce el siguiente testo al idioma {value}. No añadas otro texto en la respuesta: tu respuesta debe contener solo el testo traducido al idioma {value}:\n\n{currentResultText}",
                ["fr"] = "Traduis le texte suivant en langue {value}. N'ajoute aucun autre texte dans la réponse : ta réponse doit contenir uniquement le texte traduit en langue {value} :\n\n{currentResultText}",
                ["de"] = "Übersetze den folgenden Text in die Sprache {value}. Füge der Antwort keinen weiteren Text hinzu: Deine Antwort darf nur den übersetzten Text in der Sprache {value} enthalten:\n\n{currentResultText}",
                ["en"] = "Translate the following text into {value} language. Do not add any other text in the response: your response must contain only the translated text in {value} language:\n\n{currentResultText}",
                ["hi"] = "नीचे दिए गए टेक्स्ट का {value} भाषा में अनुवाद करें। जवाब में कोई अतिरिक्त बात न जोड़ें: आपके जवाब में सिर्फ {value} भाषा में अनुवाद किया गया टेक्स्ट होना चाहिए:\n\n{currentResultText}",
            },
            [KeySystem] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "You are a careful writing assistant. Follow the prompt instructions directly. Output only the requested text without introductory remarks or conversational filler.",
                ["hi"] = "आप एक मददगार और समझदार राइटिंग असिस्टेंट हैं। दिए गए निर्देशों का सीधे पालन करें। बिना किसी भूमिका या अतिरिक्त टिप्पणी के सिर्फ मांगा गया टेक्स्ट ही दें।",
            },
            [KeyChatSystem] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "You are a helpful assistant. The user gives an instruction and, separately, a text between <text> and </text> tags. Follow the instruction using the text. Reply with the result only, without introductions.",
                ["hi"] = "आप एक मददगार असिस्टेंट हैं। यूज़र आपको एक निर्देश और <text> तथा </text> टैग के बीच टेक्स्ट देगा। उस टेक्स्ट के आधार पर निर्देश का पालन करें और बिना किसी भूमिका के सिर्फ जवाब दें।",
            },
        };

    public static readonly IReadOnlyDictionary<string, string> Defaults =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [KeyStyle] = MultilingualDefaults[KeyStyle]["en"],
            [KeyImprove] = MultilingualDefaults[KeyImprove]["en"],
            [KeyFix] = MultilingualDefaults[KeyFix]["en"],
            [KeySummary] = MultilingualDefaults[KeySummary]["en"],
            [KeyExplain] = MultilingualDefaults[KeyExplain]["en"],
            [KeyTranslate] = MultilingualDefaults[KeyTranslate]["en"],
            [KeyPreviewRetranslate] = MultilingualDefaults[KeyPreviewRetranslate]["en"],
            [KeySystem] = MultilingualDefaults[KeySystem]["en"],
            [KeyChatSystem] = MultilingualDefaults[KeyChatSystem]["en"],
            // Lowercase aliases for backward compatibility
            ["style"] = MultilingualDefaults[KeyStyle]["en"],
            ["improve"] = MultilingualDefaults[KeyImprove]["en"],
            ["fix"] = MultilingualDefaults[KeyFix]["en"],
            ["summary"] = MultilingualDefaults[KeySummary]["en"],
            ["explain"] = MultilingualDefaults[KeyExplain]["en"],
            ["translate"] = MultilingualDefaults[KeyTranslate]["en"],
            ["preview_retranslate"] = MultilingualDefaults[KeyPreviewRetranslate]["en"],
            ["preview-retranslate"] = MultilingualDefaults[KeyPreviewRetranslate]["en"],
        };

    public static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [KeyStyle] = "Rewrite in a style (STYLE)",
            [KeyImprove] = "Correct and improve (IMPROVE)",
            [KeyFix] = "Fix errors only (FIX)",
            [KeySummary] = "Summary (SUMMARY)",
            [KeyExplain] = "Explain (EXPLAIN)",
            [KeyTranslate] = "Translate (TRANSLATE)",
            [KeyPreviewRetranslate] = "Retranslate preview (PREVIEW_RETRANSLATE)",
            [KeySystem] = "System instruction (all actions)",
            [KeyChatSystem] = "Chat system instruction",
        };

    /// <summary>Variants of the "AI agent prompt" action, from prompts/agent-prompt/NN-id.txt, in file order.</summary>
    public static readonly IReadOnlyList<(string Id, string Label, string Template)> AgentTemplates = LoadAgentTemplates();

    // Registers the agent templates so Build, Settings › Prompts, overrides and reset treat them like built-in prompts.
    static Prompts()
    {
        foreach (var (id, label, template) in AgentTemplates)
        {
            ((Dictionary<string, IReadOnlyDictionary<string, string>>)MultilingualDefaults)[AgentKey(id)] =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = template };
            ((Dictionary<string, string>)Labels)[AgentKey(id)] = $"AI agent prompt: {label}";
        }
    }

    public static string AgentKey(string id) => "AGENT_" + id;

    public static string AgentLabel(string id) => AgentTemplates.FirstOrDefault(t => t.Id == id).Label ?? id;

    private static List<(string Id, string Label, string Template)> LoadAgentTemplates()
    {
        const string prefix = "agent-prompt/";
        var asm = typeof(Prompts).Assembly;
        var list = new List<(string, string, string)>();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(prefix)).Order(StringComparer.Ordinal))
        {
            using var reader = new StreamReader(asm.GetManifestResourceStream(name)!);
            var body = reader.ReadToEnd().Replace("\r\n", "\n");
            var nl = body.IndexOf('\n');
            if (nl < 0) continue;
            var id = Regex.Replace(Path.GetFileNameWithoutExtension(name[prefix.Length..]), @"^\d+-", "");
            list.Add((id, body[..nl].TrimStart('#').Trim(), body[(nl + 1)..].Trim()));
        }
        return list;
    }

    public static string GetDefault(string key, string? lang = "en")
    {
        var targetLang = string.IsNullOrWhiteSpace(lang) ? "en" : lang.Trim().ToLowerInvariant();
        if (MultilingualDefaults.TryGetValue(key, out var dict))
        {
            if (dict.TryGetValue(targetLang, out var tmpl)) return tmpl;
            if (dict.TryGetValue("en", out var enTmpl)) return enTmpl;
            return dict.Values.FirstOrDefault() ?? "";
        }
        return Defaults.TryGetValue(key, out var def) ? def : "";
    }

    public static string Template(AppSettings s, string key)
    {
        if (s.PromptOverrides.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
            return v;
        return GetDefault(key, s.PromptLanguage);
    }

    public static string Fill(
        string template,
        string text,
        string style = "",
        string words = "",
        string level = "",
        string language = "",
        string value = "")
    {
        var result = template
            .Replace("{selectedStyle}", style)
            .Replace("{style}", style)
            .Replace("{num}", words)
            .Replace("{words}", words)
            .Replace("{value}", !string.IsNullOrEmpty(value) ? value : level)
            .Replace("{level}", level)
            .Replace("{language}", language);

        if (result.Contains("{selectedText}"))
        {
            result = result.Replace("{selectedText}", text);
            result = result.Replace("{text}", "");
        }
        else if (result.Contains("{currentResultText}"))
        {
            result = result.Replace("{currentResultText}", text);
            result = result.Replace("{text}", "");
        }
        else
        {
            result = result.Replace("{text}", text);
        }

        return result;
    }

    /// <summary>Builds the prompt for a menu action. Chat and Listen are handled elsewhere.</summary>
    public static PromptPair Build(AppSettings s, ActionSpec action, string text, bool isPreviewRetranslate = false)
    {
        var lang = s.PromptLanguage;
        var system = Template(s, KeySystem);
        string user = action.Kind switch
        {
            ActionKind.Style => Fill(Template(s, KeyStyle), text, style: Catalog.StyleLabel(action.Argument, lang)),
            ActionKind.Improve => Fill(Template(s, KeyImprove), text),
            ActionKind.Fix => Fill(Template(s, KeyFix), text),
            ActionKind.Summary => Fill(Template(s, KeySummary), text,
                words: TextUtil.SummaryWordLimit(text, int.Parse(action.Argument)).ToString()),
            ActionKind.Explain => Fill(Template(s, KeyExplain), text,
                style: Catalog.LevelLabel(action.Argument, lang),
                value: Catalog.LevelDescription(action.Argument, lang),
                level: $"{Catalog.LevelLabel(action.Argument, lang)} ({Catalog.LevelDescription(action.Argument, lang)})"),
            ActionKind.Translate => isPreviewRetranslate
                ? Fill(Template(s, KeyPreviewRetranslate), text, value: action.Argument, language: action.Argument)
                : Fill(Template(s, KeyTranslate), text, style: action.Argument, language: action.Argument),
            ActionKind.AgentPrompt => Fill(Template(s, AgentKey(action.Argument)), text),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "No prompt for this action."),
        };
        return new PromptPair(system, user);
    }

    public static PromptPair BuildRetranslate(AppSettings s, string language, string text) =>
        Build(s, new ActionSpec(ActionKind.Translate, language), text, isPreviewRetranslate: true);

    public static PromptPair BuildChat(AppSettings s, string instruction, string text) =>
        new(Template(s, KeyChatSystem),
            string.IsNullOrWhiteSpace(text)
                ? instruction.Trim()
                : $"{instruction.Trim()}\n\n<text>\n{text}\n</text>");
}
