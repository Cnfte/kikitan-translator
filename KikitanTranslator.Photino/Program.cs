using System.Drawing;
using System.Runtime.InteropServices;
using KikitanTranslator.Base;
using KikitanTranslator.Photino;
using KikitanTranslator.Photino.Handlers;
using KikitanTranslator.Utility;
using Photino.NET;
using Photino.NET.Server;
using Serilog;
using Velopack;
using Velopack.Sources;

public class Program
{
    private static int width = 900;
    private static int height = 600;
    private static int minWidth = 640;
    private static int minHeight = 480;

    private static double oldRatio;
    private static Rectangle oldWorkArea;
    private static bool windowInitialized;
    private static bool lastZoomedState;
    private static string? lastMinMaxSummary;
    private static SubclassProc? _windowSubclassProc;

    [STAThread]
    public static void Main(string[] args)
    {
#if DEBUG
        string appUrl = "http://localhost:1420";
#else
        var exeDir = AppContext.BaseDirectory;
        Directory.SetCurrentDirectory(exeDir);
        PhotinoServer.CreateStaticFileServer(args, out string baseUrl).RunAsync();
        string appUrl = $"{baseUrl}/index.html?t={DateTime.UtcNow.Ticks}";
#endif
        bool noUI = Array.Exists(args, e => e.Trim().Contains("--no-ui"));
        
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Logger.Initialize();
        AppConfig.Load();
        if (!noUI) UiDebugLog.Initialize();

        VelopackApp.Build().Run();
        
        var connector = new Connector();
        var manager = new Manager(noUI, connector);
        var messageHandler = new MessageHandler();

        messageHandler.RegisterHandler("manual_translate", new ManualTranslate(manager));
        messageHandler.RegisterHandler("control", new Control(manager));
        messageHandler.RegisterHandler("update_config", new UpdateConfig(manager));
        messageHandler.RegisterHandler("send_app_state", new SendState(manager));
        messageHandler.RegisterHandler("quit", new Quit());
        messageHandler.RegisterHandler("open_url", new OpenURL());
        messageHandler.RegisterHandler("update", new UpdateApp(manager));
        messageHandler.RegisterHandler("check_update", new CheckUpdate(manager));
        messageHandler.RegisterHandler("fetch", new Fetch());
        messageHandler.RegisterHandler("fetch_models", new FetchModels());
        messageHandler.RegisterHandler("test_proxy", new TestProxy());
        messageHandler.RegisterHandler("overlay_action", new OverlayAction(manager));

        if (noUI)
        {
            connector.OnConnectorData += messageHandler.HandleMessage;

            Log.Information("[APP] No UI requested, starting the websocket");
            connector.StartWebsocket();

            var tcs = new TaskCompletionSource();
            
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; 
                tcs.TrySetResult();
            };

            tcs.Task.GetAwaiter().GetResult();
        }

        string windowTitle = "Kikitan Translator";

        var iconFile = OperatingSystem.IsWindows() ? "kikitan_logo.ico" : "icon.png";

        var window = new PhotinoWindow()
            .SetTitle(windowTitle)
            .SetUseOsDefaultSize(false)
            .SetUseOsDefaultLocation(false)
            .SetMinSize(minWidth, minHeight)
#if DEBUG
            .SetIconFile($"Resources/wwwroot/{iconFile}")
#else
            .SetContextMenuEnabled(false)
            .SetBrowserControlInitParameters("--disable-http-cache --disk-cache-size=0")
            .SetIconFile(Path.Combine(AppContext.BaseDirectory, "wwwroot", iconFile))
#endif
            .SetSize(new Size(width, height))
            .SetChromeless(true)
            .SetTransparent(true)
            .Center()
            .SetResizable(true)
            .SetLogVerbosity(0)
            .RegisterWebMessageReceivedHandler((sender, s) =>
            {
                var win = (PhotinoWindow)sender!;
                connector.WindowHandle = win;
                connector.WebViewReady = true;

                messageHandler.HandleMessage(s, connector);
            })
            .Load(appUrl);
        
        window.WindowCreated += (_, _) =>
        {
            if (OperatingSystem.IsWindows())
            {
                AttachBorderlessFrameSubclass(window.WindowHandle);
                try { window.SetTransparent(true); } catch { }
                ApplyWindowsGlassBackdrop(window.WindowHandle);
                AppConfig.OnUpdate += () =>
                {
                    if (windowInitialized && !connector.WindowClosing)
                    {
                        try { ApplyWindowsGlassBackdrop(window.WindowHandle); } catch { }
                    }
                };
            }
            UpdateResolution(window);
            
            window.Center();
            windowInitialized = true;
            if (OperatingSystem.IsWindows())
            {
                UiDebugLog.LogWindowGeometry(window.WindowHandle, "WindowCreated");
            }
        };

        window.WindowClosing += (_, _) =>
        {
            if (OperatingSystem.IsWindows())
            {
                UiDebugLog.LogWindowGeometry(window.WindowHandle, "WindowClosing");
            }
            connector.WindowClosing = true;
            connector.WebViewReady = false;
            connector.WindowHandle = null;
            return false;
        };

        window.WindowMaximized += (_, _) =>
        {
            if (windowInitialized && !connector.WindowClosing)
            {
                lastZoomedState = true;
                if (OperatingSystem.IsWindows()) UiDebugLog.LogWindowGeometry(window.WindowHandle, "Event:WindowMaximized");
                connector.Send(Newtonsoft.Json.JsonConvert.SerializeObject(new Message { Method = "window_state", Data = "maximized" }));
            }
        };

        window.WindowRestored += (_, _) =>
        {
            if (windowInitialized && !connector.WindowClosing)
            {
                bool zoomed = OperatingSystem.IsWindows() && IsZoomed(window.WindowHandle);
                if (!zoomed && lastZoomedState)
                {
                    lastZoomedState = false;
                    if (OperatingSystem.IsWindows()) UiDebugLog.LogWindowGeometry(window.WindowHandle, "Event:WindowRestored");
                    connector.Send(Newtonsoft.Json.JsonConvert.SerializeObject(new Message { Method = "window_state", Data = "restored" }));
                }
            }
        };
        
        window.WindowSizeChanged += (_, _) => {
            if (windowInitialized && !connector.WindowClosing)
            {
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        bool zoomed = IsZoomed(window.WindowHandle);
                        if (zoomed != lastZoomedState)
                        {
                            lastZoomedState = zoomed;
                            connector.Send(Newtonsoft.Json.JsonConvert.SerializeObject(new Message { 
                                Method = "window_state", 
                                Data = zoomed ? "maximized" : "restored" 
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[WIN] WindowSizeChanged query state failed: {ex.Message}");
                    }
                }
                UpdateResolution(window);
            }
        };

        window.WindowLocationChanged += (_, _) => {
            if (windowInitialized && !connector.WindowClosing) UpdateResolution(window);
        };

        window.WaitForClose();
    }

    /// <summary>
    /// Keeps the window at its logical size and makes sure it can never grow beyond the working
    /// area of its monitor - a window larger than that covers the taskbar when it is maximised.
    /// </summary>
    static void UpdateResolution(PhotinoWindow window)
    {
        double ratio = Math.Clamp((double)window.ScreenDpi / 96, 1.0, 4.0);
        Rectangle workArea = GetWorkArea(window);

        if (Math.Abs(ratio - oldRatio) < 0.001 && workArea == oldWorkArea) return;

        int maxWidth = Math.Max(minWidth, workArea.Width);
        int maxHeight = Math.Max(minHeight, workArea.Height);

        window.MinSize = new Point(Math.Min((int)Math.Round(minWidth * ratio), maxWidth),
                                  Math.Min((int)Math.Round(minHeight * ratio), maxHeight));

        // hard cap: not even a maximised window may reach over the taskbar
        window.MaxSize = new Point(maxWidth, maxHeight);

        if (!windowInitialized)
        {
            var desired = new Size((int)Math.Round(width * ratio), (int)Math.Round(height * ratio));
            window.Size = new Size(Math.Min(desired.Width, maxWidth), Math.Min(desired.Height, maxHeight));
        }

        oldRatio = ratio;
        oldWorkArea = workArea;
        UiDebugLog.Write("WIN32", $"UpdateResolution ratio={ratio:F2} workArea=({workArea.Left},{workArea.Top},{workArea.Width}x{workArea.Height}) MinSize={window.MinSize} MaxSize={window.MaxSize} Init={windowInitialized}");
    }

    /// <summary>Screen area of the window's monitor without the taskbar, in window pixels.</summary>
    static Rectangle GetWorkArea(PhotinoWindow window)
    {
        try
        {
            IntPtr monitor = MonitorFromWindow(window.WindowHandle, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

            if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
                return Rectangle.FromLTRB(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);
        }
        catch (Exception e)
        {
            Log.Warning($"[APP] Unable to read the monitor working area ({e.Message}); trying the primary monitor");
        }

        try
        {
            Rectangle main = window.MainMonitor.WorkArea;
            if (main.Width > 0 && main.Height > 0) return main;
        }
        catch (Exception e)
        {
            Log.Warning($"[APP] Unable to read the primary monitor working area ({e.Message})");
        }

        return Rectangle.Empty;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins pMarInset);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    private static bool TryGetMonitorInfo(IntPtr hWnd, out MonitorInfo info)
    {
        info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        IntPtr monitor = MonitorFromWindow(hWnd, MonitorDefaultToNearest);
        return monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info);
    }

    private static IntPtr BorderlessFrameSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        const uint WM_SIZE = 0x0005;
        const uint WM_ERASEBKGND = 0x0014;
        const uint WM_GETMINMAXINFO = 0x0024;
        const uint WM_NCDESTROY = 0x0082;
        const uint WM_NCCALCSIZE = 0x0083;
        const uint WM_NCPAINT = 0x0085;
        const uint WM_NCACTIVATE = 0x0086;
        const uint WM_ENTERSIZEMOVE = 0x0231;
        const uint WM_EXITSIZEMOVE = 0x0232;

        if (uMsg == WM_ERASEBKGND)
        {
            // Suppress GDI background brush erase during horizontal/vertical drags to prevent
            // flashing behind the asynchronously resizing WebView2 D3D swapchain.
            return (IntPtr)1;
        }

        if (uMsg == WM_NCPAINT)
        {
            return IntPtr.Zero;
        }

        if (uMsg == WM_NCACTIVATE)
        {
            return (IntPtr)1;
        }

        if (uMsg == WM_NCDESTROY)
        {
            if (_windowSubclassProc != null)
            {
                RemoveWindowSubclass(hWnd, _windowSubclassProc, uIdSubclass);
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        if (uMsg == WM_NCCALCSIZE && wParam != IntPtr.Zero)
        {
            if (IsZoomed(hWnd) && TryGetMonitorInfo(hWnd, out var info))
            {
                var rect = Marshal.PtrToStructure<NativeRect>(lParam);
                int left = Math.Max(rect.Left, info.Work.Left);
                int top = Math.Max(rect.Top, info.Work.Top);
                int right = Math.Min(rect.Right, info.Work.Right);
                int bottom = Math.Min(rect.Bottom, info.Work.Bottom);
                if (right > left && bottom > top)
                {
                    rect.Left = left;
                    rect.Top = top;
                    rect.Right = right;
                    rect.Bottom = bottom;
                    Marshal.StructureToPtr(rect, lParam, false);
                }
            }
            // Return 0 so client area spans edge-to-edge cleanly without non-client caption or 8px Acrylic gap
            return IntPtr.Zero;
        }

        if (uMsg == WM_GETMINMAXINFO && lParam != IntPtr.Zero)
        {
            IntPtr res = DefSubclassProc(hWnd, uMsg, wParam, lParam);
            try
            {
                var mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);
                if (TryGetMonitorInfo(hWnd, out var info))
                {
                    int workW = info.Work.Right - info.Work.Left;
                    int workH = info.Work.Bottom - info.Work.Top;
                    if (workW > 0 && workH > 0)
                    {
                        mmi.MaxPosition.X = info.Work.Left - info.Monitor.Left;
                        mmi.MaxPosition.Y = info.Work.Top - info.Monitor.Top;
                        mmi.MaxSize.X = workW;
                        mmi.MaxSize.Y = workH;
                        Marshal.StructureToPtr(mmi, lParam, false);
                    }
                }
                string summary = $"MinTrack=({mmi.MinTrackSize.X}x{mmi.MinTrackSize.Y}) MaxTrack=({mmi.MaxTrackSize.X}x{mmi.MaxTrackSize.Y}) MaxSize=({mmi.MaxSize.X}x{mmi.MaxSize.Y}) MaxPos=({mmi.MaxPosition.X},{mmi.MaxPosition.Y})";
                if (summary != lastMinMaxSummary)
                {
                    lastMinMaxSummary = summary;
                    UiDebugLog.Write("WIN32", $"WM_GETMINMAXINFO {summary}");
                }
            }
            catch { }
            return res;
        }

        if (uMsg == WM_ENTERSIZEMOVE)
        {
            UiDebugLog.LogWindowGeometry(hWnd, "WM_ENTERSIZEMOVE");
        }
        else if (uMsg == WM_EXITSIZEMOVE)
        {
            UiDebugLog.LogWindowGeometry(hWnd, "WM_EXITSIZEMOVE");
        }
        else if (uMsg == WM_SIZE)
        {
            int w = (int)(lParam.ToInt64() & 0xFFFF);
            int h = (int)((lParam.ToInt64() >> 16) & 0xFFFF);
            UiDebugLog.LogWindowGeometry(hWnd, $"WM_SIZE(type={(int)wParam}, {w}x{h})", throttle: (int)wParam == 0);
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private static void AttachBorderlessFrameSubclass(IntPtr hwnd)
    {
        try
        {
            const int GWL_STYLE = -16;
            const int GWL_EXSTYLE = -20;
            const int WS_POPUP = unchecked((int)0x80000000);
            const int WS_CLIPCHILDREN = 0x02000000;
            const int WS_CAPTION = 0x00C00000;
            const int WS_SYSMENU = 0x00080000;
            const int WS_THICKFRAME = 0x00040000;
            const int WS_MINIMIZEBOX = 0x00020000;
            const int WS_MAXIMIZEBOX = 0x00010000;
            const int WS_EX_LAYERED = 0x00080000;
            const int WS_EX_APPWINDOW = 0x00040000;
            const int WS_EX_WINDOWEDGE = 0x00000100;

            // Use WS_POPUP + WS_CLIPCHILDREN | WS_MINIMIZEBOX | WS_MAXIMIZEBOX while stripping
            // WS_CAPTION | WS_SYSMENU | WS_THICKFRAME so Windows 11 DWM never allocates a 29px
            // non-client caption plate (CapBounds == 0) or duplicate native caption glyphs.
            // Edge drag-resize works explicitly via WM_NCLBUTTONDOWN(HTLEFT..HTBOTTOMRIGHT).
            int style = GetWindowLong(hwnd, GWL_STYLE);
            int targetStyle = (style | WS_POPUP | WS_CLIPCHILDREN | WS_MINIMIZEBOX | WS_MAXIMIZEBOX)
                & ~(WS_CAPTION | WS_SYSMENU | WS_THICKFRAME);
            if (targetStyle != style)
            {
                SetWindowLong(hwnd, GWL_STYLE, targetStyle);
            }

            // Photino creates transparent windows with WS_EX_LAYERED; on Windows 11 DWM / WebView2
            // this freezes the hit-test / layered redirection boundary at the initial 900px width
            // after stretching rightward. Also ensure WS_EX_APPWINDOW stays set for WS_POPUP.
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            int targetExStyle = (exStyle | WS_EX_APPWINDOW) & ~(WS_EX_LAYERED | WS_EX_WINDOWEDGE);
            if (targetExStyle != exStyle)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, targetExStyle);
            }

            _windowSubclassProc = BorderlessFrameSubclassProc;
            if (SetWindowSubclass(hwnd, _windowSubclassProc, (UIntPtr)1, UIntPtr.Zero))
            {
                // Force Win32 to recalculate non-client area and flush ExStyle change
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x0027 /* SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER */);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[WIN] Could not attach borderless frame subclass: {ex.Message}");
        }
    }

    private static void ApplyWindowsGlassBackdrop(IntPtr hwnd)
    {
        try
        {
            var margins = new Margins { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);

            int cornerPref = 2; // DWMWCP_ROUND (Windows 11)
            DwmSetWindowAttribute(hwnd, 33, ref cornerPref, sizeof(int));

            int darkMode = (AppConfig.ConfigObject?.LightMode ?? false) ? 0 : 1;
            DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));

            int backdrop = 3; // DWMSBT_TRANSIENTWINDOW (Acrylic)
            int hr = DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int));

            // On Windows 11 build 22621+, DWMWA_SYSTEMBACKDROP_TYPE handles native Acrylic directly.
            // Only fall back to AccentPolicy on Windows 10 if DWM backdrop is not supported.
            if (hr != 0)
            {
                var policy = new AccentPolicy
                {
                    AccentState = 4, // ACCENT_ENABLE_ACRYLICBLURBEHIND
                    AccentFlags = 2,
                    GradientColor = 0x01111111
                };

                int size = Marshal.SizeOf(policy);
                IntPtr pPolicy = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(policy, pPolicy, false);
                    var data = new WindowCompositionAttributeData
                    {
                        Attribute = 19, // WCA_ACCENT_POLICY
                        Data = pPolicy,
                        SizeOfData = size
                    };
                    SetWindowCompositionAttribute(hwnd, ref data);
                }
                finally
                {
                    Marshal.FreeHGlobal(pPolicy);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[GLASS] Could not apply native glass backdrop: {ex.Message}");
        }
    }
}
