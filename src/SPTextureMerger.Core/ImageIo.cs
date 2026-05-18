using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SPTextureMerger.Core;

public static class ImageIo
{
    private static readonly HashSet<string> SupportedInputExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".tif",
        ".tiff",
        ".bmp",
        ".jpg",
        ".jpeg"
    };

    public static bool IsSupportedInput(string path)
    {
        return SupportedInputExtensions.Contains(Path.GetExtension(path));
    }

    public static ImageSize ReadSize(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        return new ImageSize(frame.PixelWidth, frame.PixelHeight);
    }

    public static TextureImage LoadRgba(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource source = decoder.Frames[0];

        if (source.Format != PixelFormats.Bgra32)
        {
            source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        }

        var stride = source.PixelWidth * 4;
        var bgra = new byte[stride * source.PixelHeight];
        source.CopyPixels(bgra, stride, 0);
        return new TextureImage(source.PixelWidth, source.PixelHeight, BgraToRgba(bgra));
    }

    public static void Save(TextureImage image, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var bgra = RgbaToBgra(image.Pixels);
        var source = BitmapSource.Create(
            image.Width,
            image.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            bgra,
            image.Width * 4);

        BitmapEncoder encoder = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".tif" or ".tiff" => new TiffBitmapEncoder { Compression = TiffCompressOption.Zip },
            _ => new PngBitmapEncoder()
        };

        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    public static BitmapSource CreateBitmapSource(TextureImage image)
    {
        var source = BitmapSource.Create(
            image.Width,
            image.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            RgbaToBgra(image.Pixels),
            image.Width * 4);
        source.Freeze();
        return source;
    }

    private static byte[] BgraToRgba(byte[] bgra)
    {
        var rgba = new byte[bgra.Length];
        for (var i = 0; i < bgra.Length; i += 4)
        {
            rgba[i] = bgra[i + 2];
            rgba[i + 1] = bgra[i + 1];
            rgba[i + 2] = bgra[i];
            rgba[i + 3] = bgra[i + 3];
        }

        return rgba;
    }

    private static byte[] RgbaToBgra(byte[] rgba)
    {
        var bgra = new byte[rgba.Length];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            bgra[i] = rgba[i + 2];
            bgra[i + 1] = rgba[i + 1];
            bgra[i + 2] = rgba[i];
            bgra[i + 3] = rgba[i + 3];
        }

        return bgra;
    }
}

public readonly record struct ImageSize(int Width, int Height)
{
    public override string ToString()
    {
        return $"{Width} x {Height}";
    }
}
