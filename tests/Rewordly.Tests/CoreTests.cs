using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Rewordly.Core;
using Xunit;

namespace Rewordly.Tests;

sealed class PlainProtector : ISecretProtector
{
    public string Protect(string id, string plain) => "P:" + id + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));
    public string? Unprotect(string id, string v) => v.StartsWith("P:" + id + ":") ? Encoding.UTF8.GetString(Convert.FromBase64String(v[(id.Length + 3)..])) : null;
}

/// <summary>Records requests and replies with a canned response.</summary>
sealed class FakeHandler : HttpMessageHandler
{
    public readonly List<(HttpRequestMessage Req, string Body)> Calls = new();
    private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _reply;
    public FakeHandler(Func<HttpRequestMessage, string, HttpResponseMessage> reply) => _reply = reply;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
        Calls.Add((request, body));
        return _reply(request, body);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

public class PromptTests
{
    [Fact]
    public void Style_prompt_uses_style_and_wraps_text()
    {
        var s = new AppSettings();
        var p = Prompts.Build(s, new ActionSpec(ActionKind.Style, "Professional"), "hello wrld");
        Assert.Contains("in Professional style", p.User);
        Assert.EndsWith("hello wrld", p.User);
        Assert.NotEmpty(p.System);
    }

    [Theory]
    [InlineData(200, 70, 140)]
    [InlineData(200, 50, 100)]
    [InlineData(200, 30, 60)]
    [InlineData(7, 30, 5)] // floor of 5 words
    public void Summary_word_budget(int words, int pct, int expected)
    {
        var text = string.Join(' ', Enumerable.Repeat("word", words));
        var p = Prompts.Build(new AppSettings(), new ActionSpec(ActionKind.Summary, pct.ToString()), text);
        Assert.Contains($"maximum number of {expected} words", p.User);
    }

    [Fact]
    public void Explain_includes_level_description()
    {
        var p = Prompts.Build(new AppSettings(), new ActionSpec(ActionKind.Explain, "intermediate"), "x");
        Assert.Contains("Intermediate (comfortable with the basics", p.User);
    }

    [Fact]
    public void Translate_uses_target_language()
    {
        var p = Prompts.Build(new AppSettings(), new ActionSpec(ActionKind.Translate, "Hindi"), "Good morning");
        Assert.StartsWith("Translate the following text into Hindi language.", p.User);
        Assert.EndsWith("Good morning", p.User);
    }

    [Fact]
    public void Preview_retranslate_builds_correctly()
    {
        var p = Prompts.BuildRetranslate(new AppSettings(), "Spanish", "Result text");
        Assert.StartsWith("Translate the following text into Spanish language.", p.User);
        Assert.EndsWith("Result text", p.User);
    }

    [Theory]
    [InlineData("it", "Riscrivi il seguente testo in stile")]
    [InlineData("es", "Reescribe el siguiente texto en estilo")]
    [InlineData("fr", "Réécris le texte suivant dans le style")]
    [InlineData("de", "Schreibe den folgenden Text im Stil von")]
    [InlineData("en", "Rewrite the following text in")]
    [InlineData("hi", "निम्नलिखित पाठ को")]
    public void Multilingual_style_defaults(string lang, string expectedPrefix)
    {
        var s = new AppSettings { PromptLanguage = lang };
        var p = Prompts.Build(s, new ActionSpec(ActionKind.Style, "Formal"), "demo");
        Assert.StartsWith(expectedPrefix, p.User);
        if (lang == "hi") Assert.Contains("औपचारिक (Formal)", p.User);
    }

    [Fact]
    public void Override_template_is_used_and_placeholders_inside_user_text_survive()
    {
        var s = new AppSettings();
        s.PromptOverrides[Prompts.KeyFix] = "FIX>>{text}<<";
        var p = Prompts.Build(s, new ActionSpec(ActionKind.Fix), "keep {style} literally");
        Assert.Equal("FIX>>keep {style} literally<<", p.User);
    }

    [Fact]
    public void Blank_override_falls_back_to_default()
    {
        var s = new AppSettings();
        s.PromptOverrides[Prompts.KeyImprove] = "   ";
        Assert.Equal(Prompts.Defaults[Prompts.KeyImprove], Prompts.Template(s, Prompts.KeyImprove));
    }

    [Fact]
    public void Chat_without_text_sends_instruction_only()
    {
        var p = Prompts.BuildChat(new AppSettings(), "  Write a haiku  ", "");
        Assert.Equal("Write a haiku", p.User);
        var p2 = Prompts.BuildChat(new AppSettings(), "Reply politely", "Where is my order?");
        Assert.Equal("Reply politely\n\n<text>\nWhere is my order?\n</text>", p2.User);
    }
}

public class TextTests
{
    [Fact]
    public void Counts_words_like_word_boundary_regex()
    {
        Assert.Equal(4, TextUtil.CountWords("It's a test."));
        Assert.Equal(0, TextUtil.CountWords(""));
    }

    [Fact]
    public void Cleans_think_blocks_tags_and_fences()
    {
        Assert.Equal("Hi there", TextUtil.CleanModelOutput("<think>reasoning</think>\n Hi there "));
        Assert.Equal("Hi", TextUtil.CleanModelOutput("<text>\nHi\n</text>"));
        Assert.Equal("a = 1", TextUtil.CleanModelOutput("```python\na = 1\n```", "a=1"));
        Assert.Equal("```\nx\n```".Replace("\n", Environment.NewLine), TextUtil.CleanModelOutput("```\nx\n```", "```\nx```"));
    }

    [Fact]
    public void Collapses_blank_lines()
    {
        var r = TextUtil.NormalizeBlankLines("a\n  \n\n\n\nb");
        Assert.Equal("a" + Environment.NewLine + Environment.NewLine + "b", r);
    }

    [Theory]
    [InlineData("नमस्ते, आप कैसे हैं?", "hi-IN")]
    [InlineData("The cat is on the table and it is happy", "en-US")]
    [InlineData("Der Hund ist nicht mit der Katze", "de-DE")]
    [InlineData("こんにちは、元気ですか", "ja-JP")]
    [InlineData("Привет, как дела?", "ru-RU")]
    public void Guesses_voice_culture(string text, string culture) => Assert.Equal(culture, LanguageGuess.GuessCulture(text));
}

public class SettingsTests
{
    [Fact]
    public void Recent_lists_dedupe_and_trim()
    {
        var l = new List<string> { "A", "B", "C" };
        AppSettings.PushRecent(l, "b", 3);
        Assert.Equal(new[] { "b", "A", "C" }, l);
        AppSettings.PushRecent(l, "D", 3);
        Assert.Equal(new[] { "D", "b", "A" }, l);
    }

    [Fact]
    public void Custom_styles_validate()
    {
        var s = new AppSettings();
        Assert.Null(s.AddCustomStyle("LinkedIn post"));
        Assert.NotNull(s.AddCustomStyle("linkedin POST"));
        Assert.NotNull(s.AddCustomStyle("Formal"));
        Assert.NotNull(s.AddCustomStyle(new string('x', 46)));
        s.RecentStyles.Add("LinkedIn post");
        Assert.True(s.RemoveCustomStyle("LINKEDIN post"));
        Assert.Empty(s.CustomStyles);
        Assert.Empty(s.RecentStyles);
    }

    [Fact]
    public void Round_trips_and_encrypts_key()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rw-" + Guid.NewGuid());
        var store = new SettingsStore(dir, new PlainProtector());
        var s = store.Load();
        Assert.Equal("gemini-3.5-flash-lite", s.Active.Model);
        s.Active.Model = "gemini-3.1-flash-lite";
        store.SetApiKey(s, "gemini", " secret-key ");
        s.Theme = "dark";
        store.Save(s);

        var raw = File.ReadAllText(store.FilePath);
        Assert.DoesNotContain("secret-key", raw);
        var s2 = store.Load();
        Assert.Equal("gemini-3.1-flash-lite", s2.Active.Model);
        Assert.Equal("secret-key", store.GetApiKey(s2, "gemini"));
        Assert.Equal("dark", s2.Theme);
    }

    [Fact]
    public void Corrupt_file_falls_back_and_is_preserved()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rw-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var store = new SettingsStore(dir, new PlainProtector());
        File.WriteAllText(store.FilePath, "{not json");
        var s = store.Load();
        Assert.Equal("gemini", s.ActiveProvider);
        Assert.True(File.Exists(store.FilePath + ".corrupt"));
    }

    [Fact]
    public void Normalize_clamps_values()
    {
        var s = new AppSettings { Temperature = 5, TimeoutSeconds = 1, Theme = "neon", ActiveProvider = "nope" };
        s.Normalize();
        Assert.Equal(2, s.Temperature);
        Assert.Equal(10, s.TimeoutSeconds);
        Assert.Equal("system", s.Theme);
        Assert.Equal("gemini", s.ActiveProvider);
    }
}

public class GeminiTests
{
    static AiClientOptions Opt(string model = "gemini-3.5-flash-lite", double? temp = null) => new()
    {
        Protocol = ApiProtocol.Gemini, BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
        Model = model, ApiKey = "K123", Temperature = temp, ProviderId = "gemini",
    };

    [Fact]
    public async Task Sends_selected_model_key_system_and_temperature()
    {
        var h = new FakeHandler((_, _) => FakeHandler.Json(
            """{"candidates":[{"content":{"parts":[{"text":"thinking...","thought":true},{"text":"Hello "},{"text":"world"}]},"finishReason":"STOP"}]}"""));
        var c = AiClientFactory.Create(new HttpClient(h), Opt("gemini-3.1-flash-lite", 0.3));
        var r = await c.CompleteAsync(new PromptPair("SYS", "USER"), default);

        Assert.Equal("Hello world", r.Text);
        var (req, body) = h.Calls.Single();
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent", req.RequestUri!.ToString());
        Assert.Equal("K123", req.Headers.GetValues("x-goog-api-key").Single());
        var j = JsonNode.Parse(body)!;
        Assert.Equal("USER", j["contents"]![0]!["parts"]![0]!["text"]!.ToString());
        Assert.Equal("SYS", j["systemInstruction"]!["parts"]![0]!["text"]!.ToString());
        Assert.Equal(0.3, j["generationConfig"]!["temperature"]!.GetValue<double>());
    }

    [Fact]
    public async Task Omits_generation_config_when_temperature_default()
    {
        var h = new FakeHandler((_, _) => FakeHandler.Json("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}"""));
        await AiClientFactory.Create(new HttpClient(h), Opt()).CompleteAsync(new PromptPair("", "u"), default);
        var j = JsonNode.Parse(h.Calls[0].Body)!;
        Assert.Null(j["generationConfig"]);
        Assert.Null(j["systemInstruction"]);
    }

    [Fact]
    public async Task Retired_model_404_gives_clear_message()
    {
        var h = new FakeHandler((_, _) => FakeHandler.Json(
            """{"error":{"code":404,"message":"models/gemini-1.0-pro is not found","status":"NOT_FOUND"}}""", HttpStatusCode.NotFound));
        var ex = await Assert.ThrowsAsync<AiException>(() =>
            AiClientFactory.Create(new HttpClient(h), Opt("gemini-1.0-pro")).CompleteAsync(new PromptPair("", "u"), default));
        Assert.Contains("was not found", ex.Message);
        Assert.Contains("is not found", ex.Message);
        Assert.Equal(HttpStatusCode.NotFound, ex.Status);
    }

    [Fact]
    public async Task Blocked_prompt_is_reported()
    {
        var h = new FakeHandler((_, _) => FakeHandler.Json("""{"promptFeedback":{"blockReason":"SAFETY"}}"""));
        var ex = await Assert.ThrowsAsync<AiException>(() =>
            AiClientFactory.Create(new HttpClient(h), Opt()).CompleteAsync(new PromptPair("", "u"), default));
        Assert.Contains("SAFETY", ex.Message);
    }

    [Fact]
    public async Task Missing_key_fails_before_network()
    {
        var h = new FakeHandler((_, _) => throw new Exception("should not be called"));
        var o = new AiClientOptions { Protocol = ApiProtocol.Gemini, BaseUrl = "https://x/v1beta", Model = "m" };
        var ex = await Assert.ThrowsAsync<AiException>(() => AiClientFactory.Create(new HttpClient(h), o).CompleteAsync(new PromptPair("", "u"), default));
        Assert.Contains("API key", ex.Message);
        Assert.Empty(h.Calls);
    }

    [Fact]
    public async Task Lists_models_with_paging_and_filtering()
    {
        var h = new FakeHandler((req, _) => req.RequestUri!.Query.Contains("pageToken=p2")
            ? FakeHandler.Json("""{"models":[{"name":"models/gemini-3.5-flash-lite","supportedGenerationMethods":["generateContent"]}]}""")
            : FakeHandler.Json("""{"models":[{"name":"models/text-embedding-004","supportedGenerationMethods":["embedContent"]},{"name":"models/gemini-3.6-flash","supportedGenerationMethods":["generateContent","countTokens"]}],"nextPageToken":"p2"}"""));
        var models = await AiClientFactory.Create(new HttpClient(h), Opt()).ListModelsAsync(default);
        Assert.Equal(new[] { "gemini-3.5-flash-lite", "gemini-3.6-flash" }, models);
        Assert.Equal(2, h.Calls.Count);
    }

    [Fact]
    public async Task Timeout_is_reported_as_ai_error()
    {
        var o = new AiClientOptions { Protocol = ApiProtocol.Gemini, BaseUrl = "https://x/v1beta", Model = "m", ApiKey = "k", Timeout = TimeSpan.FromMilliseconds(200) };
        var slow = new DelayHandler(TimeSpan.FromSeconds(5));
        var ex = await Assert.ThrowsAsync<AiException>(() => AiClientFactory.Create(new HttpClient(slow), o).CompleteAsync(new PromptPair("", "u"), default));
        Assert.Contains("did not answer", ex.Message);
    }

    [Fact]
    public async Task User_cancel_is_not_reported_as_error()
    {
        var slow = new DelayHandler(TimeSpan.FromSeconds(5));
        var o = new AiClientOptions { Protocol = ApiProtocol.Gemini, BaseUrl = "https://x/v1beta", Model = "m", ApiKey = "k" };
        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AiClientFactory.Create(new HttpClient(slow), o).CompleteAsync(new PromptPair("", "u"), cts.Token));
    }
}

sealed class DelayHandler : HttpMessageHandler
{
    private readonly TimeSpan _d;
    public DelayHandler(TimeSpan d) => _d = d;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await Task.Delay(_d, ct);
        return FakeHandler.Json("{}");
    }
}

public class OpenAiTests
{
    static AiClientOptions Opt(string? key = "sk-1", string provider = "openrouter") => new()
    {
        Protocol = ApiProtocol.OpenAiCompatible, BaseUrl = "https://openrouter.ai/api/v1/", Model = "anthropic/claude-x",
        ApiKey = key, ProviderId = provider, Temperature = 0.7,
    };

    [Fact]
    public async Task Sends_chat_completion_with_bearer()
    {
        var h = new FakeHandler((_, _) => FakeHandler.Json("""{"model":"anthropic/claude-x","choices":[{"message":{"role":"assistant","content":"Done"}}]}"""));
        var r = await AiClientFactory.Create(new HttpClient(h), Opt()).CompleteAsync(new PromptPair("S", "U"), default);
        Assert.Equal("Done", r.Text);
        var (req, body) = h.Calls.Single();
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", req.RequestUri!.ToString());
        Assert.Equal("Bearer sk-1", req.Headers.Authorization!.ToString());
        Assert.True(req.Headers.Contains("X-Title"));
        var j = JsonNode.Parse(body)!;
        Assert.Equal("anthropic/claude-x", j["model"]!.ToString());
        Assert.Equal("system", j["messages"]![0]!["role"]!.ToString());
        Assert.Equal("U", j["messages"]![1]!["content"]!.ToString());
        Assert.Equal(0.7, j["temperature"]!.GetValue<double>());
    }

    [Fact]
    public async Task Local_server_without_key_sends_no_auth_header()
    {
        var h = new FakeHandler((_, _) => FakeHandler.Json("""{"choices":[{"message":{"content":[{"type":"text","text":"A"},{"type":"text","text":"B"}]}}]}"""));
        var o = new AiClientOptions { Protocol = ApiProtocol.OpenAiCompatible, BaseUrl = "http://localhost:11434/v1", Model = "llama3.2", ProviderId = "ollama" };
        var r = await AiClientFactory.Create(new HttpClient(h), o).CompleteAsync(new PromptPair("", "U"), default);
        Assert.Equal("AB", r.Text);
        Assert.Null(h.Calls[0].Req.Headers.Authorization);
    }

    [Fact]
    public async Task Auth_error_explains_key_problem()
    {
        var h = new FakeHandler((_, _) => FakeHandler.Json("""{"error":{"message":"Invalid API key"}}""", HttpStatusCode.Unauthorized));
        var ex = await Assert.ThrowsAsync<AiException>(() => AiClientFactory.Create(new HttpClient(h), Opt()).CompleteAsync(new PromptPair("", "U"), default));
        Assert.Contains("API key was refused", ex.Message);
        Assert.Contains("Invalid API key", ex.Message);
    }

    [Fact]
    public async Task Lists_models()
    {
        var h = new FakeHandler((_, _) => FakeHandler.Json("""{"data":[{"id":"z-model"},{"id":"a-model"}]}"""));
        var m = await AiClientFactory.Create(new HttpClient(h), Opt()).ListModelsAsync(default);
        Assert.Equal(new[] { "a-model", "z-model" }, m);
        Assert.EndsWith("/models", h.Calls[0].Req.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Missing_model_fails_fast()
    {
        var h = new FakeHandler((_, _) => FakeHandler.Json("{}"));
        var o = new AiClientOptions { Protocol = ApiProtocol.OpenAiCompatible, BaseUrl = "https://api.openai.com/v1", Model = "", ApiKey = "k" };
        var ex = await Assert.ThrowsAsync<AiException>(() => AiClientFactory.Create(new HttpClient(h), o).CompleteAsync(new PromptPair("", "U"), default));
        Assert.Contains("No model", ex.Message);
        Assert.Empty(h.Calls);
    }

    [Fact]
    public void Factory_reads_settings_and_overrides_model()
    {
        var store = new SettingsStore(Path.GetTempPath(), new PlainProtector());
        var s = new AppSettings { ActiveProvider = "openrouter", Temperature = 0.2 };
        s.GetProvider("openrouter").Model = "x/y";
        store.SetApiKey(s, "openrouter", "kk");
        var o = AiClientFactory.FromSettings(s, store);
        Assert.Equal("https://openrouter.ai/api/v1", o.BaseUrl);
        Assert.Equal("x/y", o.Model);
        Assert.Equal("kk", o.ApiKey);
        Assert.Equal(ApiProtocol.OpenAiCompatible, o.Protocol);
        Assert.Equal("other", AiClientFactory.FromSettings(s, store, modelOverride: "other").Model);
    }
}

public class SerializationTests
{
    [Fact]
    public void Computed_active_provider_is_not_written()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rw-" + Guid.NewGuid());
        var store = new SettingsStore(dir, new PlainProtector());
        var s = store.Load();
        store.Save(s);
        var raw = File.ReadAllText(store.FilePath);
        Assert.DoesNotContain("\"Active\"", raw);
        Assert.Contains("\"ActiveProvider\"", raw);
    }
}

public class EnvironmentKeyTests
{
    [Fact]
    public void Env_var_used_only_when_no_key_stored()
    {
        var store = new SettingsStore(Path.Combine(Path.GetTempPath(), "rw-" + Guid.NewGuid()), new PlainProtector());
        var s = new AppSettings { ActiveProvider = "groq" };
        Environment.SetEnvironmentVariable("GROQ_API_KEY", "env-key");
        try
        {
            Assert.Equal("env-key", store.GetApiKey(s, "groq"));
            Assert.False(store.HasStoredKey(s, "groq"));
            store.SetApiKey(s, "groq", "stored");
            Assert.Equal("stored", store.GetApiKey(s, "groq"));
            store.SetApiKey(s, "groq", "");
            Assert.Null(s.GetProvider("groq").ProtectedApiKey);
            Assert.Equal("env-key", store.GetApiKey(s, "groq"));
        }
        finally { Environment.SetEnvironmentVariable("GROQ_API_KEY", null); }
    }

    [Fact]
    public void Config_folder_override()
    {
        Environment.SetEnvironmentVariable("REWORDLY_CONFIG_DIR", "/tmp/rw-x");
        try { Assert.Equal("/tmp/rw-x", SettingsStore.DefaultFolder); }
        finally { Environment.SetEnvironmentVariable("REWORDLY_CONFIG_DIR", null); }
    }
}
