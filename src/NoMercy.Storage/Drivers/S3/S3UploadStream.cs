using System.Security.Cryptography;
using System.Text;
using Amazon.S3;

namespace NoMercy.Storage.Drivers.S3;

/// <summary>
/// Write-only stream that buffers data and uploads it to S3 on
/// <see cref="DisposeAsync"/>. Uses raw SigV4 signed HTTP PUT instead of the
/// AWS SDK because <see cref="AmazonS3Client"/>'s default signing path
/// — even with <c>UseChunkEncoding=false</c>, <c>DisablePayloadSigning=true</c>
/// and <c>DisableDefaultChecksumValidation=true</c> — sends requests that
/// MinIO and several other S3-compatible servers reject with
/// <c>SignatureDoesNotMatch</c>. Direct SigV4 PUT is the same protocol the
/// SDK is supposed to use; doing it by hand sidesteps whatever extra
/// chunking / canonicalization the SDK applies that the server doesn't
/// accept.
/// </summary>
internal sealed class S3UploadStream : Stream
{
    private static readonly HttpClient HttpClient = new();

    private readonly string _bucket;
    private readonly string _key;
    private readonly string _endpoint;
    private readonly string _region;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly MemoryStream _buffer = new();
    private bool _disposed;

    internal S3UploadStream(
        IAmazonS3 _ /* kept for ABI compat with the previous ctor; unused */
        ,
        string bucket,
        string key,
        string endpoint,
        string region,
        string accessKey,
        string secretKey
    )
    {
        _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _region = region ?? throw new ArgumentNullException(nameof(region));
        _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
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
            UploadAsync(CancellationToken.None).GetAwaiter().GetResult();
            _buffer.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await UploadAsync(CancellationToken.None);
        _buffer.Dispose();

        await base.DisposeAsync();
    }

    private async Task UploadAsync(CancellationToken ct)
    {
        byte[] payload = _buffer.ToArray();
        string payloadHash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

        Uri endpointUri = new(_endpoint.TrimEnd('/'));
        string host = endpointUri.Host + (endpointUri.IsDefaultPort ? "" : $":{endpointUri.Port}");

        string canonicalUri = "/" + Uri.EscapeDataString(_bucket) + "/" + S3SigV4.EscapeKey(_key);
        string canonicalQs = string.Empty;

        DateTime now = DateTime.UtcNow;
        string amzDate = now.ToString("yyyyMMddTHHmmssZ");
        string dateStamp = now.ToString("yyyyMMdd");

        string canonicalHeaders =
            $"content-length:{payload.Length}\nhost:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        string signedHeaders = "content-length;host;x-amz-content-sha256;x-amz-date";

        string canonicalRequest = string.Join(
            "\n",
            "PUT",
            canonicalUri,
            canonicalQs,
            canonicalHeaders,
            signedHeaders,
            payloadHash
        );

        string credentialScope = $"{dateStamp}/{_region}/s3/aws4_request";
        string stringToSign = string.Join(
            "\n",
            "AWS4-HMAC-SHA256",
            amzDate,
            credentialScope,
            Convert
                .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))
                .ToLowerInvariant()
        );

        byte[] signingKey = S3SigV4.DeriveSigningKey(_secretKey, dateStamp, _region);
        string signature = Convert
            .ToHexString(S3SigV4.HmacSha256(signingKey, stringToSign))
            .ToLowerInvariant();

        string authHeader =
            $"AWS4-HMAC-SHA256 Credential={_accessKey}/{credentialScope}, "
            + $"SignedHeaders={signedHeaders}, Signature={signature}";

        Uri requestUri = new(_endpoint.TrimEnd('/') + canonicalUri);
        using HttpRequestMessage req = new(HttpMethod.Put, requestUri)
        {
            Content = new ByteArrayContent(payload),
        };
        req.Content.Headers.ContentLength = payload.Length;
        req.Headers.TryAddWithoutValidation("Authorization", authHeader);
        req.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        req.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        req.Headers.Host = host;

        using HttpResponseMessage res = await HttpClient.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            string body = await res.Content.ReadAsStringAsync(ct);
            throw new IOException(
                $"S3 PUT '{_bucket}/{_key}' failed: HTTP {(int)res.StatusCode} {res.ReasonPhrase}; body: {body}"
            );
        }
    }
}
