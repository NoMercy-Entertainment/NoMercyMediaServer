namespace NoMercy.NmSystem.Extensions;

public static class NullableExtensions
{
    /// <summary>
    /// Returns the string if not null, otherwise returns an empty string.
    /// Inspired by Kotlin's String?.orEmpty()
    /// </summary>
    public static string OrEmpty(this string? value) => value ?? string.Empty;

    /// <summary>
    /// Returns the array if not null, otherwise returns an empty array.
    /// </summary>
    public static T[] OrEmpty<T>(this T[]? value) => value ?? [];

    /// <summary>
    /// Returns the list if not null, otherwise returns an empty list.
    /// </summary>
    public static List<T> OrEmpty<T>(this List<T>? value) => value ?? [];

    /// <summary>
    /// Returns the enumerable if not null, otherwise returns an empty enumerable.
    /// </summary>
    public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T>? value) => value ?? [];

    /// <summary>
    /// Returns null if the string is null, empty, or whitespace; otherwise returns the string.
    /// Inspired by Kotlin's String?.takeIf { it.isNotBlank() }
    /// </summary>
    public static string? OrNull(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
