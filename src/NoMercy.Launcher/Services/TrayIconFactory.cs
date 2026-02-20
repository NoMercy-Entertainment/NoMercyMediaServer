using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NoMercy.Launcher.Services;

public enum ServerState
{
    Disconnected,
    Starting,
    Running
}

public static class TrayIconFactory
{
    private static Bitmap? _baseIcon;

    public static WindowIcon CreateIcon(ServerState state)
    {
        Bitmap baseIcon = LoadBaseIcon();
        int width = baseIcon.PixelSize.Width;
        int height = baseIcon.PixelSize.Height;

        WriteableBitmap overlay = new(
            new(width, height),
            baseIcon.Dpi,
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (ILockedFramebuffer buffer = overlay.Lock())
        {
            baseIcon.CopyPixels(buffer, AlphaFormat.Premul);
            DrawStatusDot(buffer, state);
        }

        using MemoryStream stream = new();
        overlay.Save(stream);
        stream.Position = 0;

        return new(stream);
    }

    private static Bitmap LoadBaseIcon()
    {
        if (_baseIcon is not null) return _baseIcon;

        string iconPath = Path.Combine(
            AppContext.BaseDirectory, "Assets", "icon.png");

        if (File.Exists(iconPath))
        {
            _baseIcon = new(iconPath);
            return _baseIcon;
        }

        Stream? resourceStream = typeof(TrayIconFactory).Assembly
            .GetManifestResourceStream("NoMercy.Launcher.icon.png");

        if (resourceStream is not null)
        {
            _baseIcon = new(resourceStream);
            return _baseIcon;
        }

        _baseIcon = CreateFallbackIcon();
        return _baseIcon;
    }

    private static Bitmap CreateFallbackIcon()
    {
        int size = 64;
        WriteableBitmap bmp = new(
            new(size, size),
            new(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (ILockedFramebuffer buffer = bmp.Lock())
        {
            unsafe
            {
                byte* ptr = (byte*)buffer.Address;
                int stride = buffer.RowBytes;
                int cx = size / 2;
                int cy = size / 2;
                int radius = size / 2 - 2;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int dx = x - cx;
                        int dy = y - cy;
                        int offset = y * stride + x * 4;

                        if (dx * dx + dy * dy <= radius * radius)
                        {
                            ptr[offset + 0] = 180; // B
                            ptr[offset + 1] = 80;  // G
                            ptr[offset + 2] = 40;  // R
                            ptr[offset + 3] = 255; // A
                        }
                    }
                }
            }
        }

        using MemoryStream stream = new();
        bmp.Save(stream);
        stream.Position = 0;
        return new(stream);
    }

    private static unsafe void DrawStatusDot(
        ILockedFramebuffer buffer, ServerState state)
    {
        (byte r, byte g, byte b) = state switch
        {
            ServerState.Running =>
                ((byte)34, (byte)197, (byte)94),
            ServerState.Starting =>
                ((byte)234, (byte)179, (byte)8),
            ServerState.Disconnected =>
                ((byte)239, (byte)68, (byte)68),
            _ =>
                ((byte)239, (byte)68, (byte)68)
        };

        int width = buffer.Size.Width;
        int height = buffer.Size.Height;
        int dotRadius = Math.Max(width / 6, 4);
        int borderWidth = Math.Max(dotRadius / 4, 1);
        int cx = width - dotRadius - 1;
        int cy = height - dotRadius - 1;
        int stride = buffer.RowBytes;

        byte* ptr = (byte*)buffer.Address;
        int outerRadius = dotRadius + borderWidth;

        for (int y = Math.Max(0, cy - outerRadius);
             y <= Math.Min(height - 1, cy + outerRadius);
             y++)
        {
            for (int x = Math.Max(0, cx - outerRadius);
                 x <= Math.Min(width - 1, cx + outerRadius);
                 x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                int distSq = dx * dx + dy * dy;

                if (distSq <= outerRadius * outerRadius)
                {
                    int offset = y * stride + x * 4;

                    if (distSq <= dotRadius * dotRadius)
                    {
                        ptr[offset + 0] = b;
                        ptr[offset + 1] = g;
                        ptr[offset + 2] = r;
                        ptr[offset + 3] = 255;
                    }
                    else
                    {
                        ptr[offset + 0] = 30;
                        ptr[offset + 1] = 30;
                        ptr[offset + 2] = 30;
                        ptr[offset + 3] = 255;
                    }
                }
            }
        }
    }
}
