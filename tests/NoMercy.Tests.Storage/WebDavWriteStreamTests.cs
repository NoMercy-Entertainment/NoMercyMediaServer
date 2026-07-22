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

using NoMercy.Storage.Drivers.WebDav;
using WebDav;

namespace NoMercy.Tests.Storage;

public class WebDavWriteStreamTests
{
    [Fact]
    public async Task Upload_writes_full_body_to_PutFile()
    {
        Mock<IWebDavClient> client = new();
        byte[] captured = [];
        client
            .Setup(expression: c =>
                c.PutFile(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<PutFileParameters>())
            )
            .Returns(
                valueFunction: (string _, Stream stream, PutFileParameters _) =>
                {
                    using MemoryStream ms = new();
                    stream.CopyTo(destination: ms);
                    captured = ms.ToArray();
                    return Task.FromResult(result: new WebDavResponse(statusCode: 200));
                }
            );

        byte[] payload = Enumerable.Range(start: 0, count: 5000).Select(selector: i => (byte)i).ToArray();

        await using (
            WebDavWriteStream upload = new(
                client: client.Object,
                uri: "https://nas.local/dav/f.bin",
                overwrite: true
            )
        )
        {
            await upload.WriteAsync(buffer: payload);
        }

        captured.Should().Equal(elements: payload);
    }

    [Fact]
    public async Task Upload_nonOverwrite_sends_if_none_match_header()
    {
        Mock<IWebDavClient> client = new();
        PutFileParameters? seen = null;
        client
            .Setup(expression: c =>
                c.PutFile(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<PutFileParameters>())
            )
            .Returns(
                valueFunction: (string _, Stream _, PutFileParameters p) =>
                {
                    seen = p;
                    return Task.FromResult(result: new WebDavResponse(statusCode: 201));
                }
            );

        await using (
            WebDavWriteStream upload = new(
                client: client.Object,
                uri: "https://nas.local/dav/f.bin",
                overwrite: false
            )
        )
        {
            await upload.WriteAsync(buffer: new byte[] { 1, 2, 3 });
        }

        seen.Should().NotBeNull();
        seen!.Headers.Should().Contain(predicate: h => h.Key == "If-None-Match" && h.Value == "*");
    }
}
