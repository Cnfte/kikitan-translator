using System.Text;
using System.Text.Json;
using KikitanTranslator.Utility;

namespace KikitanTranslator.Base.Translators;

/// <summary>
/// Translator for Google's generative language REST API (generateContent). Used by custom
/// providers that speak the Gemini API style.
/// </summary>
public class GeminiChatTranslator(ApiProviderInfo provider) : HttpTranslatorBase(provider)
{
    protected override HttpRequestMessage BuildRequest(string apiKey, string model, string text, string source, string target, bool disableThinking)
    {
        var payload = new Dictionary<string, object>
        {
            ["systemInstruction"] = new { parts = new[] { new { text = BuildPrompt(source, target) } } },
            ["contents"] = new object[]
            {
                new { role = "user", parts = new[] { new { text } } }
            }
        };

        // Gemini calls it "thinking budget"; 0 switches it off (models that cannot be
        // switched off reject the whole request, which the base class retries without it).
        if (disableThinking) payload["generationConfig"] = new { thinkingConfig = new { thinkingBudget = 0 } };

        var url = Provider.WithApiKey(Provider.ChatUrl(AppConfig.ConfigObject, model), apiKey);
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        Provider.ApplyAuth(request, apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        return request;
    }

    protected override string? ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0) return null;
        if (!candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts)) return null;

        var text = string.Join("", parts.EnumerateArray()
            .Select(part => part.TryGetProperty("text", out var value) ? value.GetString() : null)
            .Where(value => !string.IsNullOrEmpty(value)));

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}