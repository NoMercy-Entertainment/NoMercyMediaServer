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

using WebDav;

namespace NoMercy.Storage.Drivers.WebDav;

/// <summary>
/// Write-only stream that buffers data on disk and PUTs it to a WebDAV
/// server on <see cref="Dispose"/>. Mirrors <c>S3WriteStream</c>'s shape.
/// </summary>
internal sealed class WebDavWriteStream : Stream
{
    private readonly IWebDavClient _client;
    private readonly string _uri;
    private readonly bool _overwrite;
    private readonly FileStream _buffer;
    private bool _disposed;

    internal WebDavWriteStream(IWebDavClient client, string uri, bool overwrite)
    {
        _client = client ?? throw new ArgumentNullException(paramName: nameof(client));
        _uri = uri ?? throw new ArgumentNullException(paramName: nameof(uri));
        _overwrite = overwrite;

        // Buffer the body on disk instead of a MemoryStream: a multi-GB media
        // upload would otherwise allocate the whole file on the managed heap
        // (and exceed the single-array size limit). A temp FileStream keeps the
        // body off the heap while remaining seekable, so PutFile still sends a
        // Content-Length (WebDAV servers commonly reject chunked PUTs).
        // DeleteOnClose removes the temp file when the stream is disposed.
        string tempPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-webdav-{Guid.NewGuid():N}.tmp");
        _buffer = new(
            path: tempPath,
            mode: FileMode.CreateNew,
            access: FileAccess.ReadWrite,
            share: FileShare.None,
            bufferSize: 81920,
            options: FileOptions.DeleteOnClose | FileOptions.Asynchronous
        );
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _buffer.Length;
    public override long Position
    {
        get => _buffer.Position;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        _buffer.Write(buffer: buffer, offset: offset, count: count);

    public override void Write(ReadOnlySpan<byte> buffer) => _buffer.Write(buffer: buffer);

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => _buffer.WriteAsync(buffer: buffer, offset: offset, count: count, cancellationToken: cancellationToken);

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _buffer.WriteAsync(buffer: buffer, cancellationToken: cancellationToken);

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;

        if (disposing)
        {
            _buffer.Seek(offset: 0, origin: SeekOrigin.Begin);
            PutAsync(ct: CancellationToken.None).GetAwaiter().GetResult();
            _buffer.Dispose();
        }

        base.Dispose(disposing: disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        _buffer.Seek(offset: 0, origin: SeekOrigin.Begin);
        await PutAsync(ct: CancellationToken.None);
        _buffer.Dispose();

        await base.DisposeAsync();
    }

    private async Task PutAsync(CancellationToken ct)
    {
        // For overwrite=false, add If-None-Match: * so the server rejects
        // a PUT that would replace an existing resource (HTTP 412).
        PutFileParameters parameters = _overwrite
            ? new() { CancellationToken = ct }
            : new()
            {
                CancellationToken = ct,
                Headers = [new(key: "If-None-Match", value: "*")],
            };

        WebDavResponse response = await _client.PutFile(requestUri: _uri, stream: _buffer, parameters: parameters);

        if (!response.IsSuccessful)
        {
            if (!_overwrite && response.StatusCode == 412)
                throw new IOException(
                    message: $"Cannot write to '{_uri}': the resource already exists and overwrite is false (HTTP 412 Precondition Failed)."
                );

            throw new IOException(
                message: $"WebDAV PUT to '{_uri}' failed: HTTP {response.StatusCode} — {response.Description}"
            );
        }
    }
}
