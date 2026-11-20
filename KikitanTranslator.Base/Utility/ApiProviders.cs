using System.Net.Http;

namespace KikitanTranslator.Utility;

public enum ApiProviderKind
{
    OpenAiCompatible,
    Anthropic,
    Google
}

public enum ApiAuthStyle
{
    Bearer,
    Anthropic,
    Google
}

/// <summary>
/// How a provider turns its "thinking" / reasoning mode off, if it supports that at all.
/// </summary>
public enum ThinkingOffStyle
{
    None,
    ReasoningEffort,
    Thinking,
    ThinkingBudget
}

/// <summary>
/// Describes a translation provider: where its endpoints live, which configuration
/// fields hold the API key / model / custom base url and which models to suggest when
/// the model list cannot be retrieved.
/// </summary>
public sealed class ApiProviderInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string BaseUrl { get; init; }
    public required Func<ConfigObject, string> ApiKey { get; init; }
    public required Func<ConfigObject, string> Model { get; init; }

    public Func<ConfigObject, string> BaseUrlOverride { get; init; } = _ => "";
    public string ModelsPath { get; init; } = "/models";
    public string ChatPath { get; init; } = "/chat/completions";
    public ApiProviderKind Kind { get; init; } = ApiProviderKind.OpenAiCompatible;
    public ApiAuthStyle Auth { get; init; } = ApiAuthStyle.Bearer;

    /// <summary>
    /// Translation is a latency sensitive task, so thinking/reasoning is switched off for
    /// every provider that allows it (see <see cref="ThinkingOffStyle"/>).
    /// </summary>
    public ThinkingOffStyle ThinkingOff { get; init; } = ThinkingOffStyle.None;

    public bool CanDisableThinking => ThinkingOff != ThinkingOffStyle.None;
    public string DefaultModel { get; init; } = "";
    public string[] FallbackModels { get; init; } = [];
    public string KeyUrl { get; init; } = "";

    public string ResolveBaseUrl(ConfigObject config)
    {
        var custom = (BaseUrlOverride(config) ?? "").Trim();

        return (custom.Length > 0 ? custom : BaseUrl).TrimEnd('/');
    }

    public string ResolveModel(ConfigObject config)
    {
        var model = (Model(config) ?? "").Trim();

        return model.Length > 0 ? model : DefaultModel;
    }

    public string ModelsUrl(ConfigObject config) => ResolveBaseUrl(config) + ModelsPath;

    /// <summary>Model list url including the api key for providers that pass it as a query parameter.</summary>
    public string ModelsUrl(ConfigObject config, string apiKey) => WithApiKey(ModelsUrl(config), apiKey);

    public string ChatUrl(ConfigObject config) => ResolveBaseUrl(config) + ChatPath;

    /// <summary>Chat url for a concrete model (the Gemini style needs the model in the path).</summary>
    public string ChatUrl(ConfigObject config, string model) => Kind == ApiProviderKind.Google
        ? $"{ResolveBaseUrl(config)}/models/{model}:generateContent"
        : ResolveBaseUrl(config) + ChatPath;

    /// <summary>Appends the api key as a query parameter for providers that do not use headers.</summary>
    public string WithApiKey(string url, string apiKey) => Auth == ApiAuthStyle.Google
        ? url + (url.Contains('?') ? "&" : "?") + "key=" + Uri.EscapeDataString(apiKey)
        : url;

    public void ApplyAuth(HttpRequestMessage request, string apiKey)
    {
        switch (Auth)
        {
            case ApiAuthStyle.Anthropic:
                request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
                request.Headers.TryAddWithoutValidation("anthropic-version", ApiProviders.AnthropicVersion);

                break;
            case ApiAuthStyle.Google:
                // the key travels in the query string (see WithApiKey), no header needed
                break;
            default:
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

                break;
        }
    }
}

public static class ApiProviders
{
    public const string AnthropicVersion = "2023-06-01";

    public static readonly ApiProviderInfo Groq = new()
    {
        Id = "groq",
        Name = "Groq",
        BaseUrl = "https://api.groq.com/openai/v1",
        ApiKey = c => c.GroqApiKey,
        Model = c => c.GroqModel,
        BaseUrlOverride = c => c.GroqBaseUrl,
        ThinkingOff = ThinkingOffStyle.ReasoningEffort,
        DefaultModel = Constants.GROQ_MODEL,
        FallbackModels = [Constants.GROQ_MODEL, "llama-3.3-70b-versatile", "llama-3.1-8b-instant"],
        KeyUrl = "https://console.groq.com/keys"
    };

    public static readonly ApiProviderInfo OpenAi = new()
    {
        Id = "openai",
        Name = "OpenAI",
        BaseUrl = "https://api.openai.com/v1",
        ApiKey = c => c.OpenAiApiKey,
        Model = c => c.OpenAiModel,
        BaseUrlOverride = c => c.OpenAiBaseUrl,
        ThinkingOff = ThinkingOffStyle.ReasoningEffort,
        DefaultModel = "gpt-4o-mini",
        FallbackModels = ["gpt-4o-mini", "gpt-4o"],
        KeyUrl = "https://platform.openai.com/api-keys"
    };

    public static readonly ApiProviderInfo Claude = new()
    {
        Id = "claude",
        Name = "Claude",
        BaseUrl = "https://api.anthropic.com/v1",
        ChatPath = "/messages",
        Kind = ApiProviderKind.Anthropic,
        Auth = ApiAuthStyle.Anthropic,
        ApiKey = c => c.ClaudeApiKey,
        Model = c => c.ClaudeModel,
        BaseUrlOverride = c => c.ClaudeBaseUrl,
        ThinkingOff = ThinkingOffStyle.Thinking,
        DefaultModel = "claude-sonnet-4-5",
        FallbackModels = ["claude-sonnet-4-5", "claude-opus-5", "claude-haiku-4-5"],
        KeyUrl = "https://console.anthropic.com/settings/keys"
    };

    public static readonly ApiProviderInfo DeepSeek = new()
    {
        Id = "deepseek",
        Name = "DeepSeek",
        BaseUrl = "https://api.deepseek.com",
        ApiKey = c => c.DeepSeekApiKey,
        Model = c => c.DeepSeekModel,
        BaseUrlOverride = c => c.DeepSeekBaseUrl,
        ThinkingOff = ThinkingOffStyle.Thinking,
        DefaultModel = "deepseek-flash",
        FallbackModels = ["deepseek-flash", "deepseek-v4-pro"],
        KeyUrl = "https://platform.deepseek.com/api_keys"
    };

    public static readonly ApiProviderInfo Kimi = new()
    {
        Id = "kimi",
        Name = "Kimi",
        BaseUrl = "https://api.moonshot.cn/v1",
        ApiKey = c => c.KimiApiKey,
        Model = c => c.KimiModel,
        BaseUrlOverride = c => c.KimiBaseUrl,
        ThinkingOff = ThinkingOffStyle.Thinking,
        DefaultModel = "kimi-k2.6",
        FallbackModels = ["kimi-k2.6", "kimi-k3"],
        KeyUrl = "https://platform.moonshot.cn/console/api-keys"
    };

    public static readonly ApiProviderInfo Glm = new()
    {
        Id = "glm",
        Name = "GLM",
        BaseUrl = "https://open.bigmodel.cn/api/paas/v4",
        ApiKey = c => c.GlmApiKey,
        Model = c => c.GlmModel,
        BaseUrlOverride = c => c.GlmBaseUrl,
        ThinkingOff = ThinkingOffStyle.Thinking,
        // GLM-5.3 / GLM-5.3-FLASH force thinking and cannot switch it off, so the default is a
        // model that can be answered without reasoning.
        DefaultModel = "glm-5.2",
        FallbackModels = ["glm-5.2", "glm-5.1", "glm-5", "glm-4.7", "glm-4.6"],
        KeyUrl = "https://open.bigmodel.cn/usercenter/apikeys"
    };

    public static readonly ApiProviderInfo[] All = [Groq, OpenAi, Claude, DeepSeek, Kimi, Glm];

    public static ApiProviderInfo? Find(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    public const string CustomPrefix = "custom:";

    /// <summary>Translator indices at or above this value refer to a user added provider.</summary>
    public const int CustomIndexBase = 1000;

    /// <summary>
    /// Turns a provider the user added by hand into a descriptor. The protocol decides which
    /// API style is spoken: openai (chat completions), claude (messages) or gemini
    /// (generateContent).
    /// </summary>
    public static ApiProviderInfo FromCustom(CustomProviderConfig custom, string? protocolOverride = null)
    {
        var protocol = (protocolOverride ?? custom.Protocol ?? "").Trim().ToLowerInvariant();
        var id = CustomPrefix + custom.Id;
        var name = custom.DisplayName;

        switch (protocol)
        {
            case "claude":
            case "anthropic":
                return new ApiProviderInfo
                {
                    Id = id,
                    Name = name,
                    BaseUrl = custom.BaseUrl,
                    BaseUrlOverride = _ => custom.BaseUrl,
                    ChatPath = "/messages",
                    Kind = ApiProviderKind.Anthropic,
                    Auth = ApiAuthStyle.Anthropic,
                    ThinkingOff = ThinkingOffStyle.Thinking,
                    ApiKey = _ => custom.ApiKey,
                    Model = _ => custom.Model
                };
            case "gemini":
            case "google":
                return new ApiProviderInfo
                {
                    Id = id,
                    Name = name,
                    BaseUrl = custom.BaseUrl,
                    BaseUrlOverride = _ => custom.BaseUrl,
                    Kind = ApiProviderKind.Google,
                    Auth = ApiAuthStyle.Google,
                    ThinkingOff = ThinkingOffStyle.ThinkingBudget,
                    ApiKey = _ => custom.ApiKey,
                    Model = _ => custom.Model
                };
            default:
                return new ApiProviderInfo
                {
                    Id = id,
                    Name = name,
                    BaseUrl = custom.BaseUrl,
                    BaseUrlOverride = _ => custom.BaseUrl,
                    Kind = ApiProviderKind.OpenAiCompatible,
                    Auth = ApiAuthStyle.Bearer,
                    ThinkingOff = ThinkingOffStyle.ReasoningEffort,
                    ApiKey = _ => custom.ApiKey,
                    Model = _ => custom.Model
                };
        }
    }

    /// <summary>
    /// Resolves the providers the user configured himself. <paramref name="protocol"/> can
    /// override the stored protocol, which the "detect protocol" button uses.
    /// </summary>
    public static ApiProviderInfo? FindCustom(ConfigObject config, string? id, string? protocol = null)
    {
        var providers = config.CustomProviders ?? [];

        if (!string.IsNullOrEmpty(id) && id.StartsWith(CustomPrefix, StringComparison.OrdinalIgnoreCase)) id = id[CustomPrefix.Length..];

        CustomProviderConfig? custom = null;
        if (!string.IsNullOrEmpty(id)) custom = providers.FirstOrDefault(provider => provider.Id == id);
        if (custom == null && int.TryParse(id, out var index) && index >= 0 && index < providers.Count) custom = providers[index];

        return custom == null ? null : FromCustom(custom, protocol);
    }

    /// <summary>All custom providers of the configuration, wrapped as descriptors.</summary>
    public static List<ApiProviderInfo> CustomDescriptors(ConfigObject config) =>
        (config.CustomProviders ?? []).Select(custom => FromCustom(custom)).ToList();
}