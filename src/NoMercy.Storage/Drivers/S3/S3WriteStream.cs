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
/// fills (8 MB each — comfortably above S3's 5 MB minimum); objects that never
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
    // S3 requires every part except the last to be at least 5 MB. 8 MB was the
    // fastest in the throughput sweep (write and read both peaked there; larger
    // parts only grew the per-part buffer copy without cutting round-trips enough
    // to pay for it). The ctor accepts an override so the sweep can re-measure.
    private const int DefaultPartSize = 8 * 1024 * 1024;

    private static readonly HttpClient SharedHttpClient = new();

    private readonly int _partSize;
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
        HttpClient? httpClient = null,
        int partSize = DefaultPartSize
    )
    {
        _bucket = bucket ?? throw new ArgumentNullException(paramName: nameof(bucket));
        _key = key ?? throw new ArgumentNullException(paramName: nameof(key));
        _endpoint = endpoint ?? throw new ArgumentNullException(paramName: nameof(endpoint));
        _region = region ?? throw new ArgumentNullException(paramName: nameof(region));
        _accessKey = accessKey ?? throw new ArgumentNullException(paramName: nameof(accessKey));
        _secretKey = secretKey ?? throw new ArgumentNullException(paramName: nameof(secretKey));
        _http = httpClient ?? SharedHttpClient;
        _partSize = partSize;
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
        Write(buffer: buffer.AsSpan(start: offset, length: count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _part.Write(buffer: buffer);
        if (_part.Length >= _partSize)
            DrainFullPartsAsync(ct: CancellationToken.None).GetAwaiter().GetResult();
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => WriteAsync(buffer: buffer.AsMemory(start: offset, length: count), cancellationToken: cancellationToken).AsTask();

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        await _part.WriteAsync(buffer: buffer, cancellationToken: cancellationToken);
        if (_part.Length >= _partSize)
            await DrainFullPartsAsync(ct: cancellationToken);
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
            FinishAsync(ct: CancellationToken.None).GetAwaiter().GetResult();
            _part.Dispose();
        }
        base.Dispose(disposing: disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        await FinishAsync(ct: CancellationToken.None);
        _part.Dispose();
        await base.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // Multipart orchestration
    // -----------------------------------------------------------------------

    private async Task DrainFullPartsAsync(CancellationToken ct)
    {
        while (_part.Length >= _partSize)
        {
            byte[] all = _part.ToArray();
            await UploadOnePartAsync(data: all[.._partSize], ct: ct);

            _part.SetLength(value: 0);
            _part.Position = 0;
            if (all.Length > _partSize)
                _part.Write(buffer: all, offset: _partSize, count: all.Length - _partSize);
        }
    }

    private async Task UploadOnePartAsync(byte[] data, CancellationToken ct)
    {
        _uploadId ??= await CreateMultipartUploadAsync(ct: ct);
        _partNumber++;
        _etags.Add(item: await UploadPartAsync(partNumber: _partNumber, data: data, ct: ct));
    }

    private async Task FinishAsync(CancellationToken ct)
    {
        // Never crossed the part threshold → a single PUT is cheaper than a
        // multipart round-trip and sidesteps the 5 MB minimum-part rule.
        if (_uploadId is null)
        {
            await SinglePutAsync(payload: _part.ToArray(), ct: ct);
            return;
        }

        try
        {
            // The final part is the only one allowed to be under 5 MB.
            if (_part.Length > 0)
                await UploadOnePartAsync(data: _part.ToArray(), ct: ct);

            await CompleteMultipartUploadAsync(ct: ct);
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
            method: HttpMethod.Post,
            canonicalQueryString: "uploads=",
            payload: [],
            contentType: null,
            ct: ct
        );
        string body = await res.Content.ReadAsStringAsync(cancellationToken: ct);
        EnsureSuccess(res: res, op: "CreateMultipartUpload", body: body);

        string? uploadId = XDocument
            .Parse(text: body)
            .Descendants()
            .FirstOrDefault(predicate: e => e.Name.LocalName == "UploadId")
            ?.Value;

        if (string.IsNullOrEmpty(value: uploadId))
            throw new IOException(
                message: $"S3 CreateMultipartUpload for '{_bucket}/{_key}' returned no UploadId. Body: {body}"
            );

        return uploadId;
    }

    private async Task<string> UploadPartAsync(int partNumber, byte[] data, CancellationToken ct)
    {
        string qs = $"partNumber={partNumber}&uploadId={Uri.EscapeDataString(stringToEscape: _uploadId!)}";
        using HttpResponseMessage res = await SendSignedAsync(method: HttpMethod.Put, canonicalQueryString: qs, payload: data, contentType: null, ct: ct);
        if (!res.IsSuccessStatusCode)
        {
            string body = await res.Content.ReadAsStringAsync(cancellationToken: ct);
            EnsureSuccess(res: res, op: $"UploadPart {partNumber}", body: body);
        }

        string? etag = res.Headers.ETag?.Tag ?? FirstHeader(res: res, name: "ETag");
        if (string.IsNullOrEmpty(value: etag))
            throw new IOException(
                message: $"S3 UploadPart {partNumber} for '{_bucket}/{_key}' returned no ETag."
            );

        return etag;
    }

    private async Task CompleteMultipartUploadAsync(CancellationToken ct)
    {
        StringBuilder xml = new();
        xml.Append(value: "<CompleteMultipartUpload>");
        for (int i = 0; i < _etags.Count; i++)
        {
            xml.Append(value: "<Part><PartNumber>")
                .Append(value: i + 1)
                .Append(value: "</PartNumber><ETag>")
                .Append(value: _etags[index: i])
                .Append(value: "</ETag></Part>");
        }
        xml.Append(value: "</CompleteMultipartUpload>");

        byte[] payload = Encoding.UTF8.GetBytes(s: xml.ToString());
        string qs = $"uploadId={Uri.EscapeDataString(stringToEscape: _uploadId!)}";
        using HttpResponseMessage res = await SendSignedAsync(
            method: HttpMethod.Post,
            canonicalQueryString: qs,
            payload: payload,
            contentType: "application/xml",
            ct: ct
        );
        string body = await res.Content.ReadAsStringAsync(cancellationToken: ct);
        EnsureSuccess(res: res, op: "CompleteMultipartUpload", body: body);

        // S3 can return HTTP 200 with an <Error> body when completion fails.
        if (body.Contains(value: "<Error>", comparisonType: StringComparison.Ordinal))
            throw new IOException(
                message: $"S3 CompleteMultipartUpload for '{_bucket}/{_key}' failed. Body: {body}"
            );
    }

    private async Task TryAbortAsync()
    {
        if (_uploadId is null)
            return;
        try
        {
            string qs = $"uploadId={Uri.EscapeDataString(stringToEscape: _uploadId)}";
            using HttpResponseMessage _ = await SendSignedAsync(
                method: HttpMethod.Delete,
                canonicalQueryString: qs,
                payload: [],
                contentType: null,
                ct: CancellationToken.None
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
            method: HttpMethod.Put,
            canonicalQueryString: string.Empty,
            payload: payload,
            contentType: null,
            ct: ct
        );
        if (!res.IsSuccessStatusCode)
        {
            string body = await res.Content.ReadAsStringAsync(cancellationToken: ct);
            EnsureSuccess(res: res, op: "PUT", body: body);
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
        string payloadHash = Convert.ToHexString(inArray: SHA256.HashData(source: payload)).ToLowerInvariant();

        Uri endpointUri = new(uriString: _endpoint.TrimEnd(trimChar: '/'));
        string host = endpointUri.Host + (endpointUri.IsDefaultPort ? "" : $":{endpointUri.Port}");
        string canonicalUri = "/" + Uri.EscapeDataString(stringToEscape: _bucket) + "/" + S3SigV4.EscapeKey(key: _key);

        DateTime now = DateTime.UtcNow;
        string amzDate = now.ToString(format: "yyyyMMddTHHmmssZ");
        string dateStamp = now.ToString(format: "yyyyMMdd");

        string canonicalHeaders =
            $"content-length:{payload.Length}\nhost:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        const string signedHeaders = "content-length;host;x-amz-content-sha256;x-amz-date";

        string canonicalRequest = string.Join(
            separator: "\n", value: [method.Method, canonicalUri, canonicalQueryString, canonicalHeaders, signedHeaders, payloadHash]
        );

        string credentialScope = $"{dateStamp}/{_region}/s3/aws4_request";
        string stringToSign = string.Join(
            separator: "\n", value:
            ["AWS4-HMAC-SHA256", amzDate, credentialScope, Convert
                .ToHexString(inArray: SHA256.HashData(source: Encoding.UTF8.GetBytes(s: canonicalRequest)))
                .ToLowerInvariant()
            ]
        );

        byte[] signingKey = S3SigV4.DeriveSigningKey(secret: _secretKey, dateStamp: dateStamp, region: _region);
        string signature = Convert
            .ToHexString(inArray: S3SigV4.HmacSha256(key: signingKey, data: stringToSign))
            .ToLowerInvariant();

        string authHeader =
            $"AWS4-HMAC-SHA256 Credential={_accessKey}/{credentialScope}, "
            + $"SignedHeaders={signedHeaders}, Signature={signature}";

        string url =
            _endpoint.TrimEnd(trimChar: '/')
            + canonicalUri
            + (canonicalQueryString.Length > 0 ? "?" + canonicalQueryString : string.Empty);

        using HttpRequestMessage req = new(method: method, requestUri: url);
        ByteArrayContent content = new(content: payload);
        content.Headers.ContentLength = payload.Length;
        if (contentType is not null)
            content.Headers.TryAddWithoutValidation(name: "Content-Type", value: contentType);
        req.Content = content;
        req.Headers.TryAddWithoutValidation(name: "Authorization", value: authHeader);
        req.Headers.TryAddWithoutValidation(name: "x-amz-content-sha256", value: payloadHash);
        req.Headers.TryAddWithoutValidation(name: "x-amz-date", value: amzDate);
        req.Headers.Host = host;

        return await _http.SendAsync(request: req, cancellationToken: ct);
    }

    private static string? FirstHeader(HttpResponseMessage res, string name) =>
        res.Headers.TryGetValues(name: name, values: out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;

    private static void EnsureSuccess(HttpResponseMessage res, string op, string body)
    {
        if (!res.IsSuccessStatusCode)
            throw new IOException(
                message: $"S3 {op} failed: HTTP {(int)res.StatusCode} {res.ReasonPhrase}; body: {body}"
            );
    }
}
