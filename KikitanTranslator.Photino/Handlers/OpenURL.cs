using System.Diagnostics;
using KikitanTranslator.Utility;
using Serilog;

namespace KikitanTranslator.Photino.Handlers;

public class OpenURL : IHandler
{
    /// <summary>
    /// Only these hosts are allowed to be opened in the browser: the project links and the
    /// console pages where the user can create an API key for one of the translation providers.
    /// </summary>
    private static readonly string[] AllowedHosts =
    [
        "buymeacoffee.com", "booth.pm", "github.com", "twitter.com", "discord.gg",
        "console.groq.com", "aistudio.google.com", "platform.openai.com", "console.anthropic.com",
        "platform.deepseek.com", "platform.moonshot.cn", "open.bigmodel.cn"
    ];

    public async Task<string?> OnDataReceived(string data)
    {
        if (data == "LOGFILES")
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine([AppConfig.GetAppFolder(), "logs"]) + "\\",
                UseShellExecute = true,
                Verb = "open"
            });

            return null;
        }

        if (!Uri.TryCreate(data, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return null;
        if (!AllowedHosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
                                      uri.Host.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase)))
        {
            Log.Warning($"[URL]  Refused to open {uri.Host}: it is not part of the allow list");

            return null;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = data,
            UseShellExecute = true
        });

        return null;
    }
}