using KikitanTranslator.Overlay;

public class OverlayServer
{
    private const int MaxEntries = 24;
    private const long IdleHideMs = 28_000;

    private Form1? _form;
    private OpenVROverlay? _openVrOverlay;
    private int _screenWidth = 1920;
    private int _screenHeight = 1080;
    private float _dpiScale = 1.0f;

    private readonly object _stateLock = new();
    private readonly List<OverlayChatEntry> _entries = [];
    private int _activeOtherIndex = -1;
    private int _activeSelfIndex = -1;
    private long _lastActivityTick;

    private float _scrollOffset;
    private float _targetScrollOffset;

    private bool _desktopEnabled = true;
    private int _overlayPosition;
    private BubbleTheme _theme = new(false, 55, 0, false, true, 100);
    private bool _hasShownOnce;
    private bool _loopsStarted;

    public void StartForm(Form1 form, Rectangle workArea)
    {
        _form = form;
        _screenWidth = workArea.Width;
        _screenHeight = workArea.Height;
        _dpiScale = Math.Clamp(form.DeviceDpi / 96f, 1.0f, 3.0f);
        StartLoops();
    }

    public void StartOpenVR(OpenVROverlay openVrOverlay)
    {
        _openVrOverlay = openVrOverlay;
        Task.Run(openVrOverlay.ImageLoop);
        StartLoops();
    }

    private void StartLoops()
    {
        if (_loopsStarted) return;
        _loopsStarted = true;
        Task.Run(RunPipeLoop);
    }

    private async Task RunPipeLoop()
    {
        OverlayReader? reader = null;
        while (reader == null)
        {
            try
            {
                reader = new OverlayReader();
            }
            catch
            {
                await Task.Delay(200);
            }
        }

        while (true)
        {
            bool animating;
            lock (_stateLock)
            {
                animating = MathF.Abs(_targetScrollOffset - _scrollOffset) > 0.5f;
            }
            await Task.Delay(animating ? 20 : 45);

            var poll = reader.Poll();
            bool shouldRenderDesktop = false;
            bool shouldRenderVr = false;

            long now = Environment.TickCount64;
            if (poll is { } update)
            {
                lock (_stateLock)
                {
                    _desktopEnabled = update.Header.HasFlag(OverlayPipeHeader.FlagDesktopEnabled);
                    _overlayPosition = Math.Clamp(update.Header.Position, 0, 5);
                    _theme = new BubbleTheme(
                        LightMode: update.Header.HasFlag(OverlayPipeHeader.FlagLightMode),
                        GlassOpacity: update.Header.Opacity,
                        UiLangCode: update.Header.UiLangCode,
                        SpeechToTextOnly: update.Header.HasFlag(OverlayPipeHeader.FlagSpeechToTextOnly),
                        NoLanguageSpace: update.Header.HasFlag(OverlayPipeHeader.FlagNoLanguageSpace),
                        ScalePercent: update.Header.ScalePercent > 0 ? update.Header.ScalePercent : 100);

                    if (update.ClearRequested)
                    {
                        ClearHistoryLocked();
                        shouldRenderDesktop = true;
                        shouldRenderVr = true;
                    }

                    foreach (var ev in update.NewEvents)
                    {
                        ApplyEventLocked(ev, now);
                        shouldRenderDesktop = true;
                        shouldRenderVr = true;
                    }

                    // Theme/toggle updates should refresh visible desktop overlay immediately
                    if (!shouldRenderDesktop && _entries.Count > 0)
                    {
                        shouldRenderDesktop = true;
                    }
                }
            }

            lock (_stateLock)
            {
                // Auto-hide and reset conversation after prolonged silence
                if (_entries.Count > 0 && _lastActivityTick > 0 && (now - _lastActivityTick) >= IdleHideMs)
                {
                    ClearHistoryLocked();
                    shouldRenderDesktop = true;
                    shouldRenderVr = true;
                }
                else if (MathF.Abs(_targetScrollOffset - _scrollOffset) > 0.5f)
                {
                    float delta = _targetScrollOffset - _scrollOffset;
                    _scrollOffset = MathF.Abs(delta) <= 1.2f ? _targetScrollOffset : _scrollOffset + delta * 0.30f;
                    shouldRenderDesktop = true;
                }
            }

            if (shouldRenderDesktop || shouldRenderVr)
            {
                RenderCurrentState(renderDesktop: shouldRenderDesktop, renderVr: shouldRenderVr);
            }
        }
    }

    private void ApplyEventLocked(OverlayEventSlot ev, long now)
    {
        string orig = ev.Original.Trim();
        string trans = ev.Translation.Trim();
        if (orig.Length == 0 && trans.Length == 0) return;

        bool isOther = ev.IsOther;
        bool isFinal = ev.IsFinal;
        ref int activeIdx = ref (isOther ? ref _activeOtherIndex : ref _activeSelfIndex);

        if (activeIdx >= 0 && activeIdx < _entries.Count)
        {
            _entries[activeIdx] = new OverlayChatEntry(isOther, orig, trans, isFinal);
            if (isFinal) activeIdx = -1;
        }
        else
        {
            _entries.Add(new OverlayChatEntry(isOther, orig, trans, isFinal));
            activeIdx = isFinal ? -1 : _entries.Count - 1;
        }

        // Trim oldest entries beyond cap while keeping active indices aligned
        while (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(0);
            if (_activeOtherIndex >= 0) _activeOtherIndex--;
            if (_activeSelfIndex >= 0) _activeSelfIndex--;
        }

        _lastActivityTick = now;
    }

    private void ClearHistoryLocked()
    {
        _entries.Clear();
        _activeOtherIndex = -1;
        _activeSelfIndex = -1;
        _scrollOffset = 0f;
        _targetScrollOffset = 0f;
        _lastActivityTick = 0;
    }

    private void RenderCurrentState(bool renderDesktop, bool renderVr)
    {
        OverlayChatEntry[] snapshot;
        BubbleTheme theme;
        bool desktopEnabled;
        int overlayPosition;
        float currentScroll;

        lock (_stateLock)
        {
            snapshot = _entries.ToArray();
            theme = _theme;
            desktopEnabled = _desktopEnabled;
            overlayPosition = _overlayPosition;
            currentScroll = _scrollOffset;
        }

        if (snapshot.Length == 0)
        {
            _form?.ClearOverlay();
            if (renderVr) _openVrOverlay?.ClearOverlay();
            return;
        }

        if (renderDesktop && _form != null)
        {
            if (!desktopEnabled)
            {
                if (_hasShownOnce) _form.ClearOverlay();
            }
            else
            {
                var frame = OverlayBubbleRenderer.RenderConversation(
                    snapshot,
                    currentScroll,
                    _screenWidth,
                    _screenHeight,
                    theme,
                    encodePngForVr: false,
                    encodeBgraForDesktop: true,
                    dpiScale: _dpiScale,
                    snapToBottom: false);

                if (frame is { } f)
                {
                    lock (_stateLock)
                    {
                        _targetScrollOffset = f.MaxScroll;
                        if (_scrollOffset > f.MaxScroll) _scrollOffset = f.MaxScroll;
                    }

                    _hasShownOnce = true;
                    _form.PresentFrame(f.BgraPremulPixels, f.Width, f.Height, overlayPosition);
                }
                else if (_hasShownOnce)
                {
                    _form.ClearOverlay();
                }
            }
        }

        if (renderVr && _openVrOverlay != null)
        {
            var vrFrame = OverlayBubbleRenderer.RenderConversation(
                snapshot,
                0f,
                _screenWidth,
                _screenHeight,
                theme,
                encodePngForVr: true,
                encodeBgraForDesktop: false,
                snapToBottom: true);

            if (vrFrame?.PngBytes is { Length: > 0 } png)
            {
                _openVrOverlay.SetOverlayFrame(png, 12000);
            }
        }
    }
}
