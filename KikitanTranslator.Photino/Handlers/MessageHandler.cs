using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Photino.NET;
using Serilog;

namespace KikitanTranslator.Photino.Handlers;

public class Message
{
    [JsonProperty("method")] public string Method;
    [JsonProperty("data")] public string Data;
}

public class MessageHandler
{
    private Dictionary<string, IHandler> _handlers = new();

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public void RegisterHandler(string methodName, IHandler handler) => _handlers.Add(methodName, handler);

    public async Task<string?> HandleMessage(string msg, Connector conn)
    {
        try
        {
            Message? m = JsonConvert.DeserializeObject<Message>(msg);
            if (m == null)
            {
                Log.Error($"[MSGH] Unable to deserialize message");
                
                return null;
            }

            if (m.Method == "window_drag")
            {
                if (conn.WindowHandle != null && conn.WebViewReady && !conn.WindowClosing && OperatingSystem.IsWindows())
                {
                    try
                    {
                        ReleaseCapture();
                        SendMessage(conn.WindowHandle.WindowHandle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
                    }
                    catch { }
                }
                return null;
            }

            if (m.Method == "window_resize")
            {
                if (conn.WindowHandle != null && conn.WebViewReady && !conn.WindowClosing && OperatingSystem.IsWindows())
                {
                    if (int.TryParse(m.Data, out int hitTest) && hitTest >= 10 && hitTest <= 17)
                    {
                        try
                        {
                            ReleaseCapture();
                            SendMessage(conn.WindowHandle.WindowHandle, 0xA1, (IntPtr)hitTest, IntPtr.Zero);
                        }
                        catch { }
                    }
                }
                return null;
            }

            if (m.Method == "window_minimize")
            {
                if (conn.WindowHandle != null)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        try
                        {
                            ShowWindow(conn.WindowHandle.WindowHandle, 6 /* SW_MINIMIZE */);
                        }
                        catch { }
                    }
                    else
                    {
                        conn.WindowHandle.SetMinimized(true);
                    }
                }
                return null;
            }
            if (m.Method == "window_maximize")
            {
                if (conn.WindowHandle != null)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        try
                        {
                            IntPtr hWnd = conn.WindowHandle.WindowHandle;
                            bool isZoomed = IsZoomed(hWnd);
                            ShowWindow(hWnd, isZoomed ? 9 /* SW_RESTORE */ : 3 /* SW_MAXIMIZE */);
                            bool actualZoomed = IsZoomed(hWnd);
                            conn.Send(JsonConvert.SerializeObject(new Message
                            {
                                Method = "window_state",
                                Data = actualZoomed ? "maximized" : "restored"
                            }));
                        }
                        catch { }
                    }
                    else
                    {
                        conn.WindowHandle.Maximized = !conn.WindowHandle.Maximized;
                        conn.Send(JsonConvert.SerializeObject(new Message
                        {
                            Method = "window_state",
                            Data = conn.WindowHandle.Maximized ? "maximized" : "restored"
                        }));
                    }
                }
                return null;
            }
            if (m.Method == "window_close")
            {
                conn.WindowClosing = true;
                conn.WebViewReady = false;
                var win = conn.WindowHandle;
                conn.WindowHandle = null;
                if (win != null && OperatingSystem.IsWindows())
                {
                    try
                    {
                        PostMessage(win.WindowHandle, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch { }
                }
                try { win?.Close(); } catch { }
                Environment.Exit(0);
                return null;
            }
            if (m.Method == "get_window_state")
            {
                if (conn.WindowHandle != null && OperatingSystem.IsWindows())
                {
                    try
                    {
                        bool isZoomed = IsZoomed(conn.WindowHandle.WindowHandle);
                        conn.Send(JsonConvert.SerializeObject(new Message
                        {
                            Method = "window_state",
                            Data = isZoomed ? "maximized" : "restored"
                        }));
                    }
                    catch { }
                }
                return null;
            }

            foreach (var handler in _handlers)
            {
                if (handler.Key == m.Method)
                {
                    var d = await handler.Value.OnDataReceived(m.Data);
                    if (!string.IsNullOrEmpty(d)) conn.Send(JsonConvert.SerializeObject(new Message { Method = m.Method, Data = d }));
                    
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"[MSGH] An error occured while trying to handle the message [{msg}]: {e}");
        }

        return null;
    }
}
