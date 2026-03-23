using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace QuickPrompt.Services;

public sealed class ScreenshotPayload
{
    public required string Base64 { get; init; }
    public required string MimeType { get; init; }
    public required int ByteSize { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
}

public class ScreenshotService
{
    private const int MaxLongSide = 1600;
    private const long JpegQuality = 82L;

    public ScreenshotPayload CapturePrimaryScreenAsPayload()
    {
        var bounds = Screen.PrimaryScreen?.Bounds ?? throw new InvalidOperationException("Primary screen is unavailable.");

        using var source = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(source))
        {
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }

        using var normalized = ResizeIfNeeded(source, MaxLongSide);
        using var ms = new MemoryStream();

        var encoder = GetEncoder(ImageFormat.Jpeg);
        if (encoder is not null)
        {
            var quality = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, JpegQuality);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = quality;
            normalized.Save(ms, encoder, encoderParams);

            return new ScreenshotPayload
            {
                Base64 = Convert.ToBase64String(ms.ToArray()),
                MimeType = "image/jpeg",
                ByteSize = (int)ms.Length,
                Width = normalized.Width,
                Height = normalized.Height
            };
        }

        normalized.Save(ms, ImageFormat.Png);
        return new ScreenshotPayload
        {
            Base64 = Convert.ToBase64String(ms.ToArray()),
            MimeType = "image/png",
            ByteSize = (int)ms.Length,
            Width = normalized.Width,
            Height = normalized.Height
        };
    }

    private static Bitmap ResizeIfNeeded(Bitmap input, int maxLongSide)
    {
        var longSide = Math.Max(input.Width, input.Height);
        if (longSide <= maxLongSide)
        {
            return new Bitmap(input);
        }

        var scale = maxLongSide / (double)longSide;
        var width = Math.Max(1, (int)Math.Round(input.Width * scale));
        var height = Math.Max(1, (int)Math.Round(input.Height * scale));

        var resized = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(resized);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.DrawImage(input, 0, 0, width, height);

        return resized;
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        return ImageCodecInfo.GetImageDecoders()
            .FirstOrDefault(codec => codec.FormatID == format.Guid);
    }
}
