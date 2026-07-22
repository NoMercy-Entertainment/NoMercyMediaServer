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

using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Api.Middleware;
using NoMercy.NmSystem.Monitoring;
using NoMercy.Storage;
using Xunit;

namespace NoMercy.Tests.Api.Middleware;

// DynamicStaticFilesMiddleware is the streaming/range-request layer every
// media file (video segments, subtitles, sprites, fonts) passes through. It
// decides: is this URL even ours (vs. api/swagger/dashboard), is the folder
// registered, does the file exist, should the response be a redirect (S3
// presigned URL), a whole-file copy, or a byte-range slice -- and it must
// translate storage-layer failures into the RIGHT status code (404 vs 502)
// without ever leaking a raw exception to a client mid-stream. None of this
// had a single test before this file.
[Trait(name: "Category", value: "Middleware")]
public sealed class DynamicStaticFilesMiddlewareTests
{
    private static DefaultHttpContext CreateContext(string path, string? rangeHeader = null)
    {
        DefaultHttpContext context = new();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (rangeHeader is not null)
            context.Request.Headers.Range = rangeHeader;
        return context;
    }

    private static Mock<IStorage> CreateStorage(
        bool exists = true,
        long size = 10,
        byte[]? content = null,
        Uri? presigned = null
    )
    {
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: exists);
        storage.Setup(expression: s => s.Size(It.IsAny<string>())).Returns(value: size);
        storage
            .Setup(expression: s => s.OpenRead(It.IsAny<string>()))
            .Returns(valueFunction: () => new MemoryStream(buffer: content ?? new byte[size]));
        storage
            .Setup(expression: s =>
                s.TryGetPresignedUrlAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: presigned);
        return storage;
    }

    private static (
        DynamicStaticFilesMiddleware Middleware,
        Mock<IStorageFactory> StorageFactory,
        MediaActivityMonitor ActivityMonitor,
        bool[] NextCalled
    ) CreateMiddleware(IStorage? storage = null)
    {
        bool[] nextCalled = [false];
        DynamicStaticFilesMiddleware middleware = new(
            next: _ =>
            {
                nextCalled[0] = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<DynamicStaticFilesMiddleware>.Instance
        );

        Mock<IStorageFactory> storageFactory = new();
        if (storage is not null)
            storageFactory
                .Setup(expression: f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()))
                .Returns(value: storage);

        return (middleware, storageFactory, new MediaActivityMonitor(), nextCalled);
    }

    private sealed class RegisteredFolder : IDisposable
    {
        public Ulid FolderId { get; }

        public RegisteredFolder(string subPath = "")
        {
            FolderId = Ulid.NewUlid();
            DynamicStaticFilesMiddleware.AddFolder(folderId: FolderId, driverId: Ulid.NewUlid(), subPath: subPath);
        }

        public void Dispose() => DynamicStaticFilesMiddleware.RemoveFolder(folderId: FolderId);
    }

    [Fact]
    public async Task InvokeAsync_PathHasNoValue_CallsNext()
    {
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            bool[] nextCalled
        ) = CreateMiddleware();
        DefaultHttpContext context = new();
        context.Request.Path = default;

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        nextCalled[0].Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_PathHasNoSegments_CallsNext()
    {
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            bool[] nextCalled
        ) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        nextCalled[0].Should().BeTrue();
    }

    [Theory]
    [InlineData(data: "/api/movies")]
    [InlineData(data: "/index.html")]
    [InlineData(data: "/swagger-ui/index.html")]
    [InlineData(data: "/images/poster.jpg")]
    [InlineData(data: "/manage/dashboard")]
    public async Task InvokeAsync_SystemRootSegment_CallsNextWithoutTouchingStorage(string path)
    {
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            bool[] nextCalled
        ) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: path);

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        nextCalled[0].Should().BeTrue();
        storageFactory.Verify(
            expression: f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()),
            times: Times.Never
        );
    }

    [Fact]
    public async Task InvokeAsync_RootSegmentIsNotAUlid_CallsNext()
    {
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            bool[] nextCalled
        ) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: "/favicon.ico");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        nextCalled[0].Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_FolderNotRegistered_CallsNext()
    {
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            bool[] nextCalled
        ) = CreateMiddleware();
        DefaultHttpContext context = CreateContext(path: $"/{Ulid.NewUlid()}/movie.mp4");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        nextCalled[0].Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_StorageFactoryThrows_CallsNextInsteadOfPropagating()
    {
        using RegisteredFolder folder = new();
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            bool[] nextCalled
        ) = CreateMiddleware();
        storageFactory
            .Setup(expression: f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()))
            .Throws(exception: new InvalidOperationException(message: "driver misconfigured"));
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        nextCalled[0].Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_StorageExistsThrows_CallsNextInsteadOfPropagating()
    {
        using RegisteredFolder folder = new();
        Mock<IStorage> storage = CreateStorage();
        storage
            .Setup(expression: s => s.Exists(It.IsAny<string>()))
            .Throws(exception: new IOException(message: "nfs mount unavailable"));
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            bool[] nextCalled
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        nextCalled[0].Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_FileDoesNotExist_CallsNext()
    {
        using RegisteredFolder folder = new();
        Mock<IStorage> storage = CreateStorage(exists: false);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            bool[] nextCalled
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/missing.mp4");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        nextCalled[0].Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_PresignedUrlAvailable_RedirectsAndSkipsBodyStreaming()
    {
        using RegisteredFolder folder = new();
        Uri presignedUrl = new(uriString: "https://cdn.example.com/movie.mp4?sig=abc");
        Mock<IStorage> storage = CreateStorage(presigned: presignedUrl);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            bool[] nextCalled
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: 302);
        context.Response.Headers.Location.ToString().Should().Be(expected: presignedUrl.ToString());
        nextCalled[0].Should().BeFalse();
        activityMonitor.IsActive.Should().BeFalse(because: "a redirect never touches playback activity");
    }

    [Fact]
    public async Task InvokeAsync_NoRangeNonStreamableFile_ServesWholeFileAndTouchesActivity()
    {
        using RegisteredFolder folder = new();
        byte[] content = Encoding.UTF8.GetBytes(s: "WEBVTT\n\n00:00.000 --> 00:01.000\nHi");
        Mock<IStorage> storage = CreateStorage(size: content.Length, content: content);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/subs.vtt");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: 200);
        context.Response.ContentLength.Should().Be(expected: content.Length);
        context.Response.ContentType.Should().Be(expected: "text/vtt; charset=utf-8");
        activityMonitor.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_NoRangeStreamableMedia_ForcesPartialContentProbeChunk()
    {
        using RegisteredFolder folder = new();
        byte[] content = new byte[2 * 1024 * 1024];
        Mock<IStorage> storage = CreateStorage(size: content.Length, content: content);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: 206);
        context.Response.ContentLength.Should().Be(expected: 1024 * 1024);
        context.Response.Headers.ContentRange.ToString().Should().StartWith(expected: "bytes 0-1048575/");
    }

    [Fact]
    public async Task InvokeAsync_RangeWithExplicitEnd_ServesExactSlice()
    {
        using RegisteredFolder folder = new();
        byte[] content = Encoding.UTF8.GetBytes(s: new string(c: 'a', count: 100));
        Mock<IStorage> storage = CreateStorage(size: content.Length, content: content);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4", rangeHeader: "bytes=10-19");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: 206);
        context.Response.ContentLength.Should().Be(expected: 10);
        context.Response.Headers.ContentRange.ToString().Should().Be(expected: "bytes 10-19/100");
        context.Response.Headers.AcceptRanges.ToString().Should().Be(expected: "bytes");
        ((MemoryStream)context.Response.Body).ToArray().Should().HaveCount(expected: 10);
    }

    [Fact]
    public async Task InvokeAsync_RangeOpenEndedAtZeroOnStreamableMedia_ServesProbeChunk()
    {
        using RegisteredFolder folder = new();
        byte[] content = new byte[2 * 1024 * 1024];
        Mock<IStorage> storage = CreateStorage(size: content.Length, content: content);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4", rangeHeader: "bytes=0-");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: 206);
        context.Response.ContentLength.Should().Be(expected: 1024 * 1024);
        context.Response.Headers.ContentRange.ToString().Should().StartWith(expected: "bytes 0-1048575/");
    }

    [Fact]
    public async Task InvokeAsync_ZeroLengthFile_StillServesSuccessfully()
    {
        using RegisteredFolder folder = new();
        Mock<IStorage> storage = CreateStorage(size: 0, content: []);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/empty.vtt");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: 200);
        context.Response.ContentLength.Should().Be(expected: 0);
    }

    [Fact]
    public async Task InvokeAsync_StreamShorterThanReportedSize_StopsAtActualStreamEnd()
    {
        using RegisteredFolder folder = new();
        byte[] actualContent = "abc"u8.ToArray();
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: true);
        // Reports a size larger than the stream actually contains -- a real
        // MemoryStream naturally returns 0 once its own backing array is
        // exhausted, which is exactly the "backend handed back fewer bytes
        // than promised" race the read loop's `if (bytesRead == 0) break;`
        // guards against.
        storage.Setup(expression: s => s.Size(It.IsAny<string>())).Returns(value: 100);
        storage
            .Setup(expression: s => s.OpenRead(It.IsAny<string>()))
            .Returns(valueFunction: () => new MemoryStream(buffer: actualContent));
        storage
            .Setup(expression: s =>
                s.TryGetPresignedUrlAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: (Uri?)null);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4", rangeHeader: "bytes=0-99");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: 206);
        ((MemoryStream)context.Response.Body).ToArray().Should().Equal(elements: actualContent);
    }

    [Fact]
    public async Task InvokeAsync_RangeHeaderWithoutDash_TreatedAsOpenEndedToEof()
    {
        // "bytes=50" has no '-' at all, so Split('-') yields a single-element
        // array -- ranges.Length is 1, short-circuiting the explicit-end check
        // without ever touching ranges[1].
        using RegisteredFolder folder = new();
        byte[] content = new byte[100];
        Mock<IStorage> storage = CreateStorage(size: content.Length, content: content);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4", rangeHeader: "bytes=50");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: 206);
        context.Response.Headers.ContentRange.ToString().Should().Be(expected: "bytes 50-99/100");
    }

    [Fact]
    public async Task InvokeAsync_RangeOpenEndedAtZeroOnNonStreamableFile_ServesToEof()
    {
        // isStreamableMedia is false here, so the "&& start == 0" probe-chunk
        // branch must short-circuit without ever evaluating start -- a
        // non-streamable file under a Range request still serves to EOF.
        using RegisteredFolder folder = new();
        byte[] content = new byte[50];
        Mock<IStorage> storage = CreateStorage(size: content.Length, content: content);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/subs.vtt", rangeHeader: "bytes=0-");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: 206);
        context.Response.Headers.ContentRange.ToString().Should().Be(expected: "bytes 0-49/50");
    }

    [Fact]
    public async Task InvokeAsync_RangeOpenEndedNonZeroStart_ServesToEndOfFile()
    {
        using RegisteredFolder folder = new();
        byte[] content = new byte[100];
        Mock<IStorage> storage = CreateStorage(size: content.Length, content: content);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4", rangeHeader: "bytes=50-");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: 206);
        context.Response.Headers.ContentRange.ToString().Should().Be(expected: "bytes 50-99/100");
    }

    [Fact]
    public async Task InvokeAsync_ExplicitEndPastEndOfFile_IsClampedToLastByte()
    {
        using RegisteredFolder folder = new();
        byte[] content = new byte[100];
        Mock<IStorage> storage = CreateStorage(size: content.Length, content: content);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(
            path: $"/{folder.FolderId}/movie.mp4",
            rangeHeader: "bytes=90-999999"
        );

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: 206);
        context.Response.Headers.ContentRange.ToString().Should().Be(expected: "bytes 90-99/100");
    }

    [Fact]
    public async Task InvokeAsync_RangeWithNonNumericStart_Returns416()
    {
        using RegisteredFolder folder = new();
        Mock<IStorage> storage = CreateStorage(size: 100);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4", rangeHeader: "bytes=abc-99");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: (int)HttpStatusCode.RequestedRangeNotSatisfiable);
    }

    [Fact]
    public async Task InvokeAsync_RangeWithNonNumericEnd_Returns416()
    {
        using RegisteredFolder folder = new();
        Mock<IStorage> storage = CreateStorage(size: 100);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4", rangeHeader: "bytes=0-xyz");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: (int)HttpStatusCode.RequestedRangeNotSatisfiable);
    }

    [Fact]
    public async Task InvokeAsync_RangeStartBeyondFileEnd_Returns416()
    {
        using RegisteredFolder folder = new();
        Mock<IStorage> storage = CreateStorage(size: 10);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        // start(500) will be clamped-compared against end (fileLength-1=9) -> start > end.
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4", rangeHeader: "bytes=500-");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: (int)HttpStatusCode.RequestedRangeNotSatisfiable);
    }

    [Theory]
    [InlineData(data: ["captions.vtt", "text/vtt; charset=utf-8"])]
    [InlineData(data: ["playlist.m3u8", "application/vnd.apple.mpegurl"])]
    [InlineData(data: ["segment.ts", "video/mp2t"])]
    [InlineData(data: ["font.woff2", "font/woff2"])]
    [InlineData(data: ["subtitle.srt", "application/x-subrip; charset=utf-8"])]
    public async Task InvokeAsync_KnownExtension_UsesContentTypeOverride(
        string filename,
        string expectedContentType
    )
    {
        using RegisteredFolder folder = new();
        byte[] content = "content"u8.ToArray();
        Mock<IStorage> storage = CreateStorage(size: content.Length, content: content);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/{filename}");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.ContentType.Should().Be(expected: expectedContentType);
    }

    [Fact]
    public async Task InvokeAsync_UnmappedExtension_FallsBackToGenericMimeMapping()
    {
        using RegisteredFolder folder = new();
        byte[] content = "content"u8.ToArray();
        Mock<IStorage> storage = CreateStorage(size: content.Length, content: content);
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/poster.jpg");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.ContentType.Should().Be(expected: "image/jpeg");
    }

    [Fact]
    public async Task InvokeAsync_FileVanishesMidServe_Returns404()
    {
        using RegisteredFolder folder = new();
        Mock<IStorage> storage = CreateStorage();
        storage.Setup(expression: s => s.Size(It.IsAny<string>())).Throws(exception: new FileNotFoundException(message: "gone"));
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: (int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvokeAsync_StorageTransportFailureDuringServe_Returns502()
    {
        using RegisteredFolder folder = new();
        Mock<IStorage> storage = CreateStorage();
        storage.Setup(expression: s => s.Size(It.IsAny<string>())).Throws(exception: new IOException(message: "nfs hiccup"));
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: (int)HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task InvokeAsync_ClientDisconnectMidStream_LeavesResponseUntouched()
    {
        using RegisteredFolder folder = new();
        Mock<IStorage> storage = CreateStorage();
        storage
            .Setup(expression: s => s.Size(It.IsAny<string>()))
            .Throws(exception: new OperationCanceledException(message: "client gone"));
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4");
        CancellationTokenSource cts = new();
        cts.Cancel();
        context.RequestAborted = cts.Token;

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        // No status was ever set for an aborted client — the default success
        // code stands because there is no client left to send anything to.
        context.Response.StatusCode.Should().Be(expected: 200);
    }

    [Fact]
    public async Task InvokeAsync_OperationCanceledButRequestNotAborted_FallsThroughToGenericHandler()
    {
        // The catch on OperationCanceledException is guarded by
        // `when (context.RequestAborted.IsCancellationRequested)`. A cancellation
        // that is NOT tied to client disconnect (e.g. an internal timeout) must
        // fail that guard and fall through to the generic catch-all as a 502,
        // not be silently swallowed as if the client had gone away.
        using RegisteredFolder folder = new();
        Mock<IStorage> storage = CreateStorage();
        storage
            .Setup(expression: s => s.Size(It.IsAny<string>()))
            .Throws(exception: new OperationCanceledException(message: "unrelated timeout"));
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4");
        context.RequestAborted = CancellationToken.None;

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: (int)HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns502()
    {
        using RegisteredFolder folder = new();
        Mock<IStorage> storage = CreateStorage();
        storage
            .Setup(expression: s => s.Size(It.IsAny<string>()))
            .Throws(exception: new InvalidOperationException(message: "boom"));
        (
            DynamicStaticFilesMiddleware middleware,
            Mock<IStorageFactory> storageFactory,
            MediaActivityMonitor activityMonitor,
            _
        ) = CreateMiddleware(storage: storage.Object);
        DefaultHttpContext context = CreateContext(path: $"/{folder.FolderId}/movie.mp4");

        await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

        context.Response.StatusCode.Should().Be(expected: (int)HttpStatusCode.BadGateway);
    }

    [Fact]
    public void AddFolder_ThenRemoveFolder_ForgetsTheRegistration()
    {
        Ulid folderId = Ulid.NewUlid();
        DynamicStaticFilesMiddleware.AddFolder(folderId: folderId, driverId: Ulid.NewUlid(), subPath: "sub/path");

        DynamicStaticFilesMiddleware.RemoveFolder(folderId: folderId);

        // Re-removing an already-forgotten folder must be a safe no-op (used by
        // the RegisteredFolder test fixture's Dispose across every test above).
        DynamicStaticFilesMiddleware.RemoveFolder(folderId: folderId);
    }

    [Fact]
    public async Task InvokeAsync_FolderRegisteredWithNullSubPath_TreatsItAsRootScope()
    {
        // subPath is declared non-nullable, but AddFolder still defends against
        // a caller that ignores the nullable contract (e.g. a driver row whose
        // SubPath column is legitimately NULL in the database). Passing a
        // null! here pins that the defensive `?? string.Empty` actually runs
        // rather than just being dead decoration.
        Ulid folderId = Ulid.NewUlid();
        DynamicStaticFilesMiddleware.AddFolder(folderId: folderId, driverId: Ulid.NewUlid(), subPath: null!);
        try
        {
            Mock<IStorage> storage = CreateStorage();
            (
                DynamicStaticFilesMiddleware middleware,
                Mock<IStorageFactory> storageFactory,
                MediaActivityMonitor activityMonitor,
                _
            ) = CreateMiddleware(storage: storage.Object);
            DefaultHttpContext context = CreateContext(path: $"/{folderId}/movie.mp4");

            await middleware.InvokeAsync(context: context, storageFactory: storageFactory.Object, activityMonitor: activityMonitor);

            storageFactory.Verify(expression: f => f.For(folderId, It.IsAny<Ulid>(), string.Empty), times: Times.Once);
        }
        finally
        {
            DynamicStaticFilesMiddleware.RemoveFolder(folderId: folderId);
        }
    }
}
