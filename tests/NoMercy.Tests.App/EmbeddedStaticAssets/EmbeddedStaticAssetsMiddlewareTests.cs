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
[Trait(name: "Category", value: "Middleware")]
public sealed class EmbeddedStaticAssetsMiddlewareTests
{
    private static ManifestEmbeddedFileProvider CreateFileProvider() =>
        new(assembly: Assembly.GetExecutingAssembly(), root: "wwwroot");

    private static (
        EmbeddedStaticAssetsMiddleware Middleware,
        FakeLogger Logger,
        bool[] NextCalled
    ) CreateMiddleware(EmbeddedStaticAssetsOptions? options = null)
    {
        bool[] nextCalled = [false];
        FakeLogger logger = new();

        EmbeddedStaticAssetsMiddleware middleware = new(
            next: _ =>
            {
                nextCalled[0] = true;
                return Task.CompletedTask;
            },
            fileProvider: CreateFileProvider(),
            options: options ?? new(),
            logger: logger
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
        return Encoding.UTF8.GetString(bytes: stream.ToArray());
    }

    [Fact]
    public async Task InvokeAsync_NonGetNonHeadMethod_CallsNextWithoutServing()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, bool[] nextCalled) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/index.html", method: "POST");

        await middleware.InvokeAsync(context: context);

        nextCalled[0].Should().BeTrue();
        ((MemoryStream)context.Response.Body).Length.Should().Be(expected: 0);
    }

    [Fact]
    public async Task InvokeAsync_RootPath_ServesIndexHtml()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, bool[] nextCalled) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/");

        await middleware.InvokeAsync(context: context);

        nextCalled[0].Should().BeFalse();
        context.Response.ContentType.Should().Be(expected: "text/html");
        ReadBody(context: context).Should().Contain(expected: "Fixture Index");
    }

    [Fact]
    public async Task InvokeAsync_UnknownPathWithExtension_CallsNext()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, bool[] nextCalled) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/does-not-exist.js");

        await middleware.InvokeAsync(context: context);

        nextCalled[0].Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_UnknownPathWithoutExtension_SpaFallbackServesIndexHtml()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, bool[] nextCalled) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/some/client/route");

        await middleware.InvokeAsync(context: context);

        nextCalled[0].Should().BeFalse();
        context.Response.StatusCode.Should().Be(expected: 200);
        ReadBody(context: context).Should().Contain(expected: "Fixture Index");
    }

    [Fact]
    public async Task InvokeAsync_KnownAsset_SetsETagAndLastModifiedAndContentLength()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/styles.css");

        await middleware.InvokeAsync(context: context);

        context.Response.ContentType.Should().Be(expected: "text/css");
        context.Response.Headers.ETag.ToString().Should().NotBeNullOrEmpty();
        context.Response.Headers.LastModified.ToString().Should().NotBeNullOrEmpty();
        context.Response.ContentLength.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    public async Task InvokeAsync_ConditionalRequestMatchingETag_Returns304WithNoBody()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext first = CreateContext(path: "/styles.css");
        await middleware.InvokeAsync(context: first);
        string etag = first.Response.Headers.ETag.ToString();

        DefaultHttpContext second = CreateContext(path: "/styles.css", ifNoneMatch: etag);
        await middleware.InvokeAsync(context: second);

        second.Response.StatusCode.Should().Be(expected: StatusCodes.Status304NotModified);
        ((MemoryStream)second.Response.Body).Length.Should().Be(expected: 0);
    }

    [Fact]
    public async Task InvokeAsync_ConditionalRequestMismatchedETag_Returns200WithBody()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/styles.css", ifNoneMatch: "\"stale-etag\"");

        await middleware.InvokeAsync(context: context);

        context.Response.StatusCode.Should().Be(expected: 200);
        ((MemoryStream)context.Response.Body).Length.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    public async Task InvokeAsync_HashedFilename_SetsImmutableCacheControl()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/app-8f3a9c2b.js");

        await middleware.InvokeAsync(context: context);

        context.Response.Headers.CacheControl.ToString().Should().Contain(expected: "immutable");
    }

    [Fact]
    public async Task InvokeAsync_NonHashedFilename_SetsShortRevalidateCacheControl()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/styles.css");

        await middleware.InvokeAsync(context: context);

        string cacheControl = context.Response.Headers.CacheControl.ToString();
        cacheControl.Should().Contain(expected: "must-revalidate");
        cacheControl.Should().NotContain(unexpected: "immutable");
    }

    [Fact]
    public async Task InvokeAsync_AcceptEncodingBrotli_ReturnsBrotliEncodedBody()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/app-8f3a9c2b.js", acceptEncoding: "br");

        await middleware.InvokeAsync(context: context);

        context.Response.Headers.ContentEncoding.ToString().Should().Be(expected: "br");
        context.Response.Headers.Vary.ToString().Should().Be(expected: "Accept-Encoding");
    }

    [Fact]
    public async Task InvokeAsync_AcceptEncodingGzip_ReturnsGzipEncodedBody()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/app-8f3a9c2b.js", acceptEncoding: "gzip");

        await middleware.InvokeAsync(context: context);

        context.Response.Headers.ContentEncoding.ToString().Should().Be(expected: "gzip");
    }

    [Fact]
    public async Task InvokeAsync_NoAcceptEncoding_ReturnsUncompressedBodyWithNoEncodingHeader()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/app-8f3a9c2b.js");

        await middleware.InvokeAsync(context: context);

        context.Response.Headers.ContentEncoding.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_SmallFile_NeverCompressedRegardlessOfAcceptEncoding()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/index.html", acceptEncoding: "br, gzip");

        await middleware.InvokeAsync(context: context);

        context.Response.Headers.ContentEncoding.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_AlreadyCompressedContentType_SkipsCompressionEvenWhenLarge()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/image.png", acceptEncoding: "br, gzip");

        await middleware.InvokeAsync(context: context);

        context.Response.ContentType.Should().Be(expected: "image/png");
        context.Response.Headers.ContentEncoding.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WebmanifestExtension_UsesCustomMimeMapping()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/manifest.webmanifest");

        await middleware.InvokeAsync(context: context);

        context.Response.ContentType.Should().Be(expected: "application/manifest+json");
    }

    [Fact]
    public async Task InvokeAsync_Woff2Extension_UsesCustomMimeMapping()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/font.woff2");

        await middleware.InvokeAsync(context: context);

        context.Response.ContentType.Should().Be(expected: "font/woff2");
    }

    [Fact]
    public async Task InvokeAsync_HeadRequest_SetsHeadersButWritesNoBody()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/index.html", method: "HEAD");

        await middleware.InvokeAsync(context: context);

        context.Response.ContentLength.Should().BeGreaterThan(expected: 0);
        ((MemoryStream)context.Response.Body).Length.Should().Be(expected: 0);
    }

    [Fact]
    public async Task InvokeAsync_HtmlInjection_InsertsMetaStylesBeforeHeadAndScriptsBeforeBody()
    {
        EmbeddedStaticAssetsOptions options = new();
        options.InjectMetaTags.Add(item: "<meta name=\"x\" content=\"y\">");
        options.InjectStyles.Add(item: "/bar.css");
        options.InjectScripts.Add(item: "/foo.js");
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware(options: options);
        DefaultHttpContext context = CreateContext(path: "/index.html");

        await middleware.InvokeAsync(context: context);

        string body = ReadBody(context: context);
        body.Should()
            .Contain(
                expected: "<meta name=\"x\" content=\"y\"><link rel=\"stylesheet\" href=\"/bar.css\"></head>"
            );
        body.Should().Contain(expected: "<script src=\"/foo.js\"></script></body>");
    }

    [Fact]
    public async Task InvokeAsync_HtmlInjection_DefaultPatternDoesNotMatchNonIndexHtml()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware(
            options: new() { InjectScripts = ["/foo.js"] }
        );
        DefaultHttpContext context = CreateContext(path: "/pages/nested.html");

        await middleware.InvokeAsync(context: context);

        ReadBody(context: context).Should().NotContain(unexpected: "/foo.js");
    }

    [Fact]
    public async Task InvokeAsync_HtmlInjection_GlobPatternMatchesNestedHtmlFiles()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware(
            options: new() { InjectScripts = ["/foo.js"], HtmlFilePatterns = ["**/*.html"] }
        );
        DefaultHttpContext context = CreateContext(path: "/pages/nested.html");

        await middleware.InvokeAsync(context: context);

        ReadBody(context: context).Should().Contain(expected: "<script src=\"/foo.js\"></script></body>");
    }

    [Fact]
    public async Task InvokeAsync_HtmlInjection_MinifyTrimsCompleteTagWhitespace()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware(
            options: new() { InjectScripts = ["  <script>window.x=1;</script>  "], MinifyInjections = true }
        );
        DefaultHttpContext context = CreateContext(path: "/index.html");

        await middleware.InvokeAsync(context: context);

        ReadBody(context: context).Should().Contain(expected: "<script>window.x=1;</script></body>");
    }

    [Fact]
    public async Task InvokeAsync_HtmlInjection_NoMinifyPreservesCompleteTagWhitespace()
    {
        (EmbeddedStaticAssetsMiddleware middleware, _, _) = CreateMiddleware(
            options: new() { InjectScripts = ["  <script>window.x=1;</script>  "], MinifyInjections = false }
        );
        DefaultHttpContext context = CreateContext(path: "/index.html");

        await middleware.InvokeAsync(context: context);

        ReadBody(context: context).Should().Contain(expected: "  <script>window.x=1;</script>  </body>");
    }

    [Fact]
    public async Task InvokeAsync_RepeatedRequestsForSameAsset_CachesAndLogsOnce()
    {
        (EmbeddedStaticAssetsMiddleware middleware, FakeLogger logger, _) = CreateMiddleware();

        await middleware.InvokeAsync(context: CreateContext(path: "/styles.css"));
        await middleware.InvokeAsync(context: CreateContext(path: "/styles.css"));

        logger.Messages.Count(predicate: message => message.Contains(value: "Cached embedded asset")).Should().Be(expected: 1);
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
            Messages.Add(item: formatter(arg1: state, arg2: exception));
        }
    }
}
