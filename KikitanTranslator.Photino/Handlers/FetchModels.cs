using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using KikitanTranslator.Utility;
using Newtonsoft.Json;
using Serilog;

namespace KikitanTranslator.Photino.Handlers;

public class ModelsRequest
{
    [JsonProperty("id")] public string Id { get; set; } = "";

    /// <summary>Optional protocol override, used by the "detect protocol" action of the ui.</summary>
    [JsonProperty("protocol")] public string? Protocol { get; set; }
    [JsonProperty("provider")] public string Provider { get; set; } = "";
}

/// <summary>
/// Retrieves the list of models a provider offers for the API key the user just typed in
/// (the settings page calls this as soon as an API key is entered).
/// </summary>
public class FetchModels : IHandler
{
    private static readonly string[] NonChatModels =
    [
        "embedding", "embed-", "rerank", "tokenizer", "whisper", "tts", "dall-e", "moderation",
        "audio", "transcribe", "realtime", "image", "video", "cogvideo", "ocr", "parser", "asr"
    ];

    public async Task<string?> OnDataReceived(string data)
    {
        var request = JsonConvert.DeserializeObject<ModelsRequest>(data);
        if (request == null) return null;

        var provider = ApiProviders.Find(request.Provider)
                       ?? ApiProviders.FindCustom(AppConfig.ConfigObject, request.Provider, request.Protocol);
        if (provider == null)
        {
            Log.Warning($"[MODEL] Model list requested for an unknown provider ({request.Provider})");

            return Serialize(new { id = request.Id, ok = false, error = "unknown_provider" });
        }

        var apiKey = provider.ApiKey(AppConfig.ConfigObject);
        if (string.IsNullOrWhiteSpace(apiKey)) return Serialize(new { id = request.Id, ok = false, error = "no_api_key" });

        var url = provider.ModelsUrl(AppConfig.ConfigObject, apiKey);
        var timer = Stopwatch.StartNew();

        try
        {
            using var client = NetworkClientFactory.Create(TimeSpan.FromSeconds(30));

            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            provider.ApplyAuth(message, apiKey);

            using var response = await client.SendAsync(message);
            var body = await response.Content.ReadAsStringAsync();

            Log.Information($"[MODEL] Model list response: provider={provider.Id}, status={(int)response.StatusCode}, elapsedMs={timer.ElapsedMilliseconds}");

            if (!response.IsSuccessStatusCode)
            {
                return Serialize(new
                {
                    id = request.Id,
                    ok = false,
                    error = $"http_{(int)response.StatusCode}",
                    status = (int)response.StatusCode,
                    message = Truncate(body)
                });
            }

            var models = ParseModels(body);
            if (models.Count == 0)
            {
                return Serialize(new
                {
                    id = request.Id,
                    ok = false,
                    error = "empty",
                    message = Truncate(body)
                });
            }

            return Serialize(new { id = request.Id, ok = true, models });
        }
        catch (Exception e)
        {
            Log.Error($"[MODEL] Unable to fetch the model list of {provider.Id}: {e}");

            return Serialize(new { id = request.Id, ok = false, error = "request_failed", message = Innermost(e).Message });
        }
    }

    private static List<string> ParseModels(string body)
    {
        var models = new List<string>();

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return models;

        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    models.Add(item.GetString() ?? "");

                    continue;
                }

                if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String) models.Add(id.GetString() ?? "");
                else if (item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String) models.Add(name.GetString() ?? "");
            }
        }
        else if (doc.RootElement.TryGetProperty("models", out var google) && google.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in google.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                {
                    models.Add(name.GetString()!.Replace("models/", ""));
                }
            }
        }

        return models
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model.Trim())
            .Where(model => !NonChatModels.Any(skip => model.Contains(skip, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string Serialize(object value) => JsonConvert.SerializeObject(value);

    internal static string Truncate(string value, int max = 300) => value.Length <= max ? value : value[..max] + "...";

    internal static Exception Innermost(Exception e)
    {
        while (e.InnerException != null) e = e.InnerException;

        return e;
    }
}