using System.Runtime.InteropServices;
using System.Text;
using KikitanTranslator.Utility;
using Serilog;

namespace KikitanTranslator.Photino;

/// <summary>
/// Thread-safe file logger dedicated to Win32 window geometry, WebView2 child bounds,
/// and frontend DOM layout telemetry during horizontal/vertical window resizing.
/// </summary>
public static class UiDebugLog
{
    private static readonly object _sync = new();
    private static string? _logFilePath;
    private static long _lastMoveLogTick;

    public static string LogFilePath => _logFilePath ??= InitPath();

    private static string InitPath()
    {
        var dir = Path.Combine(AppConfig.GetAppFolder(), "logs");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "ui_debug.log");
    }

    public static void Initialize()
    {
        try
        {
            var path = LogFilePath;
            lock (_sync)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024)
                {
                    File.WriteAllText(path, string.Empty, Encoding.UTF8);
                }

                var banner =
                    $"\r\n==================== UI DEBUG SESSION START {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} (PID={Environment.ProcessId}) ====================\r\n";
                File.AppendAllText(path, banner, Encoding.UTF8);
            }
            Log.Information($"[UI-DBG] Telemetry log initialized at {path}");
        }
        catch (Exception ex)
        {
            Log.Warning($"[UI-DBG] Failed to initialize ui_debug.log: {ex.Message}");
        }
    }

    public static void Write(string source, string message)
    {
        try
        {
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{source}] {message}\r\n";
            lock (_sync)
            {
                File.AppendAllText(LogFilePath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Never let debug telemetry crash the app
        }
    }

    public static void LogWindowGeometry(IntPtr hwnd, string reason, bool throttle = false)
    {
        if (hwnd == IntPtr.Zero || !OperatingSystem.IsWindows()) return;

        if (throttle)
        {
            long now = Environment.TickCount64;
            if (now - _lastMoveLogTick < 120) return;
            _lastMoveLogTick = now;
        }
        else
        {
            _lastMoveLogTick = Environment.TickCount64;
        }

        try
        {
            GetWindowRect(hwnd, out var wr);
            GetClientRect(hwnd, out var cr);
            uint dpi = GetDpiForWindow(hwnd);
            bool zoomed = IsZoomed(hwnd);
            bool iconic = IsIconic(hwnd);
            int style = GetWindowLong(hwnd, -16);

            var sb = new StringBuilder();
            sb.Append($"{reason} | DPI={dpi} Style=0x{style:X8} Zoomed={zoomed} Iconic={iconic} ");
            sb.Append($"Win=({wr.Left},{wr.Top})[{wr.Right - wr.Left}x{wr.Bottom - wr.Top}] ");
            sb.Append($"Client=[{cr.Right}x{cr.Bottom}]");

            var children = new List<string>();
            EnumChildWindows(hwnd, (ch, _) =>
            {
                var cls = new StringBuilder(128);
                GetClassName(ch, cls, 128);
                GetWindowRect(ch, out var cwr);
                children.Add($"0x{ch.ToInt64():X}({cls})[{cwr.Right - cwr.Left}x{cwr.Bottom - cwr.Top}@({cwr.Left - wr.Left},{cwr.Top - wr.Top})]");
                return children.Count < 8;
            }, IntPtr.Zero);

            if (children.Count > 0)
            {
                sb.Append(" Children=[").Append(string.Join(", ", children)).Append(']');
            }
            else
            {
                sb.Append(" Children=[]");
            }

            Write("WIN32", sb.ToString());
        }
        catch (Exception ex)
        {
            Write("WIN32", $"{reason} | Geometry query failed: {ex.Message}");
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
}
