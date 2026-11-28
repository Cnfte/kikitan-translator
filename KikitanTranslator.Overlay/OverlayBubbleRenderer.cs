using System.Collections.Concurrent;
using System.Text;
using SkiaSharp;

namespace KikitanTranslator.Overlay;

public readonly record struct BubbleTheme(
    bool LightMode,
    int GlassOpacity,
    int UiLangCode,
    bool SpeechToTextOnly,
    bool NoLanguageSpace,
    int ScalePercent = 100);

public sealed record OverlayChatEntry(
    bool IsOther,
    string Original,
    string Translation,
    bool IsFinal);

public readonly record struct BubbleRenderResult(
    byte[] BgraPremulPixels,
    byte[]? PngBytes,
    int Width,
    int Height,
    int CardWidth,
    int CardHeight,
    float TotalContentHeight,
    float ViewportHeight,
    float MaxScroll);

public static class OverlayBubbleRenderer
{
    private readonly record struct TypefaceKey(int RuneValue, bool Bold);

    private static readonly ConcurrentDictionary<TypefaceKey, SKTypeface> TypefaceCache = new();
    private static readonly SKTypeface FallbackTypeface = SKTypeface.FromFamilyName("Microsoft YaHei UI")
                                                          ?? SKTypeface.FromFamilyName("Segoe UI")
                                                          ?? SKTypeface.Default;

    private static SKTypeface ResolveTypeface(Rune rune, bool bold)
    {
        var key = new TypefaceKey(rune.Value, bold);
        return TypefaceCache.GetOrAdd(key, static k =>
        {
            var style = new SKFontStyle(
                k.Bold ? SKFontStyleWeight.SemiBold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright);
            return SKFontManager.Default.MatchCharacter(null, style, null, k.RuneValue) ?? FallbackTypeface;
        });
    }

    public static (string OtherPrefix, string SelfPrefix) GetSpeakerPrefixes(int uiLangCode) => uiLangCode switch
    {
        1 => ("Other: ", "Me: "),
        2 => ("相手：", "自分："),
        3 => ("상대: ", "나: "),
        _ => ("对方：", "我："),
    };

    private static List<(string text, SKColor color)> BuildRuns(string prefix, string body, SKColor prefixColor, SKColor bodyColor)
    {
        var runs = new List<(string, SKColor)>(2);
        if (!string.IsNullOrEmpty(prefix)) runs.Add((prefix, prefixColor));
        if (!string.IsNullOrEmpty(body)) runs.Add((body, bodyColor));
        return runs;
    }

    private sealed class WrappedBlock
    {
        public required List<List<(string text, SKColor color, bool bold)>> Lines;
        public float FontSize;
        public float LineHeight;
        public float MaxLineWidth;
    }

    private sealed class LayoutEntry
    {
        public required WrappedBlock Primary;
        public WrappedBlock? Secondary;
        public float TopY;
        public float Height;
    }

    /// <summary>
    /// Renders a unified chronological conversation stream (both "对方" and "我" together in one rounded card)
    /// that auto-sizes to text dimensions and smoothly scrolls when exceeding max height.
    /// </summary>
    public static BubbleRenderResult? RenderConversation(
        IReadOnlyList<OverlayChatEntry> entries,
        float scrollOffset,
        int screenWidth,
        int screenHeight,
        BubbleTheme theme,
        bool encodePngForVr = false,
        bool encodeBgraForDesktop = true,
        float dpiScale = 1.0f,
        bool snapToBottom = false)
    {
        if (entries.Count == 0) return null;

        var (otherPrefix, selfPrefix) = GetSpeakerPrefixes(theme.UiLangCode);

        bool light = theme.LightMode;
        SKColor primaryTextColor = light ? new SKColor(0x11, 0x18, 0x27, 0xFA) : new SKColor(0xF8, 0xFA, 0xFC, 0xFC);
        SKColor secondaryTextColor = light ? new SKColor(0x47, 0x55, 0x69, 0xE0) : new SKColor(0xD8, 0xE2, 0xEE, 0xD8);
        SKColor otherAccent = light ? new SKColor(0x02, 0x84, 0xC7, 0xFF) : new SKColor(0x7D, 0xD3, 0xFC, 0xFF);
        SKColor selfAccent = light ? new SKColor(0x05, 0x96, 0x69, 0xFF) : new SKColor(0x6E, 0xE7, 0xB7, 0xFF);

        float userScale = encodePngForVr ? 1.0f : Math.Clamp(theme.ScalePercent <= 0 ? 100 : theme.ScalePercent, 75, 150) / 100f;
        float scale = (encodePngForVr ? 2.0f : Math.Clamp(dpiScale, 1.0f, 3.0f)) * userScale;
        float baseScale = encodePngForVr ? 1.0f : Math.Clamp(dpiScale, 1.0f, 3.0f);
        float logicalScreenW = Math.Clamp((screenWidth > 0 ? screenWidth : 1920) / baseScale, 960f, 3840f);
        float logicalScreenH = Math.Clamp((screenHeight > 0 ? screenHeight : 1080) / baseScale, 600f, 2160f);

        float minCardW = 140f * scale;
        float maxCardW = (encodePngForVr ? 720f * scale : Math.Min(logicalScreenW * 0.56f * baseScale, Math.Clamp(logicalScreenW * 0.44f, 420f, 760f) * scale));
        float maxCardH = (encodePngForVr ? 360f * scale : Math.Min(logicalScreenH * 0.42f * baseScale, Math.Clamp(logicalScreenH * 0.25f, 190f, 280f) * scale));
        float padX = 20f * scale;
        float padY = 14f * scale;
        float shadowMargin = encodePngForVr ? 4f * scale : 12f * scale;
        float maxTextW = Math.Max(100f * scale, maxCardW - padX * 2f);

        float primaryFontSize = 19f * scale;
        float secondaryFontSize = 12.5f * scale;
        float primaryLineH = 26f * scale;
        float secondaryLineH = 18f * scale;
        float pairGap = 2.5f * scale;
        float entryGap = 11f * scale;

        var layoutEntries = new List<LayoutEntry>(entries.Count);
        float totalContentH = 0f;
        float maxMeasuredW = 0f;

        foreach (var entry in entries)
        {
            string cleanOrig = entry.Original?.Trim() ?? "";
            string cleanTrans = theme.SpeechToTextOnly ? "" : (entry.Translation?.Trim() ?? "");

            string primary;
            string secondary;
            string prefix;
            SKColor accent;

            if (entry.IsOther)
            {
                // Reference wireframe rule: 对方 -> Translation BIG on top, Original SMALL below
                primary = !string.IsNullOrEmpty(cleanTrans) ? cleanTrans : cleanOrig;
                secondary = !string.IsNullOrEmpty(cleanTrans) && !string.IsNullOrEmpty(cleanOrig) ? cleanOrig : "";
                prefix = otherPrefix;
                accent = otherAccent;
            }
            else
            {
                // Reference wireframe rule: 我 -> Original BIG on top, Translation SMALL below
                primary = !string.IsNullOrEmpty(cleanOrig) ? cleanOrig : cleanTrans;
                secondary = !string.IsNullOrEmpty(cleanOrig) && !string.IsNullOrEmpty(cleanTrans) ? cleanTrans : "";
                prefix = selfPrefix;
                accent = selfAccent;
            }

            if (string.IsNullOrEmpty(primary)) continue;

            var primaryBlock = WrapLineRuns(
                BuildRuns(prefix, primary, accent, primaryTextColor),
                primaryFontSize, primaryLineH, true, maxTextW, theme.NoLanguageSpace);

            WrappedBlock? secondaryBlock = !string.IsNullOrEmpty(secondary)
                ? WrapLineRuns(
                    BuildRuns(prefix, secondary, secondaryTextColor, secondaryTextColor),
                    secondaryFontSize, secondaryLineH, false, maxTextW, theme.NoLanguageSpace)
                : null;

            TrimLinesIfNeeded(primaryBlock, 4, maxTextW);
            TrimLinesIfNeeded(secondaryBlock, 3, maxTextW);

            maxMeasuredW = Math.Max(maxMeasuredW, primaryBlock.MaxLineWidth);
            if (secondaryBlock != null) maxMeasuredW = Math.Max(maxMeasuredW, secondaryBlock.MaxLineWidth);

            if (layoutEntries.Count > 0) totalContentH += entryGap;

            float entryTop = totalContentH;
            float entryH = primaryBlock.Lines.Count * primaryBlock.LineHeight;
            if (secondaryBlock != null)
            {
                entryH += pairGap + secondaryBlock.Lines.Count * secondaryBlock.LineHeight;
            }

            layoutEntries.Add(new LayoutEntry
            {
                Primary = primaryBlock,
                Secondary = secondaryBlock,
                TopY = entryTop,
                Height = entryH
            });

            totalContentH += entryH;
        }

        if (layoutEntries.Count == 0) return null;

        bool willScroll = (totalContentH + padY * 2f) > maxCardH + 0.5f;
        float scrollbarReserve = willScroll ? 8f * scale : 0f;

        int cardW = (int)MathF.Ceiling(Math.Clamp(maxMeasuredW + padX * 2f + scrollbarReserve, minCardW, maxCardW));
        int cardH = (int)MathF.Ceiling(Math.Clamp(totalContentH + padY * 2f, 52f * scale, maxCardH));
        float viewportH = MathF.Max(1f, cardH - padY * 2f);
        float maxScroll = MathF.Max(0f, totalContentH - viewportH);
        float effectiveScroll = snapToBottom ? maxScroll : Math.Clamp(scrollOffset, 0f, maxScroll);

        int marginInt = (int)MathF.Ceiling(shadowMargin);
        int totalW = cardW + marginInt * 2;
        int totalH = cardH + marginInt * 2;

        var info = new SKImageInfo(totalW, totalH, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        if (surface == null) return null;

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        float cornerRadius = MathF.Min(22f * scale, MathF.Min(cardW, cardH) * 0.40f);
        var cardRect = new SKRect(marginInt, marginInt, marginInt + cardW, marginInt + cardH);
        using var rrect = new SKRoundRect(cardRect, cornerRadius, cornerRadius);

        // Soft ambient drop shadow (desktop only)
        if (!encodePngForVr)
        {
            using var shadowPaint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(0, 0, 0, light ? (byte)45 : (byte)110),
                ImageFilter = SKImageFilter.CreateDropShadowOnly(0f, 3f * scale, 6.5f * scale, 6.5f * scale, new SKColor(0, 0, 0, light ? (byte)55 : (byte)120))
            };
            canvas.DrawRoundRect(rrect, shadowPaint);
        }

        // Translucent card surface calibrated by GlassOpacity
        float alphaFactor = Math.Clamp(theme.GlassOpacity, 0, 100) / 100f;
        byte fillAlpha = (byte)Math.Round(105 + alphaFactor * 140);
        SKColor fillTop = light ? new SKColor(0xFF, 0xFF, 0xFF, fillAlpha) : new SKColor(0x16, 0x19, 0x22, fillAlpha);
        SKColor fillBottom = light ? new SKColor(0xF4, 0xF6, 0xFA, fillAlpha) : new SKColor(0x0E, 0x11, 0x18, fillAlpha);

        using (var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cardRect.Left, cardRect.Top),
                new SKPoint(cardRect.Left, cardRect.Bottom),
                [fillTop, fillBottom],
                null,
                SKShaderTileMode.Clamp)
        })
        {
            canvas.DrawRoundRect(rrect, bgPaint);
        }

        // Subtle rim highlight border
        using (var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.25f * scale,
            Color = light ? new SKColor(0x0F, 0x17, 0x2A, 0x28) : new SKColor(0xFF, 0xFF, 0xFF, 0x3A)
        })
        {
            canvas.DrawRoundRect(rrect, borderPaint);
        }

        // Draw scrollable text layer into an isolated layer so top-fade mask blends seamlessly with the glass card
        var contentClip = new SKRect(cardRect.Left + 4f * scale, cardRect.Top + padY, cardRect.Right - 4f * scale, cardRect.Bottom - padY);
        float textLeft = cardRect.Left + padX;
        float contentOriginY = cardRect.Top + padY - effectiveScroll;

        canvas.Save();
        canvas.ClipRoundRect(rrect, antialias: true);
        canvas.ClipRect(contentClip);

        bool applyTopFade = effectiveScroll > 1f;
        if (applyTopFade)
        {
            canvas.SaveLayer(contentClip, null);
        }

        foreach (var item in layoutEntries)
        {
            float itemTop = contentOriginY + item.TopY;
            float itemBottom = itemTop + item.Height;
            if (itemBottom < contentClip.Top - 4f * scale || itemTop > contentClip.Bottom + 4f * scale)
                continue;

            float y = DrawBlock(canvas, item.Primary, textLeft, itemTop);
            if (item.Secondary != null)
            {
                y += pairGap;
                DrawBlock(canvas, item.Secondary, textLeft, y);
            }
        }

        if (applyTopFade)
        {
            // Soft top alpha feathering reaching true 0% before the top clip boundary so letters never hard-slice
            float fadeH = MathF.Min(32f * scale, viewportH * 0.24f);
            var fadeRect = new SKRect(contentClip.Left, contentClip.Top, contentClip.Right, contentClip.Top + fadeH);
            using var fadePaint = new SKPaint
            {
                BlendMode = SKBlendMode.DstIn,
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(fadeRect.Left, fadeRect.Top),
                    new SKPoint(fadeRect.Left, fadeRect.Bottom),
                    [new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 255)],
                    [0f, 0.14f, 1f],
                    SKShaderTileMode.Clamp)
            };
            canvas.DrawRect(fadeRect, fadePaint);
            canvas.Restore(); // End SaveLayer
        }

        canvas.Restore();

        // Subtle minimal scroll thumb on the inner right edge when scrollable
        if (maxScroll > 1f)
        {
            float trackTop = cardRect.Top + padY + 4f * scale;
            float trackBottom = cardRect.Bottom - padY - 4f * scale;
            float trackH = MathF.Max(12f * scale, trackBottom - trackTop);
            float thumbH = Math.Clamp(trackH * (viewportH / totalContentH), 22f * scale, trackH * 0.72f);
            float ratio = effectiveScroll / maxScroll;
            float thumbY = trackTop + (trackH - thumbH) * ratio;
            float thumbW = 3f * scale;
            float thumbX = cardRect.Right - 8f * scale - thumbW;

            using var thumbPaint = new SKPaint
            {
                IsAntialias = true,
                Color = light ? new SKColor(0x0F, 0x17, 0x2A, 0x40) : new SKColor(0xFF, 0xFF, 0xFF, 0x48)
            };
            var thumbRect = new SKRect(thumbX, thumbY, thumbX + thumbW, thumbY + thumbH);
            canvas.DrawRoundRect(new SKRoundRect(thumbRect, thumbW * 0.5f, thumbW * 0.5f), thumbPaint);
        }

        byte[] bgraBytes = [];
        if (encodeBgraForDesktop)
        {
            bgraBytes = new byte[totalW * totalH * 4];
            var pin = System.Runtime.InteropServices.GCHandle.Alloc(bgraBytes, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                surface.ReadPixels(info, pin.AddrOfPinnedObject(), totalW * 4, 0, 0);
            }
            finally
            {
                pin.Free();
            }
        }

        byte[]? pngBytes = null;
        if (encodePngForVr)
        {
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            pngBytes = data.ToArray();
        }

        return new BubbleRenderResult(bgraBytes, pngBytes, totalW, totalH, cardW, cardH, totalContentH, viewportH, maxScroll);
    }

    private static float DrawBlock(SKCanvas canvas, WrappedBlock block, float left, float topY)
    {
        float baselineOffset = block.FontSize * 0.90f;
        using var paint = new SKPaint { IsAntialias = true };
        var fontCache = new Dictionary<SKTypeface, SKFont>();

        try
        {
            for (int lineIdx = 0; lineIdx < block.Lines.Count; lineIdx++)
            {
                float baselineY = topY + lineIdx * block.LineHeight + baselineOffset;
                float cursorX = left;

                foreach (var (text, color, bold) in block.Lines[lineIdx])
                {
                    paint.Color = color;
                    SKTypeface? currentTf = null;
                    var segment = new StringBuilder();

                    void FlushSegment()
                    {
                        if (segment.Length == 0 || currentTf == null) return;
                        if (!fontCache.TryGetValue(currentTf, out var font))
                        {
                            font = new SKFont(currentTf, block.FontSize)
                            {
                                Subpixel = true,
                                Edging = SKFontEdging.Antialias
                            };
                            fontCache[currentTf] = font;
                        }
                        var str = segment.ToString();
                        canvas.DrawText(str, cursorX, baselineY, SKTextAlign.Left, font, paint);
                        cursorX += font.MeasureText(str, paint);
                        segment.Clear();
                    }

                    foreach (var rune in text.EnumerateRunes())
                    {
                        var tf = ResolveTypeface(rune, bold);
                        if (!ReferenceEquals(tf, currentTf))
                        {
                            FlushSegment();
                            currentTf = tf;
                        }
                        segment.Append(rune.ToString());
                    }
                    FlushSegment();
                }
            }
        }
        finally
        {
            foreach (var font in fontCache.Values) font.Dispose();
        }

        return topY + block.Lines.Count * block.LineHeight;
    }

    private static float MeasureStyledLine(List<(string text, SKColor color, bool bold)> runs, float fontSize)
    {
        using var paint = new SKPaint { IsAntialias = true };
        var fontCache = new Dictionary<SKTypeface, SKFont>();
        float width = 0f;
        try
        {
            foreach (var (text, _, bold) in runs)
            {
                SKTypeface? currentTf = null;
                var segment = new StringBuilder();
                void Flush()
                {
                    if (segment.Length == 0 || currentTf == null) return;
                    if (!fontCache.TryGetValue(currentTf, out var font))
                    {
                        font = new SKFont(currentTf, fontSize) { Subpixel = true };
                        fontCache[currentTf] = font;
                    }
                    width += font.MeasureText(segment.ToString(), paint);
                    segment.Clear();
                }

                foreach (var rune in text.EnumerateRunes())
                {
                    var tf = ResolveTypeface(rune, bold);
                    if (!ReferenceEquals(tf, currentTf))
                    {
                        Flush();
                        currentTf = tf;
                    }
                    segment.Append(rune.ToString());
                }
                Flush();
            }
        }
        finally
        {
            foreach (var f in fontCache.Values) f.Dispose();
        }
        return width;
    }

    private static void TrimLinesIfNeeded(WrappedBlock? block, int maxLines, float maxTextW)
    {
        if (block == null || block.Lines.Count <= maxLines) return;
        int skip = block.Lines.Count - maxLines;
        block.Lines = block.Lines.Skip(skip).ToList();
        if (block.Lines.Count > 0 && block.Lines[0].Count > 0)
        {
            var first = block.Lines[0][0];
            block.Lines[0][0] = ("…" + first.text, first.color, first.bold);
        }

        float maxW = 0f;
        foreach (var line in block.Lines)
        {
            maxW = Math.Max(maxW, MeasureStyledLine(line, block.FontSize));
        }
        block.MaxLineWidth = Math.Min(maxW, maxTextW);
    }

    private static WrappedBlock WrapLineRuns(
        List<(string text, SKColor color)> coloredRuns,
        float fontSize,
        float lineHeight,
        bool bold,
        float maxWidth,
        bool noSpaceLanguage)
    {
        var atoms = new List<(Rune rune, SKColor color, float width)>();
        using var dummyPaint = new SKPaint { IsAntialias = true };
        var fontCache = new Dictionary<SKTypeface, SKFont>();

        try
        {
            foreach (var (text, color) in coloredRuns)
            {
                foreach (var rune in text.Replace("\r\n", "\n").Replace('\n', ' ').EnumerateRunes())
                {
                    if (rune.Value == '\r') continue;
                    var tf = ResolveTypeface(rune, bold);
                    if (!fontCache.TryGetValue(tf, out var font))
                    {
                        font = new SKFont(tf, fontSize) { Subpixel = true };
                        fontCache[tf] = font;
                    }
                    float w = font.MeasureText(rune.ToString(), dummyPaint);
                    atoms.Add((rune, color, w));
                }
            }
        }
        finally
        {
            foreach (var f in fontCache.Values) f.Dispose();
        }

        var lines = new List<List<(string text, SKColor color, bool bold)>>();
        var currentLineAtoms = new List<(Rune rune, SKColor color, float width)>();
        float currentWidth = 0f;
        float maxMeasuredLine = 0f;

        int i = 0;
        while (i < atoms.Count)
        {
            int unitStart = i;
            var firstRune = atoms[i].rune;
            bool isSpace = Rune.IsWhiteSpace(firstRune);

            if (isSpace)
            {
                i++;
            }
            else if (firstRune.Value <= 127 && !noSpaceLanguage)
            {
                while (i < atoms.Count && atoms[i].rune.Value <= 127 && !Rune.IsWhiteSpace(atoms[i].rune))
                    i++;
            }
            else if (firstRune.Value <= 127 && noSpaceLanguage && Rune.IsLetterOrDigit(firstRune))
            {
                while (i < atoms.Count && atoms[i].rune.Value <= 127 && Rune.IsLetterOrDigit(atoms[i].rune))
                    i++;
            }
            else
            {
                i++;
            }

            float unitWidth = 0f;
            for (int k = unitStart; k < i; k++) unitWidth += atoms[k].width;

            if (isSpace && currentLineAtoms.Count == 0) continue;

            if (currentWidth + unitWidth <= maxWidth || currentLineAtoms.Count == 0)
            {
                if (unitWidth > maxWidth && currentLineAtoms.Count == 0)
                {
                    for (int k = unitStart; k < i; k++)
                    {
                        if (currentWidth + atoms[k].width > maxWidth && currentLineAtoms.Count > 0)
                        {
                            FlushAtoms(currentLineAtoms, lines, bold, ref currentWidth, ref maxMeasuredLine);
                        }
                        currentLineAtoms.Add(atoms[k]);
                        currentWidth += atoms[k].width;
                    }
                }
                else
                {
                    for (int k = unitStart; k < i; k++)
                    {
                        currentLineAtoms.Add(atoms[k]);
                        currentWidth += atoms[k].width;
                    }
                }
            }
            else
            {
                FlushAtoms(currentLineAtoms, lines, bold, ref currentWidth, ref maxMeasuredLine);
                if (!isSpace)
                {
                    if (unitWidth > maxWidth)
                    {
                        for (int k = unitStart; k < i; k++)
                        {
                            if (currentWidth + atoms[k].width > maxWidth && currentLineAtoms.Count > 0)
                            {
                                FlushAtoms(currentLineAtoms, lines, bold, ref currentWidth, ref maxMeasuredLine);
                            }
                            currentLineAtoms.Add(atoms[k]);
                            currentWidth += atoms[k].width;
                        }
                    }
                    else
                    {
                        for (int k = unitStart; k < i; k++)
                        {
                            currentLineAtoms.Add(atoms[k]);
                            currentWidth += atoms[k].width;
                        }
                    }
                }
            }
        }

        if (currentLineAtoms.Count > 0)
        {
            FlushAtoms(currentLineAtoms, lines, bold, ref currentWidth, ref maxMeasuredLine);
        }

        return new WrappedBlock
        {
            Lines = lines,
            FontSize = fontSize,
            LineHeight = lineHeight,
            MaxLineWidth = maxMeasuredLine
        };
    }

    private static void FlushAtoms(
        List<(Rune rune, SKColor color, float width)> lineAtoms,
        List<List<(string text, SKColor color, bool bold)>> lines,
        bool bold,
        ref float currentWidth,
        ref float maxMeasuredLine)
    {
        while (lineAtoms.Count > 0 && Rune.IsWhiteSpace(lineAtoms[^1].rune))
        {
            currentWidth -= lineAtoms[^1].width;
            lineAtoms.RemoveAt(lineAtoms.Count - 1);
        }

        if (lineAtoms.Count == 0)
        {
            currentWidth = 0f;
            return;
        }

        maxMeasuredLine = Math.Max(maxMeasuredLine, currentWidth);
        var runs = new List<(string, SKColor, bool)>();
        var sb = new StringBuilder();
        SKColor curColor = lineAtoms[0].color;

        foreach (var atom in lineAtoms)
        {
            if (atom.color != curColor && sb.Length > 0)
            {
                runs.Add((sb.ToString(), curColor, bold));
                sb.Clear();
                curColor = atom.color;
            }
            sb.Append(atom.rune.ToString());
        }
        if (sb.Length > 0) runs.Add((sb.ToString(), curColor, bold));

        lines.Add(runs);
        lineAtoms.Clear();
        currentWidth = 0f;
    }
}
