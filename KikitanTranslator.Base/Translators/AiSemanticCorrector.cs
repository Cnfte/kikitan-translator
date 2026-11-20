using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using KikitanTranslator.Utility;
using Serilog;

namespace KikitanTranslator.Base.Translators;

/// <summary>
/// Corrects speech-to-text transcription errors, homophones, typos and stuttering
/// via an AI provider before passing the text to translation or output.
/// </summary>
public static class AiSemanticCorrector
{
    private static readonly HttpClient _httpClient = NetworkClientFactory.Create(TimeSpan.FromSeconds(8));

    public static string? Correct(string text, string sourceLanguage, ConfigObject config)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        try
        {
            return CorrectAsync(text, sourceLanguage, config).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Warning($"[AI-CORRECT] Semantic correction exception: {ex.Message}");
            return text;
        }
    }

    private static async Task<string?> CorrectAsync(string text, string sourceLanguage, ConfigObject config)
    {
        var provider = ResolveProvider(config);
        if (provider == null)
        {
            Log.Warning("[AI-CORRECT] No AI provider available for semantic correction");
            return text;
        }

        var apiKey = provider.ApiKey(config);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Warning($"[AI-CORRECT] Provider {provider.Name} has no API key configured");
            return text;
        }

        var model = provider.ResolveModel(config);
        if (string.IsNullOrWhiteSpace(model))
        {
            Log.Warning($"[AI-CORRECT] Provider {provider.Name} has no model configured");
            return text;
        }

        var prompt = string.IsNullOrWhiteSpace(config.AiSemanticCorrectionPrompt)
            ? Constants.SEMANTIC_CORRECTION_PROMPT.Replace("[LANG_SRC]", sourceLanguage)
            : config.AiSemanticCorrectionPrompt.Replace("[LANG_SRC]", sourceLanguage);

        using var request = BuildRequest(provider, config, apiKey, model, text, prompt);
        var timer = Stopwatch.StartNew();

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Log.Warning($"[AI-CORRECT] Provider {provider.Name} returned status {(int)response.StatusCode}: {body}");
            return text;
        }

        var result = ParseResponse(provider, body);
        Log.Debug($"[AI-CORRECT] Completed in {timer.ElapsedMilliseconds}ms: '{text}' -> '{result}'");

        return string.IsNullOrWhiteSpace(result) ? text : result;
    }

    private static ApiProviderInfo? ResolveProvider(ConfigObject config)
    {
        var choice = config.AiSemanticCorrectionProvider?.Trim().ToLowerInvariant() ?? "auto";

        if (choice != "auto" && !string.IsNullOrEmpty(choice))
        {
            var specific = ApiProviders.Find(choice) ?? ApiProviders.FindCustom(config, choice);
            if (specific != null && !string.IsNullOrWhiteSpace(specific.ApiKey(config)))
            {
                return specific;
            }
        }

        // Auto resolution:
        // 1. If active translator is an AI provider, use it!
        if (config.Translator >= ApiProviders.CustomIndexBase)
        {
            var custom = ApiProviders.FindCustom(config, config.CustomProviderId)
                         ?? ApiProviders.FindCustom(config, (config.Translator - ApiProviders.CustomIndexBase).ToString());
            if (custom != null && !string.IsNullOrWhiteSpace(custom.ApiKey(config)))
                return custom;
        }
        else
        {
            var activeProvider = config.Translator switch
            {
                1 => ApiProviders.Groq,
                3 => ApiProviders.OpenAi,
                4 => ApiProviders.Claude,
                5 => ApiProviders.DeepSeek,
                6 => ApiProviders.Kimi,
                7 => ApiProviders.Glm,
                _ => null
            };

            if (activeProvider != null && !string.IsNullOrWhiteSpace(activeProvider.ApiKey(config)))
                return activeProvider;
        }

        // 2. Otherwise find the first configured provider that has an API key:
        foreach (var p in new[] { ApiProviders.DeepSeek, ApiProviders.Groq, ApiProviders.OpenAi, ApiProviders.Claude, ApiProviders.Kimi, ApiProviders.Glm })
        {
            if (!string.IsNullOrWhiteSpace(p.ApiKey(config))) return p;
        }

        foreach (var custom in config.CustomProviders ?? [])
        {
            if (!string.IsNullOrWhiteSpace(custom.ApiKey))
                return ApiProviders.FromCustom(custom);
        }

        return null;
    }

    private static HttpRequestMessage BuildRequest(ApiProviderInfo provider, ConfigObject config, string apiKey, string model, string text, string systemPrompt)
    {
        switch (provider.Kind)
        {
            case ApiProviderKind.Anthropic:
            {
                var payload = new Dictionary<string, object>
                {
                    ["model"] = model,
                    ["max_tokens"] = 1024,
                    ["system"] = systemPrompt,
                    ["messages"] = new object[]
                    {
                        new { role = "user", content = text }
                    }
                };

                var body = JsonSerializer.Serialize(payload);
                var request = new HttpRequestMessage(HttpMethod.Post, provider.ChatUrl(config));
                provider.ApplyAuth(request, apiKey);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                return request;
            }

            case ApiProviderKind.Google:
            {
                var payload = new Dictionary<string, object>
                {
                    ["systemInstruction"] = new { parts = new[] { new { text = systemPrompt } } },
                    ["contents"] = new object[]
                    {
                        new { role = "user", parts = new[] { new { text } } }
                    }
                };

                var url = provider.WithApiKey(provider.ChatUrl(config, model), apiKey);
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                provider.ApplyAuth(request, apiKey);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                return request;
            }

            default: // OpenAiCompatible
            {
                var payload = new Dictionary<string, object>
                {
                    ["model"] = model,
                    ["stream"] = false,
                    ["messages"] = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = text }
                    }
                };

                if (config.DisableThinking && provider.CanDisableThinking)
                {
                    switch (provider.ThinkingOff)
                    {
                        case ThinkingOffStyle.ReasoningEffort:
                            payload["reasoning_effort"] = "none";
                            break;
                        case ThinkingOffStyle.Thinking:
                            payload["thinking"] = new { type = "disabled" };
                            break;
                    }
                }

                var body = JsonSerializer.Serialize(payload);
                var request = new HttpRequestMessage(HttpMethod.Post, provider.ChatUrl(config));
                provider.ApplyAuth(request, apiKey);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                return request;
            }
        }
    }

    private static string? ParseResponse(ApiProviderInfo provider, string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (provider.Kind == ApiProviderKind.Google)
        {
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0) return null;
            if (!candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts)) return null;

            var text = string.Join("", parts.EnumerateArray()
                .Select(part => part.TryGetProperty("text", out var value) ? value.GetString() : null)
                .Where(value => !string.IsNullOrEmpty(value)));

            return CleanOutput(text);
        }

        if (provider.Kind == ApiProviderKind.Anthropic)
        {
            if (!doc.RootElement.TryGetProperty("content", out var content) || content.GetArrayLength() == 0) return null;
            var text = string.Join("", content.EnumerateArray()
                .Select(part => part.TryGetProperty("text", out var value) ? value.GetString() : null)
                .Where(value => !string.IsNullOrEmpty(value)));

            return CleanOutput(text);
        }

        // OpenAI compatible
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) return null;
        if (!choices[0].TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var contentProp)) return null;

        return CleanOutput(contentProp.GetString());
    }

    private static string? CleanOutput(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();
        if (trimmed.Length >= 2 && ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) || (trimmed.StartsWith('“') && trimmed.EndsWith('”'))))
        {
            trimmed = trimmed[1..^1].Trim();
        }
        return trimmed;
    }
}
