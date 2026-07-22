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
using Makaretu.Dns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Database;
using NoMercy.Networking.Discovery;
using Xunit;
using Message = Makaretu.Dns.Message;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: ExtractFingerprint must find the device's fp=... TXT record
/// (case-insensitive prefix) among possibly many other TXT strings, and
/// ExtractEndpoint must pair the SRV record's port with the A record's
/// address — both must fail soft (return null / (null,null)) when the
/// expected record is absent rather than throwing, since a malformed mDNS
/// response from a third-party device on the LAN must never crash the
/// scanner. These operate on real Makaretu.Dns message objects — no
/// multicast socket is involved.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class MdnsDeviceScannerParsingTests
{
    private static TXTRecord Txt(params string[] strings) => new() { Strings = [.. strings] };

    [Fact]
    public void ExtractFingerprint_FindsFpPrefixedString()
    {
        Message msg = new();
        msg.AdditionalRecords.Add(item: Txt(strings: ["v=1", "fp=abc123", "other=value"]));

        string? fingerprint = MdnsDeviceScanner.ExtractFingerprint(msg: msg);

        Assert.Equal(expected: "abc123", actual: fingerprint);
    }

    [Fact]
    public void ExtractFingerprint_PrefixMatchIsCaseInsensitive()
    {
        Message msg = new();
        msg.AdditionalRecords.Add(item: Txt(strings: "FP=xyz789"));

        string? fingerprint = MdnsDeviceScanner.ExtractFingerprint(msg: msg);

        Assert.Equal(expected: "xyz789", actual: fingerprint);
    }

    [Fact]
    public void ExtractFingerprint_NoTxtRecords_ReturnsNull()
    {
        Message msg = new();

        string? fingerprint = MdnsDeviceScanner.ExtractFingerprint(msg: msg);

        Assert.Null(@object: fingerprint);
    }

    [Fact]
    public void ExtractFingerprint_TxtRecordWithoutFpPrefix_ReturnsNull()
    {
        Message msg = new();
        msg.AdditionalRecords.Add(item: Txt(strings: ["v=1", "name=Living Room TV"]));

        string? fingerprint = MdnsDeviceScanner.ExtractFingerprint(msg: msg);

        Assert.Null(@object: fingerprint);
    }

    [Fact]
    public void ExtractFingerprint_MultipleTxtRecords_FindsFirstMatch()
    {
        Message msg = new();
        msg.AdditionalRecords.Add(item: Txt(strings: "v=1"));
        msg.AdditionalRecords.Add(item: Txt(strings: "fp=second-record"));

        string? fingerprint = MdnsDeviceScanner.ExtractFingerprint(msg: msg);

        Assert.Equal(expected: "second-record", actual: fingerprint);
    }

    [Fact]
    public void ExtractFingerprint_EmptyFpValue_ReturnsEmptyString()
    {
        Message msg = new();
        msg.AdditionalRecords.Add(item: Txt(strings: "fp="));

        string? fingerprint = MdnsDeviceScanner.ExtractFingerprint(msg: msg);

        Assert.Equal(expected: string.Empty, actual: fingerprint);
    }

    [Fact]
    public void ExtractEndpoint_WithSrvAndA_ReturnsIpAndPort()
    {
        Message msg = new();
        msg.AdditionalRecords.Add(item: new SRVRecord { Port = 8009, Target = new(name: "device.local") });
        msg.AdditionalRecords.Add(item: new ARecord { Address = IPAddress.Parse(ipString: "192.168.1.42") });

        (string? ip, int? port) = MdnsDeviceScanner.ExtractEndpoint(msg: msg);

        Assert.Equal(expected: "192.168.1.42", actual: ip);
        Assert.Equal(expected: 8009, actual: port);
    }

    [Fact]
    public void ExtractEndpoint_NoSrvRecord_ReturnsNullTuple()
    {
        Message msg = new();
        msg.AdditionalRecords.Add(item: new ARecord { Address = IPAddress.Parse(ipString: "192.168.1.42") });

        (string? ip, int? port) = MdnsDeviceScanner.ExtractEndpoint(msg: msg);

        Assert.Null(@object: ip);
        Assert.Null(value: port);
    }

    [Fact]
    public void ExtractEndpoint_SrvWithoutARecord_ReturnsNullIpButRealPort()
    {
        Message msg = new();
        msg.AdditionalRecords.Add(item: new SRVRecord { Port = 8009, Target = new(name: "device.local") });

        (string? ip, int? port) = MdnsDeviceScanner.ExtractEndpoint(msg: msg);

        Assert.Null(@object: ip);
        Assert.Equal(expected: 8009, actual: port);
    }

    [Fact]
    public void ExtractEndpoint_NoRecordsAtAll_ReturnsNullTuple()
    {
        Message msg = new();

        (string? ip, int? port) = MdnsDeviceScanner.ExtractEndpoint(msg: msg);

        Assert.Null(@object: ip);
        Assert.Null(value: port);
    }

    // -- Lifecycle: Start()/Dispose() idempotency and disposal safety. The
    // real multicast join and mDNS wire receive (OnInstanceDiscovered firing
    // from an actual network packet, and the DB upsert it triggers) require a
    // live LAN multicast group and are itemized as not unit-testable —
    // see the coverage report.

    [Fact]
    public void Dispose_WithoutStart_DoesNotThrow()
    {
        MdnsDeviceScanner scanner = new(
            contextFactory: new ThrowingDbContextFactory(),
            logger: NullLogger<MdnsDeviceScanner>.Instance
        );

        Exception? ex = Record.Exception(testCode: scanner.Dispose);

        Assert.Null(@object: ex);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        MdnsDeviceScanner scanner = new(
            contextFactory: new ThrowingDbContextFactory(),
            logger: NullLogger<MdnsDeviceScanner>.Instance
        );

        Exception? ex = Record.Exception(testCode: () =>
        {
            scanner.Dispose();
            scanner.Dispose();
        });

        Assert.Null(@object: ex);
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<MediaContext>
    {
        public MediaContext CreateDbContext() => throw new NotSupportedException();
    }
}
