using Valve.VR;
using Application = OVRSharp.Application;

namespace KikitanTranslator.Overlay;

public class OpenVROverlay : Application
{
    private readonly OVRSharp.Overlay _overlay;
    private readonly object _frameLock = new();
    private readonly AutoResetEvent _signal = new(false);

    private byte[]? _pendingPng;
    private bool _hasUpdate;
    private bool _clearRequested;
    private long _hideAtTick;

    private readonly float _overlayDistance = -1.5f;
    private readonly float _verticalOffset = -0.55f;

    public OpenVROverlay() : base(ApplicationType.Overlay)
    {
        Console.WriteLine("Starting overlay...");

        _overlay = new OVRSharp.Overlay("kikitan-overlay", "Kikitan Overlay");
        _overlay.WidthInMeters = 0.65f;
        _overlay.Alpha = 0.92f;
        _overlay.SetFlag(VROverlayFlags.IsPremultiplied, false);
        _overlay.SetFlag(VROverlayFlags.SendVRSmoothScrollEvents, false);
    }

    private void UpdateOverlayTransforms()
    {
        var system = OpenVR.System;
        if (system == null) return;

        var leftEye = system.GetEyeToHeadTransform(EVREye.Eye_Left);

        var overlayTransform = new HmdMatrix34_t
        {
            m0 = 1, m1 = 0, m2 = 0, m3 = leftEye.m3,
            m4 = 0, m5 = 1, m6 = 0, m7 = _verticalOffset,
            m8 = 0, m9 = 0, m10 = 1, m11 = _overlayDistance
        };

        OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(
            _overlay.Handle, OpenVR.k_unTrackedDeviceIndex_Hmd, ref overlayTransform);
    }

    public void SetOverlayFrame(byte[] pngBytes, int timeMs)
    {
        lock (_frameLock)
        {
            _pendingPng = pngBytes;
            _hideAtTick = Environment.TickCount64 + Math.Max(1500, timeMs);
            _hasUpdate = true;
            _clearRequested = false;
        }
        _signal.Set();
    }

    public void ClearOverlay()
    {
        lock (_frameLock)
        {
            _pendingPng = null;
            _hasUpdate = false;
            _clearRequested = true;
            _hideAtTick = 0;
        }
        _signal.Set();
    }

    public void ImageLoop()
    {
        string fileName = Path.Combine(Path.GetTempPath(), "kikitan_overlay.png");
        bool isVisible = false;

        while (true)
        {
            int waitMs = isVisible ? 150 : 1000;
            _signal.WaitOne(waitMs);

            byte[]? pngToWrite = null;
            bool clear = false;
            long hideAt;

            lock (_frameLock)
            {
                if (_clearRequested)
                {
                    clear = true;
                    _clearRequested = false;
                }
                else if (_hasUpdate && _pendingPng != null)
                {
                    pngToWrite = _pendingPng;
                    _pendingPng = null;
                    _hasUpdate = false;
                }
                hideAt = _hideAtTick;
            }

            if (clear)
            {
                if (isVisible)
                {
                    _overlay.Hide();
                    isVisible = false;
                }
                continue;
            }

            if (pngToWrite != null)
            {
                try
                {
                    UpdateOverlayTransforms();
                    File.WriteAllBytes(fileName, pngToWrite);
                    _overlay.Show();
                    _overlay.SetTextureFromFile(fileName);
                    isVisible = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"OpenVR overlay update error: {ex.Message}");
                }
            }
            else if (isVisible && hideAt > 0 && Environment.TickCount64 >= hideAt)
            {
                _overlay.Hide();
                isVisible = false;
            }
        }
    }
}
