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

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Api.Hubs;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

// GetDriveState is the only client-invokable method on RipperHub. It requires
// moderator, then resolves the caller's drivePath against the live drive list
// and, when a disc is present, probes it through a per-disc-type IDiscSource.
// These tests build a real RipperHub against the app's actual DI-configured
// MediaContext/UserCache (via NoMercyApiFactory) with IDriveMonitor and
// IDiscSource fully mocked — no real drive, disc, or probe ever runs.
[Trait(name: "Category", value: "Characterization")]
public class RipperHubGetDriveStateTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public RipperHubGetDriveStateTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        // Force the test host to start so UserCache.Current is populated with
        // the seeded DefaultUserId (Owner=true, Manage=true -> moderator) and
        // SecondaryUserId (Allowed=true, Owner=false, Manage=false -> not moderator).
        _factory.CreateClient();
    }

    private RipperHub CreateHub(
        Guid callerUserId,
        Mock<IDriveMonitor> driveMonitor,
        Mock<IDiscSource>? discSource = null
    )
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();

        DefaultHttpContext httpContext = new() { RequestServices = null! };
        httpContext.Request.Path = "/ripperHub";

        DiscSourceFactory discSourceFactory = new(sources: discSource is null ? [] : [discSource.Object]);

        RipperHub hub = new(
            logger: NullLogger<RipperHub>.Instance,
            httpContextAccessor: new HttpContextAccessorStub(httpContext: httpContext),
            contextFactory: contextFactory,
            connectedClients: new ConnectedClients(),
            activityLogger: Mock.Of<IActivityLogger>(),
            driveMonitor: driveMonitor.Object,
            discSourceFactory: discSourceFactory
        );

        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(
                claims: [new(type: ClaimTypes.NameIdentifier, value: callerUserId.ToString())],
                authenticationType: "TestAuth"
            )
        );

        Mock<HubCallerContext> context = new();
        context.Setup(expression: c => c.User).Returns(value: principal);
        context.Setup(expression: c => c.ConnectionId).Returns(value: Guid.NewGuid().ToString());
        context.Setup(expression: c => c.ConnectionAborted).Returns(value: CancellationToken.None);

        hub.Context = context.Object;
        hub.Clients = Mock.Of<IHubCallerClients>();

        return hub;
    }

    private static object? GetProp(object? source, string propertyName) =>
        source?.GetType().GetProperty(name: propertyName)?.GetValue(obj: source);

    [Fact]
    public async Task GetDriveState_ReturnsNull_WhenCallerIsNotModerator()
    {
        Mock<IDriveMonitor> driveMonitor = new();
        driveMonitor
            .Setup(expression: m => m.GetDrives())
            .Returns(value: [new DiscDrive(Path: "D:\\", Label: "Movie Disc", HasDisc: true, DiscType: OpticalDiscType.Dvd)]);

        RipperHub hub = CreateHub(callerUserId: TestAuthHandler.SecondaryUserId, driveMonitor: driveMonitor);

        object? result = await hub.GetDriveState(drivePath: "D:\\");

        result.Should().BeNull();
        // The moderator gate must reject before the drive list is even read.
        driveMonitor.Verify(expression: m => m.GetDrives(), times: Times.Never);
    }

    [Fact]
    public async Task GetDriveState_ReturnsNull_WhenDrivePathMatchesNoDrive()
    {
        Mock<IDriveMonitor> driveMonitor = new();
        driveMonitor
            .Setup(expression: m => m.GetDrives())
            .Returns(value: [new DiscDrive(Path: "D:\\", Label: "Movie Disc", HasDisc: true, DiscType: OpticalDiscType.Dvd)]);
        Mock<IDiscSource> discSource = new();
        discSource.Setup(expression: s => s.Type).Returns(value: OpticalDiscType.Dvd);

        RipperHub hub = CreateHub(callerUserId: TestAuthHandler.DefaultUserId, driveMonitor: driveMonitor, discSource: discSource);

        object? result = await hub.GetDriveState(drivePath: "Z:\\");

        result.Should().BeNull();
        discSource.Verify(
            expression: s => s.ProbeAsync(It.IsAny<DiscDrive>(), It.IsAny<CancellationToken>()),
            times: Times.Never
        );
    }

    [Fact]
    public async Task GetDriveState_EmptyDrivePath_NoDrivesPresent_ReturnsNull_WithoutException()
    {
        Mock<IDriveMonitor> driveMonitor = new();
        driveMonitor.Setup(expression: m => m.GetDrives()).Returns(value: []);

        RipperHub hub = CreateHub(callerUserId: TestAuthHandler.DefaultUserId, driveMonitor: driveMonitor);

        object? result = await hub.GetDriveState(drivePath: "");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDriveState_WhitespaceDrivePath_MatchesNoDrive_ReturnsNull()
    {
        Mock<IDriveMonitor> driveMonitor = new();
        driveMonitor
            .Setup(expression: m => m.GetDrives())
            .Returns(value: [new DiscDrive(Path: "D:\\", Label: "Empty Tray", HasDisc: false, DiscType: OpticalDiscType.None)]);

        RipperHub hub = CreateHub(callerUserId: TestAuthHandler.DefaultUserId, driveMonitor: driveMonitor);

        object? result = await hub.GetDriveState(drivePath: "   ");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDriveState_NullDrivePath_NoDrivesPresent_ReturnsNull_WithoutException()
    {
        Mock<IDriveMonitor> driveMonitor = new();
        driveMonitor.Setup(expression: m => m.GetDrives()).Returns(value: []);

        RipperHub hub = CreateHub(callerUserId: TestAuthHandler.DefaultUserId, driveMonitor: driveMonitor);

        object? result = await hub.GetDriveState(drivePath: null!);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDriveState_NullDrivePath_WithDrivesPresent_ReturnsNull()
    {
        // A null/blank drivePath is guarded before the TrimEnd inside the matching
        // predicate. Previously this threw NullReferenceException the moment a drive
        // was attached (the empty-drive-list case was safe only because
        // FirstOrDefault skips the predicate on an empty sequence); it now returns
        // null gracefully instead of surfacing an unhandled exception.
        Mock<IDriveMonitor> driveMonitor = new();
        driveMonitor
            .Setup(expression: m => m.GetDrives())
            .Returns(value: [new DiscDrive(Path: "D:\\", Label: "Empty Tray", HasDisc: false, DiscType: OpticalDiscType.None)]);

        RipperHub hub = CreateHub(callerUserId: TestAuthHandler.DefaultUserId, driveMonitor: driveMonitor);

        object? result = await hub.GetDriveState(drivePath: null!);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDriveState_KnownDriveWithoutDisc_ReturnsDriveInfo_WithoutProbingSource()
    {
        Mock<IDriveMonitor> driveMonitor = new();
        driveMonitor
            .Setup(expression: m => m.GetDrives())
            .Returns(value: [new DiscDrive(Path: "D:\\", Label: "Empty Tray", HasDisc: false, DiscType: OpticalDiscType.None)]);
        Mock<IDiscSource> discSource = new();
        discSource.Setup(expression: s => s.Type).Returns(value: OpticalDiscType.None);

        RipperHub hub = CreateHub(callerUserId: TestAuthHandler.DefaultUserId, driveMonitor: driveMonitor, discSource: discSource);

        object? result = await hub.GetDriveState(drivePath: "D:\\");

        result.Should().NotBeNull();
        GetProp(source: result, propertyName: "has_disc").Should().Be(expected: false);
        GetProp(source: result, propertyName: "open").Should().Be(expected: true);
        GetProp(source: result, propertyName: "disc_type").Should().Be(expected: "none");
        discSource.Verify(
            expression: s => s.ProbeAsync(It.IsAny<DiscDrive>(), It.IsAny<CancellationToken>()),
            times: Times.Never
        );
    }

    [Fact]
    public async Task GetDriveState_KnownDriveWithDisc_NoRegisteredSource_ReturnsDriveInfoWithoutProbe()
    {
        Mock<IDriveMonitor> driveMonitor = new();
        driveMonitor
            .Setup(expression: m => m.GetDrives())
            .Returns(value: [new DiscDrive(Path: "D:\\", Label: "Mystery Disc", HasDisc: true, DiscType: OpticalDiscType.Cd)]);

        // No IDiscSource registered for OpticalDiscType.Cd -> DiscSourceFactory.CreateFor
        // returns null and the probe branch must never be reached.
        RipperHub hub = CreateHub(callerUserId: TestAuthHandler.DefaultUserId, driveMonitor: driveMonitor);

        object? result = await hub.GetDriveState(drivePath: "D:\\");

        result.Should().NotBeNull();
        GetProp(source: result, propertyName: "has_disc").Should().Be(expected: true);
        GetProp(source: result, propertyName: "open").Should().Be(expected: false);
        GetProp(source: result, propertyName: "disc_type").Should().Be(expected: "cd");
        GetProp(source: result, propertyName: "label").Should().Be(expected: "Mystery Disc");
    }

    [Fact]
    public async Task GetDriveState_KnownDriveWithDisc_RegisteredSource_ReturnsProbedDiscInfo()
    {
        DiscDrive drive = new(Path: "D:\\", Label: "Fallback Label", HasDisc: true, DiscType: OpticalDiscType.Dvd);
        DiscInfo discInfo = new(
            Type: OpticalDiscType.Dvd,
            DiscLabel: "DISC_LABEL",
            Titles: [],
            AudioTracks: null,
            TotalDuration: TimeSpan.FromMinutes(minutes: 90),
            DiscTitle: "The Movie"
        );

        Mock<IDriveMonitor> driveMonitor = new();
        driveMonitor.Setup(expression: m => m.GetDrives()).Returns(value: [drive]);

        Mock<IDiscSource> discSource = new();
        discSource.Setup(expression: s => s.Type).Returns(value: OpticalDiscType.Dvd);
        discSource
            .Setup(expression: s => s.ProbeAsync(drive, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: discInfo);

        RipperHub hub = CreateHub(callerUserId: TestAuthHandler.DefaultUserId, driveMonitor: driveMonitor, discSource: discSource);

        object? result = await hub.GetDriveState(drivePath: "D:\\");

        result.Should().NotBeNull();
        GetProp(source: result, propertyName: "path").Should().Be(expected: "D:");
        GetProp(source: result, propertyName: "label").Should().Be(expected: "The Movie");
        GetProp(source: result, propertyName: "has_disc").Should().Be(expected: true);
        GetProp(source: result, propertyName: "open").Should().Be(expected: false);
        GetProp(source: result, propertyName: "disc_type").Should().Be(expected: "dvd");
        discSource.Verify(expression: s => s.ProbeAsync(drive, It.IsAny<CancellationToken>()), times: Times.Once);
    }

    [Fact]
    public async Task GetDriveState_ProbeThrows_ReturnsFallbackDriveInfo_WithoutDiscField()
    {
        DiscDrive drive = new(Path: "D:\\", Label: "Fallback Label", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        Mock<IDriveMonitor> driveMonitor = new();
        driveMonitor.Setup(expression: m => m.GetDrives()).Returns(value: [drive]);

        Mock<IDiscSource> discSource = new();
        discSource.Setup(expression: s => s.Type).Returns(value: OpticalDiscType.BluRay);
        discSource
            .Setup(expression: s => s.ProbeAsync(drive, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: new InvalidOperationException(message: "drive busy"));

        RipperHub hub = CreateHub(callerUserId: TestAuthHandler.DefaultUserId, driveMonitor: driveMonitor, discSource: discSource);

        object? result = await hub.GetDriveState(drivePath: "D:\\");

        result.Should().NotBeNull();
        GetProp(source: result, propertyName: "label").Should().Be(expected: "Fallback Label");
        GetProp(source: result, propertyName: "has_disc").Should().Be(expected: true);
        GetProp(source: result, propertyName: "open").Should().Be(expected: false);
        GetProp(source: result, propertyName: "disc_type").Should().Be(expected: "bluray");
        // The success-only "disc" field must be absent from the exception fallback
        // shape, distinguishing it from the probed-success envelope.
        GetProp(source: result, propertyName: "disc").Should().BeNull();
    }

    // Minimal IHttpContextAccessor stand-in — the real implementation is an
    // AsyncLocal-backed singleton unsuited to constructing an isolated
    // HttpContext per test.
    private sealed class HttpContextAccessorStub(HttpContext httpContext) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }
}
