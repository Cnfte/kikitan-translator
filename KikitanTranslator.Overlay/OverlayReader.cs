using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace KikitanTranslator.Overlay;

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

    public readonly bool IsOther => (EventFlags & EventFlagIsOther) != 0;
    public readonly bool IsFinal => (EventFlags & EventFlagIsFinal) != 0;

    public readonly string Original => ReadUtf8(OriginalBytes, OriginalLen);
    public readonly string Translation => ReadUtf8(TranslationBytes, TranslationLen);

    private static string ReadUtf8(byte[]? buffer, int length)
    {
        if (buffer == null || length <= 0) return string.Empty;
        int clamped = Math.Clamp(length, 0, Math.Min(buffer.Length, MaxTextBytes));
        return Encoding.UTF8.GetString(buffer, 0, clamped);
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

    public readonly bool HasFlag(int flag) => (Flags & flag) != 0;
}

public readonly record struct OverlayPollResult(
    OverlayPipeHeader Header,
    bool ClearRequested,
    List<OverlayEventSlot> NewEvents);

public class OverlayReader : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly int _headerSize = Marshal.SizeOf<OverlayPipeHeader>();
    private readonly int _slotSize = Marshal.SizeOf<OverlayEventSlot>();

    private int _previousHeaderCounter;
    private int _lastProcessedEventId;
    private int _lastClearCounter;

    public OverlayReader()
    {
        _mmf = MemoryMappedFile.OpenExisting(OverlayPipeHeader.PipeName);
        _accessor = _mmf.CreateViewAccessor();
    }

    public OverlayPollResult? Poll()
    {
        var header = ReadStruct<OverlayPipeHeader>(0);
        if (header.HeaderCounter <= 0 || header.HeaderCounter == _previousHeaderCounter)
            return null;

        _previousHeaderCounter = header.HeaderCounter;

        bool clearRequested = header.ClearCounter > _lastClearCounter;
        if (clearRequested) _lastClearCounter = header.ClearCounter;

        var newEvents = new List<OverlayEventSlot>();
        if (header.LatestEventId > _lastProcessedEventId)
        {
            for (int i = 0; i < OverlayPipeHeader.SlotCount; i++)
            {
                var slot = ReadStruct<OverlayEventSlot>(_headerSize + i * _slotSize);
                if (slot.EventId > _lastProcessedEventId && slot.EventId <= header.LatestEventId)
                {
                    newEvents.Add(slot);
                }
            }

            newEvents.Sort((a, b) => a.EventId.CompareTo(b.EventId));
            _lastProcessedEventId = header.LatestEventId;
        }

        return new OverlayPollResult(header, clearRequested, newEvents);
    }

    private T ReadStruct<T>(long offset) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        var bytes = new byte[size];
        _accessor.ReadArray(offset, bytes, 0, size);

        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    public void Dispose()
    {
        _accessor.Dispose();
        _mmf.Dispose();
    }
}
