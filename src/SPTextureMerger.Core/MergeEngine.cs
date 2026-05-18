using System.Globalization;
using System.IO;

namespace SPTextureMerger.Core;

public static class MergeEngine
{
    public static MergeValidationResult Validate(MergeProject project)
    {
        var result = new MergeValidationResult();

        if (project.Rows.Count == 0)
        {
            result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, "Add at least one texture set row.", null, null));
        }

        if (project.Columns.Count == 0)
        {
            result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, "Add at least one texture column.", null, null));
        }

        if (string.IsNullOrWhiteSpace(project.OutputDirectory))
        {
            result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, "Choose an output directory.", null, null));
        }

        if (string.IsNullOrWhiteSpace(project.OutputBaseName))
        {
            result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, "Set an output base name.", null, null));
        }

        foreach (var column in project.Columns)
        {
            if (string.IsNullOrWhiteSpace(column.Name))
            {
                result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, "Column name cannot be empty.", null, column.Id));
            }
            else if (column.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, $"Column '{column.Name}' contains invalid file name characters.", null, column.Id));
            }
        }

        ImageSize? targetSize = null;
        var preparedMasks = new Dictionary<Guid, MaskBuildResult>();

        foreach (var row in project.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, "Row name cannot be empty.", row.Id, null));
            }

            if (string.IsNullOrWhiteSpace(row.MaskPath))
            {
                result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, $"Row '{row.Name}' is missing a mask.", row.Id, null));
            }
            else if (!File.Exists(row.MaskPath))
            {
                result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, $"Mask file does not exist: {row.MaskPath}", row.Id, null));
            }
            else if (!ImageIo.IsSupportedInput(row.MaskPath))
            {
                result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, $"Unsupported mask format: {row.MaskPath}", row.Id, null));
            }
            else
            {
                try
                {
                    var maskImage = ImageIo.LoadRgba(row.MaskPath);
                    var maskSize = new ImageSize(maskImage.Width, maskImage.Height);
                    targetSize ??= maskSize;
                    if (targetSize.Value != maskSize)
                    {
                        result.Messages.Add(new ValidationMessage(
                            ValidationSeverity.Error,
                            $"Mask size for row '{row.Name}' is {maskSize}, expected {targetSize.Value}.",
                            row.Id,
                            null));
                    }

                    var mask = MaskProcessor.Build(maskImage);
                    preparedMasks[row.Id] = mask;
                    foreach (var warning in mask.Warnings)
                    {
                        result.Messages.Add(new ValidationMessage(ValidationSeverity.Warning, $"Row '{row.Name}': {warning}", row.Id, null));
                    }
                }
                catch (Exception ex)
                {
                    result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, $"Cannot read mask for row '{row.Name}': {ex.Message}", row.Id, null));
                }
            }

            foreach (var column in project.Columns)
            {
                row.TexturePaths.TryGetValue(column.Id, out var texturePath);
                if (string.IsNullOrWhiteSpace(texturePath))
                {
                    result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, $"Row '{row.Name}' is missing texture for column '{column.Name}'.", row.Id, column.Id));
                }
                else if (!File.Exists(texturePath))
                {
                    result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, $"Texture file does not exist: {texturePath}", row.Id, column.Id));
                }
                else if (!ImageIo.IsSupportedInput(texturePath))
                {
                    result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, $"Unsupported texture format: {texturePath}", row.Id, column.Id));
                }
                else
                {
                    try
                    {
                        var textureSize = ImageIo.ReadSize(texturePath);
                        targetSize ??= textureSize;
                        if (targetSize.Value != textureSize)
                        {
                            result.Messages.Add(new ValidationMessage(
                                ValidationSeverity.Error,
                                $"Texture size for row '{row.Name}', column '{column.Name}' is {textureSize}, expected {targetSize.Value}.",
                                row.Id,
                                column.Id));
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Messages.Add(new ValidationMessage(ValidationSeverity.Error, $"Cannot read texture '{texturePath}': {ex.Message}", row.Id, column.Id));
                    }
                }
            }
        }

        result.Width = targetSize?.Width;
        result.Height = targetSize?.Height;
        AddOverlapWarnings(project, preparedMasks, result);
        return result;
    }

    public static IReadOnlyList<string> GetPlannedOutputPaths(MergeProject project, string? extension = null)
    {
        var outputDirectory = project.OutputDirectory ?? string.Empty;
        var baseName = FileNameSanitizer.ForFileName(project.OutputBaseName, "TextureSet");
        var normalizedExtension = NormalizeOutputExtension(extension ?? project.OutputFormat);

        return project.Columns
            .Select(column =>
            {
                var columnName = FileNameSanitizer.ForFileName(column.Name, "Map");
                return Path.Combine(outputDirectory, $"{baseName}_{columnName}{normalizedExtension}");
            })
            .ToArray();
    }

    public static MergeResult Merge(MergeProject project, MergeOptions? options = null)
    {
        options ??= new MergeOptions();
        var validation = Validate(project);
        if (!validation.CanMerge)
        {
            throw new MergeValidationException(validation.Messages.Where(message => message.Severity == ValidationSeverity.Error).ToArray());
        }

        var outputDirectory = project.OutputDirectory!;
        Directory.CreateDirectory(outputDirectory);

        var preparedMasks = project.Rows.ToDictionary(
            row => row.Id,
            row => MaskProcessor.Build(ImageIo.LoadRgba(row.MaskPath!)));

        var outputs = new List<string>();
        var warnings = validation.Messages
            .Where(message => message.Severity == ValidationSeverity.Warning)
            .Select(message => message.Message)
            .ToList();

        var extension = NormalizeOutputExtension(options.OutputFormat);

        foreach (var column in project.Columns)
        {
            var outputPath = GetOutputPath(project, column, extension);
            if (!options.Overwrite && File.Exists(outputPath))
            {
                throw new IOException($"Output file already exists: {outputPath}");
            }

            var firstMask = preparedMasks[project.Rows[0].Id];
            var output = CreateDefaultImage(firstMask.Width, firstMask.Height, column.Behavior);
            foreach (var row in project.Rows)
            {
                var mask = preparedMasks[row.Id];
                var source = ImageIo.LoadRgba(row.TexturePaths[column.Id]!);
                ApplyRow(output, source, mask.Coverage, column.Behavior);
            }

            ImageIo.Save(output, outputPath);
            outputs.Add(outputPath);
        }

        return new MergeResult(outputs, warnings);
    }

    private static string GetOutputPath(MergeProject project, TextureColumnDefinition column, string extension)
    {
        var baseName = FileNameSanitizer.ForFileName(project.OutputBaseName, "TextureSet");
        var columnName = FileNameSanitizer.ForFileName(column.Name, "Map");
        return Path.Combine(project.OutputDirectory!, $"{baseName}_{columnName}{extension}");
    }

    private static string NormalizeOutputExtension(string outputFormat)
    {
        var normalized = outputFormat.Trim().TrimStart('.').ToLowerInvariant();
        return normalized is "tif" or "tiff" ? ".tiff" : ".png";
    }

    private static void AddOverlapWarnings(
        MergeProject project,
        IReadOnlyDictionary<Guid, MaskBuildResult> masks,
        MergeValidationResult result)
    {
        if (masks.Count < 2)
        {
            return;
        }

        var first = masks.Values.First();
        var occupied = new byte[first.Coverage.Length];
        var overlapPixels = 0;
        foreach (var row in project.Rows)
        {
            if (!masks.TryGetValue(row.Id, out var mask) || mask.Coverage.Length != occupied.Length)
            {
                continue;
            }

            for (var i = 0; i < mask.Coverage.Length; i++)
            {
                if (mask.Coverage[i] == 0)
                {
                    continue;
                }

                if (occupied[i] != 0)
                {
                    overlapPixels++;
                }

                occupied[i] = 1;
            }
        }

        if (overlapPixels > 0)
        {
            var percentage = overlapPixels / (double)occupied.Length * 100;
            result.Messages.Add(new ValidationMessage(
                ValidationSeverity.Warning,
                string.Create(CultureInfo.InvariantCulture, $"Masks overlap on {overlapPixels} pixels ({percentage:0.###}%). Later rows will override earlier rows."),
                null,
                null));
        }
    }

    private static TextureImage CreateDefaultImage(int width, int height, MergeBehavior behavior)
    {
        var pixels = new byte[width * height * 4];
        if (behavior == MergeBehavior.NormalReplaceNormalize)
        {
            for (var i = 0; i < width * height; i++)
            {
                var offset = i * 4;
                pixels[offset] = 128;
                pixels[offset + 1] = 128;
                pixels[offset + 2] = 255;
                pixels[offset + 3] = 255;
            }
        }

        return new TextureImage(width, height, pixels);
    }

    private static void ApplyRow(TextureImage destination, TextureImage source, byte[] coverage, MergeBehavior behavior)
    {
        for (var i = 0; i < coverage.Length; i++)
        {
            var alpha = coverage[i] / 255.0;
            if (alpha <= 0)
            {
                continue;
            }

            var offset = i * 4;
            if (behavior == MergeBehavior.NormalReplaceNormalize)
            {
                BlendNormal(destination.Pixels, source.Pixels, offset, alpha);
            }
            else
            {
                BlendRgba(destination.Pixels, source.Pixels, offset, alpha);
            }
        }
    }

    private static void BlendRgba(byte[] destination, byte[] source, int offset, double alpha)
    {
        for (var channel = 0; channel < 4; channel++)
        {
            destination[offset + channel] = ToByte(destination[offset + channel] * (1 - alpha) + source[offset + channel] * alpha);
        }
    }

    private static void BlendNormal(byte[] destination, byte[] source, int offset, double alpha)
    {
        var dx = ToVector(destination[offset]);
        var dy = ToVector(destination[offset + 1]);
        var dz = ToVector(destination[offset + 2]);
        var sx = ToVector(source[offset]);
        var sy = ToVector(source[offset + 1]);
        var sz = ToVector(source[offset + 2]);

        var x = dx * (1 - alpha) + sx * alpha;
        var y = dy * (1 - alpha) + sy * alpha;
        var z = dz * (1 - alpha) + sz * alpha;
        var length = Math.Sqrt(x * x + y * y + z * z);
        if (length < 0.000001)
        {
            x = 0;
            y = 0;
            z = 1;
        }
        else
        {
            x /= length;
            y /= length;
            z /= length;
        }

        destination[offset] = FromVector(x);
        destination[offset + 1] = FromVector(y);
        destination[offset + 2] = FromVector(z);
        destination[offset + 3] = ToByte(destination[offset + 3] * (1 - alpha) + source[offset + 3] * alpha);
    }

    private static double ToVector(byte value)
    {
        return value / 255.0 * 2.0 - 1.0;
    }

    private static byte FromVector(double value)
    {
        return ToByte((value * 0.5 + 0.5) * 255.0);
    }

    private static byte ToByte(double value)
    {
        if (value <= 0)
        {
            return 0;
        }

        if (value >= 255)
        {
            return 255;
        }

        return (byte)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}

public sealed class MergeValidationException : Exception
{
    public MergeValidationException(IReadOnlyList<ValidationMessage> messages)
        : base(string.Join(Environment.NewLine, messages.Select(message => message.Message)))
    {
        Messages = messages;
    }

    public IReadOnlyList<ValidationMessage> Messages { get; }
}
