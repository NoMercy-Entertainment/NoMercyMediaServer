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
using NoMercy.OpticalMedia.Sources;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// Unit-style tests for PlayMedia and StopMedia. The full app is booted via
/// the shared <see cref="NoMercyApiFactory"/> and the optical/live services are
/// swapped with mocks per test class via
/// <see cref="WebApplicationFactory.WithWebHostBuilder"/>.
/// </summary>
[Trait("Category", "OpticalMedia")]
public class OpticalMediaControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public OpticalMediaControllerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static DiscDrive MakeDrive(string path, bool hasDisc = true) =>
        new(path, "TEST DISC", hasDisc, OpticalDiscType.BluRay);

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
        sessionMock.Setup(s => s.SessionId).Returns(sessionId);
        sessionMock.Setup(s => s.CurrentQuality).Returns(MakeQuality());
        sessionMock.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);
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
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDriveMonitor>();
                    services.AddSingleton(driveMonitorMock.Object);

                    services.RemoveAll<ILiveDiscSession>();
                    services.AddTransient(_ => liveDiscSessionMock.Object);

                    services.RemoveAll<ILiveStreamingService>();
                    services.AddSingleton(liveStreamingServiceMock.Object);

                    services.RemoveAll<ISessionManager>();
                    services.AddSingleton(sessionManagerMock.Object);

                    services.RemoveAll<IDiscSessionRegistry>();
                    services.AddSingleton(discSessionRegistryMock.Object);
                });
            })
            .CreateClient()
            .AsAuthenticated();
    }

    // ── PlayMedia — happy path ─────────────────────────────────────────────

    [Fact]
    public async Task PlayMedia_ValidDriveAndTitle_ReturnsOkWithSessionAndPlaylistUrl()
    {
        Mock<ILiveSession> sessionMock = MakeSession("sess-abc");

        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(m => m.GetDrives()).Returns([MakeDrive(@"D:\")]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        liveDiscSessionMock
            .Setup(s =>
                s.StartAsync(
                    It.IsAny<DiscDrive>(),
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<string?>(),
                    It.IsAny<AudioTrackSelection[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(sessionMock.Object);

        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        Mock<ISessionManager> sessionManagerMock = new();
        sessionManagerMock.Setup(m => m.CanStartSession(It.IsAny<string>())).Returns(true);

        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();

        HttpClient client = BuildClient(
            driveMonitorMock,
            liveDiscSessionMock,
            liveStreamingServiceMock,
            sessionManagerMock,
            discSessionRegistryMock
        );

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/dashboard/optical/D%3A%5C/play/0",
            null
        );

        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        JsonDocument json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("session_id").GetString().Should().Be("sess-abc");
        json.RootElement.GetProperty("playlist_url")
            .GetString()
            .Should()
            .Contain("/api/v1/streaming/live/sessions/sess-abc/playlist.m3u8");
    }

    // ── PlayMedia — session registered with manager and registry ──────────

    [Fact]
    public async Task PlayMedia_ValidDriveAndTitle_RegistersSessionWithManagerAndRegistry()
    {
        Mock<ILiveSession> sessionMock = MakeSession("sess-xyz");

        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(m => m.GetDrives()).Returns([MakeDrive(@"D:\")]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        liveDiscSessionMock
            .Setup(s =>
                s.StartAsync(
                    It.IsAny<DiscDrive>(),
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<string?>(),
                    It.IsAny<AudioTrackSelection[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(sessionMock.Object);

        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        Mock<ISessionManager> sessionManagerMock = new();
        sessionManagerMock.Setup(m => m.CanStartSession(It.IsAny<string>())).Returns(true);

        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();

        HttpClient client = BuildClient(
            driveMonitorMock,
            liveDiscSessionMock,
            liveStreamingServiceMock,
            sessionManagerMock,
            discSessionRegistryMock
        );

        await client.PostAsync("/api/v1/dashboard/optical/D%3A%5C/play/0", null);

        sessionManagerMock.Verify(
            m => m.RegisterSession(sessionMock.Object, It.IsAny<string>()),
            Times.Once
        );
        discSessionRegistryMock.Verify(m => m.Register(It.IsAny<string>(), "sess-xyz"), Times.Once);
    }

    // ── PlayMedia — drive not found ────────────────────────────────────────

    [Fact]
    public async Task PlayMedia_DriveNotFound_Returns404()
    {
        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(m => m.GetDrives()).Returns([]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        Mock<ISessionManager> sessionManagerMock = new();
        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();

        HttpClient client = BuildClient(
            driveMonitorMock,
            liveDiscSessionMock,
            liveStreamingServiceMock,
            sessionManagerMock,
            discSessionRegistryMock
        );

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/dashboard/optical/D%3A%5C/play/0",
            null
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PlayMedia — drive has no disc ──────────────────────────────────────

    [Fact]
    public async Task PlayMedia_NoDisc_Returns400()
    {
        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(m => m.GetDrives()).Returns([MakeDrive(@"D:\", hasDisc: false)]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        Mock<ISessionManager> sessionManagerMock = new();
        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();

        HttpClient client = BuildClient(
            driveMonitorMock,
            liveDiscSessionMock,
            liveStreamingServiceMock,
            sessionManagerMock,
            discSessionRegistryMock
        );

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/dashboard/optical/D%3A%5C/play/0",
            null
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PlayMedia — invalid title index ───────────────────────────────────

    [Fact]
    public async Task PlayMedia_NonIntegerPlaylistId_Returns400()
    {
        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(m => m.GetDrives()).Returns([MakeDrive(@"D:\")]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        Mock<ISessionManager> sessionManagerMock = new();
        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();

        HttpClient client = BuildClient(
            driveMonitorMock,
            liveDiscSessionMock,
            liveStreamingServiceMock,
            sessionManagerMock,
            discSessionRegistryMock
        );

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/dashboard/optical/D%3A%5C/play/notanumber",
            null
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── StopMedia — happy path ─────────────────────────────────────────────

    [Fact]
    public async Task StopMedia_ActiveSession_Returns204AndCleansUp()
    {
        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(m => m.GetDrives()).Returns([MakeDrive(@"D:\")]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        liveStreamingServiceMock
            .Setup(s => s.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        Mock<ISessionManager> sessionManagerMock = new();

        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();
        string capturedSessionId = "sess-stop";
        discSessionRegistryMock
            .Setup(r => r.TryGet(It.IsAny<string>(), out capturedSessionId))
            .Returns(true);

        HttpClient client = BuildClient(
            driveMonitorMock,
            liveDiscSessionMock,
            liveStreamingServiceMock,
            sessionManagerMock,
            discSessionRegistryMock
        );

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/dashboard/optical/D%3A%5C/stop",
            null
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        liveStreamingServiceMock.Verify(s => s.RemoveAsync("sess-stop"), Times.Once);
        sessionManagerMock.Verify(m => m.RemoveSession("sess-stop"), Times.Once);
        discSessionRegistryMock.Verify(r => r.Remove(It.IsAny<string>()), Times.Once);
    }

    // ── StopMedia — no active session ─────────────────────────────────────

    [Fact]
    public async Task StopMedia_NoActiveSession_Returns404()
    {
        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(m => m.GetDrives()).Returns([MakeDrive(@"D:\")]);

        Mock<ILiveDiscSession> liveDiscSessionMock = new();
        Mock<ILiveStreamingService> liveStreamingServiceMock = new();
        Mock<ISessionManager> sessionManagerMock = new();

        Mock<IDiscSessionRegistry> discSessionRegistryMock = new();
        string noSession = string.Empty;
        discSessionRegistryMock
            .Setup(r => r.TryGet(It.IsAny<string>(), out noSession))
            .Returns(false);

        HttpClient client = BuildClient(
            driveMonitorMock,
            liveDiscSessionMock,
            liveStreamingServiceMock,
            sessionManagerMock,
            discSessionRegistryMock
        );

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/dashboard/optical/D%3A%5C/stop",
            null
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
