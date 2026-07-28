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

using Moq;
using NoMercy.Notifications.Push;
using Xunit;

namespace NoMercy.Tests.Notifications.Push;

public class PushDispatchQueueTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private static PushDispatchRequest Request(string channel = "encode-finished") =>
        new(channel, new PushPayload("Done", "body", null), "token");

    /// <summary>
    /// The producer here is whatever published the event. If it ever waits on
    /// the relay, a server that cannot reach nomercy.tv pays the full HTTP
    /// timeout on every publish, and a request that publishes four events pays
    /// it four times over.
    /// </summary>
    [Fact]
    public async Task Enqueue_Returns_While_The_Relay_Is_Still_Stuck_On_The_First_Send()
    {
        TaskCompletionSource entered = new();
        TaskCompletionSource release = new();

        Mock<IPushDispatcher> dispatcher = new();
        dispatcher
            .Setup(d =>
                d.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<PushPayload>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                entered.TrySetResult();
                return release.Task;
            });

        PushDispatchQueue queue = new(dispatcher.Object);
        using CancellationTokenSource cts = new();
        Task drain = queue.DrainAsync(cts.Token);

        queue.Enqueue(Request());
        await entered.Task.WaitAsync(Patience);

        queue.Enqueue(Request());
        queue.Enqueue(Request());

        Assert.False(release.Task.IsCompleted);
        dispatcher.Verify(
            d =>
                d.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<PushPayload>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        release.SetResult();
        await cts.CancelAsync();
    }

    [Fact]
    public async Task Drain_Delivers_What_Was_Enqueued()
    {
        TaskCompletionSource delivered = new();

        Mock<IPushDispatcher> dispatcher = new();
        dispatcher
            .Setup(d =>
                d.DispatchAsync(
                    "encode-failed",
                    It.IsAny<PushPayload>(),
                    "token",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => delivered.TrySetResult())
            .Returns(Task.CompletedTask);

        PushDispatchQueue queue = new(dispatcher.Object);
        using CancellationTokenSource cts = new();
        Task drain = queue.DrainAsync(cts.Token);

        queue.Enqueue(Request("encode-failed"));

        await delivered.Task.WaitAsync(Patience);
        await cts.CancelAsync();

        dispatcher.Verify(
            d =>
                d.DispatchAsync(
                    "encode-failed",
                    It.IsAny<PushPayload>(),
                    "token",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    /// <summary>
    /// An HttpClient timeout arrives as a TaskCanceledException, which is an
    /// OperationCanceledException. If that ends the loop, one slow relay call
    /// silently kills push for the lifetime of the process.
    /// </summary>
    [Fact]
    public async Task Drain_Survives_A_Transport_Timeout_And_Keeps_Delivering()
    {
        TaskCompletionSource second = new();
        int calls = 0;

        Mock<IPushDispatcher> dispatcher = new();
        dispatcher
            .Setup(d =>
                d.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<PushPayload>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                calls++;
                if (calls == 1)
                    return Task.FromException(new TaskCanceledException("relay timed out"));

                second.TrySetResult();
                return Task.CompletedTask;
            });

        PushDispatchQueue queue = new(dispatcher.Object);
        using CancellationTokenSource cts = new();
        Task drain = queue.DrainAsync(cts.Token);

        queue.Enqueue(Request());
        queue.Enqueue(Request());

        await second.Task.WaitAsync(Patience);
        Assert.False(drain.IsCompleted);

        await cts.CancelAsync();
    }
}
