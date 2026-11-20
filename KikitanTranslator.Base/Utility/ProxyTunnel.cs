using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace KikitanTranslator.Utility;

/// <summary>
/// Thrown when a proxy server refuses or fails to establish a tunnel.
/// </summary>
public class ProxyException(string message) : Exception(message);

/// <summary>
/// Creates tunneled streams to a target host through HTTP/HTTPS/SOCKS proxies.
/// This is used as <see cref="SocketsHttpHandler.ConnectCallback"/> so that every
/// outgoing API request (translation, speech recognition, model listing, ...)
/// can be routed through the proxy the user selected in the settings.
/// </summary>
public static class ProxyTunnel
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(20);
    private const int MaxHeaderBytes = 16 * 1024;

    /// <summary>
    /// Returns the proxy that is currently selected in the configuration, or null when
    /// the user selected a direct connection (or the selection is no longer valid).
    /// </summary>
    public static ProxyConfig? ActiveProxy()
    {
        var cfg = AppConfig.ConfigObject;
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.ActiveProxy)) return null;

        var proxy = cfg.Proxies?.FirstOrDefault(p => p.Id == cfg.ActiveProxy);
        if (proxy == null || !proxy.IsValid)
        {
            Log.Warning($"[PROX] The selected proxy ({cfg.ActiveProxy}) is missing or invalid; falling back to a direct connection");

            return null;
        }

        return proxy;
    }

    public static async ValueTask<Stream> ConnectAsync(DnsEndPoint target, ProxyConfig proxy, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ConnectTimeout);
        var ct = cts.Token;

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(proxy.Host, proxy.Port, ct).ConfigureAwait(false);
            Stream stream = new NetworkStream(socket, true);

            if (proxy.NormalizedType == "https")
            {
                var ssl = new SslStream(stream, false);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = proxy.Host }, ct).ConfigureAwait(false);
                stream = ssl;
            }

            switch (proxy.NormalizedType)
            {
                case "http":
                case "https":
                    await HttpConnectAsync(stream, target, proxy, ct).ConfigureAwait(false);

                    break;
                case "socks5":
                    await Socks5ConnectAsync(stream, target, proxy, ct).ConfigureAwait(false);

                    break;
                case "socks4":
                    await Socks4ConnectAsync(stream, target, proxy, ct).ConfigureAwait(false);

                    break;
                default:
                    throw new ProxyException($"Unsupported proxy type '{proxy.Type}'");
            }

            Log.Debug($"[PROX] Tunnel established: {proxy.DisplayName} -> {target.Host}:{target.Port}");

            return stream;
        }
        catch (Exception e)
        {
            socket.Dispose();

            throw e is ProxyException or OperationCanceledException
                ? e
                : new ProxyException($"Unable to connect through {proxy.DisplayName}: {e.Message}");
        }
    }

    private static async Task HttpConnectAsync(Stream stream, DnsEndPoint target, ProxyConfig proxy, CancellationToken ct)
    {
        var request = new StringBuilder();
        request.Append($"CONNECT {target.Host}:{target.Port} HTTP/1.1\r\n");
        request.Append($"Host: {target.Host}:{target.Port}\r\n");
        request.Append("Proxy-Connection: Keep-Alive\r\n");

        if (!string.IsNullOrEmpty(proxy.Username))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{proxy.Username}:{proxy.Password}"));

            request.Append($"Proxy-Authorization: Basic {credentials}\r\n");
        }

        request.Append("\r\n");

        await stream.WriteAsync(Encoding.ASCII.GetBytes(request.ToString()), ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);

        var statusLine = await ReadLineAsync(stream, ct).ConfigureAwait(false);
        var total = statusLine.Length;

        while (true)
        {
            var line = await ReadLineAsync(stream, ct).ConfigureAwait(false);
            total += line.Length;
            if (total > MaxHeaderBytes) throw new ProxyException("HTTP proxy sent an oversized response header");

            if (line.Length == 0) break;
        }

        if (!statusLine.Contains(" 200")) throw new ProxyException($"HTTP proxy refused the tunnel: {statusLine}");
    }

    private static async Task Socks5ConnectAsync(Stream stream, DnsEndPoint target, ProxyConfig proxy, CancellationToken ct)
    {
        var useAuth = !string.IsNullOrEmpty(proxy.Username);

        await stream.WriteAsync(useAuth ? new byte[] { 0x05, 0x02, 0x00, 0x02 } : new byte[] { 0x05, 0x01, 0x00 }, ct).ConfigureAwait(false);

        var greeting = new byte[2];
        await stream.ReadExactlyAsync(greeting, ct).ConfigureAwait(false);

        if (greeting[0] != 0x05) throw new ProxyException($"SOCKS5: unexpected version {greeting[0]}");
        if (greeting[1] == 0xFF) throw new ProxyException("SOCKS5: the proxy rejected every authentication method");
        if (greeting[1] == 0x02) await Socks5AuthenticateAsync(stream, proxy, ct).ConfigureAwait(false);
        else if (greeting[1] != 0x00) throw new ProxyException($"SOCKS5: unsupported authentication method {greeting[1]}");

        var host = Encoding.ASCII.GetBytes(target.Host);
        var request = new List<byte> { 0x05, 0x01, 0x00, 0x03, (byte) host.Length };
        request.AddRange(host);
        request.Add((byte) (target.Port >> 8));
        request.Add((byte) (target.Port & 0xFF));

        await stream.WriteAsync(request.ToArray(), ct).ConfigureAwait(false);

        var reply = new byte[4];
        await stream.ReadExactlyAsync(reply, ct).ConfigureAwait(false);

        if (reply[0] != 0x05) throw new ProxyException($"SOCKS5: unexpected version {reply[0]} in the connect reply");
        if (reply[1] != 0x00) throw new ProxyException($"SOCKS5: connection refused ({Socks5Error(reply[1])})");

        switch (reply[3])
        {
            case 0x01:
                await stream.ReadExactlyAsync(new byte[4], ct).ConfigureAwait(false);

                break;
            case 0x04:
                await stream.ReadExactlyAsync(new byte[16], ct).ConfigureAwait(false);

                break;
            case 0x03:
                var length = new byte[1];
                await stream.ReadExactlyAsync(length, ct).ConfigureAwait(false);
                if (length[0] > 0) await stream.ReadExactlyAsync(new byte[length[0]], ct).ConfigureAwait(false);

                break;
            default:
                throw new ProxyException($"SOCKS5: unknown address type {reply[3]}");
        }

        await stream.ReadExactlyAsync(new byte[2], ct).ConfigureAwait(false);
    }

    private static async Task Socks5AuthenticateAsync(Stream stream, ProxyConfig proxy, CancellationToken ct)
    {
        var user = Encoding.UTF8.GetBytes(proxy.Username);
        var password = Encoding.UTF8.GetBytes(proxy.Password);

        if (user.Length > 255 || password.Length > 255) throw new ProxyException("SOCKS5: username/password is longer than 255 bytes");

        var request = new List<byte> { 0x01, (byte) user.Length };
        request.AddRange(user);
        request.Add((byte) password.Length);
        request.AddRange(password);

        await stream.WriteAsync(request.ToArray(), ct).ConfigureAwait(false);

        var reply = new byte[2];
        await stream.ReadExactlyAsync(reply, ct).ConfigureAwait(false);

        if (reply[1] != 0x00) throw new ProxyException("SOCKS5: authentication failed");
    }

    private static async Task Socks4ConnectAsync(Stream stream, DnsEndPoint target, ProxyConfig proxy, CancellationToken ct)
    {
        var host = Encoding.UTF8.GetBytes(target.Host);
        var user = Encoding.UTF8.GetBytes(proxy.Username ?? "");

        var request = new List<byte> { 0x04, 0x01, (byte) (target.Port >> 8), (byte) (target.Port & 0xFF) };
        request.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // SOCKS4a: invalid IP -> the proxy resolves the hostname below
        request.AddRange(user);
        request.Add(0x00);
        request.AddRange(host);
        request.Add(0x00);

        await stream.WriteAsync(request.ToArray(), ct).ConfigureAwait(false);

        var reply = new byte[8];
        await stream.ReadExactlyAsync(reply, ct).ConfigureAwait(false);

        if (reply[1] != 0x5A) throw new ProxyException($"SOCKS4: request rejected (0x{reply[1]:X2})");
    }

    private static string Socks5Error(byte code) => code switch
    {
        0x01 => "general failure",
        0x02 => "connection not allowed",
        0x03 => "network unreachable",
        0x04 => "host unreachable",
        0x05 => "connection refused",
        0x06 => "TTL expired",
        0x07 => "unsupported command",
        0x08 => "unsupported address type",
        _ => $"error {code}"
    };

    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var builder = new StringBuilder();
        var single = new byte[1];

        while (builder.Length < MaxHeaderBytes)
        {
            var read = await stream.ReadAsync(single, ct).ConfigureAwait(false);
            if (read == 0) break;

            if (single[0] == (byte) '\n')
            {
                if (builder.Length > 0 && builder[^1] == '\r') builder.Length--;

                break;
            }

            builder.Append((char) single[0]);
        }

        return builder.ToString();
    }
}