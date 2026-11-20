using Newtonsoft.Json;

namespace KikitanTranslator.Utility;

/// <summary>
/// A provider the user added by hand (relay services, self hosted gateways, ...).
/// </summary>
public class CustomProviderConfig
{
    [JsonProperty("id")] public string Id { get; set; } = "";
    [JsonProperty("name")] public string Name { get; set; } = "";

    /// <summary>openai, claude or gemini - the API style the endpoint speaks.</summary>
    [JsonProperty("protocol")] public string Protocol { get; set; } = "openai";

    [JsonProperty("base_url")] public string BaseUrl { get; set; } = "";
    [JsonProperty("api_key")] public string ApiKey { get; set; } = "";
    [JsonProperty("model")] public string Model { get; set; } = "";

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(BaseUrl) && Uri.TryCreate(BaseUrl, UriKind.Absolute, out _);

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? BaseUrl : Name;
}