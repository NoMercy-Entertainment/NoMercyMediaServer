namespace NoMercy.App.EmbeddedStaticAssets;

/// <summary>
/// Configuration options for embedded static assets middleware.
/// </summary>
public sealed class EmbeddedStaticAssetsOptions
{
    /// <summary>
    /// Scripts to inject before the closing &lt;/body&gt; tag in HTML files.
    /// Each entry should be a complete script tag or the src path (will be wrapped in script tag).
    /// </summary>
    public List<string> InjectScripts { get; set; } = [];

    /// <summary>
    /// Styles to inject before the closing &lt;/head&gt; tag in HTML files.
    /// Each entry should be a complete link/style tag or the href path (will be wrapped in link tag).
    /// </summary>
    public List<string> InjectStyles { get; set; } = [];

    /// <summary>
    /// Meta tags to inject in the &lt;head&gt; section of HTML files.
    /// Each entry should be a complete meta tag.
    /// </summary>
    public List<string> InjectMetaTags { get; set; } = [];

    /// <summary>
    /// File patterns to apply HTML injection to. Defaults to index.html only.
    /// Use glob-like patterns: "*.html", "index.html", "pages/*.html"
    /// </summary>
    public List<string> HtmlFilePatterns { get; set; } = ["index.html"];

    /// <summary>
    /// Whether to minify injected content by removing unnecessary whitespace.
    /// Default is true.
    /// </summary>
    public bool MinifyInjections { get; set; } = true;
}
