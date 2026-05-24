namespace NoMercy.Database.Infrastructure;

internal static class PathNormalizer
{
    public static string Normalize(string value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Replace('\\', '/');

    public static string? NormalizeNullable(string? value) =>
        string.IsNullOrEmpty(value) ? value : value.Replace('\\', '/');
}
