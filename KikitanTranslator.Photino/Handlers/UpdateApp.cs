namespace KikitanTranslator.Photino.Handlers;

public class UpdateApp(Manager manager) : IHandler
{
    public Task<string?> OnDataReceived(string data)
    {
        manager.StartCloudUpdate();
        return Task.FromResult<string?>(null);
    }
}
