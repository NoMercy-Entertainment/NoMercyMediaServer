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

// Regression coverage for the live-transcode buffer-ahead fix: BufferAhead used
// to be driven entirely by the segment-request-derived prefetch frontier, which
// tracks how far the player has prefetched rather than where the user is
// actually watching. ReportPlayhead lets a live client report its true
// position so the 30s over-buffer suspend engages on the real watch position.
// These tests build a real LiveTranscodeHub against real ISessionManager /
// ILiveStreamingService mocks and a real LiveSession/LiveRuntimeSession pair,
// mocking only the SignalR plumbing (HubCallerContext) a live connection would
// normally supply.
[Trait(name: "Category", value: "Unit")]
public class LiveTranscodeHubReportPlayheadTests
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
    public async Task ReportPlayhead_ByOwner_UpdatesPlaybackPosition_AndTouchesLastAccess()
    {
        const string sessionId = "sess-owner";
        const string ownerId = "user-1";

        LiveSession session = new(sessionId: sessionId, quality: MakeQuality());
        // SeekAsync sets TranscodedPosition to the target too (PushSegment is
        // internal and not visible from this test assembly), giving a known
        // 60s TranscodedPosition to measure BufferAhead against.
        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 60), ct: CancellationToken.None);
        LiveRuntimeSession runtime = new(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));
        DateTime lastAccessBeforeCall = runtime.LastAccess;

        Mock<ISessionManager> sessionManager = new();
        sessionManager.Setup(expression: m => m.GetOwnerUserId(sessionId)).Returns(value: ownerId);

        Mock<ILiveStreamingService> streamingService = new();
        streamingService.Setup(expression: s => s.TryGetRuntime(sessionId, out runtime)).Returns(value: true);

        LiveTranscodeHub hub = CreateHub(sessionManager: sessionManager.Object, streamingService: streamingService.Object, callerUserId: ownerId);

        hub.ReportPlayhead(sessionId: sessionId, currentTimeSeconds: 42.5);

        // TranscodedPosition is 60s (from the pushed segment); the reported
        // playhead of 42.5s must be applied authoritatively.
        session.BufferAhead.Should().Be(expected: TimeSpan.FromSeconds(value: 17.5));
        runtime.LastAccess.Should().BeOnOrAfter(expected: lastAccessBeforeCall);
    }

    [Fact]
    public async Task ReportPlayhead_NegativeSeconds_ClampsToZero()
    {
        const string sessionId = "sess-clamp";
        const string ownerId = "user-1";

        LiveSession session = new(sessionId: sessionId, quality: MakeQuality());
        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 10), ct: CancellationToken.None);
        LiveRuntimeSession runtime = new(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        Mock<ISessionManager> sessionManager = new();
        sessionManager.Setup(expression: m => m.GetOwnerUserId(sessionId)).Returns(value: ownerId);

        Mock<ILiveStreamingService> streamingService = new();
        streamingService.Setup(expression: s => s.TryGetRuntime(sessionId, out runtime)).Returns(value: true);

        LiveTranscodeHub hub = CreateHub(sessionManager: sessionManager.Object, streamingService: streamingService.Object, callerUserId: ownerId);

        hub.ReportPlayhead(sessionId: sessionId, currentTimeSeconds: -5);

        session.BufferAhead.Should().Be(expected: TimeSpan.FromSeconds(seconds: 10));
    }

    [Fact]
    public async Task ReportPlayhead_ByNonOwner_IsRejected()
    {
        const string sessionId = "sess-owner";
        const string ownerId = "user-1";
        const string callerId = "user-2";

        LiveSession session = new(sessionId: sessionId, quality: MakeQuality());
        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 60), ct: CancellationToken.None);
        LiveRuntimeSession runtime = new(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));
        DateTime lastAccessBeforeCall = runtime.LastAccess;
        TimeSpan bufferAheadBeforeCall = session.BufferAhead;

        Mock<ISessionManager> sessionManager = new();
        sessionManager.Setup(expression: m => m.GetOwnerUserId(sessionId)).Returns(value: ownerId);

        Mock<ILiveStreamingService> streamingService = new();
        streamingService.Setup(expression: s => s.TryGetRuntime(sessionId, out runtime)).Returns(value: true);

        LiveTranscodeHub hub = CreateHub(sessionManager: sessionManager.Object, streamingService: streamingService.Object, callerUserId: callerId);

        hub.ReportPlayhead(sessionId: sessionId, currentTimeSeconds: 42.5);

        session.BufferAhead.Should().Be(expected: bufferAheadBeforeCall);
        runtime.LastAccess.Should().Be(expected: lastAccessBeforeCall);
    }

    [Fact]
    public void ReportPlayhead_UnknownSession_IsNoOp()
    {
        Mock<ISessionManager> sessionManager = new();
        Mock<ILiveStreamingService> streamingService = new();
        // No Setup for TryGetRuntime — unconfigured returns false with a null
        // out value, mirroring an unknown/expired session id.

        LiveTranscodeHub hub = CreateHub(sessionManager: sessionManager.Object, streamingService: streamingService.Object, callerUserId: "user-1");

        Action act = () => hub.ReportPlayhead(sessionId: "sess-unknown", currentTimeSeconds: 10);

        act.Should().NotThrow();
        // Mirrors Heartbeat's order: TryGetRuntime is checked before the owner
        // lookup, so an unknown session never reaches GetOwnerUserId.
        sessionManager.Verify(expression: m => m.GetOwnerUserId(It.IsAny<string>()), times: Times.Never);
    }

    private sealed class HttpContextAccessorStub(HttpContext httpContext) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }
}
