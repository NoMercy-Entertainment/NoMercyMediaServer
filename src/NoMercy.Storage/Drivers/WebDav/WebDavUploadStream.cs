using WebDav;

namespace NoMercy.Storage.Drivers.WebDav;

/// <summary>
/// Write-only stream that buffers data in memory and PUTs it to a WebDAV
/// server on <see cref="Dispose"/>. Mirrors <c>S3UploadStream</c>'s shape.
/// </summary>
internal sealed class WebDavUploadStream : Stream
{
    private readonly IWebDavClient _client;
    private readonly string _uri;
    private readonly bool _overwrite;
    private readonly MemoryStream _buffer = new();
    private bool _disposed;

    internal WebDavUploadStream(IWebDavClient client, string uri, bool overwrite)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        _overwrite = overwrite;
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
        _buffer.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer) => _buffer.Write(buffer);

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => _buffer.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _buffer.WriteAsync(buffer, cancellationToken);

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
            _buffer.Seek(0, SeekOrigin.Begin);
            PutAsync(CancellationToken.None).GetAwaiter().GetResult();
            _buffer.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        _buffer.Seek(0, SeekOrigin.Begin);
        await PutAsync(CancellationToken.None);
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
                Headers = [new KeyValuePair<string, string>("If-None-Match", "*")],
            };

        WebDavResponse response = await _client.PutFile(_uri, _buffer, parameters);

        if (!response.IsSuccessful)
        {
            if (!_overwrite && response.StatusCode == 412)
                throw new IOException(
                    $"Cannot write to '{_uri}': the resource already exists and overwrite is false (HTTP 412 Precondition Failed)."
                );

            throw new IOException(
                $"WebDAV PUT to '{_uri}' failed: HTTP {response.StatusCode} — {response.Description}"
            );
        }
    }
}
