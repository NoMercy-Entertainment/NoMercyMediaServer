using Amazon.S3;
using Amazon.S3.Transfer;

namespace NoMercy.Storage.Drivers.S3;

/// <summary>
/// Write-only stream that buffers data and uploads it to S3 on
/// <see cref="Dispose"/>. Writes below 5 MB use a single PUT;
/// larger writes use multipart upload via <see cref="TransferUtility"/>.
/// </summary>
internal sealed class S3UploadStream : Stream
{
    private const int MultipartThresholdBytes = 5 * 1024 * 1024;

    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly string _key;
    private readonly MemoryStream _buffer = new();
    private bool _disposed;

    internal S3UploadStream(IAmazonS3 client, string bucket, string key)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
        _key = key ?? throw new ArgumentNullException(nameof(key));
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
            UploadSync();
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
        await UploadAsync(CancellationToken.None);
        _buffer.Dispose();

        await base.DisposeAsync();
    }

    private void UploadSync()
    {
        UploadAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task UploadAsync(CancellationToken ct)
    {
        long length = _buffer.Length;

        if (length < MultipartThresholdBytes)
        {
            Amazon.S3.Model.PutObjectRequest request = new()
            {
                BucketName = _bucket,
                Key = _key,
                InputStream = _buffer,
                AutoCloseStream = false,
            };
            await _client.PutObjectAsync(request, ct);
        }
        else
        {
            TransferUtility transfer = new(_client);
            TransferUtilityUploadRequest request = new()
            {
                BucketName = _bucket,
                Key = _key,
                InputStream = _buffer,
                AutoCloseStream = false,
                PartSize = MultipartThresholdBytes,
            };
            await transfer.UploadAsync(request, ct);
        }
    }
}
