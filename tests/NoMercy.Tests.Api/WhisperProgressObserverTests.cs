using Microsoft.AspNetCore.SignalR;
using Moq;
using NoMercy.Api.Controllers.V1.Encoder;
using NoMercy.Api.Hubs;
using NoMercy.Encoder.Progress;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Verifies that <see cref="SignalRProgressObserver"/> translates
/// <see cref="IProgressObserver.OnProgress"/> calls into
/// <c>WhisperProgress</c> broadcasts on the <see cref="ContentAnalysisHub"/>
/// IHubContext.
/// </summary>
[Trait("Category", "SignalR")]
public class WhisperProgressObserverTests
{
    [Fact]
    public void OnProgress_BroadcastsWhisperProgressToAllClients()
    {
        Mock<IClientProxy> clientProxyMock = new();
        clientProxyMock
            .Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IHubClients> hubClientsMock = new();
        hubClientsMock.Setup(h => h.All).Returns(clientProxyMock.Object);

        Mock<IHubContext<ContentAnalysisHub>> hubContextMock = new();
        hubContextMock.Setup(h => h.Clients).Returns(hubClientsMock.Object);

        SignalRProgressObserver observer = new(hubContextMock.Object, "test-file-id");

        EncodingProgress progress = new(
            CorrelationId: "c1",
            PercentComplete: 42.5,
            Elapsed: TimeSpan.FromSeconds(5),
            EstimatedRemaining: null,
            CurrentFps: null,
            CurrentSpeed: null,
            CurrentStage: null,
            CurrentOperation: null
        );

        observer.OnProgress(progress);

        // Give the fire-and-forget a moment to schedule on the thread pool.
        Thread.Sleep(100);

        clientProxyMock.Verify(
            c => c.SendCoreAsync(
                "WhisperProgress",
                It.Is<object[]>(args => args.Length > 0),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public void OtherCallbacks_DoNotThrow()
    {
        Mock<IHubContext<ContentAnalysisHub>> hubContextMock = new();
        SignalRProgressObserver observer = new(hubContextMock.Object, "test-file-id");

        // None of these should throw.
        observer.OnStageStarted("stage");
        observer.OnStageCompleted("stage", TimeSpan.Zero);
        observer.OnCompleted();
        observer.OnError(new(NoMercy.Encoder.Errors.EncodingErrorKind.Unknown, "msg", null, null, false));
        observer.OnPlanResolved([], [], [], false, false);
    }
}
