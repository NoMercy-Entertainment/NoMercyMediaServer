namespace NoMercy.Encoder.Jobs;

public record PathAllowlist(string[] AllowedInputPaths, string[] AllowedOutputPaths)
{
    public bool IsInputPathAllowed(string path)
    {
        string normalized = Path.GetFullPath(path);
        return AllowedInputPaths.Any(allowed =>
            normalized.StartsWith(Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase)
        );
    }

    public bool IsOutputPathAllowed(string path)
    {
        string normalized = Path.GetFullPath(path);
        return AllowedOutputPaths.Any(allowed =>
            normalized.StartsWith(Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase)
        );
    }
}
