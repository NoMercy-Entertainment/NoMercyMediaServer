namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Builds the FFmpeg <c>ass=</c> filter expression for burning ASS/SSA
/// subtitles permanently into the video stream.
///
/// <para>Escaping rules (FFmpeg filter graph):</para>
/// <list type="bullet">
///   <item>Backslashes are doubled: <c>\</c> to <c>\\</c></item>
///   <item>Single quotes are escaped: <c>'</c> to <c>\'</c></item>
///   <item>Colons are escaped with a backslash: <c>:</c> to <c>\:</c></item>
///   <item>The path is wrapped in single quotes so the filter graph
///         parser treats it as a literal argument.</item>
/// </list>
///
/// <para>The optional <paramref name="fontDirectory"/> hint tells libass
/// where to look for fonts before scanning system directories. Pass the
/// <c>fonts/</c> subdirectory that <see cref="NoMercy.Encoder.PostProcess.FontExtractor"/>
/// populates from MKV attachments so embedded fonts load correctly.</para>
/// </summary>
public sealed class AssBurnInFilterBuilder
{
    /// <summary>
    /// Builds the <c>ass=</c> filter expression.
    /// </summary>
    /// <param name="assFilePath">
    /// Absolute or relative path to the <c>.ass</c> / <c>.ssa</c> file.
    /// </param>
    /// <param name="fontDirectory">
    /// Optional path to a directory containing fonts. Forwarded as
    /// <c>fontsdir=&lt;path&gt;</c> to libass. May be null.
    /// </param>
    /// <returns>
    /// A filter expression such as
    /// <c>ass='/path/to.ass'</c> or
    /// <c>ass='/path/to.ass':fontsdir=/fonts</c>.
    /// </returns>
    public string Build(string assFilePath, string? fontDirectory = null)
    {
        string escaped = EscapeForFilterGraph(assFilePath);
        string filter = $"ass='{escaped}'";

        if (!string.IsNullOrWhiteSpace(fontDirectory))
        {
            string escapedFontDir = EscapeForFilterGraph(fontDirectory);
            filter += $":fontsdir={escapedFontDir}";
        }

        return filter;
    }

    /// <summary>
    /// Applies FFmpeg filter-graph path escaping per the FFmpeg spec.
    /// Escape order: backslashes first, then single quotes, then colons.
    /// Forward slashes are left untouched.
    /// </summary>
    internal static string EscapeForFilterGraph(string path)
    {
        // Normalise Windows separators first so the backslash-doubling
        // pass only sees intentional backslashes (none on a pure path).
        string normalised = path.Replace('\\', '/');

        // Order is critical: \ -> \\ must precede escaping other chars.
        string escaped = normalised
            .Replace("\\", @"\\") // \ -> \\ (no-op on normalised path)
            .Replace("'", "\\'") // ' -> \' (apostrophe in filename)
            .Replace(":", "\\:"); // : -> \: (drive letter / stream specifier)

        return escaped;
    }
}
