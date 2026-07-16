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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Api.Hubs;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Networking.Messaging;
using Xunit;

namespace NoMercy.Tests.Api;

// Coverage for the network-aware adaptive step-down: ReportBufferHealth lets a
// live client report its download-buffer depth and observed downlink — a
// NETWORK signal distinct from the encoder-lead BufferAhead the server already
// tracks. These tests build a real LiveTranscodeHub against real ISessionManager
// / ILiveStreamingService mocks and a real LiveSession/LiveRuntimeSession pair,
// mocking only the SignalR plumbing (HubCallerContext) a live connection would
// normally supply — mirrors LiveTranscodeHubReportPlayheadTests.
[Trait("Category", "Unit")]
public class LiveTranscodeHubReportBufferHealthTests
{
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
            ExpectedSpeed: 2.0,
            CanRealtime: true
        );

    private static LiveTranscodeHub CreateHub(
        ISessionManager sessionManager,
        ILiveStreamingService streamingService,
        string callerUserId
    )
    {
        DefaultHttpContext httpContext = new() { RequestServices = null! };
        httpContext.Request.Path = "/liveTranscodeHub";

        LiveTranscodeHub hub = new(
            new HttpContextAccessorStub(httpContext),
            Mock.Of<IDbContextFactory<MediaContext>>(),
            new ConnectedClients(),
            Mock.Of<IActivityLogger>(),
            sessionManager,
            streamingService,
            Mock.Of<ILiveSessionPresenceTracker>(),
            NullLogger<LiveTranscodeHub>.Instance
        );

        ClaimsPrincipal principal = new(
            new ClaimsIdentity([new(ClaimTypes.NameIdentifier, callerUserId)], "TestAuth")
        );

        Mock<HubCallerContext> context = new();
        context.Setup(c => c.User).Returns(principal);
        context.Setup(c => c.UserIdentifier).Returns(callerUserId);
        context.Setup(c => c.ConnectionId).Returns(Guid.NewGuid().ToString());
        context.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);

        Mock<IHubCallerClients> clients = new();
        hub.Context = context.Object;
        hub.Clients = clients.Object;

        return hub;
    }

    [Fact]
    public void ReportBufferHealth_ByOwner_RecordsHealth_AndTouchesLastAccess()
    {
        const string sessionId = "sess-owner";
        const string ownerId = "user-1";

        LiveSession session = new(sessionId, MakeQuality());
        LiveRuntimeSession runtime = new(session, TimeSpan.FromSeconds(6));
        DateTime lastAccessBeforeCall = runtime.LastAccess;

        Mock<ISessionManager> sessionManager = new();
        sessionManager.Setup(m => m.GetOwnerUserId(sessionId)).Returns(ownerId);

        Mock<ILiveStreamingService> streamingService = new();
        streamingService.Setup(s => s.TryGetRuntime(sessionId, out runtime)).Returns(true);

        LiveTranscodeHub hub = CreateHub(sessionManager.Object, streamingService.Object, ownerId);

        hub.ReportBufferHealth(sessionId, 12.5, 4500);

        session.ClientBufferedAhead.Should().Be(TimeSpan.FromSeconds(12.5));
        session.ObservedBandwidthKbps.Should().Be(4500);
        session.HasFreshClientHealth(TimeSpan.FromSeconds(10)).Should().BeTrue();
        runtime.LastAccess.Should().BeOnOrAfter(lastAccessBeforeCall);
    }

    [Fact]
    public void ReportBufferHealth_NegativeValues_ClampToZero()
    {
        const string sessionId = "sess-clamp";
        const string ownerId = "user-1";

        LiveSession session = new(sessionId, MakeQuality());
        LiveRuntimeSession runtime = new(session, TimeSpan.FromSeconds(6));

        Mock<ISessionManager> sessionManager = new();
        sessionManager.Setup(m => m.GetOwnerUserId(sessionId)).Returns(ownerId);

        Mock<ILiveStreamingService> streamingService = new();
        streamingService.Setup(s => s.TryGetRuntime(sessionId, out runtime)).Returns(true);

        LiveTranscodeHub hub = CreateHub(sessionManager.Object, streamingService.Object, ownerId);

        hub.ReportBufferHealth(sessionId, -5, -100);

        session.ClientBufferedAhead.Should().Be(TimeSpan.Zero);
        session.ObservedBandwidthKbps.Should().Be(0);
    }

    [Fact]
    public void ReportBufferHealth_ByNonOwner_IsRejected()
    {
        const string sessionId = "sess-owner";
        const string ownerId = "user-1";
        const string callerId = "user-2";

        LiveSession session = new(sessionId, MakeQuality());
        LiveRuntimeSession runtime = new(session, TimeSpan.FromSeconds(6));
        DateTime lastAccessBeforeCall = runtime.LastAccess;

        Mock<ISessionManager> sessionManager = new();
        sessionManager.Setup(m => m.GetOwnerUserId(sessionId)).Returns(ownerId);

        Mock<ILiveStreamingService> streamingService = new();
        streamingService.Setup(s => s.TryGetRuntime(sessionId, out runtime)).Returns(true);

        LiveTranscodeHub hub = CreateHub(sessionManager.Object, streamingService.Object, callerId);

        hub.ReportBufferHealth(sessionId, 12.5, 4500);

        session.HasFreshClientHealth(TimeSpan.FromSeconds(10)).Should().BeFalse();
        runtime.LastAccess.Should().Be(lastAccessBeforeCall);
    }

    [Fact]
    public void ReportBufferHealth_UnknownSession_IsNoOp()
    {
        Mock<ISessionManager> sessionManager = new();
        Mock<ILiveStreamingService> streamingService = new();
        // No Setup for TryGetRuntime — unconfigured returns false with a null
        // out value, mirroring an unknown/expired session id.

        LiveTranscodeHub hub = CreateHub(sessionManager.Object, streamingService.Object, "user-1");

        Action act = () => hub.ReportBufferHealth("sess-unknown", 12.5, 4500);

        act.Should().NotThrow();
        sessionManager.Verify(m => m.GetOwnerUserId(It.IsAny<string>()), Times.Never);
    }

    private sealed class HttpContextAccessorStub(HttpContext httpContext) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }
}
