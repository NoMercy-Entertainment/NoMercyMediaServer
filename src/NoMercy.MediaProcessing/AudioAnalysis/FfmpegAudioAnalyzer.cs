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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.AudioAnalysis;

/// <summary>
/// Measures a track with one pass of the fork ffmpeg. Lives in MediaProcessing
/// for the same reason the chromaprint fingerprinter does: it needs the
/// Encoder-layer process runner and ffmpeg path, which Providers must not
/// reference.
/// </summary>
public sealed class FfmpegAudioAnalyzer(
    EncoderOptions options,
    IProcessRunner processRunner,
    IStorage storage,
    ILogger<FfmpegAudioAnalyzer> logger
) : IAudioAnalyzer
{
    private static readonly TimeSpan AnalysisTimeout = TimeSpan.FromMinutes(10);

    public int Version => 1;

    public async Task<AudioAnalysisResult?> AnalyzeAsync(string filePath, CancellationToken ct)
    {
        await using LocalPathLease inputLease = storage.AcquireLocalPath(filePath);

        AudioAnalysisOutputParser parser = new();

        // One pass, every detector. keydetect and silencedetect answer through
        // ametadata on stdout; loudnorm and beatdetect answer on stderr.
        string filterGraph = string.Join(
            ",",
            "keydetect",
            "beatdetect",
            "aspectralstats=measure=centroid",
            "silencedetect=n=-50dB:d=0.5",
            "loudnorm=print_format=json",
            "ametadata=mode=print:file=-"
        );

        string[] arguments =
        [
            "-nostdin",
            "-i",
            inputLease.Path,
            "-vn",
            "-sn",
            "-dn",
            "-af",
            filterGraph,
            "-f",
            "null",
            "-",
        ];

        // ffmpeg reading a stalled network mount never returns, and RunAsync
        // waits on the caller's token alone, so one unreadable file would hold
        // the analysis worker for good.
        using CancellationTokenSource analysisCts = CancellationTokenSource.CreateLinkedTokenSource(
            ct
        );
        analysisCts.CancelAfter(AnalysisTimeout);

        ProcessResult result;
        try
        {
            result = await processRunner.RunAsync(
                options.FfmpegPath,
                arguments,
                parser.ConsumeStdOut,
                parser.ConsumeStdErr,
                null,
                analysisCts.Token
            );
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(
                "audio analysis timed out after {Seconds}s for {Path}",
                AnalysisTimeout.TotalSeconds,
                filePath
            );
            return null;
        }

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                "audio analysis failed for {Path} (exit {Exit}): {Stderr}",
                filePath,
                result.ExitCode,
                result.StdErr
            );
            return null;
        }

        AudioAnalysisResult analysis = parser.Build();

        // Every detector coming back empty means the pass produced nothing
        // usable, whatever the exit code said.
        bool measuredSomething =
            analysis.Bpm is not null
            || analysis.KeyName is not null
            || analysis.IntegratedLufs is not null;

        if (!measuredSomething)
        {
            logger.LogWarning("audio analysis produced no measurements for {Path}", filePath);
            return null;
        }

        return analysis;
    }
}
