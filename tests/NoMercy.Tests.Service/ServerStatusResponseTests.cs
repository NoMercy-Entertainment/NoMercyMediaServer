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

using Newtonsoft.Json;
using NoMercy.Launcher.Models;
using Xunit;

namespace NoMercy.Tests.Service;

public class ServerStatusResponseTests
{
    [Fact]
    public void Deserialize_FullResponse_MapsAllProperties()
    {
        string json = """
            {
                "status": "running",
                "server_name": "TestServer",
                "version": "1.2.3",
                "platform": "Linux",
                "architecture": "X64",
                "os": "Linux 6.1",
                "uptime_seconds": 3600,
                "start_time": "2026-01-01T00:00:00Z",
                "is_dev": true
            }
            """;

        ServerStatusResponse? result = JsonConvert.DeserializeObject<ServerStatusResponse>(value: json);

        Assert.NotNull(@object: result);
        Assert.Equal(expected: "running", actual: result.Status);
        Assert.Equal(expected: "TestServer", actual: result.ServerName);
        Assert.Equal(expected: "1.2.3", actual: result.Version);
        Assert.Equal(expected: "Linux", actual: result.Platform);
        Assert.Equal(expected: "X64", actual: result.Architecture);
        Assert.Equal(expected: "Linux 6.1", actual: result.Os);
        Assert.Equal(expected: 3600, actual: result.UptimeSeconds);
        Assert.True(condition: result.IsDev);
    }

    [Fact]
    public void Deserialize_MinimalResponse_UsesDefaults()
    {
        string json = """{ "status": "running" }""";

        ServerStatusResponse? result = JsonConvert.DeserializeObject<ServerStatusResponse>(value: json);

        Assert.NotNull(@object: result);
        Assert.Equal(expected: "running", actual: result.Status);
        Assert.Equal(expected: string.Empty, actual: result.ServerName);
        Assert.Equal(expected: string.Empty, actual: result.Version);
        Assert.Equal(expected: string.Empty, actual: result.Platform);
        Assert.Equal(expected: string.Empty, actual: result.Architecture);
        Assert.Equal(expected: string.Empty, actual: result.Os);
        Assert.Equal(expected: 0, actual: result.UptimeSeconds);
        Assert.False(condition: result.IsDev);
    }

    [Fact]
    public void Deserialize_StartingStatus_MapsCorrectly()
    {
        string json = """{ "status": "starting", "version": "0.9.0" }""";

        ServerStatusResponse? result = JsonConvert.DeserializeObject<ServerStatusResponse>(value: json);

        Assert.NotNull(@object: result);
        Assert.Equal(expected: "starting", actual: result.Status);
        Assert.Equal(expected: "0.9.0", actual: result.Version);
    }
}
