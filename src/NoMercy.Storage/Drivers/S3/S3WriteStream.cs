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
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Amazon.S3;

namespace NoMercy.Storage.Drivers.S3;

/// <summary>
/// Write-only stream that uploads to S3 using a streaming multipart upload so a
/// large object is never buffered whole in RAM. Parts are flushed as the stream
/// fills (10 MB each — comfortably above S3's 5 MB minimum); objects that never
/// cross the threshold fall back to a single PUT, which avoids both the
/// multipart round-trip and the minimum-part-size rule.
///
/// <para>Like the original single-PUT path this signs requests with raw SigV4
/// rather than the AWS SDK, because the SDK's signing — even with chunk encoding
/// and payload signing disabled — produces requests MinIO and several other
/// S3-compatible servers reject with <c>SignatureDoesNotMatch</c>.</para>
/// </summary>
internal sealed class S3WriteStream : Stream
{
    // S3 requires every part except the last to be at least 5 MB. 10 MB keeps a
    // buffered part well above that floor while bounding peak memory.
    private const int PartSize = 10 * 1024 * 1024;

    private static readonly HttpClient SharedHttpClient = new();

    private readonly HttpClient _http;
    private readonly string _bucket;
    private readonly string _key;
    private readonly string _endpoint;
    private readonly string _region;
    private readonly string _accessKey;
    private readonly string _secretKey;

    private readonly MemoryStream _part = new();
    private readonly List<string> _etags = [];
    private string? _uploadId;
    private int _partNumber;
    private bool _disposed;

    internal S3WriteStream(
        IAmazonS3 _ /* kept for ABI compat with the previous ctor; unused */
        ,
        string bucket,
        string key,
        string endpoint,
        string region,
        string accessKey,
        string secretKey,
        HttpClient? httpClient = null
    )
    {
        _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _region = region ?? throw new ArgumentNullException(nameof(region));
        _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
        _http = httpClient ?? SharedHttpClient;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _part.Length;

    public override long Position
    {
        get => _part.Position;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _part.Write(buffer);
        if (_part.Length >= PartSize)
            DrainFullPartsAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        await _part.WriteAsync(buffer, cancellationToken);
        if (_part.Length >= PartSize)
            await DrainFullPartsAsync(cancellationToken);
    }

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
            FinishAsync(CancellationToken.None).GetAwaiter().GetResult();
            _part.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        await FinishAsync(CancellationToken.None);
        _part.Dispose();
        await base.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // Multipart orchestration
    // -----------------------------------------------------------------------

    private async Task DrainFullPartsAsync(CancellationToken ct)
    {
        while (_part.Length >= PartSize)
        {
            byte[] all = _part.ToArray();
            await UploadOnePartAsync(all[..PartSize], ct);

            _part.SetLength(0);
            _part.Position = 0;
            if (all.Length > PartSize)
                _part.Write(all, PartSize, all.Length - PartSize);
        }
    }

    private async Task UploadOnePartAsync(byte[] data, CancellationToken ct)
    {
        _uploadId ??= await CreateMultipartUploadAsync(ct);
        _partNumber++;
        _etags.Add(await UploadPartAsync(_partNumber, data, ct));
    }

    private async Task FinishAsync(CancellationToken ct)
    {
        // Never crossed the part threshold → a single PUT is cheaper than a
        // multipart round-trip and sidesteps the 5 MB minimum-part rule.
        if (_uploadId is null)
        {
            await SinglePutAsync(_part.ToArray(), ct);
            return;
        }

        try
        {
            // The final part is the only one allowed to be under 5 MB.
            if (_part.Length > 0)
                await UploadOnePartAsync(_part.ToArray(), ct);

            await CompleteMultipartUploadAsync(ct);
        }
        catch
        {
            await TryAbortAsync();
            throw;
        }
    }

    // -----------------------------------------------------------------------
    // S3 REST operations
    // -----------------------------------------------------------------------

    private async Task<string> CreateMultipartUploadAsync(CancellationToken ct)
    {
        using HttpResponseMessage res = await SendSignedAsync(
            HttpMethod.Post,
            "uploads=",
            [],
            null,
            ct
        );
        string body = await res.Content.ReadAsStringAsync(ct);
        EnsureSuccess(res, "CreateMultipartUpload", body);

        string? uploadId = XDocument
            .Parse(body)
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "UploadId")
            ?.Value;

        if (string.IsNullOrEmpty(uploadId))
            throw new IOException(
                $"S3 CreateMultipartUpload for '{_bucket}/{_key}' returned no UploadId. Body: {body}"
            );

        return uploadId;
    }

    private async Task<string> UploadPartAsync(int partNumber, byte[] data, CancellationToken ct)
    {
        string qs = $"partNumber={partNumber}&uploadId={Uri.EscapeDataString(_uploadId!)}";
        using HttpResponseMessage res = await SendSignedAsync(HttpMethod.Put, qs, data, null, ct);
        if (!res.IsSuccessStatusCode)
        {
            string body = await res.Content.ReadAsStringAsync(ct);
            EnsureSuccess(res, $"UploadPart {partNumber}", body);
        }

        string? etag = res.Headers.ETag?.Tag ?? FirstHeader(res, "ETag");
        if (string.IsNullOrEmpty(etag))
            throw new IOException(
                $"S3 UploadPart {partNumber} for '{_bucket}/{_key}' returned no ETag."
            );

        return etag;
    }

    private async Task CompleteMultipartUploadAsync(CancellationToken ct)
    {
        StringBuilder xml = new();
        xml.Append("<CompleteMultipartUpload>");
        for (int i = 0; i < _etags.Count; i++)
        {
            xml.Append("<Part><PartNumber>")
                .Append(i + 1)
                .Append("</PartNumber><ETag>")
                .Append(_etags[i])
                .Append("</ETag></Part>");
        }
        xml.Append("</CompleteMultipartUpload>");

        byte[] payload = Encoding.UTF8.GetBytes(xml.ToString());
        string qs = $"uploadId={Uri.EscapeDataString(_uploadId!)}";
        using HttpResponseMessage res = await SendSignedAsync(
            HttpMethod.Post,
            qs,
            payload,
            "application/xml",
            ct
        );
        string body = await res.Content.ReadAsStringAsync(ct);
        EnsureSuccess(res, "CompleteMultipartUpload", body);

        // S3 can return HTTP 200 with an <Error> body when completion fails.
        if (body.Contains("<Error>", StringComparison.Ordinal))
            throw new IOException(
                $"S3 CompleteMultipartUpload for '{_bucket}/{_key}' failed. Body: {body}"
            );
    }

    private async Task TryAbortAsync()
    {
        if (_uploadId is null)
            return;
        try
        {
            string qs = $"uploadId={Uri.EscapeDataString(_uploadId)}";
            using HttpResponseMessage _ = await SendSignedAsync(
                HttpMethod.Delete,
                qs,
                [],
                null,
                CancellationToken.None
            );
        }
        catch
        {
            // Best-effort cleanup — the original failure is what surfaces.
        }
    }

    private async Task SinglePutAsync(byte[] payload, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendSignedAsync(
            HttpMethod.Put,
            string.Empty,
            payload,
            null,
            ct
        );
        if (!res.IsSuccessStatusCode)
        {
            string body = await res.Content.ReadAsStringAsync(ct);
            EnsureSuccess(res, "PUT", body);
        }
    }

    // -----------------------------------------------------------------------
    // SigV4 signing — same canonicalisation as the proven single-PUT path,
    // generalised over HTTP method and canonical query string.
    // -----------------------------------------------------------------------

    private async Task<HttpResponseMessage> SendSignedAsync(
        HttpMethod method,
        string canonicalQueryString,
        byte[] payload,
        string? contentType,
        CancellationToken ct
    )
    {
        string payloadHash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

        Uri endpointUri = new(_endpoint.TrimEnd('/'));
        string host = endpointUri.Host + (endpointUri.IsDefaultPort ? "" : $":{endpointUri.Port}");
        string canonicalUri = "/" + Uri.EscapeDataString(_bucket) + "/" + S3SigV4.EscapeKey(_key);

        DateTime now = DateTime.UtcNow;
        string amzDate = now.ToString("yyyyMMddTHHmmssZ");
        string dateStamp = now.ToString("yyyyMMdd");

        string canonicalHeaders =
            $"content-length:{payload.Length}\nhost:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        const string signedHeaders = "content-length;host;x-amz-content-sha256;x-amz-date";

        string canonicalRequest = string.Join(
            "\n",
            method.Method,
            canonicalUri,
            canonicalQueryString,
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

        string url =
            _endpoint.TrimEnd('/')
            + canonicalUri
            + (canonicalQueryString.Length > 0 ? "?" + canonicalQueryString : string.Empty);

        using HttpRequestMessage req = new(method, url);
        ByteArrayContent content = new(payload);
        content.Headers.ContentLength = payload.Length;
        if (contentType is not null)
            content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        req.Content = content;
        req.Headers.TryAddWithoutValidation("Authorization", authHeader);
        req.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        req.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        req.Headers.Host = host;

        return await _http.SendAsync(req, ct);
    }

    private static string? FirstHeader(HttpResponseMessage res, string name) =>
        res.Headers.TryGetValues(name, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;

    private static void EnsureSuccess(HttpResponseMessage res, string op, string body)
    {
        if (!res.IsSuccessStatusCode)
            throw new IOException(
                $"S3 {op} failed: HTTP {(int)res.StatusCode} {res.ReasonPhrase}; body: {body}"
            );
    }
}
