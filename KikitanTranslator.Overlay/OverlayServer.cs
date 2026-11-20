using System.IO.MemoryMappedFiles;
using KikitanTranslator.Overlay;
using SkiaSharp;


public class OverlayServer
{
    private Form1 _form;
    private OpenVROverlay _openvroverlay;

    private bool _desktop;

    private async Task StartReader()
    {
        var reader = new OverlayReader();
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

        while (await timer.WaitForNextTickAsync())
        {
            var state = reader.Read();
            if (state is not null) SendText(state.Value.Text, state.Value.NoLanguageSpace, state.Value.Time);
        }
    }

    public void StartForm(Form1 form)
    {
        Task.Run(StartReader);

        _form = form;
        _desktop = true;
    }

    public void StartOpenVR(OpenVROverlay openVROverlay)
    {
        Task.Run(StartReader);

        _openvroverlay = openVROverlay;
        _openvroverlay.ImageLoop();
    }

    public void SendText(string text, bool noLanguageSpace, int time = 5000)
    {
        var img = DrawTextOnCanvas(text, noLanguageSpace, time);
        var ms = new MemoryStream(img);
        if (_desktop)
        {
            Image image = Image.FromStream(ms);
            ms.Close();

            _form.SetImage(image, time);
        }
        else
        {
            _openvroverlay.AddToQueue(ms, time);
        }

        Console.WriteLine("Image received!");
    }

    private List<string> WrapText(string text, float fontSize, float maxWidth, SKPaint paint, bool noSpaceLanguage)
    {
        var resultLines = new List<string>();
        var paragraphs = text.Replace("\r\n", "\n").Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                resultLines.Add("");
                continue;
            }

            IEnumerable<string> tokens;
            if (noSpaceLanguage)
            {
                var tokenList = new List<string>();
                var wordBuffer = new System.Text.StringBuilder();
                foreach (char c in paragraph)
                {
                    if (c <= 127 && !char.IsWhiteSpace(c))
                    {
                        wordBuffer.Append(c);
                    }
                    else
                    {
                        if (wordBuffer.Length > 0)
                        {
                            tokenList.Add(wordBuffer.ToString());
                            wordBuffer.Clear();
                        }
                        if (!char.IsWhiteSpace(c))
                        {
                            tokenList.Add(c.ToString());
                        }
                    }
                }
                if (wordBuffer.Length > 0) tokenList.Add(wordBuffer.ToString());
                tokens = tokenList;
            }
            else
            {
                tokens = paragraph.Split(' ');
            }

            string currentLine = "";
            foreach (var token in tokens)
            {
                string separator = (noSpaceLanguage || currentLine.Length == 0) ? "" : " ";
                string testLine = currentLine + separator + token;

                if (MeasureWidthWithFallback(testLine, fontSize, paint) <= maxWidth)
                {
                    currentLine = testLine;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        resultLines.Add(currentLine.Trim());
                        currentLine = token;
                    }
                    else
                    {
                        string sub = "";
                        foreach (char ch in token)
                        {
                            if (MeasureWidthWithFallback(sub + ch, fontSize, paint) <= maxWidth)
                            {
                                sub += ch;
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(sub)) resultLines.Add(sub);
                                sub = ch.ToString();
                            }
                        }
                        currentLine = sub;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(currentLine))
            {
                resultLines.Add(currentLine.Trim());
            }
        }

        return resultLines;
    }

    private byte[] DrawTextOnCanvas(string text, bool noSpaceLanguage, int time)
    {
        const int canvasWidth = 4000;
        const int canvasHeight = 1250;
        const float minFontSize = 45f;
        const float maxFontSize = 280f;
        const float paddingX = 140f;
        const float paddingY = 80f;

        float maxTextWidth = canvasWidth - paddingX * 2;
        float maxTextHeight = canvasHeight - paddingY * 2;

        var imageInfo = new SKImageInfo(canvasWidth, canvasHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(imageInfo);
        var canvas = surface.Canvas;

        canvas.Clear(new SKColor(0x18, 0x18, 0x1c, 0xeb));

        var paint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
        };

        // Binary search for largest font size where wrapped lines fit in both width and height
        float low = minFontSize;
        float high = maxFontSize;
        float bestFontSize = minFontSize;
        List<string> bestLines = null;

        for (int iter = 0; iter < 12; iter++)
        {
            float mid = (low + high) / 2f;
            var lines = WrapText(text, mid, maxTextWidth, paint, noSpaceLanguage);
            float lineHeight = mid * 1.3f;
            float totalHeight = lines.Count * lineHeight;

            if (totalHeight <= maxTextHeight)
            {
                bestFontSize = mid;
                bestLines = lines;
                low = mid + 1f;
            }
            else
            {
                high = mid - 1f;
            }
        }

        if (bestLines == null)
        {
            bestFontSize = minFontSize;
            bestLines = WrapText(text, bestFontSize, maxTextWidth, paint, noSpaceLanguage);
        }

        // If at minFontSize it still exceeds maxTextHeight, limit lines and prepend clean ellipsis
        float finalLineHeight = bestFontSize * 1.3f;
        int maxAllowedLines = Math.Max(1, (int)(maxTextHeight / finalLineHeight));

        if (bestLines.Count > maxAllowedLines)
        {
            int skip = bestLines.Count - maxAllowedLines;
            var truncated = bestLines.Skip(skip).ToList();
            if (truncated.Count > 0 && !truncated[0].StartsWith("..."))
            {
                truncated[0] = "..." + truncated[0];
            }
            bestLines = truncated;
        }

        float totalBlockHeight = bestLines.Count * finalLineHeight;
        float centerX = canvasWidth / 2f;
        float centerY = canvasHeight / 2f;
        float startY = centerY - totalBlockHeight / 2f + (bestFontSize * 1.05f);

        for (int i = 0; i < bestLines.Count; i++)
        {
            float y = startY + i * finalLineHeight;
            DrawTextWithFallback(canvas, bestLines[i], centerX, y, paint, bestFontSize);
        }

        var image = surface.Snapshot();
        var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);

        return data.ToArray();
    }
    
    public void DrawTextWithFallback(SKCanvas canvas, string text, float x, float y, SKPaint paint, float fontSize)
    {
        var fontManager = SKFontManager.Default;
        var style = new SKFontStyle(
            paint.FakeBoldText ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);
        
        float totalWidth = 0f;
        var charData = new List<(string ch, SKTypeface typeface)>();

        foreach (var c in text)
        {
            var typeface = fontManager.MatchCharacter(null, style, null, c) ?? SKTypeface.FromFamilyName("Arial Unicode MS");

            var charStr = c.ToString();
            using var measurePaint = paint.Clone();
            measurePaint.Typeface = typeface;
            measurePaint.TextSize = fontSize;
            measurePaint.TextAlign = SKTextAlign.Left;

            totalWidth += measurePaint.MeasureText(charStr);
            charData.Add((charStr, typeface));
        }
        
        float cursorX = x - totalWidth / 2f;

        foreach (var (charStr, typeface) in charData)
        {
            using var charPaint = paint.Clone();
            charPaint.Typeface = typeface;
            charPaint.TextSize = fontSize;
            charPaint.TextAlign = SKTextAlign.Left;

            canvas.DrawText(charStr, cursorX, y, charPaint);
            cursorX += charPaint.MeasureText(charStr);
        }
    }

    private float MeasureWidthWithFallback(string text, float fontSize, SKPaint paint)
    {
        var fontManager = SKFontManager.Default;
        var style = new SKFontStyle(
            paint.FakeBoldText ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        float totalWidth = 0f;
        foreach (var c in text)
        {
            var typeface = fontManager.MatchCharacter(null, style, null, c)
                           ?? SKTypeface.FromFamilyName("Arial Unicode MS");

            using var measurePaint = paint.Clone();
            measurePaint.Typeface = typeface;
            measurePaint.TextSize = fontSize;
            measurePaint.TextAlign = SKTextAlign.Left;

            totalWidth += measurePaint.MeasureText(c.ToString());
        }
        return totalWidth;
    }

    private float MeasureHeightWithFallback(string text, float fontSize, SKPaint paint)
    {
        var fontManager = SKFontManager.Default;
        var style = new SKFontStyle(
            paint.FakeBoldText ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        float maxAscent = 0f, maxDescent = 0f;
        var seen = new HashSet<SKTypeface>();

        foreach (var c in text)
        {
            var typeface = fontManager.MatchCharacter(null, style, null, c)
                           ?? SKTypeface.FromFamilyName("Arial Unicode MS");
            if (!seen.Add(typeface)) continue;

            using var f = new SKFont(typeface, fontSize);
            f.GetFontMetrics(out var m);
            maxAscent = Math.Max(maxAscent, Math.Abs(m.Ascent));
            maxDescent = Math.Max(maxDescent, Math.Abs(m.Descent));
        }

        return maxAscent + maxDescent;
    }
}