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
using NoMercy.MediaProcessing.Files;
using Stowage;
using Xunit;

namespace NoMercy.Tests.MediaProcessing.Files;

/// <summary>
/// StowageWatcher polls a (possibly network-backed) folder. Its very first scan
/// seeds the baseline; that scan used to run outside the loop's try/catch, so a
/// backend that was not reachable yet at boot (NFS/SMB/S3 mount lag) faulted the
/// watch task and left the folder permanently unwatched. The loop must survive an
/// initial-scan failure and still deliver events once the backend recovers.
/// </summary>
public class StowageWatcherResilienceTests
{
    [Fact]
    public async Task WatchLoop_SurvivesInitialScanFailure_ThenEmitsLaterCreatedEvent()
    {
        Mock<IFileStorage> storage = new();
        int calls = 0;
        IReadOnlyCollection<IOEntry> empty = [];
        IReadOnlyCollection<IOEntry> withFile = [new IOEntry("/movie.mkv")];

        storage
            .Setup(s => s.Ls(It.IsAny<IOPath?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                int n = Interlocked.Increment(ref calls);
                return n switch
                {
                    // First (seed) scan fails, as if the mount is not ready yet.
                    1 => Task.FromException<IReadOnlyCollection<IOEntry>>(
                        new IOException("network mount not ready")
                    ),
                    // Second scan seeds an empty baseline (no events).
                    2 => Task.FromResult(empty),
                    // From then on a new file exists → a Created event must fire.
                    _ => Task.FromResult(withFile),
                };
            });

        StowageWatcher watcher = new(storage.Object, "/");
        TaskCompletionSource<StowageWatcherEventArgs> created = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        watcher.Created += args => created.TrySetResult(args);

        watcher.Watch(TimeSpan.FromMilliseconds(50));

        Task finished = await Task.WhenAny(created.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        watcher.Dispose();

        Assert.True(
            finished == created.Task,
            "Watcher must recover from the initial scan failure and still emit later events."
        );
        StowageWatcherEventArgs args = await created.Task;
        Assert.Equal("/movie.mkv", args.Path);
    }
}
