using System.Diagnostics;
using System.Net.Http;
using System.Text;
using KikitanTranslator.Utility;
using Serilog;

namespace KikitanTranslator.Photino;

public sealed record CloudUpdateInfo(
    string RemoteVersion,
    string ReleasesUrl,
    string ChangelogFile);

public static class CloudUpdater
{
    public static async Task<CloudUpdateInfo?> CheckForUpdateAsync(string currentVersion)
    {
        try
        {
            using var client = NetworkClientFactory.Create(TimeSpan.FromSeconds(15));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("KikitanTranslator-Updater/2.3.0");
            client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

            var url = $"{Constants.CLOUD_UPDATE_MANIFEST_URL}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            Log.Information($"[UPD] Checking cloud update manifest: {Constants.CLOUD_UPDATE_MANIFEST_URL}");

            using var resp = await client.GetAsync(url).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                Log.Warning($"[UPD] Manifest check failed with HTTP {(int)resp.StatusCode}");
                return null;
            }

            var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var parsed = ParseManifest(text);
            if (parsed == null)
            {
                Log.Warning("[UPD] Manifest missing valid VERSION or RELEASES_URL");
                return null;
            }

            bool hasNewer = IsNewerVersion(parsed.RemoteVersion, currentVersion);
            Log.Information($"[UPD] Version check: local={currentVersion}, remote={parsed.RemoteVersion}, hasUpdate={hasNewer}");

            return hasNewer ? parsed : null;
        }
        catch (Exception ex)
        {
            Log.Warning($"[UPD] Cloud update check failed: {ex.Message}");
            return null;
        }
    }

    public static CloudUpdateInfo? ParseManifest(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        string? version = null;
        string? releasesUrl = null;
        string changelogFile = Constants.DEFAULT_CHANGELOG_FILE;

        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';')) continue;

            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim().ToUpperInvariant();
            var value = line[(eq + 1)..].Trim().Trim('"');
            if (string.IsNullOrEmpty(value)) continue;

            switch (key)
            {
                case "VERSION":
                    version = value.TrimStart('v', 'V');
                    break;
                case "RELEASES_URL":
                    releasesUrl = value;
                    break;
                case "CHANGELOG":
                case "CHANGELOG_FILE":
                    changelogFile = value.TrimStart('/');
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(version) ||
            string.IsNullOrWhiteSpace(releasesUrl) ||
            !Uri.TryCreate(releasesUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        return new CloudUpdateInfo(version, releasesUrl, changelogFile);
    }

    public static bool IsNewerVersion(string remoteVersion, string localVersion)
    {
        if (!TryParseSemVer(remoteVersion, out var remote)) return false;
        if (!TryParseSemVer(localVersion, out var local)) return true;
        return remote > local;
    }

    private static bool TryParseSemVer(string? raw, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var clean = raw.Trim().TrimStart('v', 'V');
        int cut = clean.IndexOfAny(['-', '+', ' ']);
        if (cut > 0) clean = clean[..cut];

        var parts = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts.Length > 4) return false;

        var nums = new int[4];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out nums[i]) || nums[i] < 0) return false;
        }

        version = new Version(nums[0], nums[1], nums[2], nums[3]);
        return true;
    }

    public static async Task DownloadAndApplyAsync(
        CloudUpdateInfo info,
        Action<int, string, string?> reportProgress,
        Action onBeforeExit)
    {
        try
        {
            reportProgress(2, $"正在连接更新服务器 (v{info.RemoteVersion})...", null);

            string updateDir = Path.Combine(Path.GetTempPath(), "KikitanTranslatorUpdate");
            Directory.CreateDirectory(updateDir);

            using var client = NetworkClientFactory.Create(TimeSpan.FromMinutes(15));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("KikitanTranslator-Updater/2.3.0");

            using var response = await client.GetAsync(info.ReleasesUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;
            string urlPath = Uri.TryCreate(info.ReleasesUrl, UriKind.Absolute, out var u) ? u.AbsolutePath : "";
            string ext = Path.GetExtension(urlPath).ToLowerInvariant();
            if (ext != ".zip" && ext != ".exe") ext = ".exe";

            string downloadedPath = Path.Combine(updateDir, $"KikitanTranslator-Update-v{info.RemoteVersion}{ext}");

            await using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (var output = new FileStream(downloadedPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                long lastTick = 0;
                int bytesRead;

                while ((bytesRead = await input.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
                    totalRead += bytesRead;

                    long now = Environment.TickCount64;
                    if (now - lastTick >= 150)
                    {
                        lastTick = now;
                        double dlMb = totalRead / (1024.0 * 1024.0);
                        double totMb = totalBytes.HasValue ? totalBytes.Value / (1024.0 * 1024.0) : 0;
                        int pct = totalBytes is > 0 ? Math.Clamp((int)(5 + totalRead * 90.0 / totalBytes.Value), 5, 95) : 50;
                        string sizeText = totMb > 0 ? $"{dlMb:F1} / {totMb:F1} MB" : $"{dlMb:F1} MB";
                        reportProgress(pct, $"正在下载更新包 v{info.RemoteVersion} ({sizeText})...", null);
                    }
                }
            }

            // Sniff file header in case the URL did not include a file extension
            bool isZip = ext == ".zip";
            if (!isZip)
            {
                var magic = new byte[2];
                await using (var fs = File.OpenRead(downloadedPath))
                {
                    _ = await fs.ReadAsync(magic).ConfigureAwait(false);
                }
                if (magic[0] == (byte)'P' && magic[1] == (byte)'K')
                {
                    isZip = true;
                    string zipPath = Path.ChangeExtension(downloadedPath, ".zip");
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    File.Move(downloadedPath, zipPath);
                    downloadedPath = zipPath;
                }
            }

            reportProgress(98, "下载完成，正在启动自动更新程序...", null);
            string appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (isZip)
            {
                LaunchPortableZipUpdater(downloadedPath, appDir);
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = downloadedPath,
                    Arguments = $"--auto-update=\"{appDir}\"",
                    UseShellExecute = true
                });
            }

            reportProgress(100, "正在重启并应用更新...", null);
            await Task.Delay(350).ConfigureAwait(false);
            onBeforeExit();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[UPD] Cloud auto-update failed");
            reportProgress(0, "", $"更新下载或应用失败：{ex.Message}");
        }
    }

    private static void LaunchPortableZipUpdater(string zipPath, string targetDir)
    {
        string scriptPath = Path.Combine(Path.GetDirectoryName(zipPath)!, "apply_update.ps1");
        int currentPid = Environment.ProcessId;
        string script = $@"
$ErrorActionPreference = 'SilentlyContinue'
for ($i = 0; $i -lt 30; $i++) {{
    if (-not (Get-Process -Id {currentPid} -ErrorAction SilentlyContinue)) {{ break }}
    Start-Sleep -Milliseconds 250
}}
Get-Process KikitanTranslator.Overlay -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300
$target = '{targetDir.Replace("'", "''")}'
$cfg = Join-Path $target 'config.json'
$cfgBackup = $null
if (Test-Path $cfg) {{ $cfgBackup = Get-Content $cfg -Raw }}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead('{zipPath.Replace("'", "''")}')
try {{
    foreach ($entry in $archive.Entries) {{
        if ([string]::IsNullOrEmpty($entry.Name)) {{ continue }}
        $dest = Join-Path $target $entry.FullName
        $dir = Split-Path $dest -Parent
        if (-not (Test-Path $dir)) {{ New-Item -ItemType Directory -Path $dir -Force | Out-Null }}
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $dest, $true)
    }}
}} finally {{
    $archive.Dispose()
}}
if ($cfgBackup) {{ Set-Content -Path $cfg -Value $cfgBackup -Encoding UTF8 }}
$exe = Join-Path $target 'KikitanTranslator.Photino.exe'
if (Test-Path $exe) {{ Start-Process -FilePath $exe -WorkingDirectory $target }}
Remove-Item '{zipPath.Replace("'", "''")}' -Force
";
        File.WriteAllText(scriptPath, script, Encoding.UTF8);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }
}
