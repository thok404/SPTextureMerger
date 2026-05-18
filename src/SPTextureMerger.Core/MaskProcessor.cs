namespace SPTextureMerger.Core;

public static class MaskProcessor
{
    private const byte BarrierThreshold = 24;

    public static MaskBuildResult Build(TextureImage mask)
    {
        var length = mask.Width * mask.Height;
        var barrier = new bool[length];
        for (var i = 0; i < length; i++)
        {
            var offset = i * 4;
            var r = mask.Pixels[offset];
            var g = mask.Pixels[offset + 1];
            var b = mask.Pixels[offset + 2];
            var a = mask.Pixels[offset + 3];
            var brightness = (r + g + b) / 3;
            var whiteStrength = brightness * a / 255;
            barrier[i] = whiteStrength >= BarrierThreshold && r >= 80 && g >= 80 && b >= 80;
        }

        var outside = FloodOutside(mask.Width, mask.Height, barrier);
        var coverage = new byte[length];
        var coveredPixels = 0;
        for (var i = 0; i < length; i++)
        {
            if (!outside[i])
            {
                coverage[i] = 255;
                coveredPixels++;
            }
        }

        var warnings = new List<string>();
        var ratio = length == 0 ? 0 : coveredPixels / (double)length;
        if (coveredPixels == 0)
        {
            warnings.Add("Mask produced no covered pixels. Check that the UV island outline is white and closed.");
        }
        else if (ratio > 0.95)
        {
            warnings.Add("Mask covers almost the entire image. Check for an outline touching the image border or a filled full-frame mask.");
        }
        else if (ratio < 0.0005)
        {
            warnings.Add("Mask covers a very small area. Check for gaps in the outline.");
        }

        return new MaskBuildResult(mask.Width, mask.Height, coverage, warnings, coveredPixels, ratio);
    }

    public static TextureImage CreatePreview(MaskBuildResult mask)
    {
        var pixels = new byte[mask.Width * mask.Height * 4];
        for (var i = 0; i < mask.Coverage.Length; i++)
        {
            var value = mask.Coverage[i];
            var offset = i * 4;
            pixels[offset] = value;
            pixels[offset + 1] = value;
            pixels[offset + 2] = value;
            pixels[offset + 3] = 255;
        }

        return new TextureImage(mask.Width, mask.Height, pixels);
    }

    private static bool[] FloodOutside(int width, int height, bool[] barrier)
    {
        var outside = new bool[barrier.Length];
        var queue = new Queue<int>();

        void TryEnqueue(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            var index = y * width + x;
            if (outside[index] || barrier[index])
            {
                return;
            }

            outside[index] = true;
            queue.Enqueue(index);
        }

        for (var x = 0; x < width; x++)
        {
            TryEnqueue(x, 0);
            TryEnqueue(x, height - 1);
        }

        for (var y = 0; y < height; y++)
        {
            TryEnqueue(0, y);
            TryEnqueue(width - 1, y);
        }

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var x = index % width;
            var y = index / width;
            TryEnqueue(x + 1, y);
            TryEnqueue(x - 1, y);
            TryEnqueue(x, y + 1);
            TryEnqueue(x, y - 1);
        }

        return outside;
    }
}

public sealed record MaskBuildResult(
    int Width,
    int Height,
    byte[] Coverage,
    IReadOnlyList<string> Warnings,
    int CoveredPixels,
    double CoverageRatio);

