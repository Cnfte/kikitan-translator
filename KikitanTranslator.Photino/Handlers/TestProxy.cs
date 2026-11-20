using System.Diagnostics;
using System.Net.Http;
using KikitanTranslator.Utility;
using Newtonsoft.Json;
using Serilog;

namespace KikitanTranslator.Photino.Handlers;

public class ProxyTestRequest
{
    [JsonProperty("id")] public string Id { get; set; } = "";
    [JsonProperty("proxy")] public ProxyConfig? Proxy { get; set; }
}

/// <summary>
/// Connectivity check for a proxy the user just configured. The request is sent through
/// the proxy that is being edited, not through the currently selected one.
/// </summary>
public class TestProxy : IHandler
{
    private const string ProbeUrl = "https://www.gstatic.com/generate_204";

    public async Task<string?> OnDataReceived(string data)
    {
        var request = JsonConvert.DeserializeObject<ProxyTestRequest>(data);
        if (request == null) return null;

        var proxy = request.Proxy;
        if (proxy == null || !proxy.IsValid)
        {
            return FetchModels.Serialize(new { id = request.Id, ok = false, error = "invalid_proxy", message = "Host and port are required" });
        }

        var timer = Stopwatch.StartNew();

        try
        {
            using var client = NetworkClientFactory.Create(TimeSpan.FromSeconds(20), proxy);
            using var response = await client.GetAsync(ProbeUrl);

            var status = (int) response.StatusCode;
            Log.Information($"[PROX] Proxy test finished: proxy={proxy.DisplayName}, status={status}, elapsedMs={timer.ElapsedMilliseconds}");

            return FetchModels.Serialize(new
            {
                id = request.Id,
                ok = response.IsSuccessStatusCode,
                status,
                latency_ms = timer.ElapsedMilliseconds,
                error = response.IsSuccessStatusCode ? null : $"http_{status}"
            });
        }
        catch (Exception e)
        {
            var inner = FetchModels.Innermost(e);
            Log.Error($"[PROX] Proxy test failed: proxy={proxy.DisplayName}, elapsedMs={timer.ElapsedMilliseconds}, error={inner.Message}");

            return FetchModels.Serialize(new { id = request.Id, ok = false, error = "request_failed", message = inner.Message, latency_ms = timer.ElapsedMilliseconds });
        }
    }
}