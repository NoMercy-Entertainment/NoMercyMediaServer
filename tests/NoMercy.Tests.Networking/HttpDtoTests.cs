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
[Trait(name: "Category", value: "Unit")]
public sealed class HttpDtoTests
{
    [Fact]
    public void Client_DefaultEndpoint_IsEmptyString()
    {
        Client client = new();

        Assert.Equal(expected: string.Empty, actual: client.Endpoint);
    }

    [Fact]
    public void Client_DefaultPing_IsNull()
    {
        Client client = new();

        Assert.Null(value: client.Ping);
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

        Assert.Equal(expected: sub, actual: client.Sub);
        Assert.Equal(expected: 42, actual: client.Ping);
        Assert.Same(expected: proxy.Object, actual: client.Socket);
        Assert.Equal(expected: "/videoHub", actual: client.Endpoint);
    }

    [Fact]
    public void Client_InheritsDeviceIdentityFields()
    {
        Client client = new() { DeviceId = "device-123", Name = "Living Room TV" };

        Assert.Equal(expected: "device-123", actual: client.DeviceId);
        Assert.Equal(expected: "Living Room TV", actual: client.Name);
    }

    [Fact]
    public void ClientRequest_DefaultFields_AreEmptyStrings()
    {
        ClientRequest request = new();

        Assert.Equal(expected: string.Empty, actual: request.Id);
        Assert.Equal(expected: string.Empty, actual: request.Browser);
        Assert.Equal(expected: string.Empty, actual: request.Os);
        Assert.Equal(expected: string.Empty, actual: request.Device);
        Assert.Equal(expected: string.Empty, actual: request.CustomName);
        Assert.Equal(expected: string.Empty, actual: request.Type);
        Assert.Equal(expected: string.Empty, actual: request.Name);
        Assert.Equal(expected: string.Empty, actual: request.Version);
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

        Assert.Equal(expected: "id-1", actual: request.Id);
        Assert.Equal(expected: "Chrome", actual: request.Browser);
        Assert.Equal(expected: "Windows", actual: request.Os);
        Assert.Equal(expected: "Desktop", actual: request.Device);
        Assert.Equal(expected: "Office PC", actual: request.CustomName);
        Assert.Equal(expected: "web", actual: request.Type);
        Assert.Equal(expected: "NoMercy Web", actual: request.Name);
        Assert.Equal(expected: "1.2.3", actual: request.Version);
    }
}
