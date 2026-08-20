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
using NoMercy.Encoder.Analysis;
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

    private static DiscDrive MakeDrive(
        string path,
        bool hasDisc = true,
        OpticalDiscType discType = OpticalDiscType.BluRay
    ) => new(path, "TEST DISC", hasDisc, discType);

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

    // ── ProbeTitle — real chapters, honest naming, duration-ranked kind ──

    private HttpClient BuildProbeClient(
        Mock<IDriveMonitor> driveMonitorMock,
        Mock<IDiscSource> discSourceMock
    )
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDriveMonitor>();
                    services.AddSingleton(driveMonitorMock.Object);

                    services.RemoveAll<IDiscSource>();
                    services.AddSingleton(discSourceMock.Object);
                    services.RemoveAll<DiscSourceFactory>();
                    services.AddSingleton(new DiscSourceFactory([discSourceMock.Object]));
                });
            })
            .CreateClient()
            .AsAuthenticated();
    }

    private static DiscTitle MakeTitle(
        int index,
        TimeSpan duration,
        ChapterInfo[]? chapters = null
    ) =>
        new(
            Index: index,
            Name: $"Title {index}",
            Duration: duration,
            VideoStreams: [],
            AudioStreams: [],
            Subtitles: [],
            Chapters: chapters ?? [],
            EstimatedSizeBytes: 0,
            IsMainFeature: false
        );

    [Fact]
    public async Task ProbeTitle_ReturnsRealChapterMarksFromDisc()
    {
        ChapterInfo[] marks =
        [
            new(TimeSpan.Zero, TimeSpan.FromSeconds(620), "Chapter 1"),
            new(TimeSpan.FromSeconds(620), TimeSpan.FromSeconds(1310), "Chapter 2"),
            new(TimeSpan.FromSeconds(1310), TimeSpan.FromSeconds(2000), "Chapter 3"),
        ];
        DiscTitle title = MakeTitle(0, TimeSpan.FromSeconds(2000), marks);

        // DVD (not Blu-ray): the chapters helper skips the .mpls
        // disc-content-catalog lookup for non-Blu-ray discs and wires the
        // per-title probe's own real chapter marks directly — exercising
        // that path here avoids needing a real .mpls fixture on disk while
        // still proving the real chapter data reaches the response.
        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock
            .Setup(m => m.GetDrives())
            .Returns([MakeDrive(@"D:\", discType: OpticalDiscType.Dvd)]);

        Mock<IDiscSource> discSourceMock = new();
        discSourceMock.Setup(s => s.Type).Returns(OpticalDiscType.Dvd);
        discSourceMock
            .Setup(s => s.ProbeTitleAsync(It.IsAny<DiscDrive>(), 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(title);
        discSourceMock
            .Setup(s => s.ProbeAsync(It.IsAny<DiscDrive>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscInfo(OpticalDiscType.Dvd, "TEST", [title], null, title.Duration));

        HttpClient client = BuildProbeClient(driveMonitorMock, discSourceMock);

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/dashboard/optical/D%3A%5C/title/0"
        );
        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        JsonDocument json = JsonDocument.Parse(body);

        // The pre-existing `chapters` key keeps its old {number,timestamp,title}
        // shape unchanged — nomercy-app-web reads it today, so this endpoint
        // must not reshape or drop it (compat-gate BREAKS-OLD-CLIENTS finding).
        JsonElement oldChapters = json.RootElement.GetProperty("chapters");
        oldChapters.GetArrayLength().Should().Be(3);
        oldChapters[1].GetProperty("title").GetString().Should().Be("Chapter 2");

        JsonElement chapterMarks = json.RootElement.GetProperty("chapter_marks");
        chapterMarks.GetArrayLength().Should().Be(3);
        chapterMarks[1].GetProperty("time_seconds").GetDouble().Should().Be(620);
    }

    [Fact]
    public async Task ProbeTitle_NameUnchanged_KindIsDurationBased()
    {
        DiscTitle mainFeature = MakeTitle(0, TimeSpan.FromSeconds(7200));
        DiscTitle extra = MakeTitle(1, TimeSpan.FromSeconds(1200));
        DiscInfo discInfo = new(
            OpticalDiscType.Dvd,
            "TEST",
            [mainFeature, extra, MakeTitle(2, TimeSpan.FromSeconds(600))],
            null,
            TimeSpan.FromSeconds(9000)
        );

        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock
            .Setup(m => m.GetDrives())
            .Returns([MakeDrive(@"D:\", discType: OpticalDiscType.Dvd)]);

        Mock<IDiscSource> discSourceMock = new();
        discSourceMock.Setup(s => s.Type).Returns(OpticalDiscType.Dvd);
        discSourceMock
            .Setup(s => s.ProbeAsync(It.IsAny<DiscDrive>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discInfo);
        discSourceMock
            .Setup(s => s.ProbeTitleAsync(It.IsAny<DiscDrive>(), 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mainFeature);
        discSourceMock
            .Setup(s => s.ProbeTitleAsync(It.IsAny<DiscDrive>(), 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(extra);

        HttpClient client = BuildProbeClient(driveMonitorMock, discSourceMock);

        HttpResponseMessage mainResponse = await client.GetAsync(
            "/api/v1/dashboard/optical/D%3A%5C/title/0"
        );
        HttpResponseMessage extraResponse = await client.GetAsync(
            "/api/v1/dashboard/optical/D%3A%5C/title/1"
        );

        string mainBody = await mainResponse.Content.ReadAsStringAsync();
        string extraBody = await extraResponse.Content.ReadAsStringAsync();
        mainResponse.StatusCode.Should().Be(HttpStatusCode.OK, mainBody);
        extraResponse.StatusCode.Should().Be(HttpStatusCode.OK, extraBody);

        JsonDocument mainJson = JsonDocument.Parse(mainBody);
        JsonDocument extraJson = JsonDocument.Parse(extraBody);

        // `name` keeps returning the disc title's existing value unchanged —
        // nomercy-app-web's DiscTitleInfo type reads this key today.
        mainJson.RootElement.GetProperty("name").GetString().Should().Be(mainFeature.Name);
        mainJson.RootElement.GetProperty("kind").GetString().Should().Be("main_feature");
        extraJson.RootElement.GetProperty("name").GetString().Should().Be(extra.Name);
        extraJson.RootElement.GetProperty("kind").GetString().Should().Be("extra");
    }

    // ── ProbeDisc — disc identity surfaced, best-effort ────────────────────

    [Fact]
    public async Task ProbeDisc_IncludesDiscIdentityField()
    {
        DiscTitle title = MakeTitle(0, TimeSpan.FromSeconds(3600));
        DiscInfo discInfo = new(
            OpticalDiscType.BluRay,
            "TEST",
            [title],
            null,
            title.Duration
        );

        Mock<IDriveMonitor> driveMonitorMock = new();
        driveMonitorMock.Setup(m => m.GetDrives()).Returns([MakeDrive(@"D:\")]);

        Mock<IDiscSource> discSourceMock = new();
        discSourceMock.Setup(s => s.Type).Returns(OpticalDiscType.BluRay);
        discSourceMock
            .Setup(s => s.ProbeAsync(It.IsAny<DiscDrive>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discInfo);

        HttpClient client = BuildProbeClient(driveMonitorMock, discSourceMock);

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/dashboard/optical/D%3A%5C/probe"
        );
        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        JsonDocument json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("disc_identity", out JsonElement discIdentity)
            .Should()
            .BeTrue();
        // No real optical drive is present in the test host, so identity
        // resolution fails and degrades to null rather than throwing —
        // the field's presence is what this test proves.
        discIdentity.ValueKind.Should().Be(JsonValueKind.Null);
    }
}
