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

using FluentAssertions;
using Moq;
using NoMercy.Storage.Drivers.WebDav;
using WebDav;
using Xunit;

namespace NoMercy.Tests.Storage;

public class WebDavUploadStreamTests
{
    [Fact]
    public async Task Upload_writes_full_body_to_PutFile()
    {
        Mock<IWebDavClient> client = new();
        byte[] captured = [];
        client
            .Setup(c =>
                c.PutFile(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<PutFileParameters>())
            )
            .Returns(
                (string _, Stream stream, PutFileParameters _) =>
                {
                    using MemoryStream ms = new();
                    stream.CopyTo(ms);
                    captured = ms.ToArray();
                    return Task.FromResult(new WebDavResponse(200));
                }
            );

        byte[] payload = Enumerable.Range(0, 5000).Select(i => (byte)i).ToArray();

        await using (
            WebDavUploadStream upload = new(
                client.Object,
                "https://nas.local/dav/f.bin",
                overwrite: true
            )
        )
        {
            await upload.WriteAsync(payload);
        }

        captured.Should().Equal(payload);
    }

    [Fact]
    public async Task Upload_nonOverwrite_sends_if_none_match_header()
    {
        Mock<IWebDavClient> client = new();
        PutFileParameters? seen = null;
        client
            .Setup(c =>
                c.PutFile(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<PutFileParameters>())
            )
            .Returns(
                (string _, Stream _, PutFileParameters p) =>
                {
                    seen = p;
                    return Task.FromResult(new WebDavResponse(201));
                }
            );

        await using (
            WebDavUploadStream upload = new(
                client.Object,
                "https://nas.local/dav/f.bin",
                overwrite: false
            )
        )
        {
            await upload.WriteAsync(new byte[] { 1, 2, 3 });
        }

        seen.Should().NotBeNull();
        seen!.Headers.Should().Contain(h => h.Key == "If-None-Match" && h.Value == "*");
    }
}
