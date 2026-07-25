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
using NoMercy.Storage.Drivers.S3;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="HttpResponseStream"/> exists solely to tie an
/// <see cref="HttpResponseMessage"/>'s lifetime to its content stream so an
/// S3 GetObject caller can dispose one object and have both the stream AND
/// the response released. Every member forwards to the underlying content
/// stream except <c>Dispose</c>/<c>DisposeAsync</c>, which must ALSO dispose
/// the response — a leak here holds an HTTP connection open for every S3
/// read the encoder or dashboard ever issues.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HttpResponseStreamTests
{
    private static HttpResponseStream New(byte[] content, out DisposeTrackingResponse response)
    {
        response = new(content);
        return new(response);
    }

    /// <summary>
    /// <see cref="HttpResponseMessage"/> is sealed with no dispose hook, so
    /// this subclass tracks whether Dispose was actually called — the
    /// observable proof that <see cref="HttpResponseStream"/> releases the
    /// response, not just its content stream.
    /// </summary>
    private sealed class DisposeTrackingResponse : HttpResponseMessage
    {
        public bool WasDisposed { get; private set; }

        public DisposeTrackingResponse(byte[] content)
            : base(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content);
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    [Fact]
    public void Read_returns_the_content_bytes()
    {
        byte[] payload = [1, 2, 3, 4];
        HttpResponseStream stream = New(payload, out _);

        byte[] buffer = new byte[4];
        int read = stream.Read(buffer, 0, 4);

        read.Should().Be(4);
        buffer.Should().Equal(payload);
    }

    [Fact]
    public void Read_span_overload_returns_the_content_bytes()
    {
        byte[] payload = [9, 8, 7];
        HttpResponseStream stream = New(payload, out _);

        byte[] buffer = new byte[3];
        int read = stream.Read(buffer.AsSpan());

        read.Should().Be(3);
        buffer.Should().Equal(payload);
    }

    [Fact]
    public async Task ReadAsync_byte_array_overload_returns_the_content_bytes()
    {
        byte[] payload = [5, 6, 7];
        HttpResponseStream stream = New(payload, out _);

        byte[] buffer = new byte[3];
        int read = await stream.ReadAsync(buffer, 0, 3, CancellationToken.None);

        read.Should().Be(3);
        buffer.Should().Equal(payload);
    }

    [Fact]
    public async Task ReadAsync_memory_overload_returns_the_content_bytes()
    {
        byte[] payload = [4, 3, 2];
        HttpResponseStream stream = New(payload, out _);

        byte[] buffer = new byte[3];
        int read = await stream.ReadAsync(buffer.AsMemory());

        read.Should().Be(3);
        buffer.Should().Equal(payload);
    }

    [Fact]
    public void CanRead_CanWrite_reflect_a_read_only_response_stream()
    {
        HttpResponseStream stream = New([1], out _);

        stream.CanRead.Should().BeTrue();
        stream.CanWrite.Should().BeFalse();
    }

    [Fact]
    public void Length_forwards_to_inner_stream()
    {
        byte[] payload = [1, 2, 3, 4, 5];
        HttpResponseStream stream = New(payload, out _);

        stream.Length.Should().Be(5);
    }

    [Fact]
    public void Position_get_and_set_forward_to_inner_stream()
    {
        byte[] payload = [1, 2, 3, 4, 5];
        HttpResponseStream stream = New(payload, out _);

        stream.Position = 2;

        stream.Position.Should().Be(2);
        byte[] buffer = new byte[1];
        stream.ReadExactly(buffer, 0, 1);
        buffer[0].Should().Be(3, "setting Position must actually move the inner stream's cursor");
    }

    [Fact]
    public void Seek_forwards_to_inner_stream()
    {
        byte[] payload = [10, 20, 30, 40];
        HttpResponseStream stream = New(payload, out _);

        long result = stream.Seek(2, SeekOrigin.Begin);

        result.Should().Be(2);
        byte[] buffer = new byte[1];
        stream.ReadExactly(buffer, 0, 1);
        buffer[0].Should().Be(30);
    }

    [Fact]
    public void SetLength_throws_NotSupportedException()
    {
        HttpResponseStream stream = New([1], out _);

        Action act = () => stream.SetLength(10);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Write_throws_NotSupportedException()
    {
        HttpResponseStream stream = New([1], out _);

        Action act = () => stream.Write([1], 0, 1);

        act.Should().Throw<NotSupportedException>("an HTTP GET response body is read-only");
    }

    [Fact]
    public void Flush_and_FlushAsync_do_not_throw()
    {
        HttpResponseStream stream = New([1], out _);

        Action act = () => stream.Flush();
        Func<Task> asyncAct = () => stream.FlushAsync(CancellationToken.None);

        act.Should().NotThrow();
        asyncAct.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_disposes_both_the_content_stream_and_the_response()
    {
        HttpResponseStream stream = New([1, 2, 3], out DisposeTrackingResponse response);

        stream.Dispose();

        response
            .WasDisposed.Should()
            .BeTrue(
                "Dispose must release the HttpResponseMessage, not just the content stream, or the connection leaks"
            );
    }

    [Fact]
    public async Task DisposeAsync_disposes_both_the_content_stream_and_the_response()
    {
        HttpResponseStream stream = New([1, 2, 3], out DisposeTrackingResponse response);

        await stream.DisposeAsync();

        response.WasDisposed.Should().BeTrue();
    }
}
