using System.Text;
using System.Text.Json;
using KikitanTranslator.Utility;

namespace KikitanTranslator.Base.Translators;

/// <summary>
/// Translator for every provider that speaks the OpenAI chat completions protocol
/// (OpenAI, Groq, DeepSeek, Kimi/Moonshot, GLM/Zhipu, ...).
/// </summary>
public class OpenAiChatTranslator(ApiProviderInfo provider) : HttpTranslatorBase(provider)
{
    protected override HttpRequestMessage BuildRequest(string apiKey, string model, string text, string source, string target, bool disableThinking)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = model,
            ["stream"] = false,
            ["messages"] = new object[]
            {
                new { role = "system", content = BuildPrompt(source, target) },
                new { role = "user", content = text }
            }
        };

        if (disableThinking)
        {
            switch (Provider.ThinkingOff)
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

        var request = new HttpRequestMessage(HttpMethod.Post, Provider.ChatUrl(AppConfig.ConfigObject));
        Provider.ApplyAuth(request, apiKey);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        return request;
    }

    protected override string? ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) return null;
        if (!choices[0].TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content)) return null;

        return ReadText(content)?.Trim();
    }
}