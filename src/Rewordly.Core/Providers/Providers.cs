using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Rewordly.Core;

public enum ApiProtocol { Gemini, OpenAiCompatible }

public sealed record ProviderPreset(
    string Id, string Name, ApiProtocol Protocol, string DefaultBaseUrl, string DefaultModel,
    bool NeedsKey, string KeyHelpUrl, string Notes, string[] KeyEnvironmentVariables);

public static class ProviderPresets
{
    public const string Gemini = "gemini";

    public static readonly ProviderPreset[] All =
    {
        new(Gemini, "Google Gemini", ApiProtocol.Gemini,
            "https://generativelanguage.googleapis.com/v1beta", "gemini-3.5-flash-lite", true,
            "https://aistudio.google.com/apikey", "Free tier available via Google AI Studio.", new[] { "GEMINI_API_KEY", "GOOGLE_API_KEY" }),
        new("openai", "OpenAI", ApiProtocol.OpenAiCompatible,
            "https://api.openai.com/v1", "", true,
            "https://platform.openai.com/api-keys", "Pick a model with Refresh list.", new[] { "OPENAI_API_KEY" }),
        new("openrouter", "OpenRouter (many providers)", ApiProtocol.OpenAiCompatible,
            "https://openrouter.ai/api/v1", "", true,
            "https://openrouter.ai/keys", "One key for Claude, GPT, Gemini, Llama, DeepSeek and more.", new[] { "OPENROUTER_API_KEY" }),
        new("groq", "Groq", ApiProtocol.OpenAiCompatible,
            "https://api.groq.com/openai/v1", "", true,
            "https://console.groq.com/keys", "Very fast open-weight models.", new[] { "GROQ_API_KEY" }),
        new("ollama", "Ollama (local, offline)", ApiProtocol.OpenAiCompatible,
            "http://localhost:11434/v1", "", false,
            "https://ollama.com/download", "Runs on your computer; nothing leaves the machine. No key needed.", Array.Empty<string>()),
        new("custom", "Custom OpenAI-compatible", ApiProtocol.OpenAiCompatible,
            "", "", false,
            "", "Any server with /chat/completions, e.g. LM Studio (http://localhost:1234/v1), vLLM, LiteLLM.", Array.Empty<string>()),
    };

    public static ProviderPreset Get(string id) =>
        All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}

public sealed class AiException : Exception
{
    public HttpStatusCode? Status { get; }
    public AiException(string message, HttpStatusCode? status = null, Exception? inner = null) : base(message, inner) => Status = status;
}

public sealed record AiResult(string Text, string Model, TimeSpan Elapsed);

public interface IAiClient
{
    Task<AiResult> CompleteAsync(PromptPair prompt, CancellationToken ct);
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct);
}

public sealed class AiClientOptions
{
    public required ApiProtocol Protocol { get; init; }
    public required string BaseUrl { get; init; }
    public required string Model { get; init; }
    public string? ApiKey { get; init; }
    public double? Temperature { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public string ProviderId { get; init; } = "";
}

public static class AiClientFactory
{
    public static IAiClient Create(HttpClient http, AiClientOptions o) => o.Protocol switch
    {
        ApiProtocol.Gemini => new GeminiClient(http, o),
        _ => new OpenAiCompatibleClient(http, o),
    };

    public static AiClientOptions FromSettings(AppSettings s, SettingsStore store, string? providerId = null, string? modelOverride = null)
    {
        var id = providerId ?? s.ActiveProvider;
        var preset = ProviderPresets.Get(id);
        var cfg = s.GetProvider(id);
        return new AiClientOptions
        {
            ProviderId = preset.Id,
            Protocol = preset.Protocol,
            BaseUrl = string.IsNullOrWhiteSpace(cfg.BaseUrl) ? preset.DefaultBaseUrl : cfg.BaseUrl,
            Model = modelOverride ?? cfg.Model,
            ApiKey = store.GetApiKey(s, id),
            Temperature = s.Temperature,
            Timeout = TimeSpan.FromSeconds(s.TimeoutSeconds),
        };
    }
}

internal abstract class AiClientBase : IAiClient
{
    protected readonly HttpClient Http;
    protected readonly AiClientOptions O;

    protected AiClientBase(HttpClient http, AiClientOptions o) { Http = http; O = o; }

    public abstract Task<AiResult> CompleteAsync(PromptPair prompt, CancellationToken ct);
    public abstract Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct);

    protected string Url(string path) => O.BaseUrl.TrimEnd('/') + "/" + path.TrimStart('/');

    protected void Validate(bool needModel = true)
    {
        if (string.IsNullOrWhiteSpace(O.BaseUrl)) throw new AiException("No API address is set. Open Settings > AI model.");
        if (!Uri.TryCreate(O.BaseUrl, UriKind.Absolute, out var u) || (u.Scheme != "https" && u.Scheme != "http"))
            throw new AiException($"The API address \"{O.BaseUrl}\" is not a valid http(s) URL.");
        if (needModel && string.IsNullOrWhiteSpace(O.Model)) throw new AiException("No model is selected. Open Settings > AI model and pick one.");
    }

    protected async Task<JsonNode> SendJsonAsync(HttpRequestMessage req, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(O.Timeout);
        HttpResponseMessage resp;
        try
        {
            resp = await Http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiException($"The AI service did not answer within {O.Timeout.TotalSeconds:0} seconds.");
        }
        catch (HttpRequestException ex)
        {
            throw new AiException($"Could not reach {new Uri(O.BaseUrl).Host}: {ex.Message}", null, ex);
        }

        using (resp)
        {
            var body = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            JsonNode? node = null;
            try { node = string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body); } catch { }

            if (!resp.IsSuccessStatusCode)
                throw new AiException(DescribeHttpError(resp.StatusCode, ExtractErrorMessage(node) ?? Truncate(body, 300)), resp.StatusCode);
            return node ?? throw new AiException("The AI service returned an empty or unreadable response.");
        }
    }

    private static string? ExtractErrorMessage(JsonNode? n)
    {
        try
        {
            var err = n?["error"];
            if (err is JsonValue v) return v.ToString();
            return err?["message"]?.ToString() ?? n?["message"]?.ToString();
        }
        catch { return null; }
    }

    private string DescribeHttpError(HttpStatusCode code, string detail)
    {
        var hint = (int)code switch
        {
            400 => "The request was rejected.",
            401 or 403 => "The API key was refused. Check it in Settings.",
            404 => $"The model \"{O.Model}\" was not found. It may be retired or misspelled; use Refresh list in Settings.",
            429 => "Rate limit or quota reached. Wait a moment or check your plan.",
            >= 500 => "The AI service had a problem. Try again shortly.",
            _ => "The AI service returned an error.",
        };
        return $"{hint} (HTTP {(int)code}){(string.IsNullOrWhiteSpace(detail) ? "" : "\n\n" + detail.Trim())}";
    }

    protected static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    protected static StringContent JsonBody(JsonNode n) => new(n.ToJsonString(), Encoding.UTF8, "application/json");
}

internal sealed class GeminiClient : AiClientBase
{
    public GeminiClient(HttpClient http, AiClientOptions o) : base(http, o) { }

    private HttpRequestMessage Req(HttpMethod m, string path)
    {
        var r = new HttpRequestMessage(m, Url(path));
        if (!string.IsNullOrWhiteSpace(O.ApiKey)) r.Headers.Add("x-goog-api-key", O.ApiKey);
        return r;
    }

    public override async Task<AiResult> CompleteAsync(PromptPair prompt, CancellationToken ct)
    {
        Validate();
        if (string.IsNullOrWhiteSpace(O.ApiKey)) throw new AiException("No Gemini API key is set. Open Settings > AI model.");
        var body = new JsonObject
        {
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = prompt.User }),
            }),
        };
        if (!string.IsNullOrWhiteSpace(prompt.System))
            body["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = prompt.System }) };
        if (O.Temperature is { } t)
            body["generationConfig"] = new JsonObject { ["temperature"] = t };

        var model = O.Model.StartsWith("models/") ? O.Model[7..] : O.Model;
        using var req = Req(HttpMethod.Post, $"models/{Uri.EscapeDataString(model)}:generateContent");
        req.Content = JsonBody(body);
        var sw = Stopwatch.StartNew();
        var json = await SendJsonAsync(req, ct).ConfigureAwait(false);
        return new AiResult(ParseReply(json), model, sw.Elapsed);
    }

    internal static string ParseReply(JsonNode json)
    {
        var cand = json["candidates"]?[0];
        var parts = cand?["content"]?["parts"] as JsonArray;
        var sb = new StringBuilder();
        if (parts != null)
            foreach (var p in parts)
            {
                if (p?["thought"]?.GetValue<bool>() == true) continue; // skip reasoning summaries
                var t = p?["text"]?.ToString();
                if (!string.IsNullOrEmpty(t)) sb.Append(t);
            }
        if (sb.Length > 0) return sb.ToString();

        var block = json["promptFeedback"]?["blockReason"]?.ToString();
        if (!string.IsNullOrEmpty(block)) throw new AiException($"Gemini blocked the request ({block}).");
        var finish = cand?["finishReason"]?.ToString();
        if (!string.IsNullOrEmpty(finish) && finish != "STOP")
            throw new AiException($"Gemini returned no text (finish reason: {finish}).");
        throw new AiException("Gemini returned no text.");
    }

    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        Validate(needModel: false);
        var names = new List<string>();
        string? page = null;
        for (int i = 0; i < 20; i++)
        {
            var path = "models?pageSize=1000" + (page != null ? "&pageToken=" + Uri.EscapeDataString(page) : "");
            using var req = Req(HttpMethod.Get, path);
            var json = await SendJsonAsync(req, ct).ConfigureAwait(false);
            if (json["models"] is JsonArray arr)
                foreach (var m in arr)
                {
                    var methods = (m?["supportedGenerationMethods"] as JsonArray)?.Select(x => x?.ToString()) ?? Array.Empty<string?>();
                    var name = m?["name"]?.ToString();
                    if (name != null && methods.Contains("generateContent"))
                        names.Add(name.StartsWith("models/") ? name[7..] : name);
                }
            page = json["nextPageToken"]?.ToString();
            if (string.IsNullOrEmpty(page)) break;
        }
        return names.Distinct().OrderBy(ModelSortKey).ThenBy(n => n).ToList();
    }

    // Put text-generation Gemini models first; push embeddings/image/tts/etc. down.
    private static int ModelSortKey(string n) =>
        n.StartsWith("gemini-") && !n.Contains("tts") && !n.Contains("image") && !n.Contains("embedding") ? 0 : 1;
}

internal sealed class OpenAiCompatibleClient : AiClientBase
{
    public OpenAiCompatibleClient(HttpClient http, AiClientOptions o) : base(http, o) { }

    private HttpRequestMessage Req(HttpMethod m, string path)
    {
        var r = new HttpRequestMessage(m, Url(path));
        if (!string.IsNullOrWhiteSpace(O.ApiKey)) r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", O.ApiKey);
        if (O.ProviderId == "openrouter")
        {
            r.Headers.TryAddWithoutValidation("HTTP-Referer", "https://localhost/rewordly");
            r.Headers.TryAddWithoutValidation("X-Title", "Rewordly");
        }
        return r;
    }

    public override async Task<AiResult> CompleteAsync(PromptPair prompt, CancellationToken ct)
    {
        Validate();
        var messages = new JsonArray();
        if (!string.IsNullOrWhiteSpace(prompt.System))
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = prompt.System });
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = prompt.User });
        var body = new JsonObject { ["model"] = O.Model, ["messages"] = messages, ["stream"] = false };
        if (O.Temperature is { } t) body["temperature"] = t;

        using var req = Req(HttpMethod.Post, "chat/completions");
        req.Content = JsonBody(body);
        var sw = Stopwatch.StartNew();
        var json = await SendJsonAsync(req, ct).ConfigureAwait(false);
        return new AiResult(ParseReply(json), json["model"]?.ToString() ?? O.Model, sw.Elapsed);
    }

    internal static string ParseReply(JsonNode json)
    {
        var choice = json["choices"]?[0];
        var content = choice?["message"]?["content"];
        string? text = content switch
        {
            JsonValue v => v.ToString(),
            JsonArray a => string.Concat(a.Select(p => p?["type"]?.ToString() is null or "text" ? p?["text"]?.ToString() : null)),
            _ => null,
        };
        if (!string.IsNullOrWhiteSpace(text)) return text;
        var refusal = choice?["message"]?["refusal"]?.ToString();
        if (!string.IsNullOrWhiteSpace(refusal)) throw new AiException("The model refused: " + refusal);
        var finish = choice?["finish_reason"]?.ToString();
        throw new AiException("The model returned no text" + (string.IsNullOrEmpty(finish) ? "." : $" (finish reason: {finish})."));
    }

    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        Validate(needModel: false);
        using var req = Req(HttpMethod.Get, "models");
        var json = await SendJsonAsync(req, ct).ConfigureAwait(false);
        var arr = json["data"] as JsonArray ?? json["models"] as JsonArray;
        if (arr == null) return Array.Empty<string>();
        return arr.Select(m => m?["id"]?.ToString() ?? m?["name"]?.ToString())
                  .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!)
                  .Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
