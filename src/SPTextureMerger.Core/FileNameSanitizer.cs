using System.IO;

namespace SPTextureMerger.Core;

public static class FileNameSanitizer
{
    public static string ForFileName(string? value, string fallback)
    {
        var name = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        while (name.Contains("__", StringComparison.Ordinal))
        {
            name = name.Replace("__", "_", StringComparison.Ordinal);
        }

        name = name.Trim(' ', '.', '_');
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
