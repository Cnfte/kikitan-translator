using System.Net;
using System.Net.Http;
using Serilog;

namespace KikitanTranslator.Utility;

/// <summary>
/// Creates <see cref="HttpClient"/> instances for every outgoing API request.
/// When a proxy is selected in the settings, all traffic is tunneled through it (see
/// <see cref="ProxyTunnel"/>); otherwise a plain direct connection is used.
/// </summary>
public static class NetworkClientFactory
{
    /// <summary>
    /// Creates a client that uses the proxy selected in the configuration. The proxy is
    /// re-read on every call, so changes in the settings always apply to new requests.
    /// </summary>
    public static HttpClient Create(TimeSpan? timeout = null, ProxyConfig? proxyOverride = null)
    {
        var proxy = proxyOverride ?? ProxyTunnel.ActiveProxy();

        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(20),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true
        };

        if (proxy is { IsValid: true })
        {
            var selected = proxy;

            handler.ConnectCallback = (context, ct) => ProxyTunnel.ConnectAsync(context.DnsEndPoint, selected, ct);

            Log.Debug($"[NET]  Outgoing requests are routed through {selected.DisplayName}");
        }

        return new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(100) };
    }
}