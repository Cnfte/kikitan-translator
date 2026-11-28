using KikitanTranslator.Utility;
using Serilog;

namespace KikitanTranslator.Photino.Handlers;

public class Fetch : IHandler
{
    public async Task<string?> OnDataReceived(string data)
    {
        try
        {
            using var client = NetworkClientFactory.Create(TimeSpan.FromSeconds(20));
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"KikitanTranslator/{CloudUpdater.CurrentAppVersion}");
            return await client.GetStringAsync(data);
        }
        catch (Exception e)
        {
            Log.Error($"[MSGH] Error when fetching from url {data}!: {e}");
            return "";
        }
    }
}
