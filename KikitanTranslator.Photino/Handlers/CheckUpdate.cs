using Newtonsoft.Json;

namespace KikitanTranslator.Photino.Handlers;

public class CheckUpdateRequest
{
    [JsonProperty("id")] public string Id { get; set; } = "";
}

public class CheckUpdate(Manager manager) : IHandler
{
    public async Task<string?> OnDataReceived(string data)
    {
        var request = JsonConvert.DeserializeObject<CheckUpdateRequest>(data);
        var id = request?.Id ?? "";

        var result = await manager.CheckForUpdateAsync();

        return FetchModels.Serialize(new
        {
            id,
            ok = result.Ok,
            has_update = result.HasUpdate,
            current_version = result.CurrentVersion,
            remote_version = result.RemoteVersion,
            error = result.Error
        });
    }
}
