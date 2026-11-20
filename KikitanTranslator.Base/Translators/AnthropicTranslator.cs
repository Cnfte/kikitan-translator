using System.Text;
using System.Text.Json;
using KikitanTranslator.Utility;

namespace KikitanTranslator.Base.Translators;

/// <summary>
/// Translator for Anthropic's messages API (Claude).
/// </summary>
public class AnthropicTranslator(ApiProviderInfo provider) : HttpTranslatorBase(provider)
{
    private const int MaxTokens = 4096;

    protected override HttpRequestMessage BuildRequest(string apiKey, string model, string text, string source, string target, bool disableThinking)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = model,
            ["max_tokens"] = MaxTokens,
            ["system"] = BuildPrompt(source, target),
            ["messages"] = new object[]
            {
                new { role = "user", content = text }
            }
        };

        if (disableThinking && Provider.CanDisableThinking) payload["thinking"] = new { type = "disabled" };

        var body = JsonSerializer.Serialize(payload);

        var request = new HttpRequestMessage(HttpMethod.Post, Provider.ChatUrl(AppConfig.ConfigObject));
        Provider.ApplyAuth(request, apiKey);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        return request;
    }

    protected override string? ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return null;

        var text = string.Join("", content.EnumerateArray()
            .Where(block => block.TryGetProperty("type", out var type) && type.GetString() == "text")
            .Select(block => block.TryGetProperty("text", out var value) ? value.GetString() : null)
            .Where(value => !string.IsNullOrEmpty(value)));

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}