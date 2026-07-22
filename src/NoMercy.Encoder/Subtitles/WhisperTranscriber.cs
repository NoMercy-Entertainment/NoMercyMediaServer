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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Progress;
using NoMercy.Storage;

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Produces a WebVTT or SRT subtitle file by running FFmpeg's <c>whisper</c>
/// filter (whisper.cpp) against a specific audio stream in the input.
/// The filter writes the subtitle output directly — no post-parsing needed.
/// </summary>
public class WhisperTranscriber(
    EncoderOptions options,
    IProcessRunner processRunner,
    IStorage storage,
    ILogger<WhisperTranscriber> logger
) : IWhisperTranscriber
{
    public async Task<SubtitleTrack> TranscribeAsync(
        string inputPath,
        int audioStreamIndex,
        string language,
        WhisperOptions? options_,
        IProgressObserver? progress,
        CancellationToken ct
    )
    {
        string modelPath =
            options_?.ModelPath
            ?? options.WhisperModelPath
            ?? throw new InvalidOperationException(
                message: "WhisperModelPath is not configured on EncoderOptions and no override supplied."
            );

        if (!storage.Exists(path: modelPath))
        {
            throw new FileNotFoundException(
                message: $"Whisper model not found at {modelPath}. Configure EncoderOptions.WhisperModelPath.",
                fileName: modelPath
            );
        }

        string outputDirectory =
            Path.GetDirectoryName(path: inputPath)
            ?? throw new InvalidOperationException(message: "Input path has no parent directory.");

        string outputName = $"{Path.GetFileNameWithoutExtension(path: inputPath)}.{language}.whisper";
        string format = "srt"; // whisper filter supports srt; we emit VTT by extension rename afterwards if requested
        string outputPath = Path.Combine(path1: outputDirectory, path2: $"{outputName}.{format}");

        // Ensure a stable run location for the whisper filter — it resolves
        // destination= relative to CWD.
        storage.CreateDirectory(path: outputDirectory);

        // Lease every path we hand to ffmpeg so future remote drivers can
        // stage them locally + clean up on dispose.
        await using LocalPathLease inputLease = storage.AcquireLocalPath(path: inputPath);
        await using LocalPathLease modelLease = storage.AcquireLocalPath(path: modelPath);
        await using LocalPathLease outputLease = storage.AcquireLocalPath(path: outputPath);

        // Select audio stream, apply whisper filter with model, language, queue,
        // destination, and format, then discard video output.
        int queue = 3;
        int translate = options_?.TranslateToEnglish == true ? 1 : 0;

        string whisperFilter =
            $"whisper=model={EscapeFilterPath(path: modelLease.Path)}:language={language}"
            + $":queue={queue}:destination={EscapeFilterPath(path: outputLease.Path)}:format={format}"
            + (translate == 1 ? ":translate=1" : "");

        string[] args =
        [
            "-hide_banner",
            "-i",
            inputLease.Path,
            "-map",
            $"0:a:{audioStreamIndex}",
            "-vn",
            "-af",
            whisperFilter,
            "-f",
            "null",
            "-",
        ];

        progress?.OnStageStarted(stageName: $"Whisper transcription ({language})");

        ProcessResult result = await processRunner.RunAsync(
            executable: options.FfmpegPath,
            arguments: args,
            workingDirectory: outputDirectory,
            cancellationToken: ct
        );

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                message: $"whisper ffmpeg exited with code {result.ExitCode}"
            );
        }

        if (!storage.Exists(path: outputPath))
        {
            throw new InvalidOperationException(
                message: $"Whisper filter produced no output at {outputPath}."
            );
        }

        int cueCount = CountCuesIn(srtPath: outputPath);

        progress?.OnStageCompleted(stageName: $"Whisper transcription ({language})", duration: result.Duration);

        logger.LogInformation(
            message: "Whisper transcription complete: {Cues} cues for {Language} → {Path}", args: [cueCount, language, outputPath]
        );

        return new(FilePath: outputPath, Language: language, Format: SubtitleCodecType.Srt, CueCount: cueCount);
    }

    private int CountCuesIn(string srtPath)
    {
        int count = 0;
        using Stream stream = storage.OpenRead(path: srtPath);
        using StreamReader reader = new(stream: stream);
        while (reader.ReadLine() is { } line)
        {
            if (line.Contains(value: "-->", comparisonType: StringComparison.Ordinal))
                count++;
        }
        return count;
    }

    private static string EscapeFilterPath(string path) =>
        path.Replace(oldChar: '\\', newChar: '/').Replace(oldValue: ":", newValue: "\\:");
}
