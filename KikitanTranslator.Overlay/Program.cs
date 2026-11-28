using System.Diagnostics;

namespace KikitanTranslator.Overlay;

static class Program
{
    [STAThread]
    static void Main()
    {
        if (Process.GetProcessesByName("KikitanTranslator.Overlay").Length > 1)
        {
            Console.WriteLine("Another overlay is running!");
            
            return;
        }
        
        Task.Run(async () =>
        {
            while (true)
            {
                if (Process.GetProcessesByName("KikitanTranslator.Photino").Length == 0)
                {
                    Console.WriteLine("Kikitan is not running!");

                    Environment.Exit(0);
                }

                // don't spin a cpu core while waiting
                await Task.Delay(1000);
            }
        });
        
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        var server = new OverlayServer();

        if (Process.GetProcessesByName("vrserver").Length != 0) server.StartOpenVR(new OpenVROverlay());
        
        if (Screen.PrimaryScreen != null)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            // Working area = screen without the taskbar, so the overlay can never cover it.
            Rectangle bounds = Screen.PrimaryScreen.WorkingArea;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            var form = new Form1(bounds);
            server.StartForm(form, bounds);
            
            Application.Run(form);
        }
    }
}