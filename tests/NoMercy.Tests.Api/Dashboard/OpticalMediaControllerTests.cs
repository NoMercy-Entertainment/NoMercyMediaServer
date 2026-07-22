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
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Live;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// Unit-style tests for PlayMedia and StopMedia. The full app is booted via
/// the shared <see cref="NoMercyApiFactory"/> and the optical/live services are
/// swapped with mocks per test class via
/// <see cref="WebApplicationFactory.WithWebHostBuilder"/>.
/// </summary>
[Trait(name: "Category", value: "OpticalMedia")]
public class OpticalMediaControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public OpticalMediaControllerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static DiscDrive MakeDrive(string path, bool hasDisc = true) =>
        new(Path: path, Label: "TEST DISC", HasDisc: hasDisc, DiscType: OpticalDiscType.BluRay);

    private static LiveQuality MakeQuality() =>
        new(
            Id: "1080p",
            Label: "1080p",
            Width: 1920,
            Height: 1080,
            Codec: VideoCodecType.H264,
            BitrateKbps: 8000,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 1.0,
            CanRealtime: true
        );

    private static Mock<ILiveSession> MakeSession(string sessionId = "test-session-id")
    {
        Mock<ILiveSession> sessionMock = new();
        sessionMock.Setup(expression: s => s.SessionId).Returns(value: sessionId);
        sessionMock.Setup(expression: s => s.CurrentQuality).Returns(value: MakeQuality());
        sessionMock.Setup(expression: s => s.DisposeAsync()).Returns(value: ValueTask.CompletedTask);
        return sessionMock;
    }

    private HttpClient BuildClient(
        Mock<IDriveMonitor> driveMonitorMock,
        Mock<ILiveDiscSession> liveDiscSessionMock,
        Mock<ILiveStreamingService> liveStreamingServiceMock,
        Mock<ISessionManager> sessionManagerMock,
        Mock<IDiscSessionRegistry> discSessionRegistryMock
    )
    {
        return _factory
            .WithWebHostBuilder(configuration: builder =>
            {
                builder.ConfigureTestServices(servicesConfiguration: services =>
                {
                    services.RemoveAll<IDriveMonitor>();
                    services.AddSingleton(implementationInstance: driveMonitorMock.Object);

                    services.RemoveAll<ILiveDiscSession>();
                    services.AddTransient(implementationFactory: _ => liveDiscSessionMock.Object);

                    services.RemoveAll<ILiveStreamingService>();
                    services.AddSingleton(implementationInstance: liveStreamingServiceMock.Object);

                    services.RemoveAll<ISessionManager>();
                    services.AddSingleton(implementationInstance: sessionManagerMock.Object);

                    services.RemoveAll<IDiscSessionRegistry>();
                    services.AddSingleton(implementationInstance: discSessionRegistryMock.Object);
                });
            })
            .CreateClient()
            .AsAuthenticated();
    }

    // ── PlayMedia — happy path ─────────────────────────────────────────────

    [Fact]
    public async Task PlayMedia_ValidDriveAndTitle_ReturnsOkWithSessionAndPlaylistUrl()
    {
        Mock<ILiveSession> sessionMock = MakeSession(sessionId: "sess-abc");

        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(expression: m => m.GetDrives()).Returns(value: [MakeDrive(path: @"D:\")]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        liveDiscSessionMock
            .Setup(expression: s =>
                s.StartAsync(
                    It.IsAny<DiscDrive>(),
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: sessionMock.Object);

        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        Mock<ISessionManager> sessionManagerMock = new();
        sessionManagerMock.Setup(expression: m => m.CanStartSession(It.IsAny<string>())).Returns(value: true);

        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();

        HttpClient client = BuildClient(
            driveMonitorMock: driveMonitorMock,
            liveDiscSessionMock: liveDiscSessionMock,
            liveStreamingServiceMock: liveStreamingServiceMock,
            sessionManagerMock: sessionManagerMock,
            discSessionRegistryMock: discSessionRegistryMock
        );

        HttpResponseMessage response = await client.PostAsync(
            requestUri: "/api/v1/dashboard/optical/D%3A%5C/play/0",
            content: null
        );

        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK, because: body);

        JsonDocument json = JsonDocument.Parse(json: body);
        json.RootElement.GetProperty(propertyName: "session_id").GetString().Should().Be(expected: "sess-abc");
        json.RootElement.GetProperty(propertyName: "playlist_url")
            .GetString()
            .Should()
            .Contain(expected: "/api/v1/streaming/live/sessions/sess-abc/playlist.m3u8");
    }

    // ── PlayMedia — session registered with manager and registry ──────────

    [Fact]
    public async Task PlayMedia_ValidDriveAndTitle_RegistersSessionWithManagerAndRegistry()
    {
        Mock<ILiveSession> sessionMock = MakeSession(sessionId: "sess-xyz");

        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(expression: m => m.GetDrives()).Returns(value: [MakeDrive(path: @"D:\")]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        liveDiscSessionMock
            .Setup(expression: s =>
                s.StartAsync(
                    It.IsAny<DiscDrive>(),
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: sessionMock.Object);

        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        Mock<ISessionManager> sessionManagerMock = new();
        sessionManagerMock.Setup(expression: m => m.CanStartSession(It.IsAny<string>())).Returns(value: true);

        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();

        HttpClient client = BuildClient(
            driveMonitorMock: driveMonitorMock,
            liveDiscSessionMock: liveDiscSessionMock,
            liveStreamingServiceMock: liveStreamingServiceMock,
            sessionManagerMock: sessionManagerMock,
            discSessionRegistryMock: discSessionRegistryMock
        );

        await client.PostAsync(requestUri: "/api/v1/dashboard/optical/D%3A%5C/play/0", content: null);

        sessionManagerMock.Verify(
            expression: m => m.RegisterSession(sessionMock.Object, It.IsAny<string>()),
            times: Times.Once
        );
        discSessionRegistryMock.Verify(expression: m => m.Register(It.IsAny<string>(), "sess-xyz"), times: Times.Once);
    }

    // ── PlayMedia — drive not found ────────────────────────────────────────

    [Fact]
    public async Task PlayMedia_DriveNotFound_Returns404()
    {
        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(expression: m => m.GetDrives()).Returns(value: []);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        Mock<ISessionManager> sessionManagerMock = new();
        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();

        HttpClient client = BuildClient(
            driveMonitorMock: driveMonitorMock,
            liveDiscSessionMock: liveDiscSessionMock,
            liveStreamingServiceMock: liveStreamingServiceMock,
            sessionManagerMock: sessionManagerMock,
            discSessionRegistryMock: discSessionRegistryMock
        );

        HttpResponseMessage response = await client.PostAsync(
            requestUri: "/api/v1/dashboard/optical/D%3A%5C/play/0",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    // ── PlayMedia — drive has no disc ──────────────────────────────────────

    [Fact]
    public async Task PlayMedia_NoDisc_Returns400()
    {
        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(expression: m => m.GetDrives()).Returns(value: [MakeDrive(path: @"D:\", hasDisc: false)]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        Mock<ISessionManager> sessionManagerMock = new();
        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();

        HttpClient client = BuildClient(
            driveMonitorMock: driveMonitorMock,
            liveDiscSessionMock: liveDiscSessionMock,
            liveStreamingServiceMock: liveStreamingServiceMock,
            sessionManagerMock: sessionManagerMock,
            discSessionRegistryMock: discSessionRegistryMock
        );

        HttpResponseMessage response = await client.PostAsync(
            requestUri: "/api/v1/dashboard/optical/D%3A%5C/play/0",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    // ── PlayMedia — invalid title index ───────────────────────────────────

    [Fact]
    public async Task PlayMedia_NonIntegerPlaylistId_Returns400()
    {
        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(expression: m => m.GetDrives()).Returns(value: [MakeDrive(path: @"D:\")]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        Mock<ISessionManager> sessionManagerMock = new();
        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();

        HttpClient client = BuildClient(
            driveMonitorMock: driveMonitorMock,
            liveDiscSessionMock: liveDiscSessionMock,
            liveStreamingServiceMock: liveStreamingServiceMock,
            sessionManagerMock: sessionManagerMock,
            discSessionRegistryMock: discSessionRegistryMock
        );

        HttpResponseMessage response = await client.PostAsync(
            requestUri: "/api/v1/dashboard/optical/D%3A%5C/play/notanumber",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    // ── StopMedia — happy path ─────────────────────────────────────────────

    [Fact]
    public async Task StopMedia_ActiveSession_Returns204AndCleansUp()
    {
        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(expression: m => m.GetDrives()).Returns(value: [MakeDrive(path: @"D:\")]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        liveStreamingServiceMock
            .Setup(expression: s => s.RemoveAsync(It.IsAny<string>()))
            .Returns(value: Task.CompletedTask);

        Mock<ISessionManager> sessionManagerMock = new();

        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();
        string capturedSessionId = "sess-stop";
        discSessionRegistryMock
            .Setup(expression: r => r.TryGet(It.IsAny<string>(), out capturedSessionId))
            .Returns(value: true);

        HttpClient client = BuildClient(
            driveMonitorMock: driveMonitorMock,
            liveDiscSessionMock: liveDiscSessionMock,
            liveStreamingServiceMock: liveStreamingServiceMock,
            sessionManagerMock: sessionManagerMock,
            discSessionRegistryMock: discSessionRegistryMock
        );

        HttpResponseMessage response = await client.PostAsync(
            requestUri: "/api/v1/dashboard/optical/D%3A%5C/stop",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NoContent);

        liveStreamingServiceMock.Verify(expression: s => s.RemoveAsync("sess-stop"), times: Times.Once);
        sessionManagerMock.Verify(expression: m => m.RemoveSession("sess-stop"), times: Times.Once);
        discSessionRegistryMock.Verify(expression: r => r.Remove(It.IsAny<string>()), times: Times.Once);
    }

    // ── StopMedia — no active session ─────────────────────────────────────

    [Fact]
    public async Task StopMedia_NoActiveSession_Returns404()
    {
        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(expression: m => m.GetDrives()).Returns(value: [MakeDrive(path: @"D:\")]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        Mock<ISessionManager> sessionManagerMock = new();

        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();
        string noSession = string.Empty;
        discSessionRegistryMock
            .Setup(expression: r => r.TryGet(It.IsAny<string>(), out noSession))
            .Returns(value: false);

        HttpClient client = BuildClient(
            driveMonitorMock: driveMonitorMock,
            liveDiscSessionMock: liveDiscSessionMock,
            liveStreamingServiceMock: liveStreamingServiceMock,
            sessionManagerMock: sessionManagerMock,
            discSessionRegistryMock: discSessionRegistryMock
        );

        HttpResponseMessage response = await client.PostAsync(
            requestUri: "/api/v1/dashboard/optical/D%3A%5C/stop",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }
}
