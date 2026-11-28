using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace KikitanTranslator.Photino;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct OverlayEventSlot
{
    public const int MaxTextBytes = 768;

    public const int EventFlagIsOther = 1 << 0;
    public const int EventFlagIsFinal = 1 << 1;

    public int EventId;
    public int EventFlags;
    public int OriginalLen;
    public int TranslationLen;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxTextBytes)]
    public byte[] OriginalBytes;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxTextBytes)]
    public byte[] TranslationBytes;

    public static OverlayEventSlot CreateEmpty() => new()
    {
        OriginalBytes = new byte[MaxTextBytes],
        TranslationBytes = new byte[MaxTextBytes]
    };

    public static OverlayEventSlot Create(int eventId, bool isOther, bool isFinal, string? original, string? translation)
    {
        var slot = CreateEmpty();
        slot.EventId = eventId;
        slot.EventFlags = (isOther ? EventFlagIsOther : 0) | (isFinal ? EventFlagIsFinal : 0);
        WriteUtf8(slot.OriginalBytes, out slot.OriginalLen, original);
        WriteUtf8(slot.TranslationBytes, out slot.TranslationLen, translation);
        return slot;
    }

    private static void WriteUtf8(byte[] buffer, out int length, string? value)
    {
        Array.Clear(buffer, 0, MaxTextBytes);
        if (string.IsNullOrEmpty(value))
        {
            length = 0;
            return;
        }

        var encoded = Encoding.UTF8.GetBytes(value);
        if (encoded.Length <= MaxTextBytes)
        {
            Array.Copy(encoded, buffer, encoded.Length);
            length = encoded.Length;
            return;
        }

        int byteCount = MaxTextBytes;
        while (byteCount > 0 && (encoded[byteCount] & 0xC0) == 0x80) byteCount--;
        Array.Copy(encoded, buffer, byteCount);
        length = byteCount;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct OverlayPipeHeader
{
    public const string PipeName = "kikitan-pipe-v2";
    public const int SlotCount = 8;
    public const int PipeCapacity = 16384;

    public const int FlagDesktopEnabled = 1 << 0;
    public const int FlagLightMode = 1 << 1;
    public const int FlagSpeechToTextOnly = 1 << 2;
    public const int FlagNoLanguageSpace = 1 << 3;

    public int HeaderCounter;
    public int LatestEventId;
    public int ClearCounter;
    public int Flags;
    public int Opacity;
    public int UiLangCode;
    public int Position;
    public int ScalePercent;
}

public class OverlayWriter : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly object _writeLock = new();

    private readonly int _headerSize = Marshal.SizeOf<OverlayPipeHeader>();
    private readonly int _slotSize = Marshal.SizeOf<OverlayEventSlot>();

    private OverlayPipeHeader _header;

    public OverlayWriter()
    {
        _mmf = MemoryMappedFile.CreateOrOpen(OverlayPipeHeader.PipeName, OverlayPipeHeader.PipeCapacity);
        _accessor = _mmf.CreateViewAccessor();

        // Initialize empty header & slots so fresh readers see clean state
        lock (_writeLock)
        {
            _header = new OverlayPipeHeader
            {
                HeaderCounter = 1,
                Flags = OverlayPipeHeader.FlagDesktopEnabled,
                Opacity = 55,
                Position = 0,
                ScalePercent = 100
            };
            WriteStruct(0, _header);
            var emptySlot = OverlayEventSlot.CreateEmpty();
            for (int i = 0; i < OverlayPipeHeader.SlotCount; i++)
            {
                WriteStruct(_headerSize + i * _slotSize, emptySlot);
            }
        }
    }

    public void UpdateTheme(bool desktopEnabled, bool lightMode, bool speechToTextOnly, bool noLanguageSpace, int opacity, int uiLangCode, int position = 0, int scalePercent = 100)
    {
        lock (_writeLock)
        {
            ApplyThemeFields(desktopEnabled, lightMode, speechToTextOnly, noLanguageSpace, opacity, uiLangCode, position, scalePercent);
            _header.HeaderCounter++;
            WriteStruct(0, _header);
        }
    }

    public void PushEvent(
        bool isOther,
        string? original,
        string? translation,
        bool isFinal,
        bool desktopEnabled,
        bool lightMode,
        bool speechToTextOnly,
        bool noLanguageSpace,
        int opacity,
        int uiLangCode,
        int position = 0,
        int scalePercent = 100)
    {
        lock (_writeLock)
        {
            ApplyThemeFields(desktopEnabled, lightMode, speechToTextOnly, noLanguageSpace, opacity, uiLangCode, position, scalePercent);

            int nextId = ++_header.LatestEventId;
            int slotIndex = (nextId - 1) % OverlayPipeHeader.SlotCount;
            var slot = OverlayEventSlot.Create(nextId, isOther, isFinal, original, translation);
            WriteStruct(_headerSize + slotIndex * _slotSize, slot);

            _header.HeaderCounter++;
            WriteStruct(0, _header);
        }
    }

    public void ClearHistory()
    {
        lock (_writeLock)
        {
            // Wipe all ring-buffer slots so pre-clear events can never be re-read after ClearCounter increments
            var emptySlot = OverlayEventSlot.CreateEmpty();
            for (int i = 0; i < OverlayPipeHeader.SlotCount; i++)
            {
                WriteStruct(_headerSize + i * _slotSize, emptySlot);
            }

            _header.ClearCounter++;
            _header.HeaderCounter++;
            WriteStruct(0, _header);
        }
    }

    private void ApplyThemeFields(bool desktopEnabled, bool lightMode, bool speechToTextOnly, bool noLanguageSpace, int opacity, int uiLangCode, int position, int scalePercent)
    {
        int flags = 0;
        if (desktopEnabled) flags |= OverlayPipeHeader.FlagDesktopEnabled;
        if (lightMode) flags |= OverlayPipeHeader.FlagLightMode;
        if (speechToTextOnly) flags |= OverlayPipeHeader.FlagSpeechToTextOnly;
        if (noLanguageSpace) flags |= OverlayPipeHeader.FlagNoLanguageSpace;

        _header.Flags = flags;
        _header.Opacity = Math.Clamp(opacity, 0, 100);
        _header.UiLangCode = uiLangCode;
        _header.Position = Math.Clamp(position, 0, 5);
        _header.ScalePercent = Math.Clamp(scalePercent, 75, 150);
    }

    private void WriteStruct<T>(long offset, T value) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        var bytes = new byte[size];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
        }
        finally
        {
            handle.Free();
        }
        _accessor.WriteArray(offset, bytes, 0, size);
    }

    public void Dispose()
    {
        _accessor.Dispose();
        _mmf.Dispose();
    }
}
