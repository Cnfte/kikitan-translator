using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Win32;

namespace KikitanInstaller;

public enum DependencyId
{
    WebView2,
    DotNetDesktop9,
    AspNetCore9,
    VcRedistX64
}

public sealed class DependencyItem
{
    public required DependencyId Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public bool Required { get; init; } = true;
    public bool IsInstalled { get; set; }
    public string InstalledVersion { get; set; } = string.Empty;
    public string StatusNote { get; set; } = "等待检测...";
    public bool IsBusy { get; set; }
}

public static class DependencyManager
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(8)
    };

    static DependencyManager()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd($"KikitanTranslator-Installer/{MainWindow.AppVersion}");
    }

    public static List<DependencyItem> CreateDefaults() =>
    [
        new DependencyItem
        {
            Id = DependencyId.WebView2,
            Name = "Microsoft Edge WebView2 Runtime",
            Description = "承载主界面液态玻璃渲染与前端交互（必需）"
        },
        new DependencyItem
        {
            Id = DependencyId.DotNetDesktop9,
            Name = ".NET 9.0 Windows Desktop Runtime (x64)",
            Description = "运行主程序核心与 PC / SteamVR 桌面悬浮窗（必需）"
        },
        new DependencyItem
        {
            Id = DependencyId.AspNetCore9,
            Name = "ASP.NET Core 9.0 Runtime (x64)",
            Description = "驱动本地 Photino WebView 服务与通信端口（必需）"
        },
        new DependencyItem
        {
            Id = DependencyId.VcRedistX64,
            Name = "Visual C++ 2015–2022 Redistributable (x64)",
            Description = "支持 Silero VAD 语音检测、SkiaSharp 与本地网络库（必需）"
        }
    ];

    public static void DetectAll(IEnumerable<DependencyItem> items)
    {
        var dotnetRuntimes = ScanDotNetRuntimes();

        foreach (var item in items)
        {
            switch (item.Id)
            {
                case DependencyId.WebView2:
                {
                    var ver = GetWebView2Version();
                    item.IsInstalled = !string.IsNullOrEmpty(ver);
                    item.InstalledVersion = ver ?? string.Empty;
                    item.StatusNote = item.IsInstalled ? $"已安装 v{ver}" : "未检测到，需要安装";
                    break;
                }
                case DependencyId.DotNetDesktop9:
                {
                    var ver = FindLatestMajor9(dotnetRuntimes.WindowsDesktop);
                    item.IsInstalled = !string.IsNullOrEmpty(ver);
                    item.InstalledVersion = ver ?? string.Empty;
                    item.StatusNote = item.IsInstalled ? $"已安装 v{ver}" : "未检测到 9.0 x64，需要安装";
                    break;
                }
                case DependencyId.AspNetCore9:
                {
                    var ver = FindLatestMajor9(dotnetRuntimes.AspNetCore);
                    item.IsInstalled = !string.IsNullOrEmpty(ver);
                    item.InstalledVersion = ver ?? string.Empty;
                    item.StatusNote = item.IsInstalled ? $"已安装 v{ver}" : "未检测到 9.0 x64，需要安装";
                    break;
                }
                case DependencyId.VcRedistX64:
                {
                    var ver = GetVcRedistX64Version();
                    item.IsInstalled = !string.IsNullOrEmpty(ver);
                    item.InstalledVersion = ver ?? string.Empty;
                    item.StatusNote = item.IsInstalled ? $"已安装 v{ver}" : "未检测到 x64 运行库，需要安装";
                    break;
                }
            }
        }
    }

    public static async Task<(bool Success, string Message)> InstallAsync(
        DependencyItem item,
        IProgress<(int Percent, string Status)> progress,
        CancellationToken ct = default)
    {
        try
        {
            progress.Report((5, $"正在获取 {item.Name} 官方下载地址..."));

            var (url, fileName, args) = await ResolveInstallerSpecAsync(item.Id, ct);
            string tempDir = Path.Combine(Path.GetTempPath(), "KikitanInstallerDeps");
            Directory.CreateDirectory(tempDir);
            string localPath = Path.Combine(tempDir, fileName);

            progress.Report((10, $"正在下载 {fileName}..."));
            await DownloadFileWithProgressAsync(url, localPath, (pct, downloadedMb, totalMb) =>
            {
                int mapped = 10 + (int)(pct * 0.65);
                string sizeText = totalMb > 0 ? $"{downloadedMb:F1} / {totalMb:F1} MB" : $"{downloadedMb:F1} MB";
                progress.Report((Math.Clamp(mapped, 10, 75), $"正在下载 {item.Name} ({sizeText})..."));
            }, ct);

            progress.Report((80, $"正在交互式安装 {item.Name}（如弹出 UAC 权限提示请点击「是」）..."));

            var psi = new ProcessStartInfo
            {
                FileName = localPath,
                Arguments = args,
                UseShellExecute = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return (false, "无法启动依赖安装程序。");

            await proc.WaitForExitAsync(ct);

            // Exit code 0 = success, 3010 = success (reboot recommended), 1638 = newer version already installed
            bool ok = proc.ExitCode is 0 or 3010 or 1638;
            progress.Report((100, ok ? $"{item.Name} 安装完成。" : $"{item.Name} 安装程序退出码: {proc.ExitCode}"));

            return ok
                ? (true, proc.ExitCode == 3010 ? "安装成功（部分组件可能需要重启后完全生效）" : "安装成功")
                : (false, $"安装程序返回代码 {proc.ExitCode}，请确认是否取消了权限授权。");
        }
        catch (OperationCanceledException)
        {
            return (false, "已取消安装。");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static async Task<(string Url, string FileName, string Args)> ResolveInstallerSpecAsync(DependencyId id, CancellationToken ct)
    {
        return id switch
        {
            DependencyId.WebView2 => (
                "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                "MicrosoftEdgeWebview2Setup.exe",
                "/install"
            ),
            DependencyId.VcRedistX64 => (
                "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                "vc_redist.x64.exe",
                "/install /passive /norestart"
            ),
            DependencyId.DotNetDesktop9 => await ResolveDotNet9InstallerAsync("windowsdesktop", ct),
            DependencyId.AspNetCore9 => await ResolveDotNet9InstallerAsync("aspnetcore-runtime", ct),
            _ => throw new NotSupportedException($"Unsupported dependency {id}")
        };
    }

    private static async Task<(string Url, string FileName, string Args)> ResolveDotNet9InstallerAsync(string sectionName, CancellationToken ct)
    {
        const string metadataUrl = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/9.0/releases.json";
        try
        {
            using var resp = await Http.GetAsync(metadataUrl, ct);
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (doc.RootElement.TryGetProperty("releases", out var releases) && releases.GetArrayLength() > 0)
            {
                var latest = releases[0];
                if (latest.TryGetProperty(sectionName, out var section) &&
                    section.TryGetProperty("files", out var files))
                {
                    foreach (var file in files.EnumerateArray())
                    {
                        var rid = file.TryGetProperty("rid", out var rProp) ? rProp.GetString() : null;
                        var name = file.TryGetProperty("name", out var nProp) ? nProp.GetString() : null;
                        var url = file.TryGetProperty("url", out var uProp) ? uProp.GetString() : null;

                        if (string.Equals(rid, "win-x64", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrEmpty(name) &&
                            name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("hosting", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrEmpty(url))
                        {
                            return (url, name, "/install /passive /norestart");
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback to Microsoft's official aka.ms channel links if metadata lookup fails
        }

        return sectionName == "windowsdesktop"
            ? ("https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe", "windowsdesktop-runtime-9.0-win-x64.exe", "/install /passive /norestart")
            : ("https://aka.ms/dotnet/9.0/aspnetcore-runtime-win-x64.exe", "aspnetcore-runtime-9.0-win-x64.exe", "/install /passive /norestart");
    }

    private static async Task DownloadFileWithProgressAsync(
        string url,
        string destinationPath,
        Action<double, double, double> onProgress,
        CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        long lastReportTick = 0;
        int bytesRead;

        while ((bytesRead = await input.ReadAsync(buffer, ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalRead += bytesRead;

            long now = Environment.TickCount64;
            if (now - lastReportTick >= 120)
            {
                lastReportTick = now;
                double downloadedMb = totalRead / (1024.0 * 1024.0);
                double totalMb = totalBytes.HasValue ? totalBytes.Value / (1024.0 * 1024.0) : 0;
                double pct = totalBytes is > 0 ? Math.Clamp(totalRead * 100.0 / totalBytes.Value, 0, 100) : 50;
                onProgress(pct, downloadedMb, totalMb);
            }
        }

        double finalMb = totalRead / (1024.0 * 1024.0);
        onProgress(100, finalMb, finalMb);
    }

    private static string? GetWebView2Version()
    {
        string[] regPaths =
        [
            @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
            @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
        ];

        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var path in regPaths)
            {
                try
                {
                    using var key = root.OpenSubKey(path);
                    var pv = key?.GetValue("pv") as string;
                    if (!string.IsNullOrWhiteSpace(pv) && pv != "0.0.0.0") return pv;
                }
                catch { }
            }
        }
        return null;
    }

    private static string? GetVcRedistX64Version()
    {
        string[] paths =
        [
            @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
            @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
        ];

        foreach (var path in paths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(path);
                if (key == null) continue;
                var installed = key.GetValue("Installed");
                if (installed is int i && i == 1)
                {
                    var ver = key.GetValue("Version") as string;
                    if (!string.IsNullOrWhiteSpace(ver)) return ver.TrimStart('v', 'V');
                    var major = key.GetValue("Major");
                    var minor = key.GetValue("Minor");
                    var bld = key.GetValue("Bld");
                    return $"{major}.{minor}.{bld}";
                }
            }
            catch { }
        }
        return null;
    }

    private static (List<string> WindowsDesktop, List<string> AspNetCore) ScanDotNetRuntimes()
    {
        var desktop = new List<string>();
        var aspnet = new List<string>();

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string dotnetRoot = Path.Combine(programFiles, "dotnet", "shared");

        ReadRuntimeFolder(Path.Combine(dotnetRoot, "Microsoft.WindowsDesktop.App"), desktop);
        ReadRuntimeFolder(Path.Combine(dotnetRoot, "Microsoft.AspNetCore.App"), aspnet);

        if (desktop.Count == 0 || aspnet.Count == 0)
        {
            // Also check custom DOTNET_ROOT if configured
            string? customRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!string.IsNullOrWhiteSpace(customRoot))
            {
                ReadRuntimeFolder(Path.Combine(customRoot, "shared", "Microsoft.WindowsDesktop.App"), desktop);
                ReadRuntimeFolder(Path.Combine(customRoot, "shared", "Microsoft.AspNetCore.App"), aspnet);
            }
        }

        return (desktop, aspnet);
    }

    private static void ReadRuntimeFolder(string path, List<string> target)
    {
        try
        {
            if (!Directory.Exists(path)) return;
            foreach (var dir in Directory.GetDirectories(path))
            {
                var name = Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(name) && name.StartsWith("9.", StringComparison.Ordinal))
                {
                    target.Add(name);
                }
            }
        }
        catch { }
    }

    private static string? FindLatestMajor9(List<string> versions)
    {
        if (versions.Count == 0) return null;
        return versions
            .OrderByDescending(v => Version.TryParse(v.Split('-')[0], out var parsed) ? parsed : new Version(0, 0))
            .FirstOrDefault();
    }
}
