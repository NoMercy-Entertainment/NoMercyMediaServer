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

using ProviderQueue = NoMercy.Providers.Helpers.Queue;

namespace NoMercy.Tests.Providers.Helpers;

public class QueueProcessorTests
{
    [Fact]
    [Trait(name: "Category", value: "Unit")]
    public async Task Queue_ContinuesProcessing_AfterTransientError()
    {
        ProviderQueue queue = new(
            options: new()
            {
                Concurrent = 1,
                Interval = 10,
                Start = true,
            }
        );

        int callCount = 0;

        // First task throws
        try
        {
            await queue.Enqueue<string>(
                task: async () =>
                {
                    Interlocked.Increment(location: ref callCount);
                    await Task.Delay(millisecondsDelay: 1);
                    throw new InvalidOperationException(message: "Transient failure");
                },
                url: "http://test/fail"
            );
        }
        catch (InvalidOperationException)
        {
            // Expected — Enqueue propagates via TaskCompletionSource
        }

        // Second task should still work (queue continues processing)
        string result = await queue.Enqueue<string>(
            task: async () =>
            {
                Interlocked.Increment(location: ref callCount);
                await Task.Delay(millisecondsDelay: 1);
                return "success";
            },
            url: "http://test/ok"
        );

        result.Should().Be(expected: "success");
        callCount.Should().Be(expected: 2);
    }

    [Fact]
    [Trait(name: "Category", value: "Unit")]
    public async Task Queue_PropagatesFailure_ToCaller()
    {
        ProviderQueue queue = new(
            options: new()
            {
                Concurrent = 1,
                Interval = 10,
                Start = true,
            }
        );

        InvalidOperationException thrownException = new(message: "Test error for rejection");
        Exception? caught = null;

        // Failures surface to the caller via the per-task TaskCompletionSource
        // (the old Reject event was removed as dead code).
        try
        {
            await queue.Enqueue<string>(
                task: async () =>
                {
                    await Task.Delay(millisecondsDelay: 1);
                    throw thrownException;
                },
                url: "http://test/reject"
            );
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
        caught!.Message.Should().Be(expected: "Test error for rejection");
    }

    [Fact]
    [Trait(name: "Category", value: "Unit")]
    public async Task Queue_ProcessesMultipleTasks_InOrder()
    {
        ProviderQueue queue = new(
            options: new()
            {
                Concurrent = 1,
                Interval = 10,
                Start = true,
            }
        );

        List<int> executionOrder = [];

        int result1 = await queue.Enqueue(
            task: async () =>
            {
                await Task.Delay(millisecondsDelay: 1);
                lock (executionOrder)
                    executionOrder.Add(item: 1);
                return 1;
            },
            url: "http://test/1"
        );

        int result2 = await queue.Enqueue(
            task: async () =>
            {
                await Task.Delay(millisecondsDelay: 1);
                lock (executionOrder)
                    executionOrder.Add(item: 2);
                return 2;
            },
            url: "http://test/2"
        );

        result1.Should().Be(expected: 1);
        result2.Should().Be(expected: 2);
        executionOrder.Should().ContainInOrder(expected: [1, 2]);
    }
}
