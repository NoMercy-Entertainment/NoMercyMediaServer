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
using System.Net.Sockets;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Networking.Certificate;
using NoMercy.Service.Hosting;
using Xunit;

namespace NoMercy.Tests.Service;

/// <summary>
/// Port selection and blocking-process identification are the boot step that
/// used to cascade into "Failed to start database job workers". These lock the
/// deterministic pieces: the availability probe, the next-free scan, and the
/// netstat/lsof PID parsers (tested directly so both OS formats are covered on
/// every host, not just the one the tests happen to run on).
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class PortManagerTests
{
    private static PortManager BuildManager() =>
        new(logger: NullLogger<PortManager>.Instance, certificateService: new StubCertificateService());

    private static int GetFreePort()
    {
        TcpListener probe = new(localaddr: IPAddress.Loopback, port: 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [Fact]
    public void IsPortAvailable_FreePort_ReturnsTrue()
    {
        PortManager manager = BuildManager();

        bool available = manager.IsPortAvailable(port: GetFreePort());

        Assert.True(condition: available);
    }

    [Fact]
    public void IsPortAvailable_OccupiedPort_ReturnsFalse()
    {
        int port = GetFreePort();
        TcpListener holder = new(localaddr: IPAddress.Any, port: port);
        holder.Start();
        try
        {
            PortManager manager = BuildManager();

            bool available = manager.IsPortAvailable(port: port);

            Assert.False(condition: available);
        }
        finally
        {
            holder.Stop();
        }
    }

    [Fact]
    public void FindNextAvailablePort_StartFree_ReturnsStartPort()
    {
        int port = GetFreePort();
        PortManager manager = BuildManager();

        int found = manager.FindNextAvailablePort(startPort: port);

        Assert.Equal(expected: port, actual: found);
    }

    [Fact]
    public void FindNextAvailablePort_StartOccupied_ReturnsHigherFreePort()
    {
        int port = GetFreePort();
        TcpListener holder = new(localaddr: IPAddress.Any, port: port);
        holder.Start();
        try
        {
            PortManager manager = BuildManager();

            int found = manager.FindNextAvailablePort(startPort: port);

            Assert.True(condition: found > port, userMessage: $"expected a port past the occupied {port}, got {found}");
            Assert.True(condition: manager.IsPortAvailable(port: found));
        }
        finally
        {
            holder.Stop();
        }
    }

    [Theory]
    [InlineData(data: ["  TCP    0.0.0.0:7626    0.0.0.0:0    LISTENING    1234", 1234])]
    [InlineData(data: ["TCP    [::]:7626    [::]:0    LISTENING    98765\r\n", 98765])]
    public void ParsePidFromNetstat_ValidListeningRow_ReturnsPid(string netstat, int expected)
    {
        Assert.Equal(expected: expected, actual: PortManager.ParsePidFromNetstat(processInfo: netstat));
    }

    [Theory]
    [InlineData(data: "")]
    [InlineData(data: "   ")]
    [InlineData(data: "TCP    0.0.0.0:7626    0.0.0.0:0    LISTENING    not-a-pid")]
    public void ParsePidFromNetstat_EmptyOrMalformed_ReturnsMinusOne(string netstat)
    {
        Assert.Equal(expected: -1, actual: PortManager.ParsePidFromNetstat(processInfo: netstat));
    }

    [Fact]
    public void ParsePidFromLsof_ValidDataRow_ReturnsPid()
    {
        const string lsof =
            "COMMAND   PID   USER   FD   TYPE   DEVICE   SIZE/OFF   NODE   NAME\n"
            + "NoMercyMe 4321  nomercy  10u  IPv4  0x0        0t0        TCP    *:7626 (LISTEN)\n";

        Assert.Equal(expected: 4321, actual: PortManager.ParsePidFromLsof(processInfo: lsof));
    }

    [Theory]
    [InlineData(data: "")]
    [InlineData(data: "COMMAND   PID   USER   FD   TYPE   DEVICE   SIZE/OFF   NODE   NAME\n")]
    public void ParsePidFromLsof_EmptyOrHeaderOnly_ReturnsMinusOne(string lsof)
    {
        Assert.Equal(expected: -1, actual: PortManager.ParsePidFromLsof(processInfo: lsof));
    }

    [Fact]
    public async Task EnsurePortAvailable_PortAlreadyFree_ReturnsImmediatelyWithoutConsultingCertService()
    {
        int port = GetFreePort();
        Mock<ICertificateService> certificateService = new(behavior: MockBehavior.Strict);
        PortManager manager = new(logger: NullLogger<PortManager>.Instance, certificateService: certificateService.Object);

        await manager.EnsurePortAvailable(port: port);

        // Strict mock: any unexpected call (e.g. HasValidCertificate) would have
        // thrown above. Reaching here proves the early-return path never
        // touches the certificate service at all.
        certificateService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandlePortInUse_InnerExceptionIsNotSocketException_ReturnsFalseWithoutRecovering()
    {
        PortManager manager = BuildManager();
        IOException ex = new(message: "disk full", innerException: new InvalidOperationException(message: "not a socket error"));

        bool shouldRetry = await manager.HandlePortInUse(port: 7626, ex: ex);

        Assert.False(condition: shouldRetry);
    }

    [Fact]
    public async Task HandlePortInUse_SocketErrorIsNotAddressInUse_ReturnsFalseWithoutRecovering()
    {
        PortManager manager = BuildManager();
        SocketException socketEx = new(errorCode: (int)SocketError.ConnectionRefused);
        IOException ex = new(message: "connection refused", innerException: socketEx);

        bool shouldRetry = await manager.HandlePortInUse(port: 7626, ex: ex);

        Assert.False(condition: shouldRetry);
    }

    [Fact]
    public async Task HandlePortInUse_AddressAlreadyInUseOnAFreePort_RecoversAndReturnsTrue()
    {
        // The exception CLAIMS the port collided, but the port is actually free
        // (a transient bind race) — EnsurePortAvailable's happy path resolves it
        // without touching the certificate service, and HandlePortInUse reports
        // the caller may safely retry.
        int port = GetFreePort();
        PortManager manager = BuildManager();
        SocketException socketEx = new(errorCode: (int)SocketError.AddressAlreadyInUse);
        IOException ex = new(message: "address already in use", innerException: socketEx);

        bool shouldRetry = await manager.HandlePortInUse(port: port, ex: ex);

        Assert.True(condition: shouldRetry);
    }

    private sealed class StubCertificateService : ICertificateService
    {
        public void LoadFromDb() { }

        public bool HasValidCertificate() => false;

        public bool EnsureHttpsCertificate() => false;

        public void KestrelConfig(KestrelServerOptions options) { }

        public void ConfigureHttpsListener(ListenOptions listenOptions) { }

        public Task RenewSslCertificate(string? accessToken, int maxRetries = 30) =>
            Task.CompletedTask;
    }
}
