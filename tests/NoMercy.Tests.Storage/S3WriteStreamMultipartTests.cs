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
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            string query = request.RequestUri!.Query;
            Calls.Add((request.Method.Method, query, body.LongLength));

            if (request.Method == HttpMethod.Post && query.Contains("uploads="))
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "<InitiateMultipartUploadResult><UploadId>UP-123</UploadId></InitiateMultipartUploadResult>"
                    ),
                };

            if (request.Method == HttpMethod.Put && query.Contains("partNumber="))
            {
                HttpResponseMessage ok = new(HttpStatusCode.OK);
                ok.Headers.TryAddWithoutValidation("ETag", $"\"etag-{body.LongLength}\"");
                return ok;
            }

            if (request.Method == HttpMethod.Post && query.Contains("uploadId="))
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "<CompleteMultipartUploadResult></CompleteMultipartUploadResult>"
                    ),
                };

            return new(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Upload_25Mb_UsesThreePartMultipart()
    {
        RecordingHandler handler = new();
        using HttpClient http = new(handler);

        // Pin a 10 MiB part size so the assertion is about the multipart protocol
        // (part count + sizes) at a known size, independent of the production
        // default (which is tuned by the throughput sweep).
        await using (
            S3WriteStream stream = new(
                null!,
                "bucket",
                "object.bin",
                "https://s3.example.com",
                "us-east-1",
                "AK",
                "SK",
                http,
                10 * 1024 * 1024
            )
        )
        {
            byte[] data = new byte[25 * 1024 * 1024];
            await stream.WriteAsync(data);
        }

        Assert.Single(handler.Calls, c => c.Method == "POST" && c.Query.Contains("uploads="));

        List<(string Method, string Query, long Length)> parts = handler
            .Calls.Where(c => c.Method == "PUT" && c.Query.Contains("partNumber="))
            .ToList();
        Assert.Equal(3, parts.Count);
        Assert.Equal(10L * 1024 * 1024, parts[0].Length);
        Assert.Equal(10L * 1024 * 1024, parts[1].Length);
        Assert.Equal(5L * 1024 * 1024, parts[2].Length);

        Assert.Single(handler.Calls, c => c.Method == "POST" && c.Query.Contains("uploadId="));
    }

    [Fact]
    public async Task Upload_SmallObject_UsesSinglePut()
    {
        RecordingHandler handler = new();
        using HttpClient http = new(handler);

        await using (
            S3WriteStream stream = new(
                null!,
                "bucket",
                "object.bin",
                "https://s3.example.com",
                "us-east-1",
                "AK",
                "SK",
                http
            )
        )
        {
            await stream.WriteAsync(new byte[1024]);
        }

        Assert.DoesNotContain(handler.Calls, c => c.Query.Contains("uploads="));
        Assert.Single(handler.Calls);
        Assert.Equal("PUT", handler.Calls[0].Method);
    }
}
