using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace KikitanInstaller;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            RunUninstaller();
            Shutdown();
            return;
        }

        string? screenshotDir = null;
        string? autoUpdateDir = null;
        foreach (var arg in e.Args)
        {
            if (arg.StartsWith("--capture-preview=", StringComparison.OrdinalIgnoreCase))
            {
                screenshotDir = arg["--capture-preview=".Length..].Trim('"');
            }
            else if (arg.StartsWith("--auto-update=", StringComparison.OrdinalIgnoreCase))
            {
                autoUpdateDir = arg["--auto-update=".Length..].Trim('"');
            }
        }

        var window = new MainWindow(screenshotDir, autoUpdateDir);
        MainWindow = window;
        window.Show();
    }

    private static void RunUninstaller()
    {
        var result = MessageBox.Show(
            "确定要卸载 Kikitan Translator 吗？\n（您的个人配置文件与 API 密钥 config.json 将被安全保留）",
            "Kikitan Translator 卸载向导",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            StopRunningAppProcesses();

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

            MessageBox.Show("Kikitan Translator 已成功从您的电脑移除。", "卸载完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"卸载过程中发生错误:\n{ex.Message}", "卸载提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    internal static void StopRunningAppProcesses()
    {
        string[] names = ["KikitanTranslator.Overlay", "KikitanTranslator.Photino", "KikitanTranslator"];
        foreach (var name in names)
        {
            foreach (var proc in Process.GetProcessesByName(name))
            {
                try
                {
                    proc.Kill();
                    proc.WaitForExit(2000);
                }
                catch { }
            }
        }
    }
}
