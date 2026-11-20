using Newtonsoft.Json;

namespace KikitanTranslator.Utility;

/// <summary>
/// A single user configured proxy. Supported types: http, https, socks5 and socks4(a).
/// </summary>
public class ProxyConfig
{
    [JsonProperty("id")] public string Id { get; set; } = "";
    [JsonProperty("label")] public string Label { get; set; } = "";
    [JsonProperty("type")] public string Type { get; set; } = "http";
    [JsonProperty("host")] public string Host { get; set; } = "";
    [JsonProperty("port")] public int Port { get; set; }
    [JsonProperty("username")] public string Username { get; set; } = "";
    [JsonProperty("password")] public string Password { get; set; } = "";

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Host) && Port is > 0 and <= 65535;

    [JsonIgnore]
    public string NormalizedType => (Type ?? "").Trim().ToLowerInvariant() switch
    {
        "socks" or "socks5" or "socks5h" => "socks5",
        "socks4" or "socks4a" => "socks4",
        "https" => "https",
        _ => "http"
    };

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Label)
        ? $"{NormalizedType}://{Host}:{Port}"
        : $"{Label} ({NormalizedType}://{Host}:{Port})";
}