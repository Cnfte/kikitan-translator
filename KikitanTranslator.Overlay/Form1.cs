using System.Runtime.InteropServices;

namespace KikitanTranslator.Overlay;

public partial class Form1 : Form
{
    private readonly Rectangle _workArea;

    public Form1(Rectangle workArea)
    {
        _workArea = workArea;
        InitializeComponent();

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        Text = "";
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(_workArea.Left, _workArea.Bottom - 2, 1, 1);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Keep completely invisible until the first subtitle frame arrives
        Visible = false;
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e) { }

    public void PresentFrame(byte[] bgraPremulPixels, int width, int height, int position = 0)
    {
        if (IsDisposed || width <= 0 || height <= 0) return;

        void Apply()
        {
            if (IsDisposed || !IsHandleCreated) return;

            const int marginX = 20;
            const int marginY = 18;
            int x = position switch
            {
                1 or 4 => _workArea.Left + marginX,
                2 or 5 => Math.Max(_workArea.Left + marginX, _workArea.Right - width - marginX),
                _ => _workArea.Left + (_workArea.Width - width) / 2
            };
            int y = position switch
            {
                3 or 4 or 5 => _workArea.Top + marginY,
                _ => Math.Max(_workArea.Top + 12, _workArea.Bottom - height - marginY)
            };

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                var bmi = new BITMAPINFO
                {
                    biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // Top-down DIB matches SkiaSharp row order
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0, // BI_RGB
                };

                hBitmap = CreateDIBSection(screenDc, ref bmi, 0, out IntPtr bits, IntPtr.Zero, 0);
                if (hBitmap == IntPtr.Zero || bits == IntPtr.Zero) return;

                Marshal.Copy(bgraPremulPixels, 0, bits, Math.Min(bgraPremulPixels.Length, width * height * 4));
                oldBitmap = SelectObject(memDc, hBitmap);

                if (!Visible) Visible = true;

                var ptDst = new POINT(x, y);
                var ptSrc = new POINT(0, 0);
                var size = new SIZE(width, height);
                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA,
                };

                UpdateLayeredWindow(Handle, screenDc, ref ptDst, ref size, memDc, ref ptSrc, 0, ref blend, ULW_ALPHA);
                SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero) SelectObject(memDc, oldBitmap);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (memDc != IntPtr.Zero) DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        try
        {
            if (InvokeRequired) BeginInvoke(Apply);
            else Apply();
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    public void ClearOverlay()
    {
        if (IsDisposed) return;

        void HideSafe()
        {
            if (!IsDisposed && Visible) Visible = false;
        }

        try
        {
            if (InvokeRequired) BeginInvoke(HideSafe);
            else HideSafe();
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const int ULW_ALPHA = 0x02;
    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE(int cx, int cy)
    {
        public int CX = cx;
        public int CY = cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
        public uint bmiColors;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hwnd,
        IntPtr hdcDst,
        ref POINT pptDst,
        ref SIZE psize,
        IntPtr hdcSrc,
        ref POINT pptSrc,
        uint crKey,
        ref BLENDFUNCTION pblend,
        uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BITMAPINFO pbmi,
        uint iUsage,
        out IntPtr ppvBits,
        IntPtr hSection,
        uint dwOffset);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
