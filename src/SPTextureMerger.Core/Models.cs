namespace SPTextureMerger.Core;

public enum MergeBehavior
{
    RgbaCopy,
    NormalReplaceNormalize,
    DataCopy
}

public sealed class MergeProject
{
    public string OutputBaseName { get; set; } = "TextureSet";

    public string? OutputDirectory { get; set; }

    public string OutputFormat { get; set; } = "png";

    public List<TextureColumnDefinition> Columns { get; set; } = [];

    public List<TextureSetRowDefinition> Rows { get; set; } = [];
}

public sealed class TextureColumnDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Map";

    public MergeBehavior Behavior { get; set; }
}

public sealed class TextureSetRowDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Texture Set";

    public string? MaskPath { get; set; }

    public Dictionary<Guid, string?> TexturePaths { get; set; } = [];
}

public sealed class MergeOptions
{
    public bool Overwrite { get; set; } = true;

    public string OutputFormat { get; set; } = "png";
}

public sealed class MergeResult
{
    public MergeResult(IReadOnlyList<string> outputPaths, IReadOnlyList<string> warnings)
    {
        OutputPaths = outputPaths;
        Warnings = warnings;
    }

    public IReadOnlyList<string> OutputPaths { get; }

    public IReadOnlyList<string> Warnings { get; }
}

public sealed class MergeValidationResult
{
    public List<ValidationMessage> Messages { get; } = [];

    public int? Width { get; set; }

    public int? Height { get; set; }

    public bool CanMerge => Messages.All(message => message.Severity != ValidationSeverity.Error);
}

public sealed record ValidationMessage(
    ValidationSeverity Severity,
    string Message,
    Guid? RowId,
    Guid? ColumnId);

public enum ValidationSeverity
{
    Error,
    Warning
}
