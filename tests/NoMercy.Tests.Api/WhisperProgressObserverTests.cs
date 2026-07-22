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
using NoMercy.Api.Controllers.V1.Encoder;
using NoMercy.Api.Hubs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Progress;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Verifies that <see cref="SignalRProgressObserver"/> translates
/// <see cref="IProgressObserver.OnProgress"/> calls into
/// <c>WhisperProgress</c> broadcasts on the <see cref="ContentAnalysisHub"/>
/// IHubContext.
/// </summary>
[Trait(name: "Category", value: "SignalR")]
public class WhisperProgressObserverTests
{
    [Fact]
    public void OnProgress_BroadcastsWhisperProgressToAllClients()
    {
        Mock<IClientProxy> clientProxyMock = new();
        clientProxyMock
            .Setup(expression: c =>
                c.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(value: Task.CompletedTask);

        Mock<IHubClients> hubClientsMock = new();
        hubClientsMock.Setup(expression: h => h.All).Returns(value: clientProxyMock.Object);

        Mock<IHubContext<ContentAnalysisHub>> hubContextMock = new();
        hubContextMock.Setup(expression: h => h.Clients).Returns(value: hubClientsMock.Object);

        SignalRProgressObserver observer = new(hub: hubContextMock.Object, videoFileId: "test-file-id");

        EncodingProgress progress = new(
            CorrelationId: "c1",
            PercentComplete: 42.5,
            Elapsed: TimeSpan.FromSeconds(seconds: 5),
            EstimatedRemaining: null,
            CurrentFps: null,
            CurrentSpeed: null,
            CurrentStage: null,
            CurrentOperation: null
        );

        observer.OnProgress(progress: progress);

        // Give the fire-and-forget a moment to schedule on the thread pool.
        Thread.Sleep(millisecondsTimeout: 100);

        clientProxyMock.Verify(
            expression: c =>
                c.SendCoreAsync(
                    "WhisperProgress",
                    It.Is<object[]>(args => args.Length > 0),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.AtLeastOnce
        );
    }

    [Fact]
    public void OtherCallbacks_DoNotThrow()
    {
        Mock<IHubContext<ContentAnalysisHub>> hubContextMock = new();
        SignalRProgressObserver observer = new(hub: hubContextMock.Object, videoFileId: "test-file-id");

        // None of these should throw.
        observer.OnStageStarted(stageName: "stage");
        observer.OnStageCompleted(stageName: "stage", duration: TimeSpan.Zero);
        observer.OnCompleted();
        observer.OnError(error: new(Kind: EncodingErrorKind.Unknown, Message: "msg", FfmpegStderr: null, StageName: null, Recoverable: false));
        observer.OnPlanResolved(videoStreams: [], audioStreams: [], subtitleStreams: [], hasGpu: false, isHdr: false);
    }
}
