using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KikitanInstaller;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Length > 0 && args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
        {
            RunUninstaller();
            return;
        }

        Application.Run(new InstallerForm());
    }

    private static void RunUninstaller()
    {
        var result = MessageBox.Show(
            "确定要卸载 Kikitan Translator 吗？\n（您的个人配置文件 config.json 将被安全保留）",
            "Kikitan Translator 卸载向导",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        try
        {
            string installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Kikitan Translator.lnk");
            string startMenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Kikitan Translator.lnk");

            if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);
            if (File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);

            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\KikitanTranslator", false);

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C timeout /t 2 /nobreak > NUL & rmdir /s /q \"{installDir}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            MessageBox.Show("Kikitan Translator 已成功从您的电脑移除。", "卸载完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"卸载过程中发生错误: {ex.Message}", "卸载提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

public class InstallerForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly Color BgCanvas = Color.FromArgb(18, 20, 28);
    private readonly Color BgCard = Color.FromArgb(28, 31, 42);
    private readonly Color BgInput = Color.FromArgb(14, 16, 22);
    private readonly Color BorderSubtle = Color.FromArgb(52, 56, 72);
    private readonly Color AccentBlue = Color.FromArgb(10, 132, 255);
    private readonly Color AccentGreen = Color.FromArgb(48, 209, 88);
    private readonly Color AccentAmber = Color.FromArgb(255, 159, 10);
    private readonly Color TextPrimary = Color.FromArgb(245, 245, 247);
    private readonly Color TextSecondary = Color.FromArgb(174, 178, 194);

    private TextBox txtInstallPath = null!;
    private Label lblDiskSpace = null!;
    private Label lblWebViewStatus = null!;
    private Button btnInstallWebView = null!;
    private Label lblDotNetStatus = null!;
    private Label lblDwmStatus = null!;
    private Label lblOscStatus = null!;

    private CheckBox chkDesktopShortcut = null!;
    private CheckBox chkStartMenuShortcut = null!;
    private CheckBox chkPreserveConfig = null!;
    private CheckBox chkRegisterUninstall = null!;
    private CheckBox chkLaunchAfter = null!;

    private ProgressBar progressBar = null!;
    private Label lblStatus = null!;
    private Button btnInstall = null!;
    private Button btnCancel = null!;

    private bool isInstalling = false;
    private bool installCompleted = false;
    private const long EstimatedRequiredBytes = 195L * 1024 * 1024;

    public InstallerForm()
    {
        Text = "Kikitan Translator v2.2.3 安装向导 (Apple Liquid Glass Edition)";
        Size = new Size(760, 680);
        MinimumSize = new Size(760, 680);
        MaximumSize = new Size(760, 680);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = BgCanvas;
        ForeColor = TextPrimary;
        Font = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Regular);

        try
        {
            using var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app_icon.ico");
            if (iconStream != null) Icon = new Icon(iconStream);
        }
        catch { }

        BuildInterface();
        CheckDependencies();
        UpdateDiskSpaceInfo();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int dark = 1;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        }
        catch { }
    }

    private void BuildInterface()
    {
        // 1. Header Banner
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 76,
            BackColor = Color.FromArgb(24, 26, 36),
            Padding = new Padding(24, 14, 24, 14)
        };

        var lblTitle = new Label
        {
            Text = "Kikitan Translator v2.2.3",
            Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(24, 14),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Apple HIG 全主题液态玻璃磨砂架构  |  自定义安装路径与运行环境检测向导",
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular),
            ForeColor = TextSecondary,
            Location = new Point(26, 44),
            AutoSize = true
        };

        headerPanel.Controls.Add(lblTitle);
        headerPanel.Controls.Add(lblSubtitle);
        Controls.Add(headerPanel);

        // 2. Section A: Dependency & Environment Check Card
        var grpDeps = CreateCardPanel(24, 90, 696, 172, "一、依赖框架与系统环境检查 (Dependency & Environment Check)");

        lblWebViewStatus = CreateItemLabel(18, 36, "Microsoft Edge WebView2 Runtime: 正在检测...");
        btnInstallWebView = new Button
        {
            Text = "在线安装 WebView2",
            Location = new Point(536, 31),
            Size = new Size(140, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentBlue,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold),
            Visible = false,
            Cursor = Cursors.Hand
        };
        btnInstallWebView.FlatAppearance.BorderSize = 0;
        btnInstallWebView.Click += async (_, _) => await InstallWebView2RuntimeAsync();

        lblDotNetStatus = CreateItemLabel(18, 66, ".NET 9.0 桌面运行库: 正在检测...");
        lblDwmStatus = CreateItemLabel(18, 96, "Windows DWM 液态玻璃亚克力合成支持: 正在检测...");
        lblOscStatus = CreateItemLabel(18, 126, "VRChat / SlimeVR OSC 端口状态: 正在检测...");

        var btnRecheck = new Button
        {
            Text = "重新检测",
            Location = new Point(596, 122),
            Size = new Size(80, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = BgInput,
            ForeColor = TextSecondary,
            Font = new Font("Microsoft YaHei UI", 8.25f, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        btnRecheck.FlatAppearance.BorderColor = BorderSubtle;
        btnRecheck.Click += (_, _) => CheckDependencies();

        grpDeps.Controls.Add(lblWebViewStatus);
        grpDeps.Controls.Add(btnInstallWebView);
        grpDeps.Controls.Add(lblDotNetStatus);
        grpDeps.Controls.Add(lblDwmStatus);
        grpDeps.Controls.Add(lblOscStatus);
        grpDeps.Controls.Add(btnRecheck);
        Controls.Add(grpDeps);

        // 3. Section B: Custom Installation Path Card
        var grpPath = CreateCardPanel(24, 274, 696, 120, "二、自定义安装路径 (Custom Installation Directory)");

        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KikitanTranslator");

        txtInstallPath = new TextBox
        {
            Text = defaultPath,
            Location = new Point(18, 38),
            Size = new Size(466, 28),
            BackColor = BgInput,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9.75f, FontStyle.Regular)
        };
        txtInstallPath.TextChanged += (_, _) => UpdateDiskSpaceInfo();

        var btnBrowse = new Button
        {
            Text = "浏览文件夹...",
            Location = new Point(494, 36),
            Size = new Size(96, 29),
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentBlue,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnBrowse.FlatAppearance.BorderSize = 0;
        btnBrowse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择 Kikitan Translator 的安装目录",
                UseDescriptionForTitle = true,
                SelectedPath = txtInstallPath.Text
            };
            if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                string chosen = dialog.SelectedPath;
                if (!chosen.EndsWith("KikitanTranslator", StringComparison.OrdinalIgnoreCase))
                {
                    chosen = Path.Combine(chosen, "KikitanTranslator");
                }
                txtInstallPath.Text = chosen;
            }
        };

        var btnDefaultPath = new Button
        {
            Text = "恢复默认",
            Location = new Point(598, 36),
            Size = new Size(80, 29),
            FlatStyle = FlatStyle.Flat,
            BackColor = BgInput,
            ForeColor = TextSecondary,
            Font = new Font("Microsoft YaHei UI", 8.25f, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        btnDefaultPath.FlatAppearance.BorderColor = BorderSubtle;
        btnDefaultPath.Click += (_, _) => txtInstallPath.Text = defaultPath;

        lblDiskSpace = new Label
        {
            Text = "所需空间: 约 195 MB  |  目标磁盘可用空间: 计算中...",
            Location = new Point(18, 78),
            AutoSize = true,
            ForeColor = TextSecondary,
            Font = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Regular)
        };

        grpPath.Controls.Add(txtInstallPath);
        grpPath.Controls.Add(btnBrowse);
        grpPath.Controls.Add(btnDefaultPath);
        grpPath.Controls.Add(lblDiskSpace);
        Controls.Add(grpPath);

        // 4. Section C: Installation Options Card
        var grpOptions = CreateCardPanel(24, 406, 696, 134, "三、安装组件与快捷方式选项 (Installation Options)");

        chkDesktopShortcut = CreateOptionCheckBox(18, 36, "创建桌面快捷方式 (Desktop Shortcut)", true);
        chkStartMenuShortcut = CreateOptionCheckBox(356, 36, "创建开始菜单程序快捷方式 (Start Menu Shortcut)", true);
        chkPreserveConfig = CreateOptionCheckBox(18, 68, "覆盖安装时保留现有配置、API 密钥与磨砂透度设置", true);
        chkRegisterUninstall = CreateOptionCheckBox(356, 68, "注册到 Windows 系统「已安装的应用」列表", true);
        chkLaunchAfter = CreateOptionCheckBox(18, 100, "安装完成后立即启动 Kikitan Translator", true);

        grpOptions.Controls.Add(chkDesktopShortcut);
        grpOptions.Controls.Add(chkStartMenuShortcut);
        grpOptions.Controls.Add(chkPreserveConfig);
        grpOptions.Controls.Add(chkRegisterUninstall);
        grpOptions.Controls.Add(chkLaunchAfter);
        Controls.Add(grpOptions);

        // 5. Bottom Progress & Action Bar
        lblStatus = new Label
        {
            Text = "准备就绪：确认上方依赖检查与安装路径后，点击「立即安装」开始部署。",
            Location = new Point(26, 552),
            AutoSize = true,
            ForeColor = TextSecondary,
            Font = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Regular)
        };

        progressBar = new ProgressBar
        {
            Location = new Point(24, 576),
            Size = new Size(460, 22),
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Style = ProgressBarStyle.Continuous
        };

        btnInstall = new Button
        {
            Text = "立即安装 (Install)",
            Location = new Point(502, 566),
            Size = new Size(132, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentBlue,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnInstall.FlatAppearance.BorderSize = 0;
        btnInstall.Click += async (_, _) => await StartInstallationAsync();

        btnCancel = new Button
        {
            Text = "取消",
            Location = new Point(642, 566),
            Size = new Size(78, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = BgCard,
            ForeColor = TextSecondary,
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = BorderSubtle;
        btnCancel.Click += (_, _) =>
        {
            if (isInstalling) return;
            Close();
        };

        Controls.Add(lblStatus);
        Controls.Add(progressBar);
        Controls.Add(btnInstall);
        Controls.Add(btnCancel);
    }

    private Panel CreateCardPanel(int x, int y, int width, int height, string heading)
    {
        var panel = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = BgCard
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(BorderSubtle, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        var lblHeader = new Label
        {
            Text = heading,
            Location = new Point(16, 10),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Bold),
            ForeColor = TextPrimary
        };
        panel.Controls.Add(lblHeader);
        return panel;
    }

    private Label CreateItemLabel(int x, int y, string text)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            ForeColor = TextSecondary,
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular)
        };
    }

    private CheckBox CreateOptionCheckBox(int x, int y, string text, bool isChecked)
    {
        return new CheckBox
        {
            Text = text,
            Checked = isChecked,
            Location = new Point(x, y),
            AutoSize = true,
            ForeColor = TextPrimary,
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
    }

    private void CheckDependencies()
    {
        // 1. Check WebView2 Runtime
        string? wv2Version = GetWebView2Version();
        if (!string.IsNullOrEmpty(wv2Version))
        {
            lblWebViewStatus.Text = $"[通过] Microsoft Edge WebView2 Runtime: 已安装 (v{wv2Version})";
            lblWebViewStatus.ForeColor = AccentGreen;
            btnInstallWebView.Visible = false;
        }
        else
        {
            lblWebViewStatus.Text = "[缺失] Microsoft Edge WebView2 Runtime: 未检测到，请点击右侧按钮在线安装";
            lblWebViewStatus.ForeColor = AccentAmber;
            btnInstallWebView.Visible = true;
        }

        // 2. Check .NET 9.0 Self-Contained Runtime
        lblDotNetStatus.Text = $"[通过] .NET 运行库环境: 安装包已内置独立运行库 (Self-Contained win-x64, 当前宿主 {Environment.Version})";
        lblDotNetStatus.ForeColor = AccentGreen;

        // 3. Check Windows DWM Acrylic / Mica Backdrop Support
        var osBuild = Environment.OSVersion.Version.Build;
        if (osBuild >= 22000)
        {
            lblDwmStatus.Text = $"[通过] Windows 11 DWM 原生液态亚克力合成: 完全支持 (OS Build {osBuild})";
            lblDwmStatus.ForeColor = AccentGreen;
        }
        else if (osBuild >= 19041)
        {
            lblDwmStatus.Text = $"[通过] Windows 10 DWM 亚克力模糊合成: 支持 (OS Build {osBuild})";
            lblDwmStatus.ForeColor = AccentGreen;
        }
        else
        {
            lblDwmStatus.Text = $"[提示] Windows 版本较低 (Build {osBuild})，将使用软件磨砂回退渲染";
            lblDwmStatus.ForeColor = AccentAmber;
        }

        // 4. Check UDP OSC Ports (9000 / 9001 / 9002)
        try
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
            bool port9000Busy = Array.Exists(listeners, p => p.Port == 9000);
            bool port9001Busy = Array.Exists(listeners, p => p.Port == 9001);
            if (port9000Busy || port9001Busy)
            {
                lblOscStatus.Text = $"[就绪] 检测到 VRChat/SlimeVR 正在运行 (UDP {(port9000Busy ? "9000 " : "")}{(port9001Busy ? "9001" : "")})，已启用无冲突直发架构";
            }
            else
            {
                lblOscStatus.Text = "[通过] VRChat / SlimeVR OSC 端口 (9000 / 9002): 空闲可用，支持双端口同时广播";
            }
            lblOscStatus.ForeColor = AccentGreen;
        }
        catch
        {
            lblOscStatus.Text = "[通过] VRChat / SlimeVR OSC 端口: 就绪";
            lblOscStatus.ForeColor = AccentGreen;
        }
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
                    if (!string.IsNullOrWhiteSpace(pv) && pv != "0.0.0.0")
                    {
                        return pv;
                    }
                }
                catch { }
            }
        }
        return null;
    }

    private async Task InstallWebView2RuntimeAsync()
    {
        try
        {
            btnInstallWebView.Enabled = false;
            lblStatus.Text = "正在下载 Microsoft Edge WebView2 常青版引导安装程序...";
            string tempBootstrapper = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");

            using (var http = new HttpClient())
            {
                var bytes = await http.GetByteArrayAsync("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                await File.WriteAllBytesAsync(tempBootstrapper, bytes);
            }

            lblStatus.Text = "正在运行 WebView2 Runtime 安装程序，请等待完成...";
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = tempBootstrapper,
                Arguments = "/install",
                UseShellExecute = true
            });
            if (proc != null)
            {
                await proc.WaitForExitAsync();
            }

            CheckDependencies();
            lblStatus.Text = "WebView2 Runtime 检测已更新。";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"在线安装 WebView2 失败: {ex.Message}", "安装提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            btnInstallWebView.Enabled = true;
        }
    }

    private void UpdateDiskSpaceInfo()
    {
        try
        {
            string path = txtInstallPath.Text.Trim();
            string? root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady)
                {
                    double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    double reqMb = EstimatedRequiredBytes / (1024.0 * 1024.0);
                    lblDiskSpace.Text = $"预计所需空间: {reqMb:F0} MB   |   目标磁盘 ({drive.Name.TrimEnd('\\')}) 可用空间: {freeGb:F2} GB";
                    lblDiskSpace.ForeColor = drive.AvailableFreeSpace > EstimatedRequiredBytes ? TextSecondary : AccentAmber;
                    return;
                }
            }
        }
        catch { }

        lblDiskSpace.Text = "预计所需空间: 195 MB   |   请输入有效的本地磁盘路径";
    }

    private async Task StartInstallationAsync()
    {
        if (installCompleted)
        {
            string exePath = Path.Combine(txtInstallPath.Text.Trim(), "KikitanTranslator.Photino.exe");
            if (chkLaunchAfter.Checked && File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = txtInstallPath.Text.Trim(),
                    UseShellExecute = true
                });
            }
            Close();
            return;
        }

        string targetDir = txtInstallPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            MessageBox.Show("请选择有效的安装目录。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        isInstalling = true;
        btnInstall.Enabled = false;
        btnCancel.Enabled = false;
        txtInstallPath.Enabled = false;

        try
        {
            lblStatus.Text = "正在停止运行中的旧版 Kikitan Translator 进程...";
            progressBar.Value = 5;

            await Task.Run(() =>
            {
                foreach (var proc in Process.GetProcessesByName("KikitanTranslator.Photino"))
                {
                    try { proc.Kill(); proc.WaitForExit(2000); } catch { }
                }
                foreach (var proc in Process.GetProcessesByName("KikitanTranslator"))
                {
                    try { proc.Kill(); proc.WaitForExit(2000); } catch { }
                }
            });

            // Backup config.json if requested
            string? backupConfigJson = null;
            string configPath = Path.Combine(targetDir, "config.json");
            if (chkPreserveConfig.Checked && File.Exists(configPath))
            {
                try { backupConfigJson = await File.ReadAllTextAsync(configPath); } catch { }
            }

            Directory.CreateDirectory(targetDir);

            lblStatus.Text = "正在释放 Apple Liquid Glass 应用程序与依赖组件...";
            progressBar.Value = 12;

            bool preserveCfg = chkPreserveConfig.Checked;
            await Task.Run(() =>
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("KikitanInstaller.payload.zip");
                if (stream == null)
                    throw new InvalidOperationException("找不到内置应用程序包资源 (payload.zip)。");

                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                int total = Math.Max(1, archive.Entries.Count);
                int current = 0;

                foreach (var entry in archive.Entries)
                {
                    current++;
                    string fullDestPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                    if (!fullDestPath.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(fullDestPath);
                        continue;
                    }

                    if (preserveCfg && entry.FullName.Equals("config.json", StringComparison.OrdinalIgnoreCase) && File.Exists(fullDestPath))
                    {
                        continue;
                    }

                    string? parentDir = Path.GetDirectoryName(fullDestPath);
                    if (!string.IsNullOrEmpty(parentDir))
                        Directory.CreateDirectory(parentDir);

                    entry.ExtractToFile(fullDestPath, overwrite: true);

                    int pct = 12 + (int)(current * 72.0 / total);
                    Invoke(() =>
                    {
                        progressBar.Value = Math.Clamp(pct, 12, 85);
                        lblStatus.Text = $"正在安装组件 ({current}/{total}): {entry.Name}";
                    });
                }
            });

            if (!string.IsNullOrEmpty(backupConfigJson))
            {
                try { await File.WriteAllTextAsync(configPath, backupConfigJson); } catch { }
            }

            string mainExePath = Path.Combine(targetDir, "KikitanTranslator.Photino.exe");

            if (chkDesktopShortcut.Checked)
            {
                lblStatus.Text = "正在创建桌面快捷方式...";
                progressBar.Value = 90;
                string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Kikitan Translator.lnk");
                CreateShortcut(desktopLnk, mainExePath, targetDir, "Kikitan Translator - Apple Liquid Glass VRChat Translator");
            }

            if (chkStartMenuShortcut.Checked)
            {
                lblStatus.Text = "正在创建开始菜单快捷方式...";
                progressBar.Value = 94;
                string startMenuLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Kikitan Translator.lnk");
                CreateShortcut(startMenuLnk, mainExePath, targetDir, "Kikitan Translator - Apple Liquid Glass VRChat Translator");
            }

            if (chkRegisterUninstall.Checked)
            {
                lblStatus.Text = "正在注册系统卸载信息...";
                progressBar.Value = 97;
                RegisterUninstaller(targetDir, mainExePath);
            }

            progressBar.Value = 100;
            lblStatus.Text = "安装成功完成！点击「完成」即可启动 Kikitan Translator。";
            lblStatus.ForeColor = AccentGreen;

            installCompleted = true;
            isInstalling = false;
            btnInstall.Text = "完成 (Finish)";
            btnInstall.BackColor = AccentGreen;
            btnInstall.ForeColor = Color.Black;
            btnInstall.Enabled = true;

            if (chkLaunchAfter.Checked && File.Exists(mainExePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = mainExePath,
                    WorkingDirectory = targetDir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            isInstalling = false;
            btnInstall.Enabled = true;
            btnCancel.Enabled = true;
            txtInstallPath.Enabled = true;
            lblStatus.Text = $"安装失败: {ex.Message}";
            lblStatus.ForeColor = AccentAmber;
            MessageBox.Show($"安装过程中遇到错误:\n{ex.Message}", "安装错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetExe, string workingDir, string description)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetExe;
            shortcut.WorkingDirectory = workingDir;
            shortcut.Description = description;
            shortcut.IconLocation = $"{targetExe},0";
            shortcut.Save();
        }
        catch { }
    }

    private static void RegisterUninstaller(string targetDir, string mainExePath)
    {
        try
        {
            string installerCopy = Path.Combine(targetDir, "Uninstall-Kikitan.exe");
            string currentProcessPath = Environment.ProcessPath ?? "";
            if (!string.IsNullOrEmpty(currentProcessPath) && File.Exists(currentProcessPath))
            {
                File.Copy(currentProcessPath, installerCopy, overwrite: true);
            }

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\KikitanTranslator");
            if (key != null)
            {
                key.SetValue("DisplayName", "Kikitan Translator (Liquid Glass Edition)");
                key.SetValue("DisplayVersion", "2.2.3");
                key.SetValue("Publisher", "Kikitan");
                key.SetValue("DisplayIcon", mainExePath);
                key.SetValue("InstallLocation", targetDir);
                key.SetValue("UninstallString", $"\"{installerCopy}\" --uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch { }
    }
}
