namespace SPTextureMerger.Core;

public sealed class TextureImage
{
    public TextureImage(int width, int height, byte[] pixels)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (pixels.Length != width * height * 4)
        {
            throw new ArgumentException("Pixel buffer must be width * height * 4 bytes.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public int Offset(int x, int y)
    {
        return (y * Width + x) * 4;
    }
}

