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
/// Verifies <see cref="S3WriteStream"/> follows the AWS S3 multipart REST
/// protocol against a mocked transport — no live S3/MinIO required.
/// </summary>
public class S3WriteStreamMultipartTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public readonly List<(string Method, string Query, long Length)> Calls = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            byte[] body = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken: cancellationToken);
            string query = request.RequestUri!.Query;
            Calls.Add(item: (request.Method.Method, query, body.LongLength));

            if (request.Method == HttpMethod.Post && query.Contains(value: "uploads="))
                return new(statusCode: HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        content: "<InitiateMultipartUploadResult><UploadId>UP-123</UploadId></InitiateMultipartUploadResult>"
                    ),
                };

            if (request.Method == HttpMethod.Put && query.Contains(value: "partNumber="))
            {
                HttpResponseMessage ok = new(statusCode: HttpStatusCode.OK);
                ok.Headers.TryAddWithoutValidation(name: "ETag", value: $"\"etag-{body.LongLength}\"");
                return ok;
            }

            if (request.Method == HttpMethod.Post && query.Contains(value: "uploadId="))
                return new(statusCode: HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        content: "<CompleteMultipartUploadResult></CompleteMultipartUploadResult>"
                    ),
                };

            return new(statusCode: HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Upload_25Mb_UsesThreePartMultipart()
    {
        RecordingHandler handler = new();
        using HttpClient http = new(handler: handler);

        // Pin a 10 MiB part size so the assertion is about the multipart protocol
        // (part count + sizes) at a known size, independent of the production
        // default (which is tuned by the throughput sweep).
        await using (
            S3WriteStream stream = new(
                _: null!,
                bucket: "bucket",
                key: "object.bin",
                endpoint: "https://s3.example.com",
                region: "us-east-1",
                accessKey: "AK",
                secretKey: "SK",
                httpClient: http,
                partSize: 10 * 1024 * 1024
            )
        )
        {
            byte[] data = new byte[25 * 1024 * 1024];
            await stream.WriteAsync(buffer: data);
        }

        Assert.Single(collection: handler.Calls, predicate: c => c.Method == "POST" && c.Query.Contains(value: "uploads="));

        List<(string Method, string Query, long Length)> parts = handler
            .Calls.Where(predicate: c => c.Method == "PUT" && c.Query.Contains(value: "partNumber="))
            .ToList();
        Assert.Equal(expected: 3, actual: parts.Count);
        Assert.Equal(expected: 10L * 1024 * 1024, actual: parts[index: 0].Length);
        Assert.Equal(expected: 10L * 1024 * 1024, actual: parts[index: 1].Length);
        Assert.Equal(expected: 5L * 1024 * 1024, actual: parts[index: 2].Length);

        Assert.Single(collection: handler.Calls, predicate: c => c.Method == "POST" && c.Query.Contains(value: "uploadId="));
    }

    [Fact]
    public async Task Upload_SmallObject_UsesSinglePut()
    {
        RecordingHandler handler = new();
        using HttpClient http = new(handler: handler);

        await using (
            S3WriteStream stream = new(
                _: null!,
                bucket: "bucket",
                key: "object.bin",
                endpoint: "https://s3.example.com",
                region: "us-east-1",
                accessKey: "AK",
                secretKey: "SK",
                httpClient: http
            )
        )
        {
            await stream.WriteAsync(buffer: new byte[1024]);
        }

        Assert.DoesNotContain(collection: handler.Calls, filter: c => c.Query.Contains(value: "uploads="));
        Assert.Single(collection: handler.Calls);
        Assert.Equal(expected: "PUT", actual: handler.Calls[index: 0].Method);
    }
}
