namespace KikitanTranslator.Photino.Handlers;

public class OverlayAction(Manager manager) : IHandler
{
    public Task<string?> OnDataReceived(string data)
    {
        var cmd = (data ?? string.Empty).Trim().ToLowerInvariant();
        if (cmd == "preview") manager.PreviewOverlay();
        else if (cmd == "clear") manager.ClearOverlay();
        return Task.FromResult<string?>(null);
    }
}
