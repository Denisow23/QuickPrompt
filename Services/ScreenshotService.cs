using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace QuickPrompt.Services;

public class ScreenshotService
{
    public string CapturePrimaryScreenAsBase64Png()
    {
        var bounds = Screen.PrimaryScreen?.Bounds ?? throw new InvalidOperationException("Primary screen is unavailable.");
        using var bmp = new Bitmap(bounds.Width, bounds.Height);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }
}
