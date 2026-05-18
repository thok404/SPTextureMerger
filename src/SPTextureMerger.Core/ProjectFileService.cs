using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SPTextureMerger.Core;

public static class ProjectFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Save(string configPath, MergeProject project)
    {
        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? Environment.CurrentDirectory;
        var serializable = Clone(project);
        serializable.OutputDirectory = MakeRelative(baseDirectory, serializable.OutputDirectory);
        foreach (var row in serializable.Rows)
        {
            row.MaskPath = MakeRelative(baseDirectory, row.MaskPath);
            foreach (var columnId in row.TexturePaths.Keys.ToArray())
            {
                row.TexturePaths[columnId] = MakeRelative(baseDirectory, row.TexturePaths[columnId]);
            }
        }

        Directory.CreateDirectory(baseDirectory);
        File.WriteAllText(configPath, JsonSerializer.Serialize(serializable, JsonOptions));
    }

    public static MergeProject Load(string configPath)
    {
        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? Environment.CurrentDirectory;
        var project = JsonSerializer.Deserialize<MergeProject>(File.ReadAllText(configPath), JsonOptions)
            ?? throw new InvalidDataException("Project file is empty or invalid.");

        project.OutputDirectory = Resolve(baseDirectory, project.OutputDirectory);
        foreach (var column in project.Columns)
        {
            if (column.Id == Guid.Empty)
            {
                column.Id = Guid.NewGuid();
            }
        }

        foreach (var row in project.Rows)
        {
            if (row.Id == Guid.Empty)
            {
                row.Id = Guid.NewGuid();
            }

            row.MaskPath = Resolve(baseDirectory, row.MaskPath);
            foreach (var columnId in row.TexturePaths.Keys.ToArray())
            {
                row.TexturePaths[columnId] = Resolve(baseDirectory, row.TexturePaths[columnId]);
            }
        }

        return project;
    }

    private static MergeProject Clone(MergeProject project)
    {
        return new MergeProject
        {
            OutputBaseName = project.OutputBaseName,
            OutputDirectory = project.OutputDirectory,
            OutputFormat = project.OutputFormat,
            Columns = project.Columns
                .Select(column => new TextureColumnDefinition
                {
                    Id = column.Id,
                    Name = column.Name,
                    Behavior = column.Behavior
                })
                .ToList(),
            Rows = project.Rows
                .Select(row => new TextureSetRowDefinition
                {
                    Id = row.Id,
                    Name = row.Name,
                    MaskPath = row.MaskPath,
                    TexturePaths = row.TexturePaths.ToDictionary(pair => pair.Key, pair => pair.Value)
                })
                .ToList()
        };
    }

    private static string? MakeRelative(string baseDirectory, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var relative = Path.GetRelativePath(baseDirectory, fullPath);
            return relative.StartsWith("..", StringComparison.Ordinal) ? fullPath : relative;
        }
        catch
        {
            return path;
        }
    }

    private static string? Resolve(string baseDirectory, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }
}
