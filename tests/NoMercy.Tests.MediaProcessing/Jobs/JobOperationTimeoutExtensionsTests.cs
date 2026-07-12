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

using NoMercy.MediaProcessing.Jobs.MediaJobs;

namespace NoMercy.Tests.MediaProcessing.Jobs;

/// <summary>
/// Covers the fail-fast timeout guard used by ShowExtrasJob/EpisodeExtrasJob
/// to bound hang-prone TMDB/NFS store operations. These jobs `new()` their
/// managers directly (no DI), so the operation under test here is the guard
/// itself — a Store call that never completes must throw promptly rather than
/// hang the caller indefinitely.
/// </summary>
[Trait("Category", "Unit")]
public sealed class JobOperationTimeoutExtensionsTests
{
    [Fact]
    public async Task WithTimeout_OperationNeverCompletes_ThrowsTimeoutExceptionWithinBound()
    {
        TaskCompletionSource neverCompletes = new();

        Func<Task> act = () =>
            neverCompletes.Task.WithTimeout("StalledStoreCall", TimeSpan.FromMilliseconds(50));

        TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(act);
        exception.Message.Should().Contain("StalledStoreCall");
        exception.Message.Should().Contain("50");
    }

    [Fact]
    public async Task WithTimeout_OperationCompletesInTime_CompletesNormally()
    {
        Task fastOperation = Task.Delay(5);

        await fastOperation.WithTimeout("FastStoreCall", TimeSpan.FromSeconds(5));

        fastOperation.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task WithTimeout_OperationFaults_PropagatesOriginalException()
    {
        Task faulted = Task.FromException(new InvalidOperationException("tmdb 500"));

        Func<Task> act = () => faulted.WithTimeout("FaultingStoreCall", TimeSpan.FromSeconds(5));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            act
        );
        exception.Message.Should().Be("tmdb 500");
    }

    [Fact]
    public async Task WithTimeout_NoTimeoutSupplied_UsesDefaultTimeoutConstant()
    {
        JobOperationTimeoutExtensions.DefaultTimeout.Should().Be(TimeSpan.FromMinutes(3));

        Task fastOperation = Task.Delay(5);

        await fastOperation.WithTimeout("DefaultTimeoutCall");

        fastOperation.IsCompletedSuccessfully.Should().BeTrue();
    }
}
