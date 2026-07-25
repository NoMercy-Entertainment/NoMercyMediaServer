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

using Microsoft.AspNetCore.SignalR;
using Moq;
using NoMercy.Networking.Http;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: Client must carry every field a caller needs to route a
/// SignalR message back to the right connection (Sub, Socket, Endpoint) on
/// top of the inherited Device identity fields, and ClientRequest must round
/// trip every registration field the client sends when it announces itself.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HttpDtoTests
{
    [Fact]
    public void Client_DefaultEndpoint_IsEmptyString()
    {
        Client client = new();

        Assert.Equal(string.Empty, client.Endpoint);
    }

    [Fact]
    public void Client_DefaultPing_IsNull()
    {
        Client client = new();

        Assert.Null(client.Ping);
    }

    [Fact]
    public void Client_SetSubPingEndpointSocket_RoundTrips()
    {
        Guid sub = Guid.NewGuid();
        Mock<ISingleClientProxy> proxy = new();

        Client client = new()
        {
            Sub = sub,
            Ping = 42,
            Socket = proxy.Object,
            Endpoint = "/videoHub",
        };

        Assert.Equal(sub, client.Sub);
        Assert.Equal(42, client.Ping);
        Assert.Same(proxy.Object, client.Socket);
        Assert.Equal("/videoHub", client.Endpoint);
    }

    [Fact]
    public void Client_InheritsDeviceIdentityFields()
    {
        Client client = new() { DeviceId = "device-123", Name = "Living Room TV" };

        Assert.Equal("device-123", client.DeviceId);
        Assert.Equal("Living Room TV", client.Name);
    }

    [Fact]
    public void ClientRequest_DefaultFields_AreEmptyStrings()
    {
        ClientRequest request = new();

        Assert.Equal(string.Empty, request.Id);
        Assert.Equal(string.Empty, request.Browser);
        Assert.Equal(string.Empty, request.Os);
        Assert.Equal(string.Empty, request.Device);
        Assert.Equal(string.Empty, request.CustomName);
        Assert.Equal(string.Empty, request.Type);
        Assert.Equal(string.Empty, request.Name);
        Assert.Equal(string.Empty, request.Version);
    }

    [Fact]
    public void ClientRequest_AllFieldsSet_RoundTrip()
    {
        ClientRequest request = new()
        {
            Id = "id-1",
            Browser = "Chrome",
            Os = "Windows",
            Device = "Desktop",
            CustomName = "Office PC",
            Type = "web",
            Name = "NoMercy Web",
            Version = "1.2.3",
        };

        Assert.Equal("id-1", request.Id);
        Assert.Equal("Chrome", request.Browser);
        Assert.Equal("Windows", request.Os);
        Assert.Equal("Desktop", request.Device);
        Assert.Equal("Office PC", request.CustomName);
        Assert.Equal("web", request.Type);
        Assert.Equal("NoMercy Web", request.Name);
        Assert.Equal("1.2.3", request.Version);
    }
}
