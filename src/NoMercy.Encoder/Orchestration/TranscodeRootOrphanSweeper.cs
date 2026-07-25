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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Information;
using NoMercy.Storage;

namespace NoMercy.Encoder.Orchestration;

/// <summary>
/// One-shot hosted service that runs once at startup and deletes batch-encode
/// working directories left behind under <see cref="StoragePaths.TranscodeRoot"/>
/// by a previous server crash or unclean shutdown. Mirrors
/// <see cref="NoMercy.Encoder.LiveTranscode.LiveTranscodeOrphanSweeper"/>, which
/// covers the separate live-transcode cache.
///
/// <para>
/// <see cref="EncodingOrchestrator"/> creates either an opaque
/// <c>nomercy-enc-&lt;ulid&gt;</c> dir or an output-mirrored <c>&lt;Show&gt;…</c>
/// dir under the transcode root, and removes it when the encode completes. A
/// crash mid-encode leaves the dir forever, slowly filling the cache volume.
/// At boot no encode is in flight, so every leftover is an orphan.
/// </para>
///
/// <para>
/// Safety: the root is only swept wholesale when it is the dedicated managed
/// encoder cache (<see cref="AppFiles.EncoderCachePath"/>). If the root is still
/// the unconfigured <see cref="Path.GetTempPath"/> default, deletion is limited
/// to the <c>nomercy-enc-*</c> prefix so unrelated system-temp content is never
/// touched — the mirrored show dirs are sacrificed in that fallback rather than
/// risk a stray delete outside our own scratch.
/// </para>
/// </summary>
public class TranscodeRootOrphanSweeper : IHostedService
{
    internal const string EncWorkingDirPrefix = "nomercy-enc-";

    private readonly ILogger<TranscodeRootOrphanSweeper> _logger;
    private readonly IStorage _storage;
    private readonly string _root;
    private readonly bool _sweepAllChildren;

    public TranscodeRootOrphanSweeper(ILogger<TranscodeRootOrphanSweeper> logger, IStorage storage)
        : this(
            logger,
            storage,
            StoragePaths.TranscodeRoot,
            IsDedicatedEncoderCache(StoragePaths.TranscodeRoot)
        ) { }

    internal TranscodeRootOrphanSweeper(
        ILogger<TranscodeRootOrphanSweeper> logger,
        IStorage storage,
        string root,
        bool sweepAllChildren
    )
    {
        _logger = logger;
        _storage = storage;
        _root = root;
        _sweepAllChildren = sweepAllChildren;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_storage.Exists(_root))
        {
            _logger.LogDebug(
                "TranscodeRootOrphanSweeper: transcode root {Dir} does not exist, nothing to sweep",
                _root
            );
            return Task.CompletedTask;
        }

        string pattern = _sweepAllChildren ? "*" : EncWorkingDirPrefix + "*";

        IReadOnlyList<StorageEntry> orphans;
        try
        {
            orphans = _storage
                .List(_root, pattern, false)
                .Where(entry => entry.IsDirectory)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TranscodeRootOrphanSweeper: could not enumerate {Dir}", _root);
            return Task.CompletedTask;
        }

        foreach (StorageEntry entry in orphans)
        {
            try
            {
                _storage.DeleteDirectory(entry.Path, true);
                _logger.LogInformation(
                    "TranscodeRootOrphanSweeper: deleted orphan {Dir}",
                    entry.Path
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "TranscodeRootOrphanSweeper: could not delete orphan {Dir}",
                    entry.Path
                );
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool IsDedicatedEncoderCache(string root)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string normalizedCache = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(AppFiles.EncoderCachePath)
        );
        return string.Equals(
            normalizedRoot,
            normalizedCache,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal
        );
    }
}
