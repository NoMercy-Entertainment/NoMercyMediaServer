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
[Trait(name: "Category", value: "Unit")]
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
            httpContextAccessor: new HttpContextAccessorStub(httpContext: httpContext),
            contextFactory: Mock.Of<IDbContextFactory<MediaContext>>(),
            connectedClients: new ConnectedClients(),
            activityLogger: Mock.Of<IActivityLogger>(),
            sessionManager: sessionManager,
            streamingService: streamingService,
            presenceTracker: Mock.Of<ILiveSessionPresenceTracker>(),
            logger: NullLogger<LiveTranscodeHub>.Instance
        );

        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(claims: [new(type: ClaimTypes.NameIdentifier, value: callerUserId)], authenticationType: "TestAuth")
        );

        Mock<HubCallerContext> context = new();
        context.Setup(expression: c => c.User).Returns(value: principal);
        context.Setup(expression: c => c.UserIdentifier).Returns(value: callerUserId);
        context.Setup(expression: c => c.ConnectionId).Returns(value: Guid.NewGuid().ToString());
        context.Setup(expression: c => c.ConnectionAborted).Returns(value: CancellationToken.None);

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

        LiveSession session = new(sessionId: sessionId, quality: MakeQuality());
        LiveRuntimeSession runtime = new(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));
        DateTime lastAccessBeforeCall = runtime.LastAccess;

        Mock<ISessionManager> sessionManager = new();
        sessionManager.Setup(expression: m => m.GetOwnerUserId(sessionId)).Returns(value: ownerId);

        Mock<ILiveStreamingService> streamingService = new();
        streamingService.Setup(expression: s => s.TryGetRuntime(sessionId, out runtime)).Returns(value: true);

        LiveTranscodeHub hub = CreateHub(sessionManager: sessionManager.Object, streamingService: streamingService.Object, callerUserId: ownerId);

        hub.ReportBufferHealth(sessionId: sessionId, bufferedSeconds: 12.5, observedBandwidthKbps: 4500);

        session.ClientBufferedAhead.Should().Be(expected: TimeSpan.FromSeconds(value: 12.5));
        session.ObservedBandwidthKbps.Should().Be(expected: 4500);
        session.HasFreshClientHealth(maxAge: TimeSpan.FromSeconds(seconds: 10)).Should().BeTrue();
        runtime.LastAccess.Should().BeOnOrAfter(expected: lastAccessBeforeCall);
    }

    [Fact]
    public void ReportBufferHealth_NegativeValues_ClampToZero()
    {
        const string sessionId = "sess-clamp";
        const string ownerId = "user-1";

        LiveSession session = new(sessionId: sessionId, quality: MakeQuality());
        LiveRuntimeSession runtime = new(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        Mock<ISessionManager> sessionManager = new();
        sessionManager.Setup(expression: m => m.GetOwnerUserId(sessionId)).Returns(value: ownerId);

        Mock<ILiveStreamingService> streamingService = new();
        streamingService.Setup(expression: s => s.TryGetRuntime(sessionId, out runtime)).Returns(value: true);

        LiveTranscodeHub hub = CreateHub(sessionManager: sessionManager.Object, streamingService: streamingService.Object, callerUserId: ownerId);

        hub.ReportBufferHealth(sessionId: sessionId, bufferedSeconds: -5, observedBandwidthKbps: -100);

        session.ClientBufferedAhead.Should().Be(expected: TimeSpan.Zero);
        session.ObservedBandwidthKbps.Should().Be(expected: 0);
    }

    [Fact]
    public void ReportBufferHealth_ByNonOwner_IsRejected()
    {
        const string sessionId = "sess-owner";
        const string ownerId = "user-1";
        const string callerId = "user-2";

        LiveSession session = new(sessionId: sessionId, quality: MakeQuality());
        LiveRuntimeSession runtime = new(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));
        DateTime lastAccessBeforeCall = runtime.LastAccess;

        Mock<ISessionManager> sessionManager = new();
        sessionManager.Setup(expression: m => m.GetOwnerUserId(sessionId)).Returns(value: ownerId);

        Mock<ILiveStreamingService> streamingService = new();
        streamingService.Setup(expression: s => s.TryGetRuntime(sessionId, out runtime)).Returns(value: true);

        LiveTranscodeHub hub = CreateHub(sessionManager: sessionManager.Object, streamingService: streamingService.Object, callerUserId: callerId);

        hub.ReportBufferHealth(sessionId: sessionId, bufferedSeconds: 12.5, observedBandwidthKbps: 4500);

        session.HasFreshClientHealth(maxAge: TimeSpan.FromSeconds(seconds: 10)).Should().BeFalse();
        runtime.LastAccess.Should().Be(expected: lastAccessBeforeCall);
    }

    [Fact]
    public void ReportBufferHealth_UnknownSession_IsNoOp()
    {
        Mock<ISessionManager> sessionManager = new();
        Mock<ILiveStreamingService> streamingService = new();
        // No Setup for TryGetRuntime — unconfigured returns false with a null
        // out value, mirroring an unknown/expired session id.

        LiveTranscodeHub hub = CreateHub(sessionManager: sessionManager.Object, streamingService: streamingService.Object, callerUserId: "user-1");

        Action act = () => hub.ReportBufferHealth(sessionId: "sess-unknown", bufferedSeconds: 12.5, observedBandwidthKbps: 4500);

        act.Should().NotThrow();
        sessionManager.Verify(expression: m => m.GetOwnerUserId(It.IsAny<string>()), times: Times.Never);
    }

    private sealed class HttpContextAccessorStub(HttpContext httpContext) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }
}
