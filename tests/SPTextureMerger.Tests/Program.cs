using SPTextureMerger.Core;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "GeneratedTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            Run("RGBA non-overlap merge", () => RgbaNonOverlapMerge(root));
            Run("Overlap warning and row priority", () => OverlapWarningAndPriority(root));
            Run("Normal merge normalizes and defaults", () => NormalMerge(root));
            Run("Closed outline mask fills interior", () => OutlineMaskFill());
            Run("Dimension mismatch validation", () => DimensionMismatch(root));
            Console.WriteLine("All tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void Run(string name, Action test)
    {
        test();
        Console.WriteLine($"PASS {name}");
    }

    private static void RgbaNonOverlapMerge(string root)
    {
        var column = new TextureColumnDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Albedo",
            Behavior = MergeBehavior.RgbaCopy
        };

        var maskA = Save(root, "maskA.png", RectMask(4, 4, 0, 0, 2, 4));
        var maskB = Save(root, "maskB.png", RectMask(4, 4, 2, 0, 4, 4));
        var red = Save(root, "red.png", Solid(4, 4, 255, 0, 0, 200));
        var green = Save(root, "green.png", Solid(4, 4, 0, 255, 0, 180));
        var output = Path.Combine(root, "rgba");

        var project = new MergeProject
        {
            OutputBaseName = "Asset",
            OutputDirectory = output,
            Columns = [column],
            Rows =
            [
                Row("A", maskA, column.Id, red),
                Row("B", maskB, column.Id, green)
            ]
        };

        MergeEngine.Merge(project);
        var result = ImageIo.LoadRgba(Path.Combine(output, "Asset_Albedo.png"));
        AssertPixel(result, 0, 1, 255, 0, 0, 200);
        AssertPixel(result, 3, 1, 0, 255, 0, 180);
    }

    private static void OverlapWarningAndPriority(string root)
    {
        var column = new TextureColumnDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Albedo",
            Behavior = MergeBehavior.RgbaCopy
        };

        var maskA = Save(root, "overlapMaskA.png", RectMask(4, 4, 0, 0, 3, 4));
        var maskB = Save(root, "overlapMaskB.png", RectMask(4, 4, 1, 0, 4, 4));
        var red = Save(root, "overlapRed.png", Solid(4, 4, 255, 0, 0, 255));
        var blue = Save(root, "overlapBlue.png", Solid(4, 4, 0, 0, 255, 255));
        var output = Path.Combine(root, "overlap");

        var project = new MergeProject
        {
            OutputBaseName = "Asset",
            OutputDirectory = output,
            Columns = [column],
            Rows =
            [
                Row("A", maskA, column.Id, red),
                Row("B", maskB, column.Id, blue)
            ]
        };

        var validation = MergeEngine.Validate(project);
        Assert(validation.Messages.Any(message => message.Severity == ValidationSeverity.Warning && message.Message.Contains("overlap", StringComparison.OrdinalIgnoreCase)), "Expected overlap warning.");

        MergeEngine.Merge(project);
        var result = ImageIo.LoadRgba(Path.Combine(output, "Asset_Albedo.png"));
        AssertPixel(result, 1, 1, 0, 0, 255, 255);
    }

    private static void NormalMerge(string root)
    {
        var column = new TextureColumnDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Normal",
            Behavior = MergeBehavior.NormalReplaceNormalize
        };

        var mask = Save(root, "normalMask.png", RectMask(2, 1, 0, 0, 1, 1));
        var source = new TextureImage(2, 1, [255, 255, 255, 255, 0, 0, 0, 255]);
        var sourcePath = Save(root, "normalSource.png", source);
        var output = Path.Combine(root, "normal");
        var project = new MergeProject
        {
            OutputBaseName = "Asset",
            OutputDirectory = output,
            Columns = [column],
            Rows = [Row("A", mask, column.Id, sourcePath)]
        };

        MergeEngine.Merge(project);
        var result = ImageIo.LoadRgba(Path.Combine(output, "Asset_Normal.png"));
        var offset = result.Offset(0, 0);
        var length = VectorLength(result.Pixels[offset], result.Pixels[offset + 1], result.Pixels[offset + 2]);
        Assert(Math.Abs(length - 1) < 0.02, $"Expected normalized vector, got length {length}.");
        AssertPixel(result, 1, 0, 128, 128, 255, 255);
    }

    private static void OutlineMaskFill()
    {
        var mask = Transparent(8, 8);
        for (var x = 2; x <= 5; x++)
        {
            SetPixel(mask, x, 2, 255, 255, 255, 255);
            SetPixel(mask, x, 5, 255, 255, 255, 255);
        }

        for (var y = 2; y <= 5; y++)
        {
            SetPixel(mask, 2, y, 255, 255, 255, 255);
            SetPixel(mask, 5, y, 255, 255, 255, 255);
        }

        var result = MaskProcessor.Build(mask);
        Assert(result.Coverage[3 * 8 + 3] == 255, "Expected closed outline interior to be filled.");
        Assert(result.Coverage[0] == 0, "Expected outside area to remain empty.");
    }

    private static void DimensionMismatch(string root)
    {
        var column = new TextureColumnDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Data",
            Behavior = MergeBehavior.DataCopy
        };

        var mask = Save(root, "sizeMask.png", RectMask(4, 4, 0, 0, 4, 4));
        var source = Save(root, "sizeSource.png", Solid(2, 2, 1, 2, 3, 4));
        var project = new MergeProject
        {
            OutputBaseName = "Asset",
            OutputDirectory = Path.Combine(root, "size"),
            Columns = [column],
            Rows = [Row("A", mask, column.Id, source)]
        };

        var validation = MergeEngine.Validate(project);
        Assert(!validation.CanMerge, "Expected validation to fail.");
        Assert(validation.Messages.Any(message => message.Message.Contains("expected", StringComparison.OrdinalIgnoreCase)), "Expected dimension mismatch message.");
    }

    private static TextureSetRowDefinition Row(string name, string maskPath, Guid columnId, string texturePath)
    {
        return new TextureSetRowDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            MaskPath = maskPath,
            TexturePaths = { [columnId] = texturePath }
        };
    }

    private static string Save(string root, string fileName, TextureImage image)
    {
        var path = Path.Combine(root, fileName);
        ImageIo.Save(image, path);
        return path;
    }

    private static TextureImage Transparent(int width, int height)
    {
        return new TextureImage(width, height, new byte[width * height * 4]);
    }

    private static TextureImage Solid(int width, int height, byte r, byte g, byte b, byte a)
    {
        var image = Transparent(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                SetPixel(image, x, y, r, g, b, a);
            }
        }

        return image;
    }

    private static TextureImage RectMask(int width, int height, int left, int top, int right, int bottom)
    {
        var image = Transparent(width, height);
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                SetPixel(image, x, y, 255, 255, 255, 255);
            }
        }

        return image;
    }

    private static void SetPixel(TextureImage image, int x, int y, byte r, byte g, byte b, byte a)
    {
        var offset = image.Offset(x, y);
        image.Pixels[offset] = r;
        image.Pixels[offset + 1] = g;
        image.Pixels[offset + 2] = b;
        image.Pixels[offset + 3] = a;
    }

    private static void AssertPixel(TextureImage image, int x, int y, byte r, byte g, byte b, byte a)
    {
        var offset = image.Offset(x, y);
        Assert(image.Pixels[offset] == r, $"R mismatch at {x},{y}: {image.Pixels[offset]} != {r}");
        Assert(image.Pixels[offset + 1] == g, $"G mismatch at {x},{y}: {image.Pixels[offset + 1]} != {g}");
        Assert(image.Pixels[offset + 2] == b, $"B mismatch at {x},{y}: {image.Pixels[offset + 2]} != {b}");
        Assert(image.Pixels[offset + 3] == a, $"A mismatch at {x},{y}: {image.Pixels[offset + 3]} != {a}");
    }

    private static double VectorLength(byte r, byte g, byte b)
    {
        var x = r / 255.0 * 2.0 - 1.0;
        var y = g / 255.0 * 2.0 - 1.0;
        var z = b / 255.0 * 2.0 - 1.0;
        return Math.Sqrt(x * x + y * y + z * z);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

