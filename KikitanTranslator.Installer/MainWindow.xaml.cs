using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace KikitanInstaller;

public sealed class DependencyRowViewModel : INotifyPropertyChanged
{
    public DependencyItem Model { get; }
    public string Name => Model.Name;
    public string Description => Model.Description;

    private string _statusText = "检测中...";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private bool _isInstalled;
    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            _isInstalled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InstallButtonVisibility));
        }
    }

    private bool _canInstall = true;
    public bool CanInstall
    {
        get => _canInstall;
        set { _canInstall = value; OnPropertyChanged(); }
    }

    private bool _isInstalling;
    public bool IsInstalling
    {
        get => _isInstalling;
        set { _isInstalling = value; OnPropertyChanged(); }
    }

    public Visibility InstallButtonVisibility => IsInstalled ? Visibility.Collapsed : Visibility.Visible;

    private Brush _iconBg = new SolidColorBrush(Color.FromArgb(0x24, 0x94, 0xA0, 0xB8));
    public Brush IconBg { get => _iconBg; set { _iconBg = value; OnPropertyChanged(); } }

    private Brush _iconBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA0, 0xB8));
    public Brush IconBrush { get => _iconBrush; set { _iconBrush = value; OnPropertyChanged(); } }

    private Geometry _iconGeometry = Geometry.Parse("M 9,3 L 9,10 M 9,13.5 L 9,14");
    public Geometry IconGeometry { get => _iconGeometry; set { _iconGeometry = value; OnPropertyChanged(); } }

    private Brush _pillBg = new SolidColorBrush(Color.FromArgb(0x20, 0x94, 0xA0, 0xB8));
    public Brush PillBg { get => _pillBg; set { _pillBg = value; OnPropertyChanged(); } }

    private Brush _pillBorder = new SolidColorBrush(Color.FromArgb(0x40, 0x94, 0xA0, 0xB8));
    public Brush PillBorder { get => _pillBorder; set { _pillBorder = value; OnPropertyChanged(); } }

    private Brush _pillForeground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
    public Brush PillForeground { get => _pillForeground; set { _pillForeground = value; OnPropertyChanged(); } }

    private static readonly Geometry CheckGeo = Geometry.Parse("M 3,9 L 7.5,13.5 L 15,4.5");
    private static readonly Geometry WarnGeo = Geometry.Parse("M 9,3.5 L 9,10.5 M 9,14 L 9,14.5");
    private static readonly Geometry DownloadGeo = Geometry.Parse("M 9,3 L 9,12.5 M 5,9 L 9,13 L 13,9 M 4,15.5 L 14,15.5");

    public DependencyRowViewModel(DependencyItem model) => Model = model;

    public void ApplyStateFromModel()
    {
        IsInstalling = false;
        IsInstalled = Model.IsInstalled;
        if (Model.IsInstalled)
        {
            StatusText = $"已安装 v{Model.InstalledVersion}";
            IconBg = new SolidColorBrush(Color.FromArgb(0x22, 0x22, 0xC5, 0x5E));
            IconBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
            IconGeometry = CheckGeo;
            PillBg = new SolidColorBrush(Color.FromArgb(0x1C, 0x22, 0xC5, 0x5E));
            PillBorder = new SolidColorBrush(Color.FromArgb(0x48, 0x22, 0xC5, 0x5E));
            PillForeground = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
        }
        else
        {
            StatusText = "缺失 · 必需";
            IconBg = new SolidColorBrush(Color.FromArgb(0x24, 0xF5, 0x9E, 0x0B));
            IconBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
            IconGeometry = WarnGeo;
            PillBg = new SolidColorBrush(Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B));
            PillBorder = new SolidColorBrush(Color.FromArgb(0x50, 0xF5, 0x9E, 0x0B));
            PillForeground = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
        }
    }

    public void SetWorkingState(string text)
    {
        IsInstalling = true;
        StatusText = text;
        IconBg = new SolidColorBrush(Color.FromArgb(0x26, 0x0A, 0x84, 0xFF));
        IconBrush = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA));
        IconGeometry = DownloadGeo;
        PillBg = new SolidColorBrush(Color.FromArgb(0x22, 0x0A, 0x84, 0xFF));
        PillBorder = new SolidColorBrush(Color.FromArgb(0x55, 0x0A, 0x84, 0xFF));
        PillForeground = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DependencyRowViewModel> _depRows = [];
    private readonly List<DependencyItem> _depModels = DependencyManager.CreateDefaults();
    private readonly string _defaultInstallPath;
    private readonly string? _capturePreviewDir;
    private readonly string? _autoUpdateDir;

    private int _currentStep;
    private bool _isBusy;
    private long _payloadEstimatedBytes = 95L * 1024 * 1024;
    private string _installedDir = string.Empty;

    public MainWindow(string? capturePreviewDir = null, string? autoUpdateDir = null)
    {
        InitializeComponent();
        _capturePreviewDir = capturePreviewDir;
        _autoUpdateDir = autoUpdateDir;

        _defaultInstallPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KikitanTranslator");

        LoadEmbeddedLogo();
        _payloadEstimatedBytes = ComputePayloadUncompressedBytes();

        foreach (var model in _depModels) _depRows.Add(new DependencyRowViewModel(model));
        DependenciesList.ItemsSource = _depRows;

        TxtInstallPath.Text = !string.IsNullOrWhiteSpace(_autoUpdateDir) ? _autoUpdateDir : _defaultInstallPath;
        UpdateDiskSpaceInfo();
        ShowStep(!string.IsNullOrWhiteSpace(_autoUpdateDir) ? 2 : 0);

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_autoUpdateDir))
        {
            ChkPreserveConfig.IsChecked = true;
            await RunInstallationAsync(isAutoUpdate: true);
            return;
        }

        await RefreshDependenciesAsync();

        if (!string.IsNullOrEmpty(_capturePreviewDir))
        {
            await CaptureAllPreviewStepsAsync(_capturePreviewDir);
            Close();
        }
    }

    private void LoadEmbeddedLogo()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app_icon.ico");
            if (stream == null) return;
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var bestFrame = decoder.Frames.OrderByDescending(f => f.PixelWidth).FirstOrDefault();
            if (bestFrame != null)
            {
                AppLogoImage.Source = bestFrame;
                Icon = bestFrame;
            }
        }
        catch { }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (!_isBusy) Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (!_isBusy) Close();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (!_isBusy && _currentStep == 1) ShowStep(0);
    }

    private void ShowStep(int step)
    {
        _currentStep = step;

        PageDependencies.Visibility = step == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageOptions.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageProgress.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        PageFinish.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;

        Border[] badges = [StepBadge0, StepBadge1, StepBadge2, StepBadge3];
        TextBlock[] nums = [StepNum0, StepNum1, StepNum2, StepNum3];
        System.Windows.Shapes.Path[] checks = [StepCheck0, StepCheck1, StepCheck2, StepCheck3];
        TextBlock[] texts = [StepText0, StepText1, StepText2, StepText3];
        Border[] lines = [StepLine0, StepLine1, StepLine2];

        var blue = (Brush)FindResource("AccentBlue");
        var green = (Brush)FindResource("AccentGreen");
        var primary = (Brush)FindResource("TextPrimary");
        var secondary = (Brush)FindResource("TextSecondary");
        var mutedBg = new SolidColorBrush(Color.FromRgb(0x25, 0x2C, 0x3E));
        var borderSubtle = (Brush)FindResource("BorderSubtle");

        for (int i = 0; i < 4; i++)
        {
            if (i < step)
            {
                badges[i].Background = green;
                nums[i].Visibility = Visibility.Collapsed;
                checks[i].Visibility = Visibility.Visible;
                texts[i].Foreground = primary;
                texts[i].FontWeight = FontWeights.Medium;
            }
            else if (i == step)
            {
                badges[i].Background = blue;
                nums[i].Visibility = Visibility.Visible;
                checks[i].Visibility = Visibility.Collapsed;
                nums[i].Text = (i + 1).ToString();
                nums[i].Foreground = Brushes.White;
                texts[i].Foreground = primary;
                texts[i].FontWeight = FontWeights.SemiBold;
            }
            else
            {
                badges[i].Background = mutedBg;
                nums[i].Visibility = Visibility.Visible;
                checks[i].Visibility = Visibility.Collapsed;
                nums[i].Text = (i + 1).ToString();
                nums[i].Foreground = secondary;
                texts[i].Foreground = secondary;
                texts[i].FontWeight = FontWeights.Normal;
            }
        }

        for (int i = 0; i < 3; i++)
        {
            lines[i].Background = i < step ? green : borderSubtle;
        }

        BtnBack.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        BtnCancel.Visibility = step is 0 or 1 ? Visibility.Visible : Visibility.Collapsed;

        switch (step)
        {
            case 0:
                BtnPrimary.Style = (Style)FindResource("PrimaryButtonStyle");
                BtnPrimary.Content = "下一步 →";
                BtnPrimary.IsEnabled = !_isBusy;
                break;
            case 1:
                BtnPrimary.Style = (Style)FindResource("PrimaryButtonStyle");
                BtnPrimary.Content = "开始安装";
                BtnPrimary.IsEnabled = !_isBusy;
                break;
            case 2:
                BtnPrimary.Style = (Style)FindResource("PrimaryButtonStyle");
                BtnPrimary.Content = "正在安装...";
                BtnPrimary.IsEnabled = false;
                break;
            case 3:
                BtnPrimary.Style = (Style)FindResource("SuccessButtonStyle");
                BtnPrimary.Content = "完成";
                BtnPrimary.IsEnabled = true;
                break;
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        BtnRecheckDeps.IsEnabled = !busy;
        BtnInstallAllDeps.IsEnabled = !busy;
        BtnBack.IsEnabled = !busy;
        BtnCancel.IsEnabled = !busy;
        if (_currentStep is 0 or 1) BtnPrimary.IsEnabled = !busy;
        foreach (var row in _depRows) row.CanInstall = !busy;
    }

    private async Task RefreshDependenciesAsync()
    {
        SetBusy(true);
        TxtDepSummary.Text = "正在扫描本机运行环境...";
        TxtDepSummary.Foreground = (Brush)FindResource("TextPrimary");

        await Task.Run(() => DependencyManager.DetectAll(_depModels));

        int missingCount = 0;
        foreach (var row in _depRows)
        {
            row.ApplyStateFromModel();
            if (!row.IsInstalled && row.Model.Required) missingCount++;
        }

        if (missingCount == 0)
        {
            TxtDepSummary.Text = "所有必需运行库均已就绪，点击右下角「下一步」即可开始安装。";
            TxtDepSummary.Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
            BtnInstallAllDeps.Visibility = Visibility.Collapsed;
        }
        else
        {
            TxtDepSummary.Text = $"检测到 {missingCount} 项必需运行库缺失，建议点击右侧按钮交互式安装。";
            TxtDepSummary.Foreground = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
            BtnInstallAllDeps.Visibility = Visibility.Visible;
        }

        SetBusy(false);
    }

    private async void RecheckDeps_Click(object sender, RoutedEventArgs e)
    {
        if (!_isBusy) await RefreshDependenciesAsync();
    }

    private async void InstallSingleDep_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || sender is not Button { Tag: DependencyRowViewModel row }) return;

        SetBusy(true);
        DepProgressPanel.Visibility = Visibility.Visible;
        DepProgressBar.Value = 5;
        TxtDepProgressPct.Text = "5%";

        var progress = new Progress<(int Percent, string Status)>(p =>
        {
            DepProgressBar.Value = p.Percent;
            TxtDepProgressPct.Text = $"{p.Percent}%";
            TxtDepProgressStatus.Text = p.Status;
            row.SetWorkingState(p.Percent < 80 ? $"下载中 {p.Percent}%" : "安装中...");
        });

        var (ok, message) = await DependencyManager.InstallAsync(row.Model, progress);
        if (!ok)
        {
            MessageBox.Show($"{row.Name} 安装未完成：\n{message}", "依赖安装提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        DepProgressPanel.Visibility = Visibility.Collapsed;
        SetBusy(false);
        await RefreshDependenciesAsync();
    }

    private async void InstallAllDeps_Click(object sender, RoutedEventArgs e)
    {
        if (!_isBusy) await InstallAllMissingDependenciesAsync();
    }

    private async Task InstallAllMissingDependenciesAsync()
    {
        var missing = _depRows.Where(r => !r.IsInstalled).ToList();
        if (missing.Count == 0) return;

        SetBusy(true);
        DepProgressPanel.Visibility = Visibility.Visible;

        int completed = 0;
        foreach (var row in missing)
        {
            int idx = completed;
            var progress = new Progress<(int Percent, string Status)>(p =>
            {
                int overall = Math.Clamp((int)((idx * 100.0 + p.Percent) / missing.Count), 1, 100);
                DepProgressBar.Value = overall;
                TxtDepProgressPct.Text = $"{overall}%";
                TxtDepProgressStatus.Text = $"[{idx + 1}/{missing.Count}] {p.Status}";
                row.SetWorkingState(p.Percent < 80 ? $"下载中 {p.Percent}%" : "安装中...");
            });

            var (ok, message) = await DependencyManager.InstallAsync(row.Model, progress);
            if (!ok)
            {
                var cont = MessageBox.Show(
                    $"{row.Name} 安装未完成：\n{message}\n\n是否继续安装其余缺失组件？",
                    "依赖安装提示",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (cont != MessageBoxResult.Yes) break;
            }
            completed++;
        }

        DepProgressPanel.Visibility = Visibility.Collapsed;
        SetBusy(false);
        await RefreshDependenciesAsync();
    }

    private async void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        switch (_currentStep)
        {
            case 0:
            {
                var missing = _depRows.Where(r => r.Model.Required && !r.IsInstalled).ToList();
                if (missing.Count > 0)
                {
                    string list = string.Join("\n", missing.Select(m => $"  • {m.Name}"));
                    var ans = MessageBox.Show(
                        $"以下 {missing.Count} 项必需运行库尚未安装：\n\n{list}\n\n是否现在自动为您交互式下载并安装？\n（点击「是」立即安装，点击「否」跳过并继续）",
                        "检测到缺失依赖",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (ans == MessageBoxResult.Cancel) return;
                    if (ans == MessageBoxResult.Yes)
                    {
                        await InstallAllMissingDependenciesAsync();
                        if (_depRows.Any(r => r.Model.Required && !r.IsInstalled)) return;
                    }
                }
                ShowStep(1);
                break;
            }
            case 1:
                await RunInstallationAsync();
                break;
            case 3:
                if (ChkLaunchAfter.IsChecked == true && !string.IsNullOrEmpty(_installedDir))
                {
                    string exePath = Path.Combine(_installedDir, "KikitanTranslator.Photino.exe");
                    if (File.Exists(exePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            WorkingDirectory = _installedDir,
                            UseShellExecute = true
                        });
                    }
                }
                Close();
                break;
        }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择 Kikitan Translator 的安装目录",
            InitialDirectory = Directory.Exists(TxtInstallPath.Text)
                ? TxtInstallPath.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            string chosen = dialog.FolderName;
            if (!chosen.EndsWith("KikitanTranslator", StringComparison.OrdinalIgnoreCase))
                chosen = Path.Combine(chosen, "KikitanTranslator");
            TxtInstallPath.Text = chosen;
        }
    }

    private void ResetPath_Click(object sender, RoutedEventArgs e) => TxtInstallPath.Text = _defaultInstallPath;

    private void TxtInstallPath_TextChanged(object sender, TextChangedEventArgs e) => UpdateDiskSpaceInfo();

    private void UpdateDiskSpaceInfo()
    {
        try
        {
            string path = TxtInstallPath.Text.Trim();
            string? root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady)
                {
                    double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    double reqMb = Math.Max(30.0, _payloadEstimatedBytes / (1024.0 * 1024.0));
                    TxtDiskSpace.Text = $"预计占用空间：约 {reqMb:F0} MB   ·   目标磁盘 ({drive.Name.TrimEnd('\\')}) 可用空间：{freeGb:F2} GB";
                    TxtDiskSpace.Foreground = drive.AvailableFreeSpace > _payloadEstimatedBytes
                        ? (Brush)FindResource("TextSecondary")
                        : (Brush)FindResource("AccentAmber");
                    return;
                }
            }
        }
        catch { }

        TxtDiskSpace.Text = "请输入有效的本地磁盘安装路径";
        TxtDiskSpace.Foreground = (Brush)FindResource("AccentAmber");
    }

    private async Task RunInstallationAsync(bool isAutoUpdate = false)
    {
        string targetDir = TxtInstallPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            MessageBox.Show("请选择有效的安装目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetBusy(true);
        ShowStep(2);
        TxtInstallLog.Clear();
        SetInstallProgress(4, isAutoUpdate ? "正在准备自动更新..." : "正在停止旧版运行进程...");

        void AppendLog(string line)
        {
            Dispatcher.BeginInvoke(() =>
            {
                TxtInstallLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
                TxtInstallLog.ScrollToEnd();
            });
        }

        try
        {
            AppendLog("正在检查并关闭运行中的 KikitanTranslator / Overlay 进程...");
            await Task.Run(App.StopRunningAppProcesses);
            SetInstallProgress(10, "正在准备目标安装目录...");

            string? backupConfigJson = null;
            string configPath = Path.Combine(targetDir, "config.json");
            bool preserveCfg = ChkPreserveConfig.IsChecked == true;
            if (preserveCfg && File.Exists(configPath))
            {
                try
                {
                    backupConfigJson = await File.ReadAllTextAsync(configPath);
                    AppendLog("已备份旧版目录中的 config.json。");
                }
                catch { }
            }

            Directory.CreateDirectory(targetDir);
            AppendLog($"安装目录：{targetDir}");

            SetInstallProgress(14, "正在释放主程序与桌面悬浮窗组件...");
            await Task.Run(() =>
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("KikitanInstaller.payload.zip");
                if (stream == null)
                    throw new InvalidOperationException("安装包内未找到内置应用载荷 (payload.zip)。");

                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                int total = Math.Max(1, archive.Entries.Count);
                int current = 0;

                string fullTargetRoot = Path.GetFullPath(targetDir);
                foreach (var entry in archive.Entries)
                {
                    current++;
                    string fullDestPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                    if (!fullDestPath.StartsWith(fullTargetRoot, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(fullDestPath);
                        continue;
                    }

                    if (preserveCfg && entry.FullName.Equals("config.json", StringComparison.OrdinalIgnoreCase) && File.Exists(fullDestPath))
                        continue;

                    string? parentDir = Path.GetDirectoryName(fullDestPath);
                    if (!string.IsNullOrEmpty(parentDir)) Directory.CreateDirectory(parentDir);

                    entry.ExtractToFile(fullDestPath, overwrite: true);

                    int pct = Math.Clamp(14 + (int)(current * 72.0 / total), 14, 86);
                    if (current == 1 || current == total || current % 6 == 0)
                    {
                        int c = current;
                        string name = entry.FullName;
                        Dispatcher.BeginInvoke(() => SetInstallProgress(pct, $"正在释放组件 ({c}/{total})..."));
                        AppendLog($"释放：{name}");
                    }
                }
            });

            if (!string.IsNullOrEmpty(backupConfigJson))
            {
                try { await File.WriteAllTextAsync(configPath, backupConfigJson); } catch { }
            }

            string mainExePath = Path.Combine(targetDir, "KikitanTranslator.Photino.exe");

            if (ChkDesktopShortcut.IsChecked == true)
            {
                SetInstallProgress(90, "正在创建桌面快捷方式...");
                string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Kikitan Translator.lnk");
                CreateShortcut(desktopLnk, mainExePath, targetDir, "Kikitan Translator - VRChat Translator");
                AppendLog("已创建桌面快捷方式。");
            }

            if (ChkStartMenuShortcut.IsChecked == true)
            {
                SetInstallProgress(94, "正在创建开始菜单快捷方式...");
                string startMenuLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Kikitan Translator.lnk");
                CreateShortcut(startMenuLnk, mainExePath, targetDir, "Kikitan Translator - VRChat Translator");
                AppendLog("已创建开始菜单快捷方式。");
            }

            if (ChkRegisterUninstall.IsChecked == true)
            {
                SetInstallProgress(97, "正在注册 Windows 卸载信息...");
                RegisterUninstaller(targetDir, mainExePath, _payloadEstimatedBytes);
                AppendLog("已注册到 Windows「已安装的应用」列表。");
            }

            SetInstallProgress(100, "安装完成！");
            AppendLog("所有安装步骤均已顺利完成。");

            _installedDir = targetDir;
            TxtDonePath.Text = targetDir;

            if (isAutoUpdate)
            {
                AppendLog("自动更新已完成，正在重新启动 Kikitan Translator...");
                await Task.Delay(500);
                if (File.Exists(mainExePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = mainExePath,
                        WorkingDirectory = targetDir,
                        UseShellExecute = true
                    });
                }
                SetBusy(false);
                Close();
                return;
            }

            SetBusy(false);
            ShowStep(3);
        }
        catch (Exception ex)
        {
            AppendLog($"错误：{ex.Message}");
            MessageBox.Show($"安装失败：\n{ex.Message}", "安装错误", MessageBoxButton.OK, MessageBoxImage.Error);
            SetBusy(false);
            ShowStep(1);
        }
    }

    private void SetInstallProgress(int percent, string stage)
    {
        InstallProgressBar.Value = Math.Clamp(percent, 0, 100);
        TxtInstallPct.Text = $"{Math.Clamp(percent, 0, 100)}%";
        TxtInstallStage.Text = stage;
    }

    private void OpenInstallFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_installedDir))
        {
            Process.Start(new ProcessStartInfo { FileName = _installedDir, UseShellExecute = true });
        }
    }

    private static long ComputePayloadUncompressedBytes()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("KikitanInstaller.payload.zip");
            if (stream == null) return 95L * 1024 * 1024;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            long total = 0;
            foreach (var e in archive.Entries) total += e.Length;
            return Math.Max(total, 30L * 1024 * 1024);
        }
        catch
        {
            return 95L * 1024 * 1024;
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

    private static void RegisterUninstaller(string targetDir, string mainExePath, long estimatedBytes)
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
                key.SetValue("DisplayName", "Kikitan Translator");
                key.SetValue("DisplayVersion", "2.3.0");
                key.SetValue("Publisher", "Kikitan");
                key.SetValue("DisplayIcon", mainExePath);
                key.SetValue("InstallLocation", targetDir);
                key.SetValue("UninstallString", $"\"{installerCopy}\" --uninstall");
                key.SetValue("EstimatedSize", (int)Math.Max(1024, estimatedBytes / 1024), RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch { }
    }

    private async Task CaptureAllPreviewStepsAsync(string outDir)
    {
        Directory.CreateDirectory(outDir);

        async Task RenderToFileAsync(string name)
        {
            UpdateLayout();
            await Task.Delay(120);
            var dpi = VisualTreeHelper.GetDpi(this);
            int pxW = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
            int pxH = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
            var rtb = new RenderTargetBitmap(pxW, pxH, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            rtb.Render(this);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            await using var fs = File.Create(Path.Combine(outDir, name));
            enc.Save(fs);
        }

        // Step 1A: real detected state
        ShowStep(0);
        await RenderToFileAsync("step1_ready.png");

        // Step 1B: simulate missing dependency state to verify inline Install + Install All buttons never clip/overlap
        if (_depRows.Count >= 2)
        {
            _depRows[1].Model.IsInstalled = false;
            _depRows[1].ApplyStateFromModel();
            TxtDepSummary.Text = "检测到 1 项必需运行库缺失，建议点击右侧按钮交互式安装。";
            TxtDepSummary.Foreground = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
            BtnInstallAllDeps.Visibility = Visibility.Visible;
            await RenderToFileAsync("step1_missing_dep.png");
        }

        // Step 2: path & options
        ShowStep(1);
        await RenderToFileAsync("step2_options.png");

        // Step 3: progress
        ShowStep(2);
        SetInstallProgress(64, "正在释放组件 (42/65)...");
        TxtInstallLog.Text = "[08:45:01] 正在检查并关闭运行中的进程...\r\n[08:45:02] 安装目录：C:\\Users\\Administrator\\AppData\\Local\\KikitanTranslator\r\n[08:45:03] 释放：KikitanTranslator.Photino.exe\r\n[08:45:03] 释放：KikitanTranslator.Overlay.exe\r\n[08:45:04] 释放：wwwroot/index.html";
        await RenderToFileAsync("step3_progress.png");

        // Step 4: completion
        _installedDir = _defaultInstallPath;
        TxtDonePath.Text = _defaultInstallPath;
        ShowStep(3);
        await RenderToFileAsync("step4_finish.png");
    }
}
