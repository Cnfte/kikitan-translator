using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using KikitanTranslator.Utility;
using Serilog;

namespace KikitanTranslator.Base.Translators;

/// <summary>
/// Shared plumbing for the HTTP based translators: retries, logging and API-key lookup.
/// Concrete translators only build the request and read the translation out of the response.
/// </summary>
public abstract class HttpTranslatorBase : ITranslator
{
    private const int MaxAttempts = 3;

    /// <summary>
    /// Providers (or single models) that answered "unsupported parameter" when we tried to
    /// switch thinking off. They are remembered so that the failing request is only sent once.
    /// </summary>
    private static readonly ConcurrentDictionary<string, byte> ThinkingOffRejected = new();

    protected readonly ApiProviderInfo Provider;

    private readonly HttpClient _httpClient;

    protected HttpTranslatorBase(ApiProviderInfo provider)
    {
        Provider = provider;
        _httpClient = NetworkClientFactory.Create(TimeSpan.FromSeconds(90));
    }

    protected string Tag => Provider.Id.ToUpperInvariant();

    public string? Translate(string text, string source, string target) => TranslateAsync(text, source, target, MaxAttempts).GetAwaiter().GetResult();

    private async Task<string?> TranslateAsync(string text, string source, string target, int attemptsLeft)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var timer = Stopwatch.StartNew();
        var attempt = MaxAttempts - attemptsLeft + 1;

        var apiKey = Provider.ApiKey(AppConfig.ConfigObject);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error($"[{Tag}] No API key configured");

            return null;
        }

        var model = Provider.ResolveModel(AppConfig.ConfigObject);
        if (string.IsNullOrWhiteSpace(model))
        {
            Log.Error($"[{Tag}] No model configured");

            return null;
        }

        // Translation has to be fast, so thinking/reasoning is disabled unless the user
        // explicitly wants it or the model does not support switching it off.
        var disableThinking = AppConfig.ConfigObject.DisableThinking && Provider.CanDisableThinking &&
                              !ThinkingOffRejected.ContainsKey($"{Provider.Id}|{model}");

        try
        {
            using var request = BuildRequest(apiKey, model, text, source, target, disableThinking);

            Log.Debug($"[{Tag}] Translation attempt started: request={requestId}, attempt={attempt}/{MaxAttempts}, model={model}, chars={text.Length}, source={source}, target={target}");

            using var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Log.Debug($"[{Tag}] Translation response: request={requestId}, attempt={attempt}/{MaxAttempts}, status={(int)response.StatusCode}, elapsedMs={timer.ElapsedMilliseconds}");

            if (!response.IsSuccessStatusCode)
            {
                var status = (int) response.StatusCode;
                Log.Error($"[{Tag}] Translation API error {status}: {Truncate(body)}");

                // Models that do not support the "no thinking" parameter reject the whole
                // request with a 400; remember that and repeat the request without it.
                if (disableThinking && status == 400)
                {
                    ThinkingOffRejected.TryAdd($"{Provider.Id}|{model}", 0);
                    Log.Warning($"[{Tag}] {model} does not accept the parameter that disables thinking; retrying without it");

                    return await TranslateAsync(text, source, target, attemptsLeft);
                }

                if (attemptsLeft > 1)
                {
                    await Task.Delay(1000);

                    return await TranslateAsync(text, source, target, attemptsLeft - 1);
                }

                return null;
            }

            var translated = ParseResponse(body);
            if (translated == null) Log.Error($"[{Tag}] Unable to read the translation out of the response: {Truncate(body)}");

            return translated;
        }
        catch (Exception e)
        {
            Log.Error(e, $"[{Tag}] Translation failed: request={requestId}, attempt={attempt}/{MaxAttempts}, elapsedMs={timer.ElapsedMilliseconds}");

            return null;
        }
    }

    protected abstract HttpRequestMessage BuildRequest(string apiKey, string model, string text, string source, string target, bool disableThinking);

    protected abstract string? ParseResponse(string body);

    protected static string BuildPrompt(string source, string target) =>
        Constants.TRANSLATION_PROMPT.Replace("LANG_SRC", source).Replace("LANG_TARGET", target);

    protected static string? ReadText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Array => string.Join("", element.EnumerateArray().Select(ReadText).Where(v => !string.IsNullOrEmpty(v))),
        _ => null
    };

    private static string Truncate(string value, int max = 500) => value.Length <= max ? value : value[..max] + "...";

    public void Dispose() => _httpClient.Dispose();
}