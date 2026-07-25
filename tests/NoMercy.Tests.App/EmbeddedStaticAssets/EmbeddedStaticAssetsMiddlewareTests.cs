// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using NoMercy.App.EmbeddedStaticAssets;
using Xunit;

namespace NoMercy.Tests.App.EmbeddedStaticAssets;

/// <summary>
/// Every scenario here runs the middleware against a real
/// <see cref="ManifestEmbeddedFileProvider"/> reading this TEST assembly's own
/// <c>wwwroot/</c> embedded resources (see NoMercy.Tests.App.csproj) — the same
/// mechanism <c>NoMercy.App</c> itself uses in production — so ETag hashing,
/// gzip/brotli negotiation, cache-header selection, and HTML injection are all
/// exercised against genuine embedded bytes, never a mocked <see cref="IFileProvider"/>.
/// </summary>
[Trait("Category", "Middleware")]
public sealed class EmbeddedStaticAssetsMiddlewareTests
{
    private static ManifestEmbeddedFileProvider CreateFileProvider() =>
        new(Assembly.GetExecutingAssembly(), "wwwroot");

    private static (
        EmbeddedStaticAssetsMiddleware Middleware,
        FakeLogger Logger,
        bool[] NextCalled
    ) CreateMiddleware(EmbeddedStaticAssetsOptions? options = null)
    {
        bool[] nextCalled = [false];
        FakeLogger logger = new();

        EmbeddedStaticAssetsMiddleware middleware = new(
            _ =>
            {
                nextCalled[0] = true;
                return Task.CompletedTask;
            },
            CreateFileProvider(),
            options ?? new(),
            logger
        );

        return (middleware, logger, nextCalled);
    }

    private static DefaultHttpContext CreateContext(
        string path,
        string method = "GET",
        string? acceptEncoding = null,
        string? ifNoneMatch = null
    )
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (acceptEncoding is not null)
            context.Request.Headers.AcceptEncoding = acceptEncoding;

        if (ifNoneMatch is not null)
            context.Request.Headers.IfNoneMatch = ifNoneMatch;

        return context;
    }

    private static string ReadBody(DefaultHttpContext context)
    {
        MemoryStream stream = (MemoryStream)context.Response.Body;
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public async Task InvokeAsync_NonGetNonHeadMethod_CallsNextWithoutServing()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, bool[] nextCalled) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/index.html", "POST");

        await middleware.InvokeAsync(context);

        nextCalled[0].Should().BeTrue();
        ((MemoryStream)context.Response.Body).Length.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_RootPath_ServesIndexHtml()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, bool[] nextCalled) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/");

        await middleware.InvokeAsync(context);

        nextCalled[0].Should().BeFalse();
        context.Response.ContentType.Should().Be("text/html");
        ReadBody(context).Should().Contain("Fixture Index");
    }

    [Fact]
    public async Task InvokeAsync_UnknownPathWithExtension_CallsNext()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, bool[] nextCalled) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/does-not-exist.js");

        await middleware.InvokeAsync(context);

        nextCalled[0].Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_UnknownPathWithoutExtension_SpaFallbackServesIndexHtml()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, bool[] nextCalled) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/some/client/route");

        await middleware.InvokeAsync(context);

        nextCalled[0].Should().BeFalse();
        context.Response.StatusCode.Should().Be(200);
        ReadBody(context).Should().Contain("Fixture Index");
    }

    [Fact]
    public async Task InvokeAsync_KnownAsset_SetsETagAndLastModifiedAndContentLength()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/styles.css");

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Be("text/css");
        context.Response.Headers.ETag.ToString().Should().NotBeNullOrEmpty();
        context.Response.Headers.LastModified.ToString().Should().NotBeNullOrEmpty();
        context.Response.ContentLength.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InvokeAsync_ConditionalRequestMatchingETag_Returns304WithNoBody()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext first = CreateContext("/styles.css");
        await middleware.InvokeAsync(first);
        string etag = first.Response.Headers.ETag.ToString();

        DefaultHttpContext second = CreateContext("/styles.css", ifNoneMatch: etag);
        await middleware.InvokeAsync(second);

        second.Response.StatusCode.Should().Be(StatusCodes.Status304NotModified);
        ((MemoryStream)second.Response.Body).Length.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_ConditionalRequestMismatchedETag_Returns200WithBody()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/styles.css", ifNoneMatch: "\"stale-etag\"");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(200);
        ((MemoryStream)context.Response.Body).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InvokeAsync_HashedFilename_SetsImmutableCacheControl()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/app-8f3a9c2b.js");

        await middleware.InvokeAsync(context);

        context.Response.Headers.CacheControl.ToString().Should().Contain("immutable");
    }

    [Fact]
    public async Task InvokeAsync_NonHashedFilename_SetsShortRevalidateCacheControl()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/styles.css");

        await middleware.InvokeAsync(context);

        string cacheControl = context.Response.Headers.CacheControl.ToString();
        cacheControl.Should().Contain("must-revalidate");
        cacheControl.Should().NotContain("immutable");
    }

    [Fact]
    public async Task InvokeAsync_AcceptEncodingBrotli_ReturnsBrotliEncodedBody()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/app-8f3a9c2b.js", acceptEncoding: "br");

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContentEncoding.ToString().Should().Be("br");
        context.Response.Headers.Vary.ToString().Should().Be("Accept-Encoding");
    }

    [Fact]
    public async Task InvokeAsync_AcceptEncodingGzip_ReturnsGzipEncodedBody()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/app-8f3a9c2b.js", acceptEncoding: "gzip");

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContentEncoding.ToString().Should().Be("gzip");
    }

    [Fact]
    public async Task InvokeAsync_NoAcceptEncoding_ReturnsUncompressedBodyWithNoEncodingHeader()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/app-8f3a9c2b.js");

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContentEncoding.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_SmallFile_NeverCompressedRegardlessOfAcceptEncoding()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/index.html", acceptEncoding: "br, gzip");

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContentEncoding.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_AlreadyCompressedContentType_SkipsCompressionEvenWhenLarge()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/image.png", acceptEncoding: "br, gzip");

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Be("image/png");
        context.Response.Headers.ContentEncoding.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WebmanifestExtension_UsesCustomMimeMapping()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/manifest.webmanifest");

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Be("application/manifest+json");
    }

    [Fact]
    public async Task InvokeAsync_Woff2Extension_UsesCustomMimeMapping()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/font.woff2");

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Be("font/woff2");
    }

    [Fact]
    public async Task InvokeAsync_HeadRequest_SetsHeadersButWritesNoBody()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext("/index.html", "HEAD");

        await middleware.InvokeAsync(context);

        context.Response.ContentLength.Should().BeGreaterThan(0);
        ((MemoryStream)context.Response.Body).Length.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_HtmlInjection_InsertsMetaStylesBeforeHeadAndScriptsBeforeBody()
    {
        EmbeddedStaticAssetsOptions options = new();
        options.InjectMetaTags.Add("<meta name=\"x\" content=\"y\">");
        options.InjectStyles.Add("/bar.css");
        options.InjectScripts.Add("/foo.js");
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware(options);
        DefaultHttpContext context = CreateContext("/index.html");

        await middleware.InvokeAsync(context);

        string body = ReadBody(context);
        body.Should()
            .Contain(
                "<meta name=\"x\" content=\"y\"><link rel=\"stylesheet\" href=\"/bar.css\"></head>"
            );
        body.Should().Contain("<script src=\"/foo.js\"></script></body>");
    }

    [Fact]
    public async Task InvokeAsync_HtmlInjection_DefaultPatternDoesNotMatchNonIndexHtml()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware(
            new() { InjectScripts = ["/foo.js"] }
        );
        DefaultHttpContext context = CreateContext("/pages/nested.html");

        await middleware.InvokeAsync(context);

        ReadBody(context).Should().NotContain("/foo.js");
    }

    [Fact]
    public async Task InvokeAsync_HtmlInjection_GlobPatternMatchesNestedHtmlFiles()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware(
            new() { InjectScripts = ["/foo.js"], HtmlFilePatterns = ["**/*.html"] }
        );
        DefaultHttpContext context = CreateContext("/pages/nested.html");

        await middleware.InvokeAsync(context);

        ReadBody(context).Should().Contain("<script src=\"/foo.js\"></script></body>");
    }

    [Fact]
    public async Task InvokeAsync_HtmlInjection_MinifyTrimsCompleteTagWhitespace()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware(
            new() { InjectScripts = ["  <script>window.x=1;</script>  "], MinifyInjections = true }
        );
        DefaultHttpContext context = CreateContext("/index.html");

        await middleware.InvokeAsync(context);

        ReadBody(context).Should().Contain("<script>window.x=1;</script></body>");
    }

    [Fact]
    public async Task InvokeAsync_HtmlInjection_NoMinifyPreservesCompleteTagWhitespace()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware(
            new() { InjectScripts = ["  <script>window.x=1;</script>  "], MinifyInjections = false }
        );
        DefaultHttpContext context = CreateContext("/index.html");

        await middleware.InvokeAsync(context);

        ReadBody(context).Should().Contain("  <script>window.x=1;</script>  </body>");
    }

    [Fact]
    public async Task InvokeAsync_RepeatedRequestsForSameAsset_CachesAndLogsOnce()
    {
        (EmbeddedStaticAssetsMiddleware middleware, FakeLogger logger, _) = CreateMiddleware();

        await middleware.InvokeAsync(CreateContext("/styles.css"));
        await middleware.InvokeAsync(CreateContext("/styles.css"));

        logger.Messages.Count(message => message.Contains("Cached embedded asset")).Should().Be(1);
    }

    /// <summary>
    /// Minimal capturing <see cref="ILogger{T}"/> — <c>NullLogger</c> would swallow
    /// the "Cached embedded asset" debug line the caching-once requirement depends on.
    /// </summary>
    private sealed class FakeLogger : ILogger<EmbeddedStaticAssetsMiddleware>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
