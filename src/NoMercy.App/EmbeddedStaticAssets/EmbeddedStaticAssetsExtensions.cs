using System.Reflection;
using Microsoft.Extensions.FileProviders;

namespace NoMercy.App.EmbeddedStaticAssets;

/// <summary>
/// Extension methods for configuring embedded static assets middleware.
/// </summary>
public static class EmbeddedStaticAssetsExtensions
{
    /// <summary>
    /// Maps embedded static assets with optimizations similar to MapStaticAssets.
    /// Provides ETag generation, content negotiation (gzip/brotli), appropriate cache headers,
    /// and optional HTML injection for scripts, styles, and meta tags.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="assembly">The assembly containing the embedded resources. Defaults to the entry assembly.</param>
    /// <param name="embeddedResourceRoot">The root path within the embedded resources. Defaults to "wwwroot".</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEmbeddedStaticAssets(
        this IApplicationBuilder app,
        Assembly? assembly = null,
        string embeddedResourceRoot = "wwwroot")
    {
        return app.UseEmbeddedStaticAssets(new EmbeddedStaticAssetsOptions(), assembly, embeddedResourceRoot);
    }

    /// <summary>
    /// Maps embedded static assets with optimizations and custom configuration.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="options">Configuration options including scripts/styles to inject.</param>
    /// <param name="assembly">The assembly containing the embedded resources. Defaults to the entry assembly.</param>
    /// <param name="embeddedResourceRoot">The root path within the embedded resources. Defaults to "wwwroot".</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEmbeddedStaticAssets(
        this IApplicationBuilder app,
        EmbeddedStaticAssetsOptions options,
        Assembly? assembly = null,
        string embeddedResourceRoot = "wwwroot")
    {
        assembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        ManifestEmbeddedFileProvider embeddedProvider = new(assembly, embeddedResourceRoot);

        app.UseMiddleware<EmbeddedStaticAssetsMiddleware>(embeddedProvider, options);

        return app;
    }

    /// <summary>
    /// Maps embedded static assets with a configuration action.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">Action to configure options including scripts/styles to inject.</param>
    /// <param name="assembly">The assembly containing the embedded resources. Defaults to the entry assembly.</param>
    /// <param name="embeddedResourceRoot">The root path within the embedded resources. Defaults to "wwwroot".</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEmbeddedStaticAssets(
        this IApplicationBuilder app,
        Action<EmbeddedStaticAssetsOptions> configure,
        Assembly? assembly = null,
        string embeddedResourceRoot = "wwwroot")
    {
        EmbeddedStaticAssetsOptions options = new();
        configure(options);

        return app.UseEmbeddedStaticAssets(options, assembly, embeddedResourceRoot);
    }
}
